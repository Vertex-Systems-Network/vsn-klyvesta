using System.Collections.ObjectModel;
using Klyvesta.Domain.Notifications;

namespace Klyvesta.Domain.Preferences;

public sealed record CustomerNotificationChannelPreference(
    NotificationChannel Channel,
    bool Enabled);

public sealed record CustomerPreferenceAuthority(
    bool CustomerScoped,
    bool ContainsContactPii,
    bool CanDispatchNotifications,
    bool CanChangeSecurityPolicy,
    bool CanPlaceOrders)
{
    public static CustomerPreferenceAuthority PreferenceOnly { get; } =
        new(
            CustomerScoped: true,
            ContainsContactPii: false,
            CanDispatchNotifications: false,
            CanChangeSecurityPolicy: false,
            CanPlaceOrders: false);
}

public sealed class CustomerPreferenceSnapshot
{
    private readonly ReadOnlyCollection<CustomerNotificationChannelPreference> _notificationChannels;

    public CustomerPreferenceSnapshot(
        Guid customerId,
        long revision,
        IEnumerable<CustomerNotificationChannelPreference> notificationChannels,
        DateTimeOffset updatedAt)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID is required.", nameof(customerId));
        }

        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision), "Preference revision cannot be negative.");
        }

        if (updatedAt == default)
        {
            throw new ArgumentException("Preference update time is required.", nameof(updatedAt));
        }

        ArgumentNullException.ThrowIfNull(notificationChannels);
        var normalized = notificationChannels
            .Select(NormalizeChannelPreference)
            .OrderBy(preference => preference.Channel)
            .ToArray();
        if (normalized.Select(preference => preference.Channel).Distinct().Count() != normalized.Length)
        {
            throw new ArgumentException("Notification channel preferences must be unique.", nameof(notificationChannels));
        }

        CustomerId = customerId;
        Revision = revision;
        _notificationChannels = Array.AsReadOnly(normalized);
        UpdatedAt = updatedAt;
    }

    public Guid CustomerId { get; }

    public long Revision { get; }

    public IReadOnlyList<CustomerNotificationChannelPreference> NotificationChannels => _notificationChannels;

    public DateTimeOffset UpdatedAt { get; }

    public CustomerPreferenceAuthority Authority =>
        CustomerPreferenceAuthority.PreferenceOnly with { CustomerScoped = CustomerId != Guid.Empty };

    public bool IsEnabled(NotificationChannel channel)
    {
        ValidateChannel(channel);
        return _notificationChannels.FirstOrDefault(preference => preference.Channel == channel)?.Enabled ?? false;
    }

    public CustomerPreferenceSnapshot WithChannel(
        NotificationChannel channel,
        bool enabled,
        DateTimeOffset updatedAt)
    {
        ValidateChannel(channel);
        var channels = _notificationChannels
            .Where(preference => preference.Channel != channel)
            .Append(new CustomerNotificationChannelPreference(channel, enabled));
        return new CustomerPreferenceSnapshot(CustomerId, Revision + 1, channels, updatedAt);
    }

    public static CustomerPreferenceSnapshot PrivacySafeDefaults(Guid customerId, DateTimeOffset updatedAt)
    {
        return new CustomerPreferenceSnapshot(
            customerId,
            revision: 0,
            [
                new CustomerNotificationChannelPreference(NotificationChannel.Email, Enabled: false),
                new CustomerNotificationChannelPreference(NotificationChannel.Sms, Enabled: false),
                new CustomerNotificationChannelPreference(NotificationChannel.Push, Enabled: false),
                new CustomerNotificationChannelPreference(NotificationChannel.InApp, Enabled: true),
                new CustomerNotificationChannelPreference(NotificationChannel.WhatsApp, Enabled: false),
            ],
            updatedAt);
    }

    private static CustomerNotificationChannelPreference NormalizeChannelPreference(
        CustomerNotificationChannelPreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);
        ValidateChannel(preference.Channel);
        return preference;
    }

    public static void ValidateChannel(NotificationChannel channel)
    {
        if (channel == NotificationChannel.Unknown || !Enum.IsDefined(channel))
        {
            throw new ArgumentException("A supported notification channel is required.", nameof(channel));
        }
    }
}
