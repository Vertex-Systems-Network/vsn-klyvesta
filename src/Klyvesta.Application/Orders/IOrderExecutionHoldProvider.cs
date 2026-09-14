namespace Klyvesta.Application.Orders;

public sealed record OrderExecutionHold(bool IsHeld, string? ReasonCode)
{
    public static OrderExecutionHold Allow { get; } = new(false, null);

    public static OrderExecutionHold Hold(string reasonCode)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            throw new ArgumentException("Hold reason code is required.", nameof(reasonCode));
        }

        return new OrderExecutionHold(true, reasonCode);
    }
}

public interface IOrderExecutionHoldProvider
{
    OrderExecutionHold GetHold(string accountReference);
}
