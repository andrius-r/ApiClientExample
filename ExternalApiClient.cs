using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace ApiClientExample;

public sealed class ExternalApiClient(HttpClient httpClient, IOptions<ExternalApiOptions> options)
{
    private readonly IOptions<ExternalApiOptions> options = options;

    const string hostHeader = "Host";
    const string timestampHeader = "X-Timestamp";
    const string contentDigestHeader = "Content-Digest";

    public async Task<ExternalApiResult> SendAsync(ExternalApiRequest request, CancellationToken cancellationToken)
    {
        var options = this.options.Value;

        if (string.IsNullOrWhiteSpace(options.Url) ||
            string.IsNullOrWhiteSpace(options.Credential) ||
            string.IsNullOrWhiteSpace(options.Secret))
        {
            throw new InvalidOperationException("External API configuration is incomplete.");
        }

        if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("External API URL must be an absolute HTTPS address.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(request)
        };
        await Sign(httpRequest, options.Credential, options.Secret, cancellationToken);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        return new ExternalApiResult(response.IsSuccessStatusCode, response.StatusCode, responseBody);
    }

    private static async Task Sign(HttpRequestMessage httpRequest, string credential, string secret, CancellationToken cancellationToken)
    {
        if (httpRequest.RequestUri == null) throw new NullReferenceException(nameof(httpRequest.RequestUri) + " is null");
        if (httpRequest.Content == null) throw new NullReferenceException(nameof(httpRequest.Content) + " is null");

        string host = httpRequest.RequestUri.Authority;
        string method = httpRequest.Method.ToString().ToUpperInvariant();
        string pathAndQuery = httpRequest.RequestUri.PathAndQuery;
        var contentBytes = await httpRequest.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentHash = Convert.ToHexString(SHA256.HashData(contentBytes));
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = CreateSignature(secret, method, pathAndQuery, host, timestamp, contentHash);
        var signedHeaders = String.Join(';', hostHeader, timestampHeader, contentDigestHeader).ToLowerInvariant();

        // Host header is added automatically by middleware.
        httpRequest.Headers.Add(timestampHeader, timestamp);
        httpRequest.Headers.Add(contentDigestHeader, contentHash);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "HMAC-SHA256",
            $"Credential={credential}" +
            $"&SignedHeaders={signedHeaders}" +
            $"&Signature={signature}");
    }

    private static string CreateSignature(string secret, string method, string pathAndQuery, params string[] signedValues)
    {
        var payload = Encoding.UTF8.GetBytes($"{method}\n{pathAndQuery}\n{string.Join(';', signedValues)}");
        return Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload));
    }
}
