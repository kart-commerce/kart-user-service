using Kart.User.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kart.User.Application.Common.Interfaces;

/// <summary>
/// Application owns the interface, Infrastructure implements it (matches
/// kart-identity-service's <c>IIdentityDbContext</c> pattern) — keeps Application/Domain free of
/// any EF Core provider dependency beyond the abstractions package.
/// </summary>
public interface IUserDbContext
{
    DbSet<UserProfile> UserProfiles { get; }
    DbSet<Address> Addresses { get; }
    DbSet<OutboxEvent> OutboxEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
