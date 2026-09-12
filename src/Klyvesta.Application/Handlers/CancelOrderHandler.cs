using Klyvesta.Application.Commands;
using Klyvesta.Domain.Entities;
using Klyvesta.Domain.Services;

namespace Klyvesta.Application.Handlers;

/// <summary>
/// Handles order cancellation commands.
/// </summary>
public sealed class CancelOrderHandler
{
    private readonly IBrokerAdapter _brokerAdapter;

    public CancelOrderHandler(IBrokerAdapter brokerAdapter)
    {
        _brokerAdapter = brokerAdapter;
    }

    /// <summary>
    /// Handle order cancellation.
    /// </summary>
    public async Task<CancelOrderResult> HandleAsync(
        CancelOrderCommand command,
        CancellationToken ct)
    {
        var result = await _brokerAdapter.CancelOrderAsync(command.OrderId, ct);

        return result.Success
            ? CancelOrderResult.Succeeded(command.OrderId)
            : CancelOrderResult.Failed(result.Details ?? "Cancellation failed");
    }
}

/// <summary>
/// Result of handling a cancel order command.
/// </summary>
public sealed class CancelOrderResult
{
    public bool Success { get; }
    public Guid? OrderId { get; }
    public string? Error { get; }

    private CancelOrderResult(bool success, Guid? orderId, string? error)
    {
        Success = success;
        OrderId = orderId;
        Error = error;
    }

    public static CancelOrderResult Succeeded(Guid orderId)
        => new(true, orderId, null);

    public static CancelOrderResult Failed(string error)
        => new(false, null, error);
}
