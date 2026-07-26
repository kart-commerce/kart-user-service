using Kart.Shared.Domain;
using Kart.User.Domain.Enums;
using Kart.User.Domain.ValueObjects;

namespace Kart.User.Domain.Entities;

/// <summary>
/// Aggregate root (ddd-model.md). Identified by <see cref="UserId"/>, referenced from
/// kart-identity-service's own user identifier — this service never generates its own
/// (requirement-spec.md §4: a profile record must not exist independent of a corresponding
/// <c>UserRegistered</c> event). <see cref="Address"/> is a child entity, not a separate
/// aggregate — see ddd-model.md's "Why one aggregate" reasoning (the per-type default-address
/// invariant can only be enforced inside one transaction boundary).
///
/// Business-error outcomes (e.g. "no such address") are surfaced as <see cref="Result{T}"/>,
/// per kart-conventions.md's Error Handling section — exceptions are reserved for genuine
/// infrastructure failures, not expected domain outcomes a caller already anticipates.
/// </summary>
public sealed class UserProfile
{
    private readonly List<Address> _addresses = new();

    public string UserId { get; private set; } = string.Empty;
    public IdentityContactCopy ContactCopy { get; private set; } = IdentityContactCopy.Empty;
    public Preferences Preferences { get; private set; } = Preferences.Default;
    public bool AppInstalled { get; private set; }
    public ErasureStatus ErasureStatus { get; private set; } = ErasureStatus.Active;
    public DateTimeOffset? ErasedAt { get; private set; }

    public IReadOnlyCollection<Address> Addresses => _addresses.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = string.Empty;

    private UserProfile() { }

    /// <summary>
    /// A <see cref="UserProfile"/> may only come into existence by consuming
    /// <c>UserRegistered</c> (requirement-spec.md §4) — there is no client-facing "create"
    /// operation on this aggregate's own API surface.
    /// </summary>
    public static UserProfile CreateFromRegistration(string userId, string? email, DateTimeOffset now)
    {
        const string actor = "system:identity-registration-consumer";
        return new UserProfile
        {
            UserId = userId,
            // updatedAt: null, not `now` — this establishes "no contactCopy reconciliation has
            // happened yet," not a real sync timestamp. Seeding it with the registration/shell-
            // creation processing time would make the very next UserAccountUpdated look stale
            // (its own event timestamp is almost always earlier than whenever we process it),
            // defeating ReconcileContactCopy's apply-if-newer check for the exact first-sync
            // case it needs to allow unconditionally (IdentityContactCopy.IsNewerThan treats a
            // null baseline as always-apply).
            ContactCopy = new IdentityContactCopy(email, displayName: null, updatedAt: null),
            Preferences = Preferences.Default,
            AppInstalled = false,
            ErasureStatus = ErasureStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actor,
            UpdatedBy = actor
        };
    }

    /// <summary>
    /// Whole-preferences-object replace (edge-cases.md — last-write-wins, chosen over field-level
    /// merge). Returns whether the write touched <c>notificationOptIn</c>/<c>appInstalled</c>,
    /// since that determines whether <c>UserNotificationPreferenceUpdated</c> also fires
    /// (ddd-model.md Modeling Decision 2).
    /// </summary>
    public bool UpdatePreferences(Preferences? preferences, bool? appInstalled, DateTimeOffset now, string actor)
    {
        var touchedNotificationSignal = preferences is not null || appInstalled is not null;

        if (preferences is not null)
        {
            Preferences = preferences;
        }

        if (appInstalled is not null)
        {
            AppInstalled = appInstalled.Value;
        }

        UpdatedAt = now;
        UpdatedBy = actor;
        return touchedNotificationSignal;
    }

    public Address AddAddress(
        AddressType type,
        string line1,
        string? line2,
        string city,
        string? region,
        string postalCode,
        string countryCode,
        string? phone,
        bool isDefault,
        DateTimeOffset now,
        string actor)
    {
        if (isDefault)
        {
            ClearDefaultsOfType(type, now, actor);
        }

        var address = Address.Create(UserId, type, line1, line2, city, region, postalCode, countryCode, phone, isDefault, now, actor);
        _addresses.Add(address);

        UpdatedAt = now;
        UpdatedBy = actor;
        return address;
    }

    public Result<Address> UpdateAddress(
        Guid addressId,
        AddressType type,
        string line1,
        string? line2,
        string city,
        string? region,
        string postalCode,
        string countryCode,
        string? phone,
        bool isDefault,
        DateTimeOffset now,
        string actor)
    {
        var address = _addresses.FirstOrDefault(a => a.AddressId == addressId);
        if (address is null)
        {
            return Result.Failure<Address>(Error.NotFound($"Address '{addressId}' does not exist for user '{UserId}'."));
        }

        if (isDefault)
        {
            ClearDefaultsOfType(type, now, actor, except: addressId);
        }

        address.ReplaceWith(type, line1, line2, city, region, postalCode, countryCode, phone, isDefault, now, actor);

        UpdatedAt = now;
        UpdatedBy = actor;
        return Result.Success(address);
    }

    public Result RemoveAddress(Guid addressId, DateTimeOffset now, string actor)
    {
        var address = _addresses.FirstOrDefault(a => a.AddressId == addressId);
        if (address is null)
        {
            return Result.Failure(Error.NotFound($"Address '{addressId}' does not exist for user '{UserId}'."));
        }

        _addresses.Remove(address);

        UpdatedAt = now;
        UpdatedBy = actor;
        return Result.Success();
    }

    /// <summary>
    /// Reconciles the denormalized Identity contact copy (ADR-0006), applied only if the
    /// incoming <paramref name="updatedAt"/> is strictly newer than what's already stored
    /// (architecture.md's ordering guard). Returns whether the update was actually applied.
    /// </summary>
    public bool ReconcileContactCopy(string? email, string? displayName, DateTimeOffset updatedAt, DateTimeOffset now)
    {
        if (!ContactCopy.IsNewerThan(updatedAt))
        {
            return false;
        }

        ContactCopy = new IdentityContactCopy(email, displayName, updatedAt);
        UpdatedAt = now;
        UpdatedBy = "system:identity-account-sync-consumer";
        return true;
    }

    /// <summary>
    /// ADR-0016 — one-directional PII tombstone. Idempotent: calling this on an
    /// already-<see cref="Enums.ErasureStatus.Erased"/> profile is a no-op (api-contract.yaml:
    /// "a repeat call against an already-Erased profile is a no-op").
    /// </summary>
    public bool Erase(DateTimeOffset now)
    {
        if (ErasureStatus == ErasureStatus.Erased)
        {
            return false;
        }

        const string actor = "system:user-erasure-workflow";
        ErasureStatus = ErasureStatus.Erased;
        ErasedAt = now;
        ContactCopy = new IdentityContactCopy(email: "[erased]", displayName: "[erased]", ContactCopy.UpdatedAt);

        foreach (var address in _addresses)
        {
            address.Tombstone(now);
        }

        UpdatedAt = now;
        UpdatedBy = actor;
        return true;
    }

    private void ClearDefaultsOfType(AddressType type, DateTimeOffset now, string actor, Guid? except = null)
    {
        foreach (var existing in _addresses.Where(a => a.Type == type && a.IsDefault && a.AddressId != except))
        {
            existing.ClearDefault(now, actor);
        }
    }
}
