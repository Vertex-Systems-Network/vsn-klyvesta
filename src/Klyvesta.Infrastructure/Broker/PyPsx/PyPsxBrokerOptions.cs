namespace Klyvesta.Infrastructure.Broker.PyPsx;

public sealed class PyPsxBrokerOptions
{
    public const string SectionName = "PyPsx";

    public string BaseUrl { get; init; } = "https://brokerapi-paper.pypsx.com";
    public string KeyId { get; init; } = string.Empty;
    public string KeySecret { get; init; } = string.Empty;
    public string Environment { get; init; } = "sandbox";

    public void Validate()
    {
        if (!string.Equals(Environment, "sandbox", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only the pyPSX sandbox environment is permitted.");
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "brokerapi-paper.pypsx.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The pyPSX adapter only permits the documented sandbox base URL.");
        }

        if (string.IsNullOrWhiteSpace(KeyId) || string.IsNullOrWhiteSpace(KeySecret))
        {
            throw new InvalidOperationException("pyPSX sandbox credentials are required at runtime and must remain server-side.");
        }
    }
}
