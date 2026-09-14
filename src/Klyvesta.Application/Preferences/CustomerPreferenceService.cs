using Klyvesta.Domain.Notifications;
using Klyvesta.Domain.Preferences;

namespace Klyvesta.Application.Preferences;

public interface ICustomerPreferenceStore
{
    ValueTask<CustomerPreferenceSnapshot?> FindAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    ValueTask<CustomerPreferenceSnapshot> CommitAsync(
        CustomerPreferenceSnapshot candidate,
        long expectedRevision,
        CancellationToken cancellationToken = default);
}

public sealed class CustomerPreferenceService
{
    private readonly ICustomerPreferenceStore _store;
    private readonly TimeProvider _timeProvider;

    public CustomerPreferenceService(ICustomerPreferenceStore store, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _store = store;
        _timeProvider = timeProvider;
    }

    public async ValueTask<CustomerPreferenceSnapshot> GetAsync(
        Guid authenticatedCustomerId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        EnsureOwnership(authenticatedCustomerId, customerId);
        return await _store.FindAsync(customerId, cancellationToken).ConfigureAwait(false)
            ?? CustomerPreferenceSnapshot.PrivacySafeDefaults(customerId, DateTimeOffset.UnixEpoch);
    }

    public async ValueTask<CustomerPreferenceSnapshot> SetNotificationChannelAsync(
        Guid authenticatedCustomerId,
        Guid customerId,
        long expectedRevision,
        NotificationChannel channel,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        EnsureOwnership(authenticatedCustomerId, customerId);
        CustomerPreferenceSnapshot.ValidateChannel(channel);
        if (expectedRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedRevision), "Expected revision cannot be negative.");
        }

        var current = await _store.FindAsync(customerId, cancellationToken).ConfigureAwait(false)
            ?? CustomerPreferenceSnapshot.PrivacySafeDefaults(customerId, DateTimeOffset.UnixEpoch);
        if (current.Revision != expectedRevision)
        {
            throw new InvalidOperationException("CUSTOMER_PREFERENCE_REVISION_CONFLICT");
        }

        if (current.IsEnabled(channel) == enabled)
        {
            return current;
        }

        var candidate = current.WithChannel(channel, enabled, _timeProvider.GetUtcNow());
        return await _store.CommitAsync(candidate, expectedRevision, cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureOwnership(Guid authenticatedCustomerId, Guid customerId)
    {
        if (authenticatedCustomerId == Guid.Empty)
        {
            throw new ArgumentException("Authenticated customer ID is required.", nameof(authenticatedCustomerId));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID is required.", nameof(customerId));
        }

        if (authenticatedCustomerId != customerId)
        {
            throw new InvalidOperationException("CUSTOMER_PREFERENCE_OWNERSHIP_MISMATCH");
        }
    }
}
