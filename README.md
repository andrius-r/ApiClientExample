# ApiClientExample
Example client with HMAC authentication.

## Configuration

Configure the external API under `ExternalApi`:

- `Url`: absolute HTTPS endpoint
- `Credential`: client identifier sent in the HMAC authorization header
- `Secret`: shared secret used to sign the JSON request body (in Base64)

The app submits JSON in this shape:

```json
{
  "text": "user entered text",
  "fileName": "example.txt",
  "contentType": "text/plain",
  "fileContentBase64": "..."
}
```

The request uses this header format:

```text
X-Timestamp: <unix-timestamp>
x-ms-content-sha256: <base64-sha256-request-body>
Authorization: HMAC-SHA256 Credential=<credential>&SignedHeaders=host;x-timestamp;x-ms-content-sha256&Signature=<base64-signature>
```

The signature is `HMACSHA256(secret, "<HTTP method>\n<path and query>\n<host>;<timestamp>;<content digest>")`.
