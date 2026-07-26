using Kart.User.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kart.User.UnitTests.TestSupport;

/// <summary>
/// EF Core InMemory-backed <see cref="UserDbContext"/> instances — enough to exercise handler
/// logic (querying/saving the aggregate) without a real PostgreSQL instance. <see cref="Create"/>
/// (no args) starts a fresh, uniquely-named database; <see cref="Create(string)"/> opens another
/// connection to an already-named one. Tests that seed data and then exercise a handler should
/// use two separate <see cref="UserDbContext"/> instances against the same database name — the
/// same shape production has (each request/consumer gets its own scoped DbContext against the
/// same underlying store) — rather than reusing one instance for both, which the InMemory
/// provider's change tracker handles inconsistently for this aggregate's backing-field-only
/// <c>Addresses</c> collection navigation.
/// </summary>
public static class InMemoryUserDbContextFactory
{
    public static UserDbContext Create() => Create(Guid.NewGuid().ToString());

    public static UserDbContext Create(string databaseName)
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;

        return new UserDbContext(options);
    }
}
