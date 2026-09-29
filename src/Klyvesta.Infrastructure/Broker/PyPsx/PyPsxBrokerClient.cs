using System.Net.Http.Json;
using System.Text.Json;

namespace Klyvesta.Infrastructure.Broker.PyPsx;

public sealed class PyPsxBrokerClient(HttpClient httpClient, PyPsxBrokerOptions options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PyPsxBrokerResult<JsonElement?>> GetHealthAsync(CancellationToken cancellationToken = default)
        => await GetAsync("/health", "Health", authenticated: false, cancellationToken);

    public async Task<PyPsxBrokerResult<JsonElement?>> GetConfigAsync(CancellationToken cancellationToken = default)
        => await GetAsync("/v1/partner-api/config", "GetConfig", authenticated: true, cancellationToken);

    public async Task<PyPsxBrokerResult<JsonElement?>> GetAccountAsync(
        string accountId,
        CancellationToken cancellationToken = default)
        => await GetAsync($"/v1/partner-api/accounts/{Uri.EscapeDataString(accountId)}", "GetAccount", authenticated: true, cancellationToken);

    public async Task<PyPsxBrokerResult<JsonElement?>> GetPortfolioAsync(
        string accountId,
        CancellationToken cancellationToken = default)
        => await GetAsync($"/v1/partner-api/accounts/{Uri.EscapeDataString(accountId)}/portfolio", "GetPortfolio", authenticated: true, cancellationToken);

    public async Task<PyPsxBrokerResult<JsonElement?>> GetStatementAsync(
        string accountId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
        => await GetAsync($"/v1/partner-api/accounts/{Uri.EscapeDataString(accountId)}/statement?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", "GetStatement", authenticated: true, cancellationToken);

    private async Task<PyPsxBrokerResult<JsonElement?>> GetAsync(
        string path,
        string operation,
        bool authenticated,
        CancellationToken cancellationToken)
    {
        options.Validate();
        var requestId = Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Klyvesta-Request-Id", requestId);

        if (authenticated)
        {
            request.Headers.Add("PYPSX-ORG-API-KEY-ID", options.KeyId);
            request.Headers.Add("PYPSX-ORG-API-SECRET-KEY", options.KeySecret);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);

            if (response.IsSuccessStatusCode && body is JsonElement value)
            {
                return new(requestId, operation, PyPsxResultState.Success, value, (int)response.StatusCode, null, null);
            }

            var state = (int)response.StatusCode >= 500
                ? PyPsxResultState.RetryableFailure
                : PyPsxResultState.Rejected;
            return new(requestId, operation, state, body, (int)response.StatusCode, null, $"HTTP_{(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(requestId, operation, PyPsxResultState.Unknown, null, null, null, "TIMEOUT_OR_AMBIGUOUS_READ");
        }
        catch (HttpRequestException)
        {
            return new(requestId, operation, PyPsxResultState.Unknown, null, null, null, "NETWORK_FAILURE");
        }
    }
}
