using Klyvesta.Application.Ledger;
using Klyvesta.Domain.Ledger;

namespace Klyvesta.Infrastructure.Ledger;

public sealed class InMemoryLedgerJournalStore : ILedgerJournalStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, LedgerJournalEntry> _entries = [];
    private readonly Dictionary<(string Actor, string Operation, string Key), Guid> _idempotency = [];
    private readonly Dictionary<Guid, Guid> _reversals = [];

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public ValueTask<LedgerCommitResult> CommitAsync(
        LedgerJournalEntry candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var scope = (candidate.ActorReference, candidate.Operation, candidate.IdempotencyKey);
            if (_idempotency.TryGetValue(scope, out var existingEntryId))
            {
                var existing = _entries[existingEntryId];
                if (!StringComparer.Ordinal.Equals(existing.NormalizedRequestHash, candidate.NormalizedRequestHash))
                {
                    throw new InvalidOperationException("LEDGER_IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD");
                }

                return ValueTask.FromResult(new LedgerCommitResult(existing, WasExisting: true));
            }

            if (candidate.ReversesEntryId is { } originalEntryId)
            {
                if (!_entries.ContainsKey(originalEntryId))
                {
                    throw new InvalidOperationException("LEDGER_REVERSAL_TARGET_NOT_FOUND");
                }

                if (_reversals.TryGetValue(originalEntryId, out var existingReversalId))
                {
                    throw new InvalidOperationException(
                        $"LEDGER_ENTRY_ALREADY_REVERSED:{existingReversalId:D}");
                }
            }

            _entries.Add(candidate.EntryId, candidate);
            _idempotency.Add(scope, candidate.EntryId);
            if (candidate.ReversesEntryId is { } reversalTarget)
            {
                _reversals.Add(reversalTarget, candidate.EntryId);
            }

            return ValueTask.FromResult(new LedgerCommitResult(candidate, WasExisting: false));
        }
    }

    public ValueTask<LedgerJournalEntry?> FindByIdAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _entries.TryGetValue(entryId, out var entry);
            return ValueTask.FromResult(entry);
        }
    }

    public ValueTask<IReadOnlyList<LedgerJournalEntry>> ListByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account ID is required.", nameof(accountId));
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            IReadOnlyList<LedgerJournalEntry> entries = _entries.Values
                .Where(entry => entry.Lines.Any(line => line.AccountId == accountId))
                .OrderBy(entry => entry.PostedAt)
                .ThenBy(entry => entry.EntryId)
                .ToArray();
            return ValueTask.FromResult(entries);
        }
    }
}
