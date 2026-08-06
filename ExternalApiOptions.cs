namespace ApiClientExample;

public sealed class ExternalApiOptions
{
    public const string SectionName = "ExternalApi";

    public string Url { get; set; } = string.Empty;

    public string Credential { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;
}
