# Implementation Plan: Snakk as OAuth/SSO Provider

## Overview
Enable users who register on `snakk.com` to seamlessly authenticate on custom community domains (e.g., `forum.example.com`) using a "Login with Snakk" flow.

**Goal**: When a user visits a custom domain, they can click "Login with Snakk" and be authenticated using their existing Snakk account.

---

## Architecture

```
┌─────────────────────────┐     ┌─────────────────────────┐
│  custom-domain.com      │     │  snakk.com              │
│  (Snakk.Web instance)   │     │  (Snakk.Web instance)   │
├─────────────────────────┤     ├─────────────────────────┤
│ 1. User clicks          │     │                         │
│    "Login with Snakk"   │────▶│ 2. /oauth/authorize     │
│                         │     │    (user logs in or     │
│                         │     │     is already in)      │
│ 4. /auth/callback       │◀────│ 3. Redirect with code   │
│    Exchange code→token  │     │                         │
│    Create local session │     │                         │
└─────────────────────────┘     └─────────────────────────┘
            │                               │
            └───────────┬───────────────────┘
                        ▼
              ┌─────────────────────┐
              │  Snakk.Api          │
              │  (Shared backend)   │
              ├─────────────────────┤
              │ POST /oauth/token   │
              │ GET  /oauth/userinfo│
              └─────────────────────┘
```

---

## Phase 1: Database Schema

### New Tables

**OAuthClient** - Registered OAuth clients (one per custom domain)
```sql
CREATE TABLE OAuthClient (
    Id INT IDENTITY PRIMARY KEY,
    PublicId NVARCHAR(26) NOT NULL UNIQUE,        -- ULID
    CommunityId INT NOT NULL,                      -- FK to Community
    ClientId NVARCHAR(64) NOT NULL UNIQUE,         -- Random client identifier
    ClientSecretHash NVARCHAR(256) NOT NULL,       -- Hashed secret
    Name NVARCHAR(100) NOT NULL,                   -- Display name
    RedirectUris NVARCHAR(MAX) NOT NULL,           -- JSON array of allowed URIs
    IsEnabled BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL,
    LastModifiedAt DATETIME2 NULL,

    CONSTRAINT FK_OAuthClient_Community
        FOREIGN KEY (CommunityId) REFERENCES Community(Id)
);

CREATE INDEX IX_OAuthClient_ClientId ON OAuthClient(ClientId);
```

**OAuthAuthorizationCode** - Short-lived authorization codes
```sql
CREATE TABLE OAuthAuthorizationCode (
    Id INT IDENTITY PRIMARY KEY,
    Code NVARCHAR(64) NOT NULL UNIQUE,             -- Random code
    ClientId INT NOT NULL,                          -- FK to OAuthClient
    UserId INT NOT NULL,                            -- FK to User
    RedirectUri NVARCHAR(2048) NOT NULL,
    Scopes NVARCHAR(500) NULL,                      -- Space-separated scopes
    CodeChallenge NVARCHAR(128) NULL,               -- PKCE
    CodeChallengeMethod NVARCHAR(10) NULL,          -- "S256" or "plain"
    ExpiresAt DATETIME2 NOT NULL,                   -- Short expiry (10 min)
    CreatedAt DATETIME2 NOT NULL,
    UsedAt DATETIME2 NULL,                          -- Set when exchanged

    CONSTRAINT FK_OAuthAuthorizationCode_Client
        FOREIGN KEY (ClientId) REFERENCES OAuthClient(Id),
    CONSTRAINT FK_OAuthAuthorizationCode_User
        FOREIGN KEY (UserId) REFERENCES [User](Id)
);

CREATE INDEX IX_OAuthAuthorizationCode_Code ON OAuthAuthorizationCode(Code);
CREATE INDEX IX_OAuthAuthorizationCode_ExpiresAt ON OAuthAuthorizationCode(ExpiresAt);
```

