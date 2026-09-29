using System.Net.Http.Json;
using System.Runtime.CompilerServices;
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

    public Task<PyPsxBrokerResult<JsonElement?>> GetFeesAsync(
        CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Get,
            "/v1/partner-api/fees",
            "GetFees",
            authenticated: true,
            body: null,
            cancellationToken);

    public Task<PyPsxBrokerResult<JsonElement?>> GetQuoteAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        ValidateSymbol(symbol);
        return SendAsync(
            HttpMethod.Get,
            $"/v1/partner-api/market/quote/{Uri.EscapeDataString(symbol)}",
            "GetQuote",
            authenticated: true,
            body: null,
            cancellationToken);
    }

    public Task<PyPsxBrokerResult<JsonElement?>> GetQuotesAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        var normalizedSymbols = symbols
            .Select(symbol => symbol?.Trim().ToUpperInvariant())
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedSymbols.Length == 0)
        {
            throw new ArgumentException("At least one symbol is required.", nameof(symbols));
        }

        return SendAsync(
            HttpMethod.Get,
            $"/v1/partner-api/market/quotes?symbols={Uri.EscapeDataString(string.Join(",", normalizedSymbols))}",
            "GetQuotes",
            authenticated: true,
            body: null,
            cancellationToken);
    }

    public Task<PyPsxBrokerResult<JsonElement?>> GetMarketDepthAsync(
        string symbol,
        int levels = 5,
        CancellationToken cancellationToken = default)
    {
        ValidateSymbol(symbol);
        if (levels is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(levels), "Depth levels must be between 1 and 50.");
        }

        return SendAsync(
            HttpMethod.Get,
            $"/v1/partner-api/market/depth/{Uri.EscapeDataString(symbol)}?levels={levels}",
            "GetMarketDepth",
            authenticated: true,
            body: null,
            cancellationToken);
    }

    public Task<PyPsxBrokerResult<JsonElement?>> GetKlinesAsync(
        string symbol,
        int limit = 30,
        CancellationToken cancellationToken = default)
    {
        ValidateSymbol(symbol);
        if (limit is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Kline limit must be between 1 and 200.");
        }

        return SendAsync(
            HttpMethod.Get,
            $"/v1/partner-api/market/klines/{Uri.EscapeDataString(symbol)}?limit={limit}",
            "GetKlines",
            authenticated: true,
            body: null,
            cancellationToken);
    }

    public Task<PyPsxBrokerResult<JsonElement?>> GetInstrumentsAsync(
        CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Get,
            "/v1/partner-api/market/instruments",
            "GetInstruments",
            authenticated: true,
            body: null,
            cancellationToken);

    private static void ValidateSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("A market symbol is required.", nameof(symbol));
        }
    }

    public Task<PyPsxBrokerResult<JsonElement?>> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, $"/v1/partner-api/orders/{Uri.EscapeDataString(orderId)}", "GetOrder", authenticated: true, body: null, cancellationToken);

    public Task<PyPsxBrokerResult<JsonElement?>> CancelOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Delete, $"/v1/partner-api/orders/{Uri.EscapeDataString(orderId)}", "CancelOrder", authenticated: true, body: null, cancellationToken);

    public async IAsyncEnumerable<PyPsxStreamEvent> StreamAsync(
        string channels = "prices,fills",
        int maxReconnectAttempts = 3,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(channels))
        {
            throw new ArgumentException("At least one stream channel is required.", nameof(channels));
        }

        if (maxReconnectAttempts < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxReconnectAttempts));
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                options.Validate();
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"/v1/partner-api/stream?channels={Uri.EscapeDataString(channels)}");
                request.Headers.Add("PYPSX-ORG-API-KEY-ID", options.KeyId);
                request.Headers.Add("PYPSX-ORG-API-SECRET-KEY", options.KeySecret);
                request.Headers.Add("Accept", "text/event-stream");

                using var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);
                string? eventName = null;
                while (await reader.ReadLineAsync(cancellationToken) is { } line)
                {
                    if (line.Length == 0)
                    {
                        eventName = null;
                        continue;
                    }

                    if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
                    {
                        eventName = line["event:".Length..].Trim();
                        continue;
                    }

                    if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return new PyPsxStreamEvent(
                            eventName ?? "message",
                            line["data:".Length..].Trim(),
                            DateTimeOffset.UtcNow);
                    }
                }

                if (attempt >= maxReconnectAttempts)
                {
                    yield break;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt >= maxReconnectAttempts)
                {
                    yield break;
                }
            }
            catch (HttpRequestException)
            {
                if (attempt >= maxReconnectAttempts)
                {
                    yield break;
                }
            }

            var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
            await Task.Delay(delay, cancellationToken);
        }
    }

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
