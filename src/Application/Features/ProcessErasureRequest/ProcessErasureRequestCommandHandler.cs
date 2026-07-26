using System.Text.Json;
using Kart.Shared.Auditing;
using Kart.Shared.Domain;
using Kart.User.Application.Common.Interfaces;
using Kart.User.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kart.User.Application.Features.ProcessErasureRequest;

/// <summary>
/// ADR-0016/ADR-0017 — the GDPR-style tombstone workflow. A compliance-critical mutation, so
/// (unlike this service's other writes) it also records an audit-trail entry via
/// <c>Kart.Shared.Auditing</c>, the platform's one shared audit-log-writer abstraction.
/// </summary>
public sealed class ProcessErasureRequestCommandHandler(
    IUserDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IAuditLogWriter auditLogWriter)
    : IRequestHandler<ProcessErasureRequestCommand, Result<ErasureResponse>>
{
    public async Task<Result<ErasureResponse>> Handle(ProcessErasureRequestCommand request, CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserProfiles
            .Include(p => p.Addresses)
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure<ErasureResponse>(Error.NotFound($"No profile exists for user '{request.UserId}'."));
        }

        var now = dateTimeProvider.UtcNow;
        var wasApplied = profile.Erase(now);

        if (wasApplied)
        {
            dbContext.OutboxEvents.Add(OutboxEvent.Create(
                profile.UserId,
                eventType: "UserDataErased",
                payloadJson: JsonSerializer.Serialize(new { userId = profile.UserId, erasedAt = now }),
                now,
                createdBy: request.ActingPrincipalId));

            await auditLogWriter.WriteAsync(
                AuditLogEntry.Create(
                    serviceName: "kart-user-service",
                    actorId: request.ActingPrincipalId,
                    actorType: "service",
                    action: "user.erasure.tombstoned",
                    entityType: "UserProfile",
                    entityId: profile.UserId),
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new ErasureResponse(profile.UserId, now, WasAlreadyErased: !wasApplied));
    }
}