**OAuthRefreshToken** - Long-lived refresh tokens (optional, for token refresh)
```sql
CREATE TABLE OAuthRefreshToken (
    Id INT IDENTITY PRIMARY KEY,
    Token NVARCHAR(64) NOT NULL UNIQUE,
    ClientId INT NOT NULL,
    UserId INT NOT NULL,
    Scopes NVARCHAR(500) NULL,
    ExpiresAt DATETIME2 NOT NULL,                   -- Long expiry (30 days)
    CreatedAt DATETIME2 NOT NULL,
    RevokedAt DATETIME2 NULL,

    CONSTRAINT FK_OAuthRefreshToken_Client
        FOREIGN KEY (ClientId) REFERENCES OAuthClient(Id),
    CONSTRAINT FK_OAuthRefreshToken_User
        FOREIGN KEY (UserId) REFERENCES [User](Id)
);

CREATE INDEX IX_OAuthRefreshToken_Token ON OAuthRefreshToken(Token);
```

### Database Entities

Create in `src/core/Snakk.Infrastructure.Database/Entities/`:

**OAuthClientDatabaseEntity.cs**
```csharp
[Table("OAuthClient")]
public class OAuthClientDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }
    public int CommunityId { get; set; }
    public required string ClientId { get; set; }
    public required string ClientSecretHash { get; set; }
    public required string Name { get; set; }
    public required string RedirectUris { get; set; }  // JSON array
    public bool IsEnabled { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }

    public virtual CommunityDatabaseEntity Community { get; set; } = null!;
}
```

**OAuthAuthorizationCodeDatabaseEntity.cs**
```csharp
[Table("OAuthAuthorizationCode")]
public class OAuthAuthorizationCodeDatabaseEntity
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public int ClientId { get; set; }
    public int UserId { get; set; }
    public required string RedirectUri { get; set; }
    public string? Scopes { get; set; }
    public string? CodeChallenge { get; set; }
    public string? CodeChallengeMethod { get; set; }
    public required DateTime ExpiresAt { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public virtual OAuthClientDatabaseEntity Client { get; set; } = null!;
    public virtual UserDatabaseEntity User { get; set; } = null!;
}
```

---

## Phase 2: Domain Layer

### Value Objects

Create in `src/core/Snakk.Domain/ValueObjects/`:

**OAuthClientId.cs**
```csharp
public record OAuthClientId
{
    public string Value { get; }
    private OAuthClientId(string value) => Value = value;
    public static OAuthClientId From(string value) => new(value);
    public static OAuthClientId New() => new(Ulid.NewUlid().ToString());
}
```

### Domain Entities

**OAuthClient.cs**
```csharp
public class OAuthClient
{
    public OAuthClientId PublicId { get; private set; }
    public CommunityId CommunityId { get; private set; }
    public string ClientId { get; private set; }
    public string Name { get; private set; }
    public IReadOnlyList<string> RedirectUris { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static OAuthClient Create(
        CommunityId communityId,
        string name,
        IEnumerable<string> redirectUris);

    public bool ValidateRedirectUri(string uri);
    public void AddRedirectUri(string uri);
    public void RemoveRedirectUri(string uri);
    public void Disable();
    public void Enable();
}
```

---

## Phase 3: API Endpoints

### OAuth Endpoints

Create `src/core/Snakk.Api/Endpoints/OAuthEndpoints.cs`:

```csharp
public static class OAuthEndpoints
{
    public static void MapOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/oauth").WithTags("OAuth");

        // Authorization endpoint - user-facing, returns HTML or redirects
        group.MapGet("/authorize", AuthorizeAsync)
            .WithName("OAuthAuthorize");

        // Authorization consent submission
        group.MapPost("/authorize", AuthorizeSubmitAsync)
            .WithName("OAuthAuthorizeSubmit");

        // Token endpoint - machine-to-machine
        group.MapPost("/token", TokenAsync)
            .WithName("OAuthToken");

        // User info endpoint - get current user details
        group.MapGet("/userinfo", UserInfoAsync)
            .WithName("OAuthUserInfo")
            .RequireAuthorization();
    }
}
```

### Authorization Endpoint (`GET /oauth/authorize`)

