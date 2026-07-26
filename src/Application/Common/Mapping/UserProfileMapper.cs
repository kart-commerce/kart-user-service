using Kart.User.Application.Common.Models;
using Kart.User.Domain.Entities;

namespace Kart.User.Application.Common.Mapping;

/// <summary>
/// Single source of truth for projecting the PostgreSQL write-model aggregate into the shape
/// shared by the MongoDB read model and the API response (see
/// <see cref="UserProfileResponse"/>'s own doc comment). Used both by command handlers building
/// an immediate response and by <c>Infrastructure/Messaging/ReadModelProjectionHostedService</c>
/// rebuilding the Mongo projection — the read model "must be rebuildable from the PostgreSQL
/// write model" (requirement-spec.md §4), and this is the one place that rebuild logic lives.
/// </summary>
public static class UserProfileMapper
{
    public static UserProfileResponse ToResponse(UserProfile profile) => new(
        UserId: profile.UserId,
        Email: profile.ContactCopy.Email,
        DisplayName: profile.ContactCopy.DisplayName,
        Addresses: profile.Addresses
            .Select(a => new AddressResponse(
                a.AddressId,
                a.Type.ToString(),
                a.Line1,
                a.Line2,
                a.City,
                a.Region,
                a.PostalCode,
                a.CountryCode,
                a.Phone,
                a.IsDefault))
            .ToList(),
        Preferences: new PreferencesResponse(
            profile.Preferences.Locale,
            profile.Preferences.Currency,
            new NotificationOptInResponse(
                profile.Preferences.NotificationOptIn.Email,
                profile.Preferences.NotificationOptIn.Sms,
                profile.Preferences.NotificationOptIn.Push),
            profile.Preferences.MarketingConsent),
        AppInstalled: profile.AppInstalled,
        ErasureStatus: profile.ErasureStatus.ToString(),
        LastUpdatedAt: profile.UpdatedAt);
}
