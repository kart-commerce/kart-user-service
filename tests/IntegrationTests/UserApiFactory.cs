using Kart.User.Infrastructure.Messaging;
using Kart.User.Infrastructure.Persistence;
using Kart.User.Infrastructure.Persistence.ReadModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Mongo2Go;
using MongoDB.Driver;

namespace Kart.User.IntegrationTests;

/// <summary>
/// WebApplicationFactory wiring for HTTP endpoint tests — swaps the real PostgreSQL provider for
/// Sqlite in-memory and the real MongoDB for an ephemeral local <c>mongod</c> instance
/// (Mongo2Go), removes every RabbitMQ-dependent hosted service (no real broker in this test
/// environment — matches kart-identity-service's own <c>IdentityApiFactory</c> "no real
/// RabbitMQ" precedent), and swaps JWT bearer auth for a header-driven test scheme so tests can
/// simulate a caller's <c>sub</c>/<c>scope</c> claims without a live Identity JWKS endpoint.
/// </summary>
public sealed class UserApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _sqliteConnection = new("DataSource=:memory:");
    private readonly MongoDbRunner _mongoRunner = MongoDbRunner.Start(singleNodeReplSet: false);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            _sqliteConnection.Open();

            services.RemoveAll<DbContextOptions<UserDbContext>>();
            services.AddDbContext<UserDbContext>(options => options.UseSqlite(_sqliteConnection));

            services.RemoveAll<IMongoClient>();
            services.RemoveAll<IMongoDatabase>();
            services.RemoveAll<IMongoCollection<UserReadModelDocument>>();
            services.AddSingleton<IMongoClient>(_ => new MongoClient(_mongoRunner.ConnectionString));
            services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase("kart_user_test"));
            services.AddSingleton(sp => sp.GetRequiredService<IMongoDatabase>().GetCollection<UserReadModelDocument>("user_read_model"));

            RemoveHostedService<RabbitMqTopologyStartupHostedService>(services);
            RemoveHostedService<OutboxRelayHostedService>(services);
            RemoveHostedService<UserRegisteredConsumerHostedService>(services);
            RemoveHostedService<UserAccountUpdatedConsumerHostedService>(services);
            // ReadModelProjectionHostedService is intentionally NOT removed — it has no
            // RabbitMQ dependency (a pure Postgres-to-Mongo poller) and exercising it end-to-end
            // is exactly what the projection-related integration tests want.

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    private static void RemoveHostedService<THostedService>(IServiceCollection services) where THostedService : IHostedService
    {
        var descriptor = services.FirstOrDefault(d => d.ImplementationType == typeof(THostedService));
        if (descriptor is not null)
        {
            services.Remove(descriptor);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _sqliteConnection.Dispose();
            _mongoRunner.Dispose();
        }
    }

    /// <summary>Ensures the Sqlite schema exists — <c>EnsureCreated()</c> against the current EF
    /// model, not real migrations (same technique kart-identity-service's own test factory uses).</summary>
    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }
}
