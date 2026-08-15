using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.User.Application.Common.Behaviours;

/// <summary>Never logs request/response bodies (PII such as address lines/phone numbers) — only
/// the request type name and elapsed time, per observability-standards.md's "no PII in plaintext
/// logs" rule.</summary>
public sealed class LoggingBehaviour<TRequest, TResponse>(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        // Checkpoint-logging taxonomy stage 3 ("<Command>HandlerStarted", first line inside
        // Handle()) generalized here rather than duplicated in every handler — this behavior
        // already wraps every MediatR request, so it's the one place that's true by construction
        // instead of by every handler author remembering to add it.
        logger.LogInformation("Stage {Stage}: {RequestName} handler started", $"{requestName}HandlerStarted", requestName);

        var response = await next();

        logger.LogInformation("Stage {Stage}: {RequestName} completed in {ElapsedMilliseconds}ms", $"{requestName}Completed", requestName, stopwatch.ElapsedMilliseconds);
        return response;
    }
}
