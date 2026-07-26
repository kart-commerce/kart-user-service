namespace Kart.User.Application.Common.Interfaces;

/// <summary>
/// The ambient "who is acting" accessor — the single source both the BRD §24.3 audit-column
/// stamping (<c>created_by</c>/<c>updated_by</c>) and the BRD §24.1.4 row-level-security session
/// variable (<c>app.current_principal</c>) read from, per database-design.md's explicit
/// "RLS and audit-column injection read one ambient current-principal accessor... not two
/// independently-maintained notions of 'who is acting'" rule.
///
/// Implemented in the Api layer (resolves from <c>HttpContext.User</c> for interactive
/// requests) since only that layer has an <c>HttpContext</c> to read; a well-known
/// <c>system:*</c> implementation is used instead wherever a handler runs with no HTTP request
/// at all (the <c>UserRegistered</c>/<c>UserAccountUpdated</c> event consumers, the Outbox/
/// projection pollers) — see <c>Infrastructure/Messaging</c>'s hosted services.
/// </summary>
public interface ICurrentPrincipalAccessor
{
    /// <summary>The owning <c>userId</c> for a self-service request, a service's client-credentials
    /// <c>client_id</c> for a service-to-service call, or a well-known <c>system:*</c> id.</summary>
    string PrincipalId { get; }

    /// <summary><c>"user"</c>, <c>"service"</c>, or <c>"system"</c> — read by the RLS policy
    /// alongside <see cref="PrincipalId"/> (database-design.md's Row-Level Security Policy).</summary>
    string PrincipalKind { get; }
}
