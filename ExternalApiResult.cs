using System.Net;

namespace ApiClientExample;

public sealed record ExternalApiResult(
    bool IsSuccessStatusCode,
    HttpStatusCode StatusCode,
    string ResponseBody,
    bool IsJson);
