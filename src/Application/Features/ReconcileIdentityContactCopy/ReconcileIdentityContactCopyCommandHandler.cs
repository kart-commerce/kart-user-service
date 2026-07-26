using System.Text.Json;
using Kart.User.Application.Common.Interfaces;
using Kart.User.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kart.User.Application.Features.ReconcileIdentityContactCopy;

/// <summary>
/// Upsert keyed on user id, generalized from USR-1's idempotency mechanism
/// (design-decisions.md) to cover out-of-order delivery too: if <c>UserAccountUpdated</c> is
/// somehow processed before <c>UserRegistered</c>, a shell profile is created here rather than
/// dropping the event.
/// </summary>
public sealed class ReconcileIdentityContactCopyCommandHandler(
    IUserDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<ReconcileIdentityContactCopyCommandHandler> logger)
    : IRequestHandler<ReconcileIdentityContactCopyCommand>
{
    public async Task Handle(ReconcileIdentityContactCopyCommand request, CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);
        var now = dateTimeProvider.UtcNow;
        var shellProfileCreated = profile is null;

        if (profile is null)
        {
            logger.LogInformation("UserAccountUpdated arrived before UserRegistered for {UserId}; creating shell profile", request.UserId);
            profile = Domain.Entities.UserProfile.CreateFromRegistration(request.UserId, email: null, now);
            dbContext.UserProfiles.Add(profile);
        }

        var applied = profile.ReconcileContactCopy(request.Email, request.DisplayName, request.UpdatedAt, now);
        if (!applied)
        {
            logger.LogInformation(
                "UserAccountUpdated for {UserId} is not newer than the currently-stored contactCopy; ignored",
                request.UserId);

            // Still persist a freshly-created shell profile even when this particular update
            // turned out not to be newer (defensive — with a null contactCopy baseline this
            // should not happen on a genuinely new shell, but a dropped shell would otherwise
            // violate "a profile must not exist independent of a corresponding UserRegistered
            // event" for a user id we've now seen evidence of).
            if (shellProfileCreated)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        // Modeling decision (ddd-model.md): does NOT publish UserProfileUpdated externally —
        // Analytics already receives UserAccountUpdated directly from Identity. This row is
        // internal-only, solely to keep the Mongo projection's email/displayName copy in sync.
        dbContext.OutboxEvents.Add(OutboxEvent.Create(
            request.UserId,
            OutboxEvent.ReadModelProjectionRequested,
            payloadJson: JsonSerializer.Serialize(new { userId = request.UserId }),
            now,
            createdBy: "system:identity-account-sync-consumer"));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