Query parameters:
- `response_type` = "code" (required)
- `client_id` (required)
- `redirect_uri` (required, must match registered URI)
- `scope` (optional, e.g., "openid profile email")
- `state` (required for CSRF protection)
- `code_challenge` (optional, for PKCE)
- `code_challenge_method` (optional, "S256" recommended)

Flow:
1. Validate client_id exists and is enabled
2. Validate redirect_uri matches registered URIs
3. If user not logged in → redirect to login with return URL
4. If user logged in → show consent page (or auto-approve for same-org)
5. On consent → generate authorization code, redirect to redirect_uri

### Token Endpoint (`POST /oauth/token`)

Body (form-urlencoded):
- `grant_type` = "authorization_code" or "refresh_token"
- `code` (for authorization_code grant)
- `redirect_uri` (must match authorize request)
- `client_id`
- `client_secret`
- `code_verifier` (for PKCE)
- `refresh_token` (for refresh_token grant)

Response:
```json
{
    "access_token": "eyJ...",
    "token_type": "Bearer",
    "expires_in": 3600,
    "refresh_token": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4...",
    "scope": "openid profile"
}
```

### UserInfo Endpoint (`GET /oauth/userinfo`)

Requires Bearer token in Authorization header.

Response:
```json
{
    "sub": "user_public_id",
    "name": "Display Name",
    "email": "user@example.com",
    "email_verified": true,
    "picture": "https://snakk.com/avatars/user_public_id"
}
```

---

## Phase 4: Application Layer

### Use Cases

**OAuthUseCase.cs**
```csharp
public class OAuthUseCase
{
    // Validate authorization request
    public Task<Result<OAuthClient>> ValidateAuthorizationRequestAsync(
        string clientId,
        string redirectUri,
        string responseType,
        string? scope);

    // Create authorization code after user consents
    public Task<string> CreateAuthorizationCodeAsync(
        string clientId,
        string userId,
        string redirectUri,
        string? scope,
        string? codeChallenge,
        string? codeChallengeMethod);

    // Exchange authorization code for tokens
    public Task<Result<TokenResponse>> ExchangeCodeForTokensAsync(
        string code,
        string clientId,
        string clientSecret,
        string redirectUri,
        string? codeVerifier);

    // Refresh access token
    public Task<Result<TokenResponse>> RefreshTokenAsync(
        string refreshToken,
        string clientId,
        string clientSecret);

    // Validate access token and get user info
    public Task<Result<UserInfo>> ValidateAccessTokenAsync(string accessToken);
}
```

### JWT Token Generation

Use existing JWT infrastructure or create dedicated OAuth tokens:

```csharp
public class OAuthTokenService
{
    public string GenerateAccessToken(User user, OAuthClient client, string[] scopes)
    {
        var claims = new[]
        {
            new Claim("sub", user.PublicId.Value),
            new Claim("client_id", client.ClientId),
            new Claim("scope", string.Join(" ", scopes)),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
        };

        // Generate JWT with 1 hour expiry
        return GenerateJwt(claims, TimeSpan.FromHours(1));
    }

    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }
}
```

---

## Phase 5: Web Client Changes

### Auto-registration of OAuth Clients

When a custom domain is added to a community, automatically create an OAuth client:

**In CommunityDomainService or similar:**
```csharp
public async Task AddCustomDomainAsync(CommunityId communityId, string domain)
{
    // ... add domain to CommunityDomain table ...

    // Auto-create OAuth client for this domain
    var client = OAuthClient.Create(
        communityId,
        name: $"SSO Client for {domain}",
        redirectUris: new[] { $"https://{domain}/auth/sso/callback" }
    );

    await _oauthClientRepository.AddAsync(client);
}
```

### Login Page Changes

Update `src/clients/Snakk.Web/Pages/Auth/Login.cshtml`:

```html
@if (Model.IsCustomDomain)
{
    <div class="divider">OR</div>

    <a href="@Model.SsoLoginUrl" class="btn btn-outline btn-block gap-2">
        <svg><!-- Snakk logo --></svg>
        Login with Snakk
    </a>

    <p class="text-sm text-muted mt-2 text-center">
        Use your existing Snakk account
    </p>
}
```

### SSO Callback Handler

