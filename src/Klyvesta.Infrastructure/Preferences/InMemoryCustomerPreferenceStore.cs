using Klyvesta.Application.Preferences;
using Klyvesta.Domain.Preferences;

namespace Klyvesta.Infrastructure.Preferences;

public sealed class InMemoryCustomerPreferenceStore : ICustomerPreferenceStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, CustomerPreferenceSnapshot> _preferences = [];

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _preferences.Count;
            }
        }
    }

    public ValueTask<CustomerPreferenceSnapshot?> FindAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID is required.", nameof(customerId));
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _preferences.TryGetValue(customerId, out var snapshot);
            return ValueTask.FromResult(snapshot);
        }
    }

    public ValueTask<CustomerPreferenceSnapshot> CommitAsync(
        CustomerPreferenceSnapshot candidate,
        long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (expectedRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedRevision), "Expected revision cannot be negative.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_preferences.TryGetValue(candidate.CustomerId, out var current))
            {
                if (current.Revision != expectedRevision)
                {
                    throw new InvalidOperationException("CUSTOMER_PREFERENCE_REVISION_CONFLICT");
                }
            }
            else if (expectedRevision != 0)
            {
                throw new InvalidOperationException("CUSTOMER_PREFERENCE_REVISION_CONFLICT");
            }

            if (candidate.Revision != expectedRevision + 1)
            {
                throw new InvalidOperationException("CUSTOMER_PREFERENCE_INVALID_NEXT_REVISION");
            }

            _preferences[candidate.CustomerId] = candidate;
            return ValueTask.FromResult(candidate);
        }
    }
}
