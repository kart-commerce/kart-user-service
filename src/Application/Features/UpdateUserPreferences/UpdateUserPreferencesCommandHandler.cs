using System.Text.Json;
using Kart.Shared.Domain;
using Kart.User.Application.Common.Interfaces;
using Kart.User.Application.Common.Mapping;
using Kart.User.Application.Common.Models;
using Kart.User.Domain.Entities;
using Kart.User.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kart.User.Application.Features.UpdateUserPreferences;

public sealed class UpdateUserPreferencesCommandHandler(IUserDbContext dbContext, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateUserPreferencesCommand, Result<UserProfileResponse>>
{
    public async Task<Result<UserProfileResponse>> Handle(UpdateUserPreferencesCommand request, CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserProfiles
            .Include(p => p.Addresses)
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure<UserProfileResponse>(Error.NotFound($"No profile exists for user '{request.UserId}'."));
        }

        var now = dateTimeProvider.UtcNow;
        var preferences = request.Preferences is null
            ? null
            : new Preferences(
                request.Preferences.Locale,
                request.Preferences.Currency,
                request.Preferences.NotificationOptIn is null
                    ? NotificationOptIn.Default
                    : new NotificationOptIn(
                        request.Preferences.NotificationOptIn.Email,
                        request.Preferences.NotificationOptIn.Sms,
                        request.Preferences.NotificationOptIn.Push),
                request.Preferences.MarketingConsent);

        var touchedNotificationSignal = profile.UpdatePreferences(preferences, request.AppInstalled, now, request.ActingPrincipalId);

        // event-contract.md: UserProfileUpdated's payload is just `userId` — every consumer
        // (Analytics) re-reads current state from this service's own GET endpoint if it needs
        // more, per the platform's general "read model is the source of truth for shape" bias.
        dbContext.OutboxEvents.Add(OutboxEvent.Create(
            profile.UserId,
            eventType: "UserProfileUpdated",
            payloadJson: JsonSerializer.Serialize(new { userId = profile.UserId }),
            now,
            createdBy: request.ActingPrincipalId));

        if (touchedNotificationSignal)
        {
            var optIn = profile.Preferences.NotificationOptIn;
            dbContext.OutboxEvents.Add(OutboxEvent.Create(
                profile.UserId,
                eventType: "UserNotificationPreferenceUpdated",
                payloadJson: JsonSerializer.Serialize(new
                {
                    userId = profile.UserId,
                    notificationOptIn = new { email = optIn.Email, sms = optIn.Sms, push = optIn.Push },
                    appInstalled = profile.AppInstalled
                }),
                now,
                createdBy: request.ActingPrincipalId));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(UserProfileMapper.ToResponse(profile));
    }
}
