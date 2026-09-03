using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Klyvesta.Domain.Ledger;

namespace Klyvesta.Application.Ledger;

public sealed record LedgerPostingInput(
    Guid AccountId,
    LedgerSide Side,
    decimal Amount,
    string Currency);

public sealed record LedgerPostCommand(
    string EntryType,
    string CorrelationId,
    string ActorReference,
    string Operation,
    string IdempotencyKey,
    DateTimeOffset EffectiveAt,
    IReadOnlyCollection<LedgerPostingInput> Lines,
    Guid? ReversesEntryId = null);

public sealed record LedgerCommitResult(
    LedgerJournalEntry Entry,
    bool WasExisting);

public sealed record LedgerPostResult(
    LedgerJournalEntry Entry,
    bool WasExisting);

public interface ILedgerJournalStore
{
    ValueTask<LedgerCommitResult> CommitAsync(
        LedgerJournalEntry candidate,
        CancellationToken cancellationToken = default);

    ValueTask<LedgerJournalEntry?> FindByIdAsync(
        Guid entryId,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<LedgerJournalEntry>> ListByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);
}

public sealed class LedgerPostingService
{
    private readonly ILedgerJournalStore _store;
    private readonly TimeProvider _timeProvider;

    public LedgerPostingService(ILedgerJournalStore store, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _store = store;
        _timeProvider = timeProvider;
    }

    public async ValueTask<LedgerPostResult> PostAsync(
        LedgerPostCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommand(command);

        var postings = command.Lines
            .Select(line => new LedgerPosting(line.AccountId, line.Side, line.Amount, line.Currency))
            .ToArray();
        var requestHash = LedgerRequestHasher.Compute(command, postings);
        var candidate = new LedgerJournalEntry(
            Guid.CreateVersion7(),
            command.EntryType,
            command.CorrelationId,
            command.ActorReference,
            command.Operation,
            command.IdempotencyKey,
            requestHash,
            command.EffectiveAt,
            _timeProvider.GetUtcNow(),
            command.ReversesEntryId,
            postings);

        var result = await _store.CommitAsync(candidate, cancellationToken).ConfigureAwait(false);
        return new LedgerPostResult(result.Entry, result.WasExisting);
    }

    public async ValueTask<LedgerPostResult> ReverseAsync(
        Guid originalEntryId,
        string correlationId,
        string actorReference,
        string operation,
        string idempotencyKey,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken = default)
    {
        if (originalEntryId == Guid.Empty)
        {
            throw new ArgumentException("Original entry ID is required.", nameof(originalEntryId));
        }

        var original = await _store.FindByIdAsync(originalEntryId, cancellationToken).ConfigureAwait(false);
        if (original is null)
        {
            throw new InvalidOperationException("LEDGER_ENTRY_NOT_FOUND");
        }

        var reversalLines = original.Lines
            .Select(line => new LedgerPostingInput(
                line.AccountId,
                line.Side == LedgerSide.Debit ? LedgerSide.Credit : LedgerSide.Debit,
                line.Amount,
                line.Currency))
            .ToArray();

        var command = new LedgerPostCommand(
            EntryType: $"REVERSAL:{original.EntryType}",
            CorrelationId: correlationId,
            ActorReference: actorReference,
            Operation: operation,
            IdempotencyKey: idempotencyKey,
            EffectiveAt: effectiveAt,
            Lines: reversalLines,
            ReversesEntryId: original.EntryId);

        return await PostAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateCommand(LedgerPostCommand command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.EntryType);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.CorrelationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ActorReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.IdempotencyKey);
        ArgumentNullException.ThrowIfNull(command.Lines);

        if (command.Lines.Count < 2)
        {
            throw new ArgumentException("A ledger posting command requires at least two lines.", nameof(command));
        }
    }
}

internal static class LedgerRequestHasher
{
    public static string Compute(LedgerPostCommand command, IReadOnlyCollection<LedgerPosting> postings)
    {
        var builder = new StringBuilder();
        builder.Append(command.EntryType.Trim()).Append('|');
        builder.Append(command.EffectiveAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)).Append('|');
        builder.Append(command.ReversesEntryId?.ToString("D", CultureInfo.InvariantCulture) ?? "none");

        foreach (var line in postings
                     .OrderBy(line => line.Currency, StringComparer.Ordinal)
                     .ThenBy(line => line.AccountId)
                     .ThenBy(line => line.Side)
                     .ThenBy(line => line.Amount))
        {
            builder.Append('|')
                .Append(line.Currency).Append(':')
                .Append(line.AccountId.ToString("D", CultureInfo.InvariantCulture)).Append(':')
                .Append((int)line.Side).Append(':')
                .Append(line.Amount.ToString("G29", CultureInfo.InvariantCulture));
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
