using System.Text.Json;
using Kart.User.Application.Common.Interfaces;
using Kart.User.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kart.User.Application.Features.CreateUserProfileOnRegistration;

/// <summary>
/// Upsert-on-userId (edge-cases.md "Duplicate/out-of-order UserRegistered delivery"): a
/// redelivery for a user id that already has a profile record is a no-op, not a duplicate
/// insert or an error.
/// </summary>
public sealed class CreateUserProfileOnRegistrationCommandHandler(
    IUserDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<CreateUserProfileOnRegistrationCommandHandler> logger)
    : IRequestHandler<CreateUserProfileOnRegistrationCommand>
{
    public async Task Handle(CreateUserProfileOnRegistrationCommand request, CancellationToken cancellationToken)
    {
        var alreadyExists = await dbContext.UserProfiles.AnyAsync(p => p.UserId == request.UserId, cancellationToken);
        if (alreadyExists)
        {
            logger.LogInformation("UserRegistered redelivered for {UserId}; profile already exists, no-op", request.UserId);
            return;
        }

        var now = dateTimeProvider.UtcNow;
        var profile = Domain.Entities.UserProfile.CreateFromRegistration(request.UserId, request.Email, now);
        dbContext.UserProfiles.Add(profile);

        // Internal-only projection trigger (never externally published — see OutboxEvent's own
        // doc comment) so the Mongo read model gets its initial document.
        dbContext.OutboxEvents.Add(OutboxEvent.Create(
            request.UserId,
            OutboxEvent.ReadModelProjectionRequested,
            payloadJson: JsonSerializer.Serialize(new { userId = request.UserId }),
            now,
            createdBy: "system:identity-registration-consumer"));

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Stage {Stage}: UserProfile projection persisted for {UserId} from UserRegistered", "UserProfileProjectionPersisted", request.UserId);
    }
}
