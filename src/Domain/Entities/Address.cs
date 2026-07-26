using Kart.User.Domain.Enums;

namespace Kart.User.Domain.Entities;

/// <summary>
/// Child entity of <see cref="UserProfile"/> (ddd-model.md) — never its own aggregate root.
/// Every write here happens inside the same transaction as its parent's <c>UpdatedAt</c> bump,
/// enforcing the per-type default-address invariant atomically.
/// </summary>
public sealed class Address
{
    public Guid AddressId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public AddressType Type { get; private set; }
    public string Line1 { get; private set; } = string.Empty;
    public string? Line2 { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string? Region { get; private set; }
    public string PostalCode { get; private set; } = string.Empty;
    public string CountryCode { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public bool IsDefault { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = string.Empty;

    private Address() { }

    public static Address Create(
        string userId,
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
        return new Address
        {
            AddressId = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Line1 = line1,
            Line2 = line2,
            City = city,
            Region = region,
            PostalCode = postalCode,
            CountryCode = countryCode,
            Phone = phone,
            IsDefault = isDefault,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actor,
            UpdatedBy = actor
        };
    }

    /// <summary>Whole-record replace (edge-cases.md's last-write-wins concurrency decision).</summary>
    public void ReplaceWith(
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
        Type = type;
        Line1 = line1;
        Line2 = line2;
        City = city;
        Region = region;
        PostalCode = postalCode;
        CountryCode = countryCode;
        Phone = phone;
        IsDefault = isDefault;
        UpdatedAt = now;
        UpdatedBy = actor;
    }

    public void ClearDefault(DateTimeOffset now, string actor)
    {
        if (!IsDefault)
        {
            return;
        }

        IsDefault = false;
        UpdatedAt = now;
        UpdatedBy = actor;
    }

    /// <summary>ADR-0016 item 2 — one-directional PII tombstone.</summary>
    public void Tombstone(DateTimeOffset now)
    {
        Line1 = "[erased]";
        Line2 = null;
        Phone = null;
        UpdatedAt = now;
        UpdatedBy = "system:user-erasure-workflow";
    }
}
