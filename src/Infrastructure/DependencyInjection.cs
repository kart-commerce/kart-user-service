using Kart.User.Application.Common.Interfaces;
using Kart.User.Infrastructure.Messaging;
using Kart.User.Infrastructure.Persistence;
using Kart.User.Infrastructure.Persistence.ReadModel;
using Kart.User.Infrastructure.Security;
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
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            var manifestPath = Path.IsPathRooted(options.ManifestPath)
                ? options.ManifestPath
                : Path.Combine(AppContext.BaseDirectory, options.ManifestPath);
            return MessageBusManifestLoader.Load(manifestPath);
        });
        services.AddSingleton<IConnectionFactory>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            return new ConnectionFactory { HostName = options.HostName, DispatchConsumersAsync = true };
        });

        services.AddHostedService<RabbitMqTopologyStartupHostedService>();
        services.AddHostedService<OutboxRelayHostedService>();
        services.AddHostedService<ReadModelProjectionHostedService>();
        services.AddHostedService<UserRegisteredConsumerHostedService>();
        services.AddHostedService<UserAccountUpdatedConsumerHostedService>();

        return services;
    }
}
