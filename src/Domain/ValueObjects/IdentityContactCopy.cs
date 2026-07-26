namespace Kart.User.Domain.ValueObjects;

/// <summary>
/// ddd-model.md's <c>IdentityContactCopy</c> value object — the denormalized, eventually-
/// consistent copy of Identity-owned fields (ADR-0006). <see cref="Entities.UserProfile"/> never
/// treats this as write-authoritative for conflict purposes; it is written only by consuming
/// <c>UserAccountUpdated</c>, and only when <see cref="UpdatedAt"/> is strictly newer than the
/// currently-stored value (the apply-if-newer ordering guard — architecture.md).
/// </summary>
public sealed class IdentityContactCopy
{
    public string? Email { get; private set; }
    public string? DisplayName { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private IdentityContactCopy() { }

    public IdentityContactCopy(string? email, string? displayName, DateTimeOffset? updatedAt)
    {
        Email = email;
        DisplayName = displayName;
        UpdatedAt = updatedAt;
    }

    public static IdentityContactCopy Empty => new(email: null, displayName: null, updatedAt: null);

    /// <summary>
    /// Apply-if-newer guard (ddd-model.md invariant): an incoming <c>UserAccountUpdated</c> is
    /// only applied if its own <paramref name="updatedAt"/> is strictly newer than what's already
    /// stored, or nothing has been stored yet.
    /// </summary>
    public bool IsNewerThan(DateTimeOffset updatedAt) => UpdatedAt is null || updatedAt > UpdatedAt;
}
