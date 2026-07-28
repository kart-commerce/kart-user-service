using Kart.Shared.Auditing;
using Kart.Shared.Configuration;
using Kart.Shared.ErrorHandling;
using Kart.Shared.Observability;
using Kart.User.Api;
using Kart.User.Api.Endpoints;
using Kart.User.Api.Middleware;
using Kart.User.Application;
using Kart.User.Application.Common.Interfaces;
using Kart.User.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// kart-conventions.md Configuration Management: GlobalConfig external-secrets-file bootstrap,
// shared across every service - never reimplemented per service. See appsettings.Local.json.example.
builder.AddKartGlobalConfig();

builder.AddKartObservability("kart-user-service");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentPrincipalAccessor, HttpContextCurrentPrincipalAccessor>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

app.MapUserEndpoints();
app.MapInternalUserEndpoints();

app.Run();

public partial class Program;
