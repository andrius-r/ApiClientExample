namespace ApiClientExample;

public sealed record ExternalApiRequest(
    string Text,
    string? FileName = null,
    string? ContentType = null,
    string? FileContentBase64 = null);
