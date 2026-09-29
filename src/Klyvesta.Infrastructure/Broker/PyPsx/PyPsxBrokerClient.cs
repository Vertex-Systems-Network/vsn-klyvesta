using System.Net.Http.Json;
using System.Text.Json;

namespace Klyvesta.Infrastructure.Broker.PyPsx;

public sealed class PyPsxBrokerClient(HttpClient httpClient, PyPsxBrokerOptions options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<PyPsxBrokerResult<JsonElement?>> GetHealthAsync(CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, "/health", "Health", authenticated: false, body: null, cancellationToken);

    public Task<PyPsxBrokerResult<JsonElement?>> GetConfigAsync(CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, "/v1/partner-api/config", "GetConfig", authenticated: true, body: null, cancellationToken);

    public Task<PyPsxBrokerResult<JsonElement?>> GetAccountAsync(
        string accountId,
        CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, $"/v1/partner-api/accounts/{Uri.EscapeDataString(accountId)}", "GetAccount", authenticated: true, body: null, cancellationToken);

    public Task<PyPsxBrokerResult<JsonElement?>> GetPortfolioAsync(
        string accountId,
        CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, $"/v1/partner-api/accounts/{Uri.EscapeDataString(accountId)}/portfolio", "GetPortfolio", authenticated: true, body: null, cancellationToken);

    public Task<PyPsxBrokerResult<JsonElement?>> GetStatementAsync(
        string accountId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, $"/v1/partner-api/accounts/{Uri.EscapeDataString(accountId)}/statement?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", "GetStatement", authenticated: true, body: null, cancellationToken);

    public Task<PyPsxBrokerResult<JsonElement?>> SubmitOrderAsync(
        string accountId,
        string symbol,
        string side,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Order quantity must be positive.");
        }

        var body = new
        {
            sub_account_id = accountId,
            symbol,
            side,
            quantity
        };

        return SendAsync(HttpMethod.Post, "/v1/partner-api/orders", "SubmitOrder", authenticated: true, body, cancellationToken);
    }

    public Task<PyPsxBrokerResult<JsonElement?>> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, $"/v1/partner-api/orders/{Uri.EscapeDataString(orderId)}", "GetOrder", authenticated: true, body: null, cancellationToken);

    public Task<PyPsxBrokerResult<JsonElement?>> CancelOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Delete, $"/v1/partner-api/orders/{Uri.EscapeDataString(orderId)}", "CancelOrder", authenticated: true, body: null, cancellationToken);

    private async Task<PyPsxBrokerResult<JsonElement?>> SendAsync(
        HttpMethod method,
        string path,
        string operation,
        bool authenticated,
        object? body,
        CancellationToken cancellationToken)
    {
        options.Validate();
        var requestId = Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Klyvesta-Request-Id", requestId);

        if (authenticated)
        {
            request.Headers.Add("PYPSX-ORG-API-KEY-ID", options.KeyId);
            request.Headers.Add("PYPSX-ORG-API-SECRET-KEY", options.KeySecret);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var parsed = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
            JsonElement? value = parsed;

            if (response.IsSuccessStatusCode)
            {
                return new(requestId, operation, PyPsxResultState.Success, value, (int)response.StatusCode, null, null);
            }

            var state = (int)response.StatusCode >= 500
                ? PyPsxResultState.RetryableFailure
                : PyPsxResultState.Rejected;
            return new(requestId, operation, state, value, (int)response.StatusCode, null, $"HTTP_{(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(requestId, operation, PyPsxResultState.Unknown, null, null, null, "TIMEOUT_OR_AMBIGUOUS");
        }
        catch (HttpRequestException)
        {
            return new(requestId, operation, PyPsxResultState.Unknown, null, null, null, "NETWORK_FAILURE");
        }
    }
}
