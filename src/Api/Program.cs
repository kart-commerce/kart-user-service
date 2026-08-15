using Kart.Shared.Auditing;
using Kart.Shared.Configuration;
using Kart.Shared.ErrorHandling;
using Kart.Shared.Observability;
using Kart.User.Api;
using Kart.User.Api.Endpoints;
using Kart.User.Api.HealthChecks;
using Kart.User.Api.Middleware;
using Kart.User.Application;
using Kart.User.Application.Common.Interfaces;
using Kart.User.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// kart-conventions.md Configuration Management: GlobalConfig external-secrets-file bootstrap,
// shared across every service - never reimplemented per service. See appsettings.Local.json.example.
builder.AddKartGlobalConfig("kart-user-service");

builder.AddKartObservability("kart-user-service");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentPrincipalAccessor, HttpContextCurrentPrincipalAccessor>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// /health/live: process is up, no dependency check. /health/ready: this service's job depends
// on Postgres being reachable AND migrated (a connectable-but-unmigrated database, e.g. tonight's
// missing user_outbox_events table, is not "ready") - matching kart-infra's service-chart probe
// convention (search-service's OpenSearchHealthCheck is the same pattern for its own data store).
builder.Services.AddHealthChecks()
    .AddCheck<UserDbHealthCheck>("user-db", tags: ["ready"]);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddKartAuditing();

// No .Map<TException>() calls needed: this service's business/domain errors all flow through
// the Result/Error pattern (kart-conventions.md), returned directly by endpoint handlers as a
// Problem response — never thrown as exceptions. FluentValidation's ValidationException (the
// one exception type this service's pipeline does throw, via ValidationBehaviour) is handled by
// KartErrorHandlingOptions' own default (HandleFluentValidationExceptions = true).
builder.Services.AddKartErrorHandling();

var app = builder.Build();

await StartupConnectivityChecks.RunAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseKartErrorHandling();
app.UseHttpsRedirection();
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseMiddleware<UserContextEnrichmentMiddleware>();
app.UseAuthorization();

app.MapPrometheusScrapingEndpoint();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapUserEndpoints();
app.MapInternalUserEndpoints();

app.Run();

public partial class Program;
