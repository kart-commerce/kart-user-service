using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kart.User.IntegrationTests;

/// <summary>
/// A header-driven fake authentication scheme replacing real JWT-bearer validation in tests
/// (no live Identity JWKS endpoint in this test environment). A request carrying
/// <c>X-Test-Sub</c> is authenticated with that value as the <c>sub</c> claim; adding
/// <c>X-Test-Scope: admin</c> additionally grants the <c>scope</c> claim
/// <c>InternalUserEndpoints</c>' erasure-intake route requires. No header at all means
/// anonymous, exactly like an unauthenticated real request.
/// </summary>
public sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string SubHeader = "X-Test-Sub";
    public const string ScopeHeader = "X-Test-Scope";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SubHeader, out var sub) || string.IsNullOrWhiteSpace(sub))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new("sub", sub!) };
        if (Request.Headers.TryGetValue(ScopeHeader, out var scope) && !string.IsNullOrWhiteSpace(scope))
        {
            claims.Add(new Claim("scope", scope!));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
