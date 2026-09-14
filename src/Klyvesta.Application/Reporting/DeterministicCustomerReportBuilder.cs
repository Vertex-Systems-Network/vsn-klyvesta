using Klyvesta.Domain.Portfolios;
using Klyvesta.Domain.Reporting;

namespace Klyvesta.Application.Reporting;

public sealed record CustomerReportRequest(
    Guid AuthenticatedCustomerId,
    Guid CustomerId,
    string AccountReference,
    CustomerReportPeriod Period,
    DateTimeOffset GeneratedAt,
    PortfolioProjectionSnapshot Portfolio,
    CustomerReportDeliveryMetadata Delivery);

public interface ICustomerReportBuilder
{
    CustomerPortfolioReport Build(CustomerReportRequest request);
}

public sealed class DeterministicCustomerReportBuilder : ICustomerReportBuilder
{
    public CustomerPortfolioReport Build(CustomerReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureCustomerScope(request.AuthenticatedCustomerId, request.CustomerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AccountReference);
        ArgumentNullException.ThrowIfNull(request.Period);
        ArgumentNullException.ThrowIfNull(request.Portfolio);
        ArgumentNullException.ThrowIfNull(request.Delivery);

        var period = request.Period.Normalize();
        var delivery = request.Delivery.Normalize();
        var accountReference = request.AccountReference.Trim();
        ValidateGeneratedAt(request.GeneratedAt, period);
        ValidatePortfolio(request.Portfolio, accountReference);

        var positions = request.Portfolio.Positions
            .Select(position => new CustomerReportPosition(
                position.InstrumentReference.Trim(),
                position.Quantity,
                position.AverageCost,
                position.Quantity * position.AverageCost))
            .OrderBy(position => position.InstrumentReference, StringComparer.Ordinal)
            .ToArray();
        var investedCostBasis = positions.Sum(position => position.CostBasis);
        var bookValue = request.Portfolio.Cash + investedCostBasis;

        return new CustomerPortfolioReport(
            accountReference,
            period,
            request.GeneratedAt,
            request.Portfolio.Cash,
            investedCostBasis,
            bookValue,
            positions,
            request.Portfolio.LastSequence,
            request.Portfolio.UniqueSourceEventCount,
            request.Portfolio.UniqueExecutionCount,
            delivery,
            CustomerReportAuthority.ReadOnlyPaper);
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
            throw new InvalidOperationException("CUSTOMER_REPORT_SCOPE_MISMATCH");
        }
    }

    private static void ValidateGeneratedAt(DateTimeOffset generatedAt, CustomerReportPeriod period)
    {
        if (generatedAt == default)
        {
            throw new ArgumentException("Report generation time is required.", nameof(generatedAt));
        }

        var generatedDate = DateOnly.FromDateTime(generatedAt.UtcDateTime);
        if (period.EndDate > generatedDate)
        {
            throw new InvalidOperationException("CUSTOMER_REPORT_PERIOD_IN_FUTURE");
        }
    }

    private static void ValidatePortfolio(PortfolioProjectionSnapshot portfolio, string requestedAccountReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portfolio.AccountReference);
        ArgumentNullException.ThrowIfNull(portfolio.Positions);
        if (!StringComparer.Ordinal.Equals(portfolio.AccountReference.Trim(), requestedAccountReference))
        {
            throw new InvalidOperationException("CUSTOMER_REPORT_ACCOUNT_SCOPE_MISMATCH");
        }

        if (portfolio.Cash < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(portfolio), "Paper portfolio cash cannot be negative.");
        }

        if (portfolio.LastSequence < -1 || portfolio.UniqueSourceEventCount < 0 || portfolio.UniqueExecutionCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(portfolio), "Portfolio projection counters are invalid.");
        }

        if (portfolio.UniqueExecutionCount > portfolio.UniqueSourceEventCount)
        {
            throw new InvalidOperationException("CUSTOMER_REPORT_EXECUTION_COUNT_INVALID");
        }

        var duplicateInstrument = portfolio.Positions
            .GroupBy(position => position.InstrumentReference, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateInstrument is not null)
        {
            throw new InvalidOperationException("CUSTOMER_REPORT_DUPLICATE_INSTRUMENT");
        }

        foreach (var position in portfolio.Positions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(position.InstrumentReference);
            if (position.Quantity <= 0m || position.AverageCost <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(portfolio), "Paper report positions require positive quantity and average cost.");
            }
        }
    }
}
