using Kart.User.Application.Common.Models;

namespace Kart.User.Application.Common.Interfaces;

/// <summary>
/// The MongoDB <c>user_read_model</c> collection (database-design.md) — the eventually-consistent
/// query side of this service's CQRS split. Application owns the interface; Infrastructure
/// implements it against the MongoDB driver, the same "Application owns the interface" pattern
/// already used for <see cref="IUserDbContext"/> on the write side.
/// </summary>
public interface IUserReadModelRepository
{
    Task<UserProfileResponse?> GetByIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Point-write keyed by <c>userId</c> (upsert) — the projector's own idempotent
    /// re-projection mechanism (edge-cases.md "Duplicate/out-of-order UserRegistered delivery").</summary>
    Task UpsertAsync(UserProfileResponse document, CancellationToken cancellationToken);
}
