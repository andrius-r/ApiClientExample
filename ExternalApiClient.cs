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
    const string contentDigestHeader = "x-ms-content-sha256";

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

        return new ExternalApiResult(response.IsSuccessStatusCode, response.StatusCode, responseBody, HasJsonContentType(response));
    }

    /// <summary>
    /// Checks if the HTTP response content type represents a JSON payload.
    /// </summary>
    public static bool HasJsonContentType(HttpResponseMessage response)
    {
        // Matches 'application/json', 'text/json', or structured suffixes like 'application/problem+json'
        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        return mediaType != null && (
            mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Equals("text/json", StringComparison.OrdinalIgnoreCase) ||
            mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <param name="credential">
    /// Value for the "Credential" field. This should only include a client id, despite the definition of the word "credential" involving both an id and a proof.
    /// </param>
    /// <remarks>
    /// The implementation is largely based on
    /// <see href="https://docs.azure.cn/en-us/azure-app-configuration/rest-api-authentication-hmac#c">Microsoft Azure documentation</see>.
    /// </remarks>
    private static async Task Sign(HttpRequestMessage httpRequest, string credential, string secret, CancellationToken cancellationToken)
    {
        if (httpRequest.RequestUri == null) throw new NullReferenceException(nameof(httpRequest.RequestUri) + " is null");
        if (httpRequest.Content == null) throw new NullReferenceException(nameof(httpRequest.Content) + " is null");

        string host = httpRequest.RequestUri.Authority;
        string method = httpRequest.Method.ToString().ToUpperInvariant();
        string pathAndQuery = httpRequest.RequestUri.PathAndQuery;
        var contentBytes = await httpRequest.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentHash = Convert.ToBase64String(SHA256.HashData(contentBytes));
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var payload = Encoding.UTF8.GetBytes($"{method}\n{pathAndQuery}\n{string.Join(';', host, timestamp, contentHash)}");
        var key = Convert.FromBase64String(secret);
        var signature = Convert.ToBase64String(HMACSHA256.HashData(key, payload));
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
}
