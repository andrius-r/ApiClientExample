namespace ApiClientExample;

public sealed record ExternalApiRequest(
    string Text,
    string FileName,
    string? ContentType,
    string FileContentBase64);
