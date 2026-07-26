using MediatR;

namespace Kart.User.Application.Features.ReconcileIdentityContactCopy;

/// <summary>
/// USR-7. Consumed from Identity's <c>UserAccountUpdated</c> (<c>userId</c>, <c>email</c>,
/// <c>displayName</c>, <c>updatedAt</c>) — ADR-0006. Dispatched by
/// <c>Infrastructure/Messaging/UserAccountUpdatedConsumerHostedService</c>.
/// </summary>
public sealed record ReconcileIdentityContactCopyCommand(
    string UserId,
    string? Email,
    string? DisplayName,
    DateTimeOffset UpdatedAt) : IRequest;
