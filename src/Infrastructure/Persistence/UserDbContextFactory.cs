using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kart.User.Infrastructure.Persistence;

/// <summary>Design-time-only factory for <c>dotnet ef migrations add</c> — never used at runtime
/// (matches kart-identity-service's <c>IdentityDbContextFactory</c> pattern exactly).</summary>
public sealed class UserDbContextFactory : IDesignTimeDbContextFactory<UserDbContext>
{
    public UserDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("USER_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=kart_user;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<UserDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new UserDbContext(optionsBuilder.Options);
    }
}