Create `src/clients/Snakk.Web/Pages/Auth/Sso/Callback.cshtml.cs`:

```csharp
public class CallbackModel : PageModel
{
    public async Task<IActionResult> OnGetAsync(
        string code,
        string state,
        string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            return RedirectToPage("/Auth/Login", new { error });
        }

        // Validate state matches what we stored
        var expectedState = HttpContext.Session.GetString("oauth_state");
        if (state != expectedState)
        {
            return RedirectToPage("/Auth/Login", new { error = "invalid_state" });
        }

        // Exchange code for tokens
        var tokenResponse = await _apiClient.ExchangeOAuthCodeAsync(
            code,
            _configuration["OAuth:ClientId"],
            _configuration["OAuth:ClientSecret"],
            GetCallbackUri());

        if (tokenResponse == null)
        {
            return RedirectToPage("/Auth/Login", new { error = "token_exchange_failed" });
        }

        // Get user info
        var userInfo = await _apiClient.GetOAuthUserInfoAsync(tokenResponse.AccessToken);

        // Create local session (set auth cookie)
        await _authService.SignInWithOAuthAsync(userInfo, tokenResponse);

        // Redirect to original destination
        var returnUrl = HttpContext.Session.GetString("oauth_return_url") ?? "/";
        return Redirect(returnUrl);
    }
}
```

### SSO Initiation

Create `src/clients/Snakk.Web/Pages/Auth/Sso/Index.cshtml.cs`:

```csharp
public class IndexModel : PageModel
{
    public IActionResult OnGet(string? returnUrl)
    {
        // Generate state for CSRF protection
        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        HttpContext.Session.SetString("oauth_state", state);
        HttpContext.Session.SetString("oauth_return_url", returnUrl ?? "/");

        // Build authorization URL
        var authUrl = new UriBuilder(_configuration["OAuth:AuthorizeUrl"]);
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["response_type"] = "code";
        query["client_id"] = _configuration["OAuth:ClientId"];
        query["redirect_uri"] = GetCallbackUri();
        query["scope"] = "openid profile email";
        query["state"] = state;

        // Add PKCE if enabled
        if (_configuration.GetValue<bool>("OAuth:UsePkce"))
        {
            var codeVerifier = GenerateCodeVerifier();
            HttpContext.Session.SetString("oauth_code_verifier", codeVerifier);
            query["code_challenge"] = GenerateCodeChallenge(codeVerifier);
            query["code_challenge_method"] = "S256";
        }

        authUrl.Query = query.ToString();

        return Redirect(authUrl.ToString());
    }
}
```

---

## Phase 6: Configuration

### API Configuration (appsettings.json)

```json
{
  "OAuth": {
    "Issuer": "https://snakk.com",
    "AccessTokenLifetimeMinutes": 60,
    "RefreshTokenLifetimeDays": 30,
    "AuthorizationCodeLifetimeMinutes": 10,
    "RequirePkce": false,
    "AllowedScopes": ["openid", "profile", "email"]
  },
  "Jwt": {
    "SecretKey": "your-256-bit-secret-key-here",
    "Issuer": "https://snakk.com",
    "Audience": "snakk-oauth-clients"
  }
}
```

### Web Client Configuration (for custom domains)

```json
{
  "OAuth": {
    "AuthorizeUrl": "https://snakk.com/oauth/authorize",
    "TokenUrl": "https://api.snakk.com/oauth/token",
    "UserInfoUrl": "https://api.snakk.com/oauth/userinfo",
    "ClientId": "auto-generated-per-domain",
    "ClientSecret": "auto-generated-per-domain",
    "UsePkce": true
  }
}
```

---

## Phase 7: Security Considerations

### PKCE (Proof Key for Code Exchange)

Always use PKCE for public clients (web apps):

```csharp
public static string GenerateCodeVerifier()
{
    var bytes = RandomNumberGenerator.GetBytes(32);
    return Base64UrlEncode(bytes);
}

public static string GenerateCodeChallenge(string codeVerifier)
{
    using var sha256 = SHA256.Create();
    var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
    return Base64UrlEncode(hash);
}
```

