using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ApiClientExample;

public sealed class ExternalApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IOptions<ExternalApiOptions> _options;

    public ExternalApiClient(HttpClient httpClient, IOptions<ExternalApiOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<ExternalApiResult> SendAsync(ExternalApiRequest request, CancellationToken cancellationToken)
    {
        var options = _options.Value;

        if (string.IsNullOrWhiteSpace(options.Url) ||
            string.IsNullOrWhiteSpace(options.KeyId) ||
            string.IsNullOrWhiteSpace(options.Secret))
        {
            throw new InvalidOperationException("External API configuration is incomplete.");
        }

        if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("External API URL must be an absolute HTTPS address.");
        }

        var json = JsonSerializer.Serialize(request, SerializerOptions);
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var signature = CreateSignature(options.Secret, timestamp, json);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "HMAC",
            $"{options.KeyId}:{timestamp}:{signature}");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        return new ExternalApiResult(response.IsSuccessStatusCode, response.StatusCode, responseBody);
    }

    private static string CreateSignature(string secret, string timestamp, string json)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var payload = Encoding.UTF8.GetBytes($"{timestamp}\n{json}");
        return Convert.ToBase64String(hmac.ComputeHash(payload));
    }
}
