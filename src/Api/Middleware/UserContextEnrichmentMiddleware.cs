using Serilog.Context;

namespace Kart.User.Api.Middleware;

/// <summary>
/// Pushes <c>userId</c> onto Serilog's <see cref="LogContext"/> for every authenticated request —
/// this service's own primary correlation field (design-decisions.md's Observability &amp;
/// Instrumentation decision), alongside the mandatory <c>traceId</c>/<c>service</c>/<c>level</c>
/// fields <c>Kart.Shared.Observability</c> already enriches every log line with. A no-op for
/// anonymous/internal-service requests.
/// </summary>
public sealed class UserContextEnrichmentMiddleware(RequestDelegate next)
{
    private const string SubjectClaimType = "sub";

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(SubjectClaimType)?.Value
            : null;

        if (userId is null)
        {
            await next(context);
            return;
        }

        using (LogContext.PushProperty("userId", userId))
        {
            await next(context);
        }
    }
}