### Redirect URI Validation

Strict validation to prevent open redirects:

```csharp
public bool ValidateRedirectUri(string requestedUri, IEnumerable<string> allowedUris)
{
    if (!Uri.TryCreate(requestedUri, UriKind.Absolute, out var uri))
        return false;

    // Must be HTTPS (except localhost for dev)
    if (uri.Scheme != "https" && uri.Host != "localhost")
        return false;

    // Must exactly match a registered URI
    return allowedUris.Any(allowed =>
        string.Equals(allowed, requestedUri, StringComparison.OrdinalIgnoreCase));
}
```

### Token Security

- Access tokens: Short-lived JWTs (1 hour)
- Refresh tokens: Long-lived opaque tokens (30 days), stored hashed
- Authorization codes: Very short-lived (10 minutes), single-use

### Rate Limiting

Apply rate limits to OAuth endpoints:
- `/oauth/authorize`: 10 requests/minute per IP
- `/oauth/token`: 20 requests/minute per client_id

---

## Phase 8: User Experience

### Consent Screen

When user authorizes, show:
```
┌─────────────────────────────────────────────────────┐
│                    [Snakk Logo]                     │
│                                                     │
│  "Example Forum" wants to access your account       │
│                                                     │
│  This will allow Example Forum to:                  │
│  ✓ See your display name and profile picture        │
│  ✓ See your email address                           │
│                                                     │
│  ┌─────────────┐  ┌─────────────────────────┐      │
│  │   Cancel    │  │   Allow Access          │      │
│  └─────────────┘  └─────────────────────────┘      │
│                                                     │
│  Logged in as: user@example.com                     │
│  Not you? [Sign out]                                │
└─────────────────────────────────────────────────────┘
```

### Auto-Approve for Same Community

If user is authorizing access to a domain that belongs to a community they're already part of, auto-approve without showing consent screen.

---

## Critical Files to Create/Modify

| Area | Files |
|------|-------|
| Database Entities | `OAuthClientDatabaseEntity.cs`, `OAuthAuthorizationCodeDatabaseEntity.cs`, `OAuthRefreshTokenDatabaseEntity.cs` |
| DbContext | `SnakkDbContext.cs` - add DbSets and relationships |
| Migration | `AddOAuthTables.cs` |
| Domain Entities | `OAuthClient.cs` |
| Value Objects | `OAuthClientId.cs` |
| Repositories | `IOAuthClientRepository.cs`, `IOAuthAuthorizationCodeRepository.cs` |
| Use Cases | `OAuthUseCase.cs` |
| Services | `OAuthTokenService.cs` |
| API Endpoints | `OAuthEndpoints.cs` |
| API Pages | `Authorize.cshtml` (consent screen) |
| Web Pages | `Auth/Sso/Index.cshtml`, `Auth/Sso/Callback.cshtml` |
| Web Services | Update `SnakkApiClient.cs` with OAuth methods |
| Configuration | Update `appsettings.json` for both API and Web |

---

## Verification Steps

1. **Database Migration**
   - Run migration, verify tables created
   - Create test OAuth client manually

2. **Authorization Flow**
   - Visit `/oauth/authorize?client_id=...&redirect_uri=...`
   - Verify redirect to login if not authenticated
   - Verify consent screen appears
   - Verify redirect with code on approval

3. **Token Exchange**
   - POST to `/oauth/token` with code
   - Verify access_token returned
   - Verify token is valid JWT

4. **UserInfo**
   - GET `/oauth/userinfo` with Bearer token
   - Verify user details returned

5. **End-to-End**
   - On custom domain, click "Login with Snakk"
   - Authenticate on snakk.com
   - Verify redirect back to custom domain
   - Verify user is logged in on custom domain

---

## Future Enhancements

1. **OpenID Connect Compliance** - Add ID tokens, discovery endpoint (`.well-known/openid-configuration`)
2. **Token Revocation** - Allow users to revoke app access
3. **Admin Dashboard** - Manage OAuth clients, view active sessions
4. **Refresh Token Rotation** - Issue new refresh token on each use
5. **Device Flow** - For CLI/device authentication
