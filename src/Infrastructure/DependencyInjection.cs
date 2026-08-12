using Kart.User.Application.Common.Interfaces;
using Kart.User.Infrastructure.Messaging;
using Kart.User.Infrastructure.Persistence;
using Kart.User.Infrastructure.Persistence.ReadModel;
using Kart.User.Infrastructure.Security;
using Kart.Shared.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using RabbitMQ.Client;

namespace Kart.User.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // --- AuthN: this service validates Identity-issued JWTs, it never mints them ---------
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName));
        services.AddMemoryCache();
        services.AddHttpClient(nameof(JwksSigningKeyResolver));
        services.AddSingleton<JwksSigningKeyResolver>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwksSigningKeyResolver, IOptions<JwtOptions>>((options, resolver, jwtOptions) =>
            {
                // Without this, ASP.NET Core's default inbound-claim mapping renames the token's
                // literal "sub" claim to the long ClaimTypes.NameIdentifier URI before this
                // service's own IsSelf check (UserEndpoints.cs, `FindFirst("sub")`) ever sees it —
                // the same claim-mapping defect a previous flow found in kart-category-service's
                // AdminOnly policy, here silently making every self-scoped endpoint 403 for every
                // caller regardless of whether they actually own the resource. Never caught by
                // this service's own tests (a header-driven TestAuthHandler, not real JWT
                // validation) — only surfaced against a real Identity-issued token.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Value.Issuer,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    IssuerSigningKeyResolver = (_, _, kid, _) => resolver.ResolveSigningKeys(kid)
                };
            });
        services.AddAuthorization();

        // --- PostgreSQL write side ---------------------------------------------------------
        services.AddScoped<CurrentPrincipalConnectionInterceptor>();
        services.AddDbContext<UserDbContext>((sp, options) =>
        {
            options.UseNpgsql(configuration.GetConnectionString("UserDb"));
            options.AddInterceptors(sp.GetRequiredService<CurrentPrincipalConnectionInterceptor>());
        });
        services.AddScoped<IUserDbContext>(sp => sp.GetRequiredService<UserDbContext>());

        // --- MongoDB read side --------------------------------------------------------------
        services.AddOptions<MongoOptions>().Bind(configuration.GetSection(MongoOptions.SectionName));
        services.AddSingleton<IMongoClient>(sp => new MongoClient(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
            return sp.GetRequiredService<IMongoClient>().GetDatabase(options.DatabaseName);
        });
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
            return sp.GetRequiredService<IMongoDatabase>().GetCollection<UserReadModelDocument>(options.CollectionName);
        });
        services.AddScoped<IUserReadModelRepository, UserReadModelRepository>();

        // --- Config-driven message bus (BRD §9) ---------------------------------------------
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddKartMessageBusManifest(sp => sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value.ManifestPath);
        services.AddKartRabbitMqConnectionFactory(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            return new RabbitMqConnectionSettings(options.HostName, UserName: options.UserName, Password: options.Password);
        });

        services.AddKartRabbitMqTopologyStartup();
        services.AddHostedService<OutboxRelayHostedService>();
        services.AddHostedService<ReadModelProjectionHostedService>();
        services.AddHostedService<UserRegisteredConsumerHostedService>();
        services.AddHostedService<UserAccountUpdatedConsumerHostedService>();

        return services;
    }
}
