namespace ApiClientExample;

public sealed class ExternalApiOptions
{
    public const string SectionName = "ExternalApi";

    public string Url { get; set; } = string.Empty;

    public string Credential { get; set; } = string.Empty;

    public string Secret
    {
        get;
        set { secretBytes = null; field = value; }
    } = string.Empty;

    public BinaryEncodingFormat SecretEncoding
    {
        get;
        set { secretBytes = null; field = value; }
    }

    private byte[]? secretBytes;
    internal byte[] SecretBytes => secretBytes ??= SecretEncoding switch
    {
        BinaryEncodingFormat.Base64 => Convert.FromBase64String(Secret),
        BinaryEncodingFormat.Hex or _ => Convert.FromHexString(Secret),
    };
}

public enum BinaryEncodingFormat
{
    Base64,
    Hex,
}
