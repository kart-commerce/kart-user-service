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

        var response = await next();

        logger.LogInformation("{RequestName} completed in {ElapsedMilliseconds}ms", requestName, stopwatch.ElapsedMilliseconds);
        return response;
    }
}
