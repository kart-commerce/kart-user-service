using Kart.User.Application.Common.Interfaces;
using Kart.User.Application.Common.Models;
using MongoDB.Driver;

namespace Kart.User.Infrastructure.Persistence.ReadModel;

public sealed class UserReadModelRepository(IMongoCollection<UserReadModelDocument> collection) : IUserReadModelRepository
{
    public async Task<UserProfileResponse?> GetByIdAsync(string userId, CancellationToken cancellationToken)
    {
        var document = await collection.Find(d => d.Id == userId).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToResponse(document);
    }

    public async Task UpsertAsync(UserProfileResponse document, CancellationToken cancellationToken)
    {
        var mapped = ToDocument(document);
        await collection.ReplaceOneAsync(
            d => d.Id == mapped.Id,
            mapped,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    private static UserProfileResponse ToResponse(UserReadModelDocument document) => new(
        UserId: document.Id,
        Email: document.Email,
        DisplayName: document.DisplayName,
        Addresses: document.Addresses
            .Select(a => new AddressResponse(a.AddressId, a.Type, a.Line1, a.Line2, a.City, a.Region, a.PostalCode, a.CountryCode, a.Phone, a.IsDefault))
            .ToList(),
        Preferences: new PreferencesResponse(
            document.Preferences.Locale,
            document.Preferences.Currency,
            new NotificationOptInResponse(document.Preferences.NotificationOptIn.Email, document.Preferences.NotificationOptIn.Sms, document.Preferences.NotificationOptIn.Push),
            document.Preferences.MarketingConsent),
        AppInstalled: document.AppInstalled,
        ErasureStatus: document.ErasureStatus,
        LastUpdatedAt: new DateTimeOffset(document.LastUpdatedAt, TimeSpan.Zero));

    private static UserReadModelDocument ToDocument(UserProfileResponse response) => new()
    {
        Id = response.UserId,
        Email = response.Email,
        DisplayName = response.DisplayName,
        Addresses = response.Addresses
            .Select(a => new AddressDocument
            {
                AddressId = a.AddressId,
                Type = a.Type,
                Line1 = a.Line1,
                Line2 = a.Line2,
                City = a.City,
                Region = a.Region,
                PostalCode = a.PostalCode,
                CountryCode = a.CountryCode,
                Phone = a.Phone,
                IsDefault = a.IsDefault
            })
            .ToList(),
        Preferences = new PreferencesDocument
        {
            Locale = response.Preferences.Locale,
            Currency = response.Preferences.Currency,
            NotificationOptIn = new NotificationOptInDocument
            {
                Email = response.Preferences.NotificationOptIn.Email,
                Sms = response.Preferences.NotificationOptIn.Sms,
                Push = response.Preferences.NotificationOptIn.Push
            },
            MarketingConsent = response.Preferences.MarketingConsent
        },
        AppInstalled = response.AppInstalled,
        ErasureStatus = response.ErasureStatus,
        LastUpdatedAt = response.LastUpdatedAt.UtcDateTime
    };
}
