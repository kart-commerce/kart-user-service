namespace Kart.User.Application.Common;

/// <summary>Business-flow tags for KartFlowContext.Push, per kart-conventions.md's per-flow tracing/logging standard. Kept as the same literal value across every service (see kart-identity-service's own copy).</summary>
public static class FlowNames
{
    public const string UserRegistrationLoginAuthentication = "UserRegistrationLoginAuthentication";
}
