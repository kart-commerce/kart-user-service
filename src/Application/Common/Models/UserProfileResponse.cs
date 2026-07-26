namespace Kart.User.Application.Common.Models;

/// <summary>
/// api-contract.yaml <c>UserProfileResponse</c> — also the exact shape of the <c>user_read_model</c>
/// MongoDB document (database-design.md: "a direct, whole-document reflection of the single
/// UserProfile aggregate"), so this one type serves both the Mongo storage shape and the HTTP
/// response shape rather than duplicating it as two parallel DTOs.
/// </summary>
public sealed record UserProfileResponse(
    string UserId,
    string? Email,
    string? DisplayName,
    IReadOnlyList<AddressResponse> Addresses,
    PreferencesResponse Preferences,
    bool AppInstalled,
    string ErasureStatus,
    DateTimeOffset LastUpdatedAt);

public sealed record AddressResponse(
    Guid AddressId,
    string Type,
    string Line1,
    string? Line2,
    string City,
    string? Region,
    string PostalCode,
    string CountryCode,
    string? Phone,
    bool IsDefault);

public sealed record NotificationOptInResponse(bool Email, bool Sms, bool Push);

public sealed record PreferencesResponse(
    string? Locale,
    string? Currency,
    NotificationOptInResponse NotificationOptIn,
    bool MarketingConsent);
