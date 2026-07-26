using Kart.User.Application.Common.Interfaces;

namespace Kart.User.Api;

/// <summary>
/// Resolves "who is acting" from the current HTTP request's validated JWT (BRD §24.3 / §24.1.4).
/// Falls back to a well-known <c>system:*</c> principal when there is no active HTTP request —
/// this same registration is used for every DI scope in the process, including the ones the
/// message-consumer/Outbox/projection hosted services create via <c>IServiceScopeFactory</c>,
/// which never have an <see cref="HttpContext"/> at all.
/// </summary>
public sealed class HttpContextCurrentPrincipalAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentPrincipalAccessor
{
    private const string SubjectClaimType = "sub";
    private const string ClientIdClaimType = "client_id";

    public string PrincipalId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return "system:unattended";
            }

            return user.FindFirst(SubjectClaimType)?.Value
                ?? user.FindFirst(ClientIdClaimType)?.Value
                ?? "system:unattended";
        }
    }

    public string PrincipalKind
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return "system";
            }

            return user.FindFirst(ClientIdClaimType) is not null ? "service" : "user";
        }
    }
}
