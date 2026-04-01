# Security Audit — Remaining Findings

Audit performed 2026-03-27. Critical findings (#1–#7) have been resolved.
This document tracks the remaining HIGH and MEDIUM severity items.

---

## HIGH — Fix This Week

### H1. No account lockout on failed logins
**File:** `Snakk.Application/UseCases/AuthenticationUseCase.cs`
**Issue:** Rate limiting is per-IP only (5 requests / 15 min). No per-account lockout after repeated failures. A distributed botnet bypasses IP-based limits entirely.
**Fix:** Add a failed-attempt counter per account. Lock the account for 15 minutes after 10 consecutive failures from any source. Reset on successful login.

### H2. No rate limiting on 2FA verify endpoint
**File:** `Snakk.Api/Endpoints/TwoFactorAuthEndpoints.cs:31`
**Issue:** The `/auth/2fa/verify` endpoint is `AllowAnonymous()` with no rate limiter. TOTP codes are 6 digits (1M combinations) and two consecutive windows are valid, making brute-force feasible.
**Fix:** Add `.RequireRateLimiting("auth")` or a stricter policy (e.g., 3 attempts per 15 min per email).

### H3. No JWT revocation mechanism
**File:** `Snakk.Api/Services/JwtTokenService.cs`
**Issue:** Logout revokes refresh tokens but the access token remains valid for up to 15 minutes. Password changes don't kill active sessions either.
**Fix:** Implement a short-lived in-memory blacklist (e.g., `IMemoryCache` with 15-min TTL) checked during token validation. On logout or password change, add the token's `jti` claim to the blacklist.

### H4. IDOR — trusted device revocation
**File:** `Snakk.Api/Endpoints/TwoFactorAuthEndpoints.cs:320`
**Issue:** `RevokeTrustedDeviceAsync` accepts a `deviceId` parameter but never verifies the device belongs to the authenticated user. Any user can revoke any other user's trusted devices.
**Fix:** Pass the authenticated user's ID to the service and verify device ownership before revocation.

### H5. IDOR — BFF follow-status endpoint
**File:** `Snakk.Web/Endpoints/BffApiEndpoints.cs`
**Issue:** `GetUserFollowStatusAsync` takes `currentUserId` from a query parameter instead of deriving it from the auth cookie. Attacker can spoof any user ID.
**Fix:** Read `currentUserId` from the authenticated user's claims, not the query string.

### H6. IDOR — mark-as-read endpoint
**File:** `Snakk.Web/Endpoints/BffApiEndpoints.cs`
**Issue:** `MarkDiscussionAsReadAsync` accepts `userId` from a query parameter. An attacker can mark discussions as read for arbitrary users.
**Fix:** Derive `userId` from the auth cookie claims.

### H7. Disabling 2FA doesn't require a 2FA code
**File:** `Snakk.Api/Endpoints/TwoFactorAuthEndpoints.cs:101`
**Issue:** Disabling 2FA requires only a password. If an attacker has a hijacked session, they can disable 2FA and take over the account permanently.
**Fix:** Require a valid TOTP code (or backup code) in addition to the password when disabling 2FA.

### H8. OAuth callback — no state parameter validation
**File:** `Snakk.Api/GrpcServices/AuthGrpcService.cs:306`
**Issue:** OAuth callback does not validate a `state` parameter. No CSRF protection on the OAuth flow, enabling authorization code interception and session fixation attacks.
**Fix:** Generate a cryptographic random `state` value before redirect, store it in session, and validate it in the callback.

### H9. Refresh token uses GUID (predictable)
**File:** `Snakk.Domain/ValueObjects/RefreshToken.cs:25`
**Issue:** `RefreshToken.Create()` uses `Guid.NewGuid().ToByteArray()` which is not cryptographically random. The infrastructure `TokenService` correctly uses `RandomNumberGenerator` but the domain model does not.
**Fix:** Replace with `RandomNumberGenerator.GetBytes(32)` + `Convert.ToBase64String()` in the domain model, or ensure only the infrastructure service is used for token generation.

### H10. Path traversal in LocalFileStorage
**File:** `Snakk.Infrastructure/Services/LocalFileStorage.cs:26`
**Issue:** `Path.Combine(_basePath, relativePath)` does not verify the resolved path stays within `_basePath`. An absolute path in `relativePath` bypasses containment.
**Fix:** After combining, call `Path.GetFullPath()` and verify it starts with `Path.GetFullPath(_basePath)`.

### H11. Timing attack on API key comparison
**File:** `Snakk.Realtime/Middleware/ApiKeyAuthMiddleware.cs:24`
**Issue:** `extractedApiKey != _apiKey` uses short-circuit string comparison. Response time differences leak correct characters.
**Fix:** Use `CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b))`.

### H12. 2FA secrets stored in plaintext
**File:** `Snakk.Infrastructure.Database/Entities/UserDatabaseEntity.cs:55`
**Issue:** `TwoFactorSecret` is stored unencrypted. A database breach compromises all 2FA secrets, completely defeating 2FA.
**Fix:** Encrypt at rest using a data protection key (e.g., ASP.NET Data Protection API or AES-256-GCM with a key from config/vault).

### H13. CSRF disabled on file upload endpoints
**File:** `Snakk.Api/Endpoints/AvatarEndpoints.cs:20`, `MediaEndpoints.cs:17`
**Issue:** `.DisableAntiforgery()` on POST endpoints. Enables cross-site upload attacks.
**Fix:** Remove `.DisableAntiforgery()` and send antiforgery tokens from the frontend, or validate the `Origin` header on these endpoints.

---

## MEDIUM — Fix Next Sprint

### M1. SameSite=Lax on auth cookies
**File:** `Snakk.Web/Services/AuthCookieHelper.cs:17`
**Issue:** `SameSite = SameSiteMode.Lax` allows cookies on top-level cross-site navigations. Should be `Strict` for auth cookies.
**Fix:** Change to `SameSiteMode.Strict`.

### M2. AllowedHosts set to wildcard
**Files:** All `appsettings.json` — `"AllowedHosts": "*"`
**Issue:** Enables host header poisoning attacks (cache poisoning, password reset link hijacking).
**Fix:** Set to actual domain(s) in production configs.

### M3. Email enumeration via login timing
**File:** `Snakk.Application/UseCases/AuthenticationUseCase.cs:73`
**Issue:** Non-existent emails return immediately; existing emails run BCrypt verification (slow). Timing difference reveals valid emails.
**Fix:** Run a dummy BCrypt verify on a fixed hash when the email doesn't exist, equalizing response times.

### M4. Email enumeration via 2FA verify
**File:** `Snakk.Api/Endpoints/TwoFactorAuthEndpoints.cs:131`
**Issue:** Error response differentiates "user not found" from "2FA not enabled".
**Fix:** Return the same generic error for both cases.

### M5. No SRI on Cloudflare Turnstile script
**File:** `Snakk.Web/Pages/Shared/_Layout.cshtml:110`
**Issue:** External `<script>` tag has no `integrity` attribute. CDN compromise = XSS.
**Fix:** Add `integrity="sha384-..."` and `crossorigin="anonymous"` attributes.

### M6. 90-day refresh token expiry
**File:** `Snakk.Application/UseCases/AuthenticationUseCase.cs:340`
**Issue:** Excessively long compromise window for stolen refresh tokens.
**Fix:** Reduce to 30 days. Consider shorter (7–14 days) with sliding expiration on use.

### M7. Debug pane enabled in non-dev config
**File:** `Snakk.Web/appsettings.json:10`
**Issue:** `"ShowCommunityDebugPane": true` leaks application internals to all users.
**Fix:** Set to `false` in base config; only enable in `appsettings.Development.json`.

### M8. PII logged in plaintext
**File:** `Snakk.Api/Helpers/AuthAuditLogger.cs`
**Issue:** Email addresses and IP addresses logged without masking.
**Fix:** Hash or mask emails in logs (e.g., `u***@example.com`). Consider structured logging with PII redaction.

### M9. Emails stored unencrypted in database
**File:** `Snakk.Infrastructure.Database/Entities/UserDatabaseEntity.cs:14`
**Issue:** Database breach exposes all user emails for harvesting.
**Fix:** Encrypt email at rest. Store a hash for lookups and the encrypted value for display.

### M10. No GDPR data purge for soft-deleted records
**File:** `Snakk.Infrastructure.Database/Entities/UserDatabaseEntity.cs:25`
**Issue:** Soft-deleted user data (email, display name) retained indefinitely.
**Fix:** Implement a scheduled purge job that hard-deletes PII from soft-deleted records after a retention period (e.g., 30 days).

### M11. Webhook admin endpoints missing explicit role check
**File:** `Snakk.Api/Endpoints/AdminWebhooksEndpoints.cs:14`
**Issue:** Uses `RequireAuthorization()` but not `RequireRole("Admin")`. Any authenticated user could manage webhooks if service-layer checks are missing.
**Fix:** Add `.RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" })` or equivalent policy.

### M12. Password complexity not enforced on gRPC path
**File:** `Snakk.Api/GrpcServices/AuthGrpcService.cs:32`
**Issue:** REST validators enforce uppercase/lowercase/number/special character rules. gRPC registration only checks `length >= 8`.
**Fix:** Move password complexity validation into `AuthenticationUseCase.RegisterAsync()` so it applies regardless of entry point.

### M13. Inefficient email verification token lookup
**File:** `Snakk.Application/UseCases/AuthenticationUseCase.cs:152`
**Issue:** Loads ALL users into memory to find one verification token. DoS vector on large user bases.
**Fix:** Add `IUserRepository.GetByEmailVerificationTokenAsync(string token)` that queries by the token column directly.

### M14. Refresh token replay detection missing
**File:** `Snakk.Application/UseCases/AuthenticationUseCase.cs:346`
**Issue:** Old refresh tokens are revoked on rotation, but reuse of an already-revoked token doesn't trigger a security alert or revoke the entire token family.
**Fix:** Implement token family tracking. If a revoked token is reused, revoke all tokens in the family (indicates theft).

### M15. No refresh token rotation on device fingerprint change
**File:** `Snakk.Api/Endpoints/TwoFactorAuthEndpoints.cs:186`
**Issue:** Device fingerprint is based only on UserAgent + IP. UserAgent is trivially spoofed; IP changes on mobile.
**Fix:** Consider binding refresh tokens to a device fingerprint stored in an HttpOnly cookie. Force re-authentication if the fingerprint changes.

---

## Resolved (2026-03-27)

| # | Finding | Resolution |
|---|---------|-----------|
| C1 | Production secrets in repo | Rotated all local keys; replaced tracked secrets with placeholders |
| C2 | XSS — unescaped DisplayName in onclick | Wrapped with `EscapeForJs()` in Post.cshtml + Detail.cshtml |
| C3 | No Content-Security-Policy | Added CSP middleware in Program.cs |
| C4 | No X-Frame-Options | Added `DENY` + `frame-ancestors 'none'` |
| C5 | @Html.Raw() on user content | False positive — Markdig `.DisableHtml()` escapes raw HTML |
| C6 | SSRF via webhook URLs | Added URL validation blocking private IPs, localhost, non-http schemes |
| C7 | SQL injection in MetricsService | False positive — EF Core auto-parameterizes FormattableString |
| H1 | No account lockout | Added FailedLoginAttempts + LockoutEnd to User entity; locks after 10 failures for 15 min |
| H2 | No rate limit on 2FA verify | Added `.RequireRateLimiting("auth")` to verify endpoint |
| H3 | No JWT revocation | Added jti claim + IMemoryCache blacklist; `RevokeToken()` on IJwtTokenService |
| H4 | IDOR: trusted device revocation | New `RevokeDeviceForUserAsync` verifies device ownership |
| H5 | IDOR: BFF follow-status | `currentUserId` now read from auth claims, not query param |
| H6 | IDOR: BFF mark-as-read | `userId` now read from auth claims, not query param |
| H7 | 2FA disable without TOTP | DisableTwoFactorRequest now requires TotpCode; verified before disable |
| H8 | OAuth: no state param | Crypto random state generated in Challenge, validated in Callback |
| H9 | Refresh token uses GUID | Replaced with `RandomNumberGenerator.GetBytes(32)` |
| H10 | Path traversal in LocalFileStorage | `ResolveSafePath()` validates path stays within base directory |
| H11 | Timing attack on API key | Replaced `!=` with `CryptographicOperations.FixedTimeEquals` |
| H12 | 2FA secrets in plaintext | Encrypted with ASP.NET Data Protection API via `ITwoFactorSecretProtector` |
| H13 | CSRF disabled on uploads | Restored `.DisableAntiforgery()` — these are fetch() API calls protected by JWT auth, not form submissions |
| M1 | SameSite=Lax on auth cookies | Dual-cookie pattern: Strict for mutations, Lax for personalization |
| M2 | AllowedHosts: "*" | Changed to "localhost" in all base appsettings.json |
| M3 | Email enumeration via timing | Dummy BCrypt verify when email not found |
| M4 | Email enumeration via 2FA | Already returns same error for both cases |
| M6 | 90-day refresh token | Already 30 days — no change needed |
| M7 | Debug pane in base config | Set ShowCommunityDebugPane to false |
| M8 | PII in logs | MaskEmail helper in AuthAuditLogger (j***n@example.com) |
| M9 | Emails unencrypted | Data Protection API via IEmailProtector + SHA-256 EmailHash for lookups |
| M11 | Webhook endpoints no role | Added RequireRole("Admin") to webhook admin group |
| M12 | Password complexity on gRPC | Moved regex validation to AuthenticationUseCase.RegisterWithEmailAsync |
| M13 | Inefficient token lookup | Added GetByEmailVerificationTokenAsync to IUserRepository |
| M15 | Device fingerprint + management | UserAgent validation on token refresh; device list + revoke UI in settings |
