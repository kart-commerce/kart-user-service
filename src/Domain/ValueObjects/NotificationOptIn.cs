namespace Kart.User.Domain.ValueObjects;

/// <summary>Per-channel notification opt-in flags, nested inside <see cref="Preferences"/>.</summary>
public sealed class NotificationOptIn
{
    public bool Email { get; private set; }
    public bool Sms { get; private set; }
    public bool Push { get; private set; }

    private NotificationOptIn() { }

    public NotificationOptIn(bool email, bool sms, bool push)
    {
        Email = email;
        Sms = sms;
        Push = push;
    }

    public static NotificationOptIn Default => new(email: false, sms: false, push: false);
}
