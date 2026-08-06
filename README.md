# ApiClientExample
Example client with HMAC authentication.

## Configuration

Configure the external API under `ExternalApi`:

- `Url`: absolute HTTPS endpoint
- `KeyId`: client identifier sent in the HMAC authorization header
- `Secret`: shared secret used to sign the JSON request body

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
Authorization: HMAC <keyId>:<timestamp>:<base64-signature>
```

The signature is `HMACSHA256(secret, "<timestamp>\n<json-body>")`.
