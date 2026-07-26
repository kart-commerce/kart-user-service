using MongoDB.Bson.Serialization.Attributes;

namespace Kart.User.Infrastructure.Persistence.ReadModel;

/// <summary>
/// database-design.md's <c>user_read_model</c> document shape — "a direct, whole-document
/// reflection of the single UserProfile aggregate," one document per <c>userId</c>, keyed by
/// <see cref="Id"/> for the point lookup <c>GET /v1/users/{userId}</c> needs.
/// </summary>
public sealed class UserReadModelDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public List<AddressDocument> Addresses { get; set; } = new();
    public PreferencesDocument Preferences { get; set; } = new();
    public bool AppInstalled { get; set; }
    public string ErasureStatus { get; set; } = "Active";
    public DateTime LastUpdatedAt { get; set; }
}

public sealed class AddressDocument
{
    public Guid AddressId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsDefault { get; set; }
}

public sealed class NotificationOptInDocument
{
    public bool Email { get; set; }
    public bool Sms { get; set; }
    public bool Push { get; set; }
}

public sealed class PreferencesDocument
{
    public string? Locale { get; set; }
    public string? Currency { get; set; }
    public NotificationOptInDocument NotificationOptIn { get; set; } = new();
    public bool MarketingConsent { get; set; }
}
