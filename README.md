# Codeworx.AspNetCore.Authentication.Introspection

An ASP.NET Core authentication handler that implements **OAuth 2.0 Token Introspection ([RFC 7662](https://www.rfc-editor.org/info/rfc7662))**.

Instead of validating JWTs locally, the handler takes the bearer token from the `Authorization` HTTP header and calls the identity provider's **introspection endpoint** to verify the token's validity. This makes it ideal for **reference (opaque) tokens** and for scenarios where immediate token revocation matters.

The implementation is inspired by [Microsoft.AspNetCore.Authentication.JwtBearer](https://github.com/dotnet/aspnetcore/tree/main/src/Security/Authentication/JwtBearer/src) and adds the introspection lookup capability.

## Features

- Bearer token validation via the OAuth 2.0 introspection endpoint
- Issuer, audience, and lifetime validation of the introspection response (all enabled by default, fail closed)
- OpenID Connect discovery (`.well-known/openid-configuration`) or static configuration
- Response caching with hashed cache keys (SHA-256 — raw tokens are never used as cache keys) and optional payload encryption via ASP.NET Core Data Protection
- HTTPS enforcement for metadata **and** the introspection endpoint
- Full events pipeline (message received, payload received, authentication failed, challenge, forbidden)
- Configurable via code or `appsettings.json`

## Getting started

### 1. Install the package

```shell
dotnet add package Codeworx.AspNetCore.Authentication.Introspection
```

### 2. Register a distributed cache

Response caching is enabled by default (`EnableCache = true`) and requires an `IDistributedCache` registration:

```csharp
builder.Services.AddDistributedMemoryCache();
// or e.g. builder.Services.AddStackExchangeRedisCache(...)
```

If you don't want caching, disable it (`options.EnableCache = false`) and skip this step.

### 3. Set up authentication

```csharp
builder.Services.AddAuthentication(IntrospectionDefaults.AuthenticationScheme)
    .AddIntrospection(options =>
    {
        options.Authority = "https://idp.example.com";
        options.ClientId = "resource-server-client";
        options.ClientSecret = "secret";
        options.Audience = "api://my-api";
    });

builder.Services.AddAuthorization();
```

```csharp
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/secure", () => "Hello!")
   .RequireAuthorization();
```

The handler discovers the introspection endpoint from the authority's metadata document (`{Authority}/.well-known/openid-configuration`). Both `iss` and `aud` of the introspection response are validated — a token that is `active` but not issued for your API is rejected.

### Static configuration (skip discovery)

```csharp
.AddIntrospection(options =>
{
    options.Configuration = new OpenIdConnectConfiguration
    {
        Issuer = "https://idp.example.com",
        IntrospectionEndpoint = "https://idp.example.com/connect/introspect",
    };
    options.ClientId = "resource-server-client";
    options.ClientSecret = "secret";
    options.Audience = "api://my-api";
});
```

### Configuration via appsettings.json

Scheme options can also be bound from the standard `Authentication:Schemes` configuration section:

```json
{
  "Authentication": {
    "Schemes": {
      "Introspection": {
        "Authority": "https://idp.example.com",
        "ClientId": "resource-server-client",
        "ClientSecret": "secret",
        "Audience": "api://my-api",
        "ValidateIssuer": true,
        "ValidateAudience": true,
        "ValidateLifetime": true,
        "EnableCache": true,
        "EncryptCachedPayload": true,
        "CacheDuration": "00:10:00",
        "RequireHttpsMetadata": true,
        "ValidAudiences": [ "api://my-api", "api://my-api-v2" ],
        "ValidIssuers": [ "https://idp.example.com" ]
      }
    }
  }
}
```

Code-based configuration and configuration files can be combined; code runs afterwards and wins.

## Options reference

### Endpoint & metadata

| Option | Default | Description |
|---|---|---|
| `Authority` | — | Base URL of the identity provider. Used to build `MetadataAddress` when no explicit metadata/configuration is provided. |
| `MetadataAddress` | `{Authority}/.well-known/openid-configuration` | Discovery endpoint for OIDC metadata. |
| `Configuration` | — | Static `OpenIdConnectConfiguration`. When set, metadata discovery and the backchannel defaults are not used. Must contain `IntrospectionEndpoint`. |
| `ConfigurationManager` | — | Custom `IConfigurationManager<OpenIdConnectConfiguration>` for full control over metadata retrieval. |
| `RequireHttpsMetadata` | `true` | Requires HTTPS for the metadata address **and** the introspection endpoint. Disable only for development. |
| `RefreshInterval` | 30 s | Minimum time between metadata retrievals after a failure or explicit refresh. |
| `AutomaticRefreshInterval` | 5 min | Interval after which metadata is automatically refreshed. |

### Client credentials

| Option | Default | Description |
|---|---|---|
| `ClientId` | — | `client_id` sent to the introspection endpoint (HTTP Basic authentication). |
| `ClientSecret` | — | `client_secret` sent to the introspection endpoint (HTTP Basic authentication). |

### Validation

All validation is enabled by default and runs on every introspection response. When validation is enabled but nothing is configured (e.g. no audience), authentication **fails closed** with 401.

| Option | Default | Description |
|---|---|---|
| `ValidateIssuer` | `true` | Validates `iss` against the issuer from OIDC metadata plus `ValidIssuers`. Case-sensitive per RFC 7519. |
| `ValidIssuers` | empty | Additional accepted `iss` values. |
| `ValidateAudience` | `true` | Validates `aud` against `Audience` plus `ValidAudiences`. Case-sensitive per RFC 7519. |
| `Audience` | — | Single accepted `aud` value. |
| `ValidAudiences` | empty | Additional accepted `aud` values. |
| `ValidateLifetime` | `true` | Validates `exp`/`nbf` of the introspection response. Tokens without `exp` are rejected. |
| `ClockSkew` | 5 min | Clock skew tolerance for lifetime validation. |
| `ValidationParameters` | auto | Advanced: fully custom validation parameters. Built automatically from the options above when not set. |

### Caching

| Option | Default | Description |
|---|---|---|
| `EnableCache` | `true` | Caches introspection responses in the registered `IDistributedCache`. |
| `CacheDuration` | 10 min | Maximum cache lifetime. Entries never outlive the token's own `exp` (a 30-second buffer accounts for clock differences between the cache server and the backend). |
| `CacheKeyProvider` | SHA-256 | `Func<string, string>` deriving the cache key from the bearer token. Default: `"introspection:" + Base64Url(SHA256(token))`, so raw tokens never appear as cache keys in shared caches (e.g. Redis). |
| `EncryptCachedPayload` | `false` | Encrypts the cached introspection payload using ASP.NET Core Data Protection. Unprotect failures (e.g. key ring rotation) are logged and treated as a cache miss — the introspection endpoint is called again. |
| `AccessTokenProtector` | auto | The `IDataProtector` used for `EncryptCachedPayload`. Created automatically per scheme; override only for advanced scenarios. |

> **Note:** Cached introspection responses mean a revoked token remains accepted until its cache entry expires. Keep `CacheDuration` aligned with your revocation requirements, or disable caching.

### Backchannel

| Option | Default | Description |
|---|---|---|
| `Backchannel` | — | `HttpClient` used for metadata and introspection calls. |
| `BackchannelHttpHandler` | — | Custom `HttpMessageHandler` for the backchannel (ignored when `Backchannel` is set). |
| `BackchannelTimeout` | 1 min | Timeout for backchannel HTTP calls. |

### Miscellaneous

| Option | Default | Description |
|---|---|---|
| `SaveToken` | `true` | Stores the bearer token in `AuthenticationProperties` after successful authentication. |
| `IncludeErrorDetails` | `true` | Includes `error`/`error_description` in the `WWW-Authenticate` challenge header. Set to `false` in production to avoid disclosing failure details. |
| `Challenge` | `Introspection` | The scheme value of the `WWW-Authenticate` header. |
| `Events` | — | `IntrospectionEvents` instance — see below. |
| `Forward*` | — | Standard ASP.NET Core scheme forwarding (`ForwardAuthenticate`, `ForwardChallenge`, …). |

## Events

```csharp
.AddIntrospection(options =>
{
    options.Events = new IntrospectionEvents
    {
        // Adjust, reject, or supply the token (e.g. read it from a cookie).
        OnMessageReceived = context =>
        {
            context.Token = context.Request.Cookies["access_token"];
            return Task.CompletedTask;
        },

        // Inspect or replace the payload right after a successful introspection call.
        OnPayloadReceived = context =>
        {
            return Task.CompletedTask;
        },

        // Inspect failures; set context.Result to suppress the exception.
        OnAuthenticationFailed = context =>
        {
            return Task.CompletedTask;
        },

        // Customize the 401 challenge.
        OnChallenge = context => Task.CompletedTask,

        // Customize the 403 response.
        OnForbidden = context => Task.CompletedTask,
    };
});
```

## Security considerations

- **HTTPS everywhere:** with `RequireHttpsMetadata = true` (default), both the metadata address and the introspection endpoint must use HTTPS — otherwise startup/request processing fails. This protects the bearer token and your client credentials in transit.
- **Audience validation is your job:** per RFC 7662, `active: true` does not mean the token was issued for *your* API. Always configure `Audience`/`ValidAudiences` — the handler enforces this by default.
- **Cache hygiene:** cache keys are SHA-256 hashes of the token, never the token itself. Enable `EncryptCachedPayload` when the cache infrastructure is shared or not fully trusted.
- **Error details:** set `IncludeErrorDetails = false` in production to keep failure reasons out of the `WWW-Authenticate` header.

## License

[MIT](LICENSE)
