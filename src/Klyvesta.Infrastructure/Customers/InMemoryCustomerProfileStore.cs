using Klyvesta.Application.Customers;
using Klyvesta.Domain.Customers;

namespace Klyvesta.Infrastructure.Customers;

public sealed class InMemoryCustomerProfileStore : ICustomerProfileStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, CustomerWorkspace> _customers = [];

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _customers.Count;
            }
        }
    }

    public ValueTask<CustomerWorkspace?> FindAsync(
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
            _customers.TryGetValue(customerId, out var workspace);
            return ValueTask.FromResult(workspace);
        }
    }

    public ValueTask<CustomerWorkspace> CommitAsync(
        CustomerWorkspace candidate,
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
            if (_customers.TryGetValue(candidate.CustomerId, out var current))
            {
                if (current.Revision != expectedRevision)
                {
                    throw new InvalidOperationException("CUSTOMER_DATA_REVISION_CONFLICT");
                }
            }
            else if (expectedRevision != 0)
            {
                throw new InvalidOperationException("CUSTOMER_DATA_REVISION_CONFLICT");
            }

            if (candidate.Revision != expectedRevision + 1)
            {
                throw new InvalidOperationException("CUSTOMER_DATA_INVALID_NEXT_REVISION");
            }

            _customers[candidate.CustomerId] = candidate;
            return ValueTask.FromResult(candidate);
        }
    }
}
