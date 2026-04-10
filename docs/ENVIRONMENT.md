# Environment Variables

Reference for all environment variables supported by the Snakk Docker image.

Most of these are configured automatically by the [browser-based setup wizard](../README.md#first-run-setup) on first install. You only need to set them manually for advanced deployments (custom infrastructure, S3-compatible storage, OAuth, etc.).

## Required at first launch

These must be set before starting the container for the first time.

| Variable | Description | Example |
|---|---|---|
| `POSTGRES_PASSWORD` | Password for the bundled PostgreSQL container | `openssl rand -base64 32` |

## Database connection

| Variable | Description | Default |
|---|---|---|
| `ConnectionStrings__DbConnection` | Full PostgreSQL connection string | (built from `DB_HOST` etc. in compose) |
| `DB_HOST` | Database host | `postgres` |
| `DB_PORT` | Database port | `5432` |

## Application

| Variable | Description | Default |
|---|---|---|
| `SNAKK_PORT` | Host port to bind the gateway to | `17000` |
| `SNAKK_VERSION` | Image tag to pull (e.g. `1.2.3`, `1.2`, `1`, `latest`) | `latest` |
| `ASPNETCORE_ENVIRONMENT` | `Production` or `Development` | `Production` |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | Trust `X-Forwarded-*` from reverse proxies | `true` |
| `Gateway__DisableHealthChecks` | Disable YARP active health checks (reduces log noise) | `true` |

## File storage

By default, uploads are stored in the `snakk-storage` Docker volume. To use S3-compatible storage instead:

| Variable | Description |
|---|---|
| `FileStorage__Provider` | `Local` or `S3` |
| `FileStorage__BasePath` | Local path inside container (Local provider) |
| `FileStorage__S3__Endpoint` | S3 endpoint URL (e.g. `https://s3.amazonaws.com`) |
| `FileStorage__S3__BucketName` | S3 bucket name |
| `FileStorage__S3__AccessKey` | S3 access key |
| `FileStorage__S3__SecretKey` | S3 secret key |
| `FileStorage__S3__Region` | S3 region (e.g. `us-east-1`) |
| `FileStorage__S3__PublicUrlBase` | CDN URL prefix (e.g. `https://cdn.example.com`) |

## OAuth providers

Optional. Each provider needs both `ClientId` and `ClientSecret`. Without these, the OAuth login button is hidden.

| Variable | Description |
|---|---|
| `Authentication__Google__ClientId` | Google OAuth client ID |
| `Authentication__Google__ClientSecret` | Google OAuth client secret |
| `Authentication__GitHub__ClientId` | GitHub OAuth client ID |
| `Authentication__GitHub__ClientSecret` | GitHub OAuth client secret |
| `Authentication__Discord__ClientId` | Discord OAuth client ID |
| `Authentication__Discord__ClientSecret` | Discord OAuth client secret |

OAuth callback URLs to register with each provider:
- Google: `https://your-domain/auth/oauth/google/callback`
- GitHub: `https://your-domain/auth/oauth/github/callback`
- Discord: `https://your-domain/auth/oauth/discord/callback`

## Cloudflare Turnstile (CAPTCHA)

Optional bot protection on login, register, and forgot-password forms.

| Variable | Description |
|---|---|
| `Turnstile__SiteKey` | Cloudflare Turnstile site key |
| `Turnstile__SecretKey` | Cloudflare Turnstile secret key |

## Setup wizard

| Variable | Description |
|---|---|
| `SETUP_PASSWORD` | Optional password gate for the setup wizard. Without this, anyone hitting `/setup` can configure the instance during initial install. |

## JWT

The setup wizard generates these automatically. Don't change them after initial setup or all existing sessions become invalid.

| Variable | Description |
|---|---|
| `Jwt__SecretKey` | HMAC-SHA256 signing key (min 32 chars) |
| `Jwt__Issuer` | Token issuer claim | 
| `Jwt__Audience` | Token audience claim |
| `Jwt__ExpirationMinutes` | Access token lifetime (default `15`) |

## Multi-community

| Variable | Description |
|---|---|
| `Snakk__Domain` | The primary domain (e.g. `snakk.community`) |
| `Snakk__PrimaryDomains` | Comma-separated list of primary platform domains. Custom-domain communities are matched against any other host. |
| `Snakk__DefaultCommunitySlug` | Slug to use as fallback for unscoped routes (default `main`) |
| `Snakk__DomainCache__ExpirationMinutes` | How long to cache domain → community lookups (default `15`) |
| `Snakk__DomainCache__NegativeExpirationMinutes` | How long to cache "no community for this domain" (default `5`) |

## Volumes (not env vars, but related)

| Volume | Mount point | What's in it |
|---|---|---|
| `snakk-storage` | `/app/storage` | All persistent data: avatars, uploaded media, generated SVGs, app config (`/conf`), JWT keys |
| `pgdata` | `/var/lib/postgresql/data` | PostgreSQL data files |

**Back up both volumes** to preserve a complete instance. Restoring is the reverse: stop the stack, restore both volumes, start the stack.
