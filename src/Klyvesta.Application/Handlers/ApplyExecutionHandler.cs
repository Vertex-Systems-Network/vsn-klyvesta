using Klyvesta.Application.Commands;
using Klyvesta.Domain.Entities;
using Klyvesta.Domain.Services;

namespace Klyvesta.Application.Handlers;

/// <summary>
/// Handles execution application commands.
/// Updates positions and ledger based on broker fills.
/// </summary>
public sealed class ApplyExecutionHandler
{
    private readonly IPositionService _positionService;
    private readonly ILedgerService _ledgerService;

    public ApplyExecutionHandler(
        IPositionService positionService,
        ILedgerService ledgerService)
    {
        _positionService = positionService;
        _ledgerService = ledgerService;
    }

    /// <summary>
    /// Handle execution application.
    /// Updates position and posts ledger entries.
    /// </summary>
    public async Task<ApplyExecutionResult> HandleAsync(
        ApplyExecutionCommand command,
        CancellationToken ct)
    {
        try
        {
            // Update position based on execution
            await _positionService.ApplyExecutionAsync(command.Execution, command.Side, ct);

            // Post ledger entries
            var transaction = CreateLedgerTransaction(command);
            var postingResult = await _ledgerService.PostAsync(transaction, ct);

            if (!postingResult.Success)
                return ApplyExecutionResult.Failed(postingResult.Error ?? "Ledger posting failed");

            return ApplyExecutionResult.Succeeded(command.Execution.Id, postingResult.TransactionId!.Value);
        }
        catch (Exception ex)
        {
            return ApplyExecutionResult.Failed($"Execution application failed: {ex.Message}");
        }
    }

    private static LedgerTransaction CreateLedgerTransaction(ApplyExecutionCommand command)
    {
        var execution = command.Execution;
        var totalValue = execution.TotalValue;

        // For buy: Debit Securities Account, Credit Cash Account
        // For sell: Debit Cash Account, Credit Securities Account
        var lines = command.Side == Domain.Enums.OrderSide.Buy
            ? new[]
            {
                LedgerPostingLine.Debit(GetSecuritiesAccountId(command.CustomerId, execution), totalValue, $"Buy {execution.Quantity} {GetSymbol(execution)}"),
                LedgerPostingLine.Credit(GetCashAccountId(command.CustomerId), totalValue, $"Cash for buy {execution.Quantity} {GetSymbol(execution)}")
            }
            : new[]
            {
                LedgerPostingLine.Debit(GetCashAccountId(command.CustomerId), totalValue, $"Cash for sell {execution.Quantity} {GetSymbol(execution)}"),
                LedgerPostingLine.Credit(GetSecuritiesAccountId(command.CustomerId, execution), totalValue, $"Sell {execution.Quantity} {GetSymbol(execution)}")
            };

        return new LedgerTransaction(
            $"Execution {execution.ExecutionId} for order {execution.OrderIntentId}",
            lines,
            correlationId: execution.Id.ToString(),
            idempotencyKey: execution.ExecutionId);
    }

    private static Guid GetCashAccountId(Guid customerId)
    {
        // In real implementation, this would come from account repository
        // For now, use deterministic derivation
        return HashHelpers.Hash(customerId, "CASH");
    }

    private static Guid GetSecuritiesAccountId(Guid customerId, Execution execution)
    {
        // In real implementation, this would come from account repository
        // For now, use deterministic derivation per symbol
        return HashHelpers.Hash(customerId, execution.OrderIntentId.ToString());
    }

    private static string GetSymbol(Execution execution)
    {
        // Would need to look up symbol from order intent in real implementation
        return "UNKNOWN";
    }
}

/// <summary>
/// Result of handling an apply execution command.
/// </summary>
public sealed class ApplyExecutionResult
{
    public bool Success { get; }
    public Guid? ExecutionId { get; }
    public Guid? TransactionId { get; }
    public string? Error { get; }

    private ApplyExecutionResult(bool success, Guid? executionId, Guid? transactionId, string? error)
    {
        Success = success;
        ExecutionId = executionId;
        TransactionId = transactionId;
        Error = error;
    }

    public static ApplyExecutionResult Succeeded(Guid executionId, Guid transactionId)
        => new(true, executionId, transactionId, null);

    public static ApplyExecutionResult Failed(string error)
        => new(false, null, null, error);
}

/// <summary>
/// Simple hash helper for account ID derivation.
/// </summary>
internal static class HashHelpers
{
    public static Guid Hash(Guid seed, string salt)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes($"{seed:N}{salt}");
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return new Guid(hash.Take(16).ToArray());
    }
}
