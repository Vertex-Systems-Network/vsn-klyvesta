using Klyvesta.Domain.Activity;
using Klyvesta.Domain.Ledger;
using Klyvesta.Domain.Orders;

namespace Klyvesta.Application.Activity;

public sealed record CustomerActivityRequest(
    Guid AuthenticatedCustomerId,
    Guid CustomerId,
    string AccountReference,
    DateTimeOffset AsOf,
    IReadOnlyList<CustomerOrderActivityEvidence> OrderEvents,
    IReadOnlyList<CustomerLedgerActivityEvidence> LedgerEvents);

public interface ICustomerActivityBuilder
{
    CustomerActivityTimeline Build(CustomerActivityRequest request);
}

public sealed class DeterministicCustomerActivityBuilder : ICustomerActivityBuilder
{
    public CustomerActivityTimeline Build(CustomerActivityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureCustomerScope(request.AuthenticatedCustomerId, request.CustomerId);
        var accountReference = NormalizeReference(request.AccountReference, nameof(request.AccountReference));
        if (request.AsOf == default)
        {
            throw new ArgumentException("Activity as-of time is required.", nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.OrderEvents);
        ArgumentNullException.ThrowIfNull(request.LedgerEvents);

        var items = new List<CustomerActivityItem>(request.OrderEvents.Count + request.LedgerEvents.Count);
        AddOrderEvents(items, request, accountReference);
        AddLedgerEvents(items, request, accountReference);

        var orderedItems = items
            .OrderByDescending(item => item.OccurredAt)
            .ThenBy(item => item.Source)
            .ThenBy(item => item.SourceEventId)
            .ToArray();

        return new CustomerActivityTimeline(
            request.CustomerId,
            accountReference,
            request.AsOf,
            orderedItems,
            CustomerActivityAuthority.ReadOnlyPaper);
    }

    private static void AddOrderEvents(
        List<CustomerActivityItem> items,
        CustomerActivityRequest request,
        string accountReference)
    {
        var eventIds = new HashSet<Guid>();
        foreach (var evidence in request.OrderEvents)
        {
            ArgumentNullException.ThrowIfNull(evidence);
            if (evidence.SourceEventId == Guid.Empty)
            {
                throw new ArgumentException("Order activity source event ID is required.", nameof(request));
            }

            if (!eventIds.Add(evidence.SourceEventId))
            {
                throw new InvalidOperationException("CUSTOMER_ACTIVITY_DUPLICATE_ORDER_EVENT");
            }

            if (evidence.CustomerId != request.CustomerId)
            {
                throw new InvalidOperationException("CUSTOMER_ACTIVITY_ORDER_CUSTOMER_SCOPE_MISMATCH");
            }

            if (evidence.OccurredAt == default || evidence.OccurredAt > request.AsOf)
            {
                throw new InvalidOperationException("CUSTOMER_ACTIVITY_ORDER_TIME_INVALID");
            }

            ArgumentNullException.ThrowIfNull(evidence.Intent);
            var intentAccountReference = NormalizeReference(evidence.Intent.AccountReference, nameof(evidence.Intent.AccountReference));
            if (!StringComparer.Ordinal.Equals(intentAccountReference, accountReference))
            {
                throw new InvalidOperationException("CUSTOMER_ACTIVITY_ORDER_ACCOUNT_SCOPE_MISMATCH");
            }

            var instrumentReference = NormalizeReference(
                evidence.Intent.InstrumentReference,
                nameof(evidence.Intent.InstrumentReference));
            var code = MapOrderCode(evidence.Intent, evidence.BrokerOrder, evidence.OccurredAt);
            var filledQuantity = evidence.BrokerOrder?.FilledQuantity;

            items.Add(new CustomerActivityItem(
                evidence.SourceEventId,
                CustomerActivitySource.Order,
                code,
                evidence.OccurredAt,
                instrumentReference,
                evidence.Intent.Quantity,
                filledQuantity,
                null,
                null,
                CustomerActivityFlow.None));
        }
    }

    private static CustomerActivityCode MapOrderCode(
        OrderIntent intent,
        ManagedBrokerOrder? brokerOrder,
        DateTimeOffset occurredAt)
    {
        if (brokerOrder is null)
        {
            return intent.State switch
            {
                OrderIntentState.Created => CustomerActivityCode.OrderIntentCreated,
                OrderIntentState.Validating => CustomerActivityCode.OrderValidationPending,
                OrderIntentState.Rejected => CustomerActivityCode.OrderRejected,
                OrderIntentState.Approved => CustomerActivityCode.OrderApproved,
                OrderIntentState.ExecutionPending => CustomerActivityCode.OrderExecutionPending,
                OrderIntentState.ExecutionCreated => CustomerActivityCode.OrderExecutionCreated,
                OrderIntentState.Cancelled => CustomerActivityCode.OrderCancelled,
                OrderIntentState.Expired => CustomerActivityCode.OrderExpired,
                _ => CustomerActivityCode.OrderStatusUnknown,
            };
        }

        if (intent.State != OrderIntentState.ExecutionCreated)
        {
            throw new InvalidOperationException("CUSTOMER_ACTIVITY_BROKER_STATE_WITHOUT_EXECUTION");
        }

        if (brokerOrder.OrderIntentId != intent.Id || brokerOrder.Id != intent.PlannedBrokerOrderId)
        {
            throw new InvalidOperationException("CUSTOMER_ACTIVITY_BROKER_ORDER_SCOPE_MISMATCH");
        }

        if (brokerOrder.RequestedQuantity != intent.Quantity || brokerOrder.FilledQuantity > brokerOrder.RequestedQuantity)
        {
            throw new InvalidOperationException("CUSTOMER_ACTIVITY_BROKER_QUANTITY_MISMATCH");
        }

        foreach (var execution in brokerOrder.Executions)
        {
            if (string.IsNullOrWhiteSpace(execution.ExecutionId) ||
                execution.Quantity <= 0m ||
                execution.Price <= 0m ||
                execution.TradeAt == default ||
                execution.TradeAt > occurredAt)
            {
                throw new InvalidOperationException("CUSTOMER_ACTIVITY_EXECUTION_EVIDENCE_INVALID");
            }
        }

        return brokerOrder.State switch
        {
            ManagedBrokerOrderState.PendingSubmit => CustomerActivityCode.OrderSubmitPending,
            ManagedBrokerOrderState.Submitting => CustomerActivityCode.OrderSubmitting,
            ManagedBrokerOrderState.Submitted => CustomerActivityCode.OrderSubmitted,
            ManagedBrokerOrderState.Open => CustomerActivityCode.OrderOpen,
            ManagedBrokerOrderState.PartiallyFilled => CustomerActivityCode.OrderPartiallyFilled,
            ManagedBrokerOrderState.CancelPending => CustomerActivityCode.OrderCancelPending,
            ManagedBrokerOrderState.Filled => CustomerActivityCode.OrderFilled,
            ManagedBrokerOrderState.Cancelled => CustomerActivityCode.OrderCancelled,
            ManagedBrokerOrderState.Rejected => CustomerActivityCode.OrderRejected,
            ManagedBrokerOrderState.Unknown => CustomerActivityCode.OrderStatusUnknown,
            _ => CustomerActivityCode.OrderStatusUnknown,
        };
    }

    private static void AddLedgerEvents(
        List<CustomerActivityItem> items,
        CustomerActivityRequest request,
        string accountReference)
    {
        var entryIds = new HashSet<Guid>();
        var canonicalCustomerId = request.CustomerId.ToString("D");

        foreach (var evidence in request.LedgerEvents)
        {
            ArgumentNullException.ThrowIfNull(evidence);
            if (evidence.CustomerId != request.CustomerId)
            {
                throw new InvalidOperationException("CUSTOMER_ACTIVITY_LEDGER_CUSTOMER_SCOPE_MISMATCH");
            }

            ArgumentNullException.ThrowIfNull(evidence.Entry);
            if (!entryIds.Add(evidence.Entry.EntryId))
            {
                throw new InvalidOperationException("CUSTOMER_ACTIVITY_DUPLICATE_LEDGER_EVENT");
            }

            if (evidence.Entry.PostedAt == default || evidence.Entry.PostedAt > request.AsOf)
            {
                throw new InvalidOperationException("CUSTOMER_ACTIVITY_LEDGER_TIME_INVALID");
            }

            ArgumentNullException.ThrowIfNull(evidence.Accounts);
            var accounts = BuildAccountMap(evidence.Accounts);
            foreach (var line in evidence.Entry.Lines)
            {
                if (!accounts.TryGetValue(line.AccountId, out var account))
                {
                    throw new InvalidOperationException("CUSTOMER_ACTIVITY_LEDGER_ACCOUNT_EVIDENCE_MISSING");
                }

                if (!StringComparer.Ordinal.Equals(account.Currency, line.Currency))
                {
                    throw new InvalidOperationException("CUSTOMER_ACTIVITY_LEDGER_CURRENCY_MISMATCH");
                }
            }

            var targetAccounts = accounts.Values
                .Where(account => StringComparer.Ordinal.Equals(
                    NormalizeReference(account.AccountReference, nameof(account.AccountReference)),
                    accountReference))
                .ToArray();

            if (targetAccounts.Length == 0)
            {
                throw new InvalidOperationException("CUSTOMER_ACTIVITY_LEDGER_ACCOUNT_SCOPE_MISMATCH");
            }

            if (targetAccounts.Length != 1 ||
                !StringComparer.Ordinal.Equals(targetAccounts[0].OwnerCustomerId, canonicalCustomerId))
            {
                throw new InvalidOperationException("CUSTOMER_ACTIVITY_LEDGER_OWNER_SCOPE_MISMATCH");
            }

            var targetAccount = targetAccounts[0];
            var customerLines = evidence.Entry.Lines
                .Where(line => line.AccountId == targetAccount.AccountId)
                .ToArray();
            if (customerLines.Length == 0)
            {
                throw new InvalidOperationException("CUSTOMER_ACTIVITY_LEDGER_CUSTOMER_POSTING_MISSING");
            }

            var sides = customerLines.Select(line => line.Side).Distinct().ToArray();
            var flow = sides.Length == 1
                ? sides[0] == LedgerSide.Debit ? CustomerActivityFlow.Debit : CustomerActivityFlow.Credit
                : CustomerActivityFlow.Mixed;
            var amount = customerLines.Sum(line => line.Amount);

            items.Add(new CustomerActivityItem(
                evidence.Entry.EntryId,
                CustomerActivitySource.Ledger,
                evidence.Entry.ReversesEntryId.HasValue
                    ? CustomerActivityCode.LedgerReversal
                    : CustomerActivityCode.LedgerPosted,
                evidence.Entry.PostedAt,
                null,
                null,
                null,
                amount,
                targetAccount.Currency,
                flow));
        }
    }

    private static Dictionary<Guid, LedgerAccount> BuildAccountMap(IReadOnlyList<LedgerAccount> accounts)
    {
        var result = new Dictionary<Guid, LedgerAccount>();
        foreach (var account in accounts)
        {
            ArgumentNullException.ThrowIfNull(account);
            if (!result.TryAdd(account.AccountId, account))
            {
                throw new InvalidOperationException("CUSTOMER_ACTIVITY_DUPLICATE_LEDGER_ACCOUNT");
            }
        }

        return result;
    }

    private static void EnsureCustomerScope(Guid authenticatedCustomerId, Guid customerId)
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
            throw new InvalidOperationException("CUSTOMER_ACTIVITY_SCOPE_MISMATCH");
        }
    }

    private static string NormalizeReference(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > 128 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("Reference must be at most 128 non-control characters.", parameterName);
        }

        return normalized;
    }
}
