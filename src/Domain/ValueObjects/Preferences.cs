namespace Kart.User.Domain.ValueObjects;

/// <summary>
/// ddd-model.md's <c>Preferences</c> value object — requirement-spec.md §2's small fixed initial
/// set, stored as one JSONB column on the write model so additive fields don't require a schema
/// migration. Modeled here as an EF Core owned type so the write-side column and this aggregate's
/// own field stay in lockstep, per ddd-model.md.
/// </summary>
public sealed class Preferences
{
    public string? Locale { get; private set; }
    public string? Currency { get; private set; }
    public NotificationOptIn NotificationOptIn { get; private set; } = NotificationOptIn.Default;
    public bool MarketingConsent { get; private set; }

    private Preferences() { }

    public Preferences(string? locale, string? currency, NotificationOptIn notificationOptIn, bool marketingConsent)
    {
        Locale = locale;
        Currency = currency;
        NotificationOptIn = notificationOptIn;
        MarketingConsent = marketingConsent;
    }

    public static Preferences Default => new(locale: null, currency: null, NotificationOptIn.Default, marketingConsent: false);
}
