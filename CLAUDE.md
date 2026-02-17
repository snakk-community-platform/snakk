# Snakk - Claude Code Instructions

## 🚨 MANDATORY: ALWAYS ASK BEFORE CODE CHANGES

### CRITICAL RULE: Request Approval First

**You MUST ask for explicit approval before making ANY code changes.**

#### Required Workflow:

1. **Analyze** - Understand the request thoroughly
2. **Explain** - Describe what changes you would make, which files would be affected, and why
3. **Ask** - Request confirmation: "Should I proceed with these changes?"
4. **Wait** - Wait for explicit user approval (YES, "go ahead", "proceed", etc.)
5. **Execute** - Only after approval, make the changes

#### What Requires Approval:
- ✅ Modifying code files (.cs, .js, .cshtml, .razor, .css, .json)
- ✅ Creating new files
- ✅ Deleting files
- ✅ Updating configuration files
- ✅ Modifying database migrations or entities
- ✅ Updating documentation files (.md)

#### What Does NOT Require Approval:
- ❌ Reading files (Read, Grep, Glob tools)
- ❌ Searching/exploring the codebase
- ❌ Explaining code or architecture
- ❌ Answering questions
- ❌ Creating TODO lists or plans

**Violation of this rule is unacceptable.**

---

## 🚨 CRITICAL ARCHITECTURE RULES

### Backend-for-Frontend (BFF) Pattern - MANDATORY

**NEVER allow JavaScript to call the internal API directly.**

#### The Rule:
- ✅ JavaScript calls `/bff/*` endpoints only
- ✅ ASP.NET Snakk.Web and Snakk.AdminWeb act as BFF layers
- ✅ BFF layers handle all communication with internal API
- ❌ **FORBIDDEN**: Direct `fetch()` calls from JavaScript to `/api/*`
- ❌ **FORBIDDEN**: `window.apiBaseUrl` or `snakkApiBaseUrl` in JavaScript
- ❌ **FORBIDDEN**: SignalR connections directly to API

#### Why:
1. **Security**: Internal API will be firewalled - only accessible to ASP.NET projects
2. **Validation**: BFF layer can validate/sanitize requests before forwarding
3. **Auth**: BFF handles JWT/cookie conversion and validation
4. **Flexibility**: Can change internal API without breaking frontend

#### How to Implement:

**JavaScript Side:**
```javascript
// ❌ WRONG - Direct API call
fetch(`${apiBaseUrl}/api/users/${userId}/stats`)

// ✅ CORRECT - BFF call
fetch(`/bff/users/${userId}/stats`)
```

**ASP.NET BFF Endpoint:**
```csharp
// In Snakk.Web or Snakk.AdminWeb
app.MapGet("/bff/users/{userId}/stats", async (string userId, HttpClient httpClient) =>
{
    // Validate request, check auth, etc.
    var response = await httpClient.GetAsync($"https://api-internal/api/users/{userId}/stats");
    return Results.Content(await response.Content.ReadAsStringAsync(), "application/json");
});
```

#### Enforcement Checklist:

Before approving ANY code that involves HTTP requests from JavaScript:

- [ ] Does it call `/api/*` directly? → REJECT
- [ ] Does it use `apiBaseUrl` or `snakkApiBaseUrl`? → REJECT
- [ ] Does it connect SignalR directly to API? → REJECT
- [ ] Does it call `/bff/*` instead? → APPROVE

#### Files That Need Refactoring:

These files currently violate the BFF pattern and must be refactored:

1. **components/site.js** - Entity popup stats (5 API calls)
2. **components/auth-navbar.js** - Auth status and logout (2 API calls)
3. **pages/profile.js** - User stats and activity (9 API calls)
4. **pages/discussion-detail.js** - Posts, moderation, avatars (7 API calls)
5. **pages/frontpage.js** - Discussion previews (1 API call)
6. **pages/frontpage-discussions.js** - Discussion previews and avatars (2 API calls)
7. **pages/space-detail.js** - Discussion previews (1 API call)
8. **services/realtime.js** - SignalR connection (1 connection)

---

## Clean Architecture (C# Backend)

### Layer Separation - MANDATORY

**NEVER mix concerns across architectural layers.**

- **Snakk.Api**: HTTP endpoints only, no business logic
- **Snakk.Application**: DTOs, service interfaces, use cases
- **Snakk.Infrastructure**: Service implementations, database access
- **Snakk.Domain**: Domain entities, events, value objects
- **Snakk.Infrastructure.Database**: Database entities, DbContext

#### The Rules:

❌ **FORBIDDEN**:
- Api layer referencing Infrastructure types
- Api layer containing business logic
- Domain layer with infrastructure dependencies

✅ **REQUIRED**:
- DTOs in Application layer
- Service interfaces in Application layer
- Service implementations in Infrastructure layer
- Endpoints only handle HTTP concerns

---

## Frontend Component Rules

### Main Platform (Snakk.Web)
- **NO component library** - Use Tailwind CSS utility classes only
- daisyUI for pre-built Tailwind components
- Vanilla JavaScript (ES6+ IIFE modules)

### Admin Panel (Snakk.AdminWeb)
- **Blazor Server** + Microsoft Fluent UI Components
- **NOT Next.js** - The admin panel uses Blazor, not React/Next.js
- Use Microsoft Fluent UI components (NOT shadcn/ui)

---

## JavaScript Security - MANDATORY

### XSS Prevention

**NEVER use `innerHTML` with user-generated content.**

```javascript
// ❌ WRONG - XSS vulnerable
element.innerHTML = userContent;

// ✅ CORRECT - Safe
element.textContent = userContent;
// OR
const div = document.createElement('div');
div.textContent = userContent;
element.appendChild(div);
```

### Content Escaping

**ALWAYS escape user content when building HTML.**

```javascript
// ✅ Use the utility
const escaped = window.snakkUtils.escapeHtml(userContent);
```

---

## Git Commit Guidelines

### Commit Messages

When creating commits:

1. **Be descriptive**: Explain the "why" not just the "what"
2. **Use conventional format**: `feat:`, `fix:`, `refactor:`, `docs:`, etc.
3. **Keep it concise**: 1-2 sentences maximum
4. **Always add co-author**:
   ```
   Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>
   ```

### What to Commit

- ❌ Don't commit sensitive files (.env, credentials, etc.)
- ✅ Stage specific files by name (avoid `git add -A`)
- ✅ Review changes with `git diff` before committing

---

## Project Structure

### Core (.NET 10)
```
src/core/
├── Snakk.Domain/                    # Domain entities, events (no dependencies)
├── Snakk.Application/               # DTOs, interfaces, use cases
├── Snakk.Infrastructure/            # Service implementations
├── Snakk.Infrastructure.Database/   # EF Core entities, DbContext, migrations
├── Snakk.Shared/                    # Enums, utilities
└── Snakk.Sdk/                       # Client SDK for admin panel
```

### Services (.NET 10)
```
src/services/
├── Snakk.Api/           # Internal REST API (port 5242, firewalled)
└── Snakk.Realtime/      # SignalR hub (port 5300, .NET 9)
```

### Applications
```
src/apps/
├── Snakk.Web/           # Main platform (Razor Pages + HTMX + Tailwind)
│   ├── Pages/           # Razor Pages
│   ├── Endpoints/       # BFF endpoints
│   └── wwwroot/js/
│       ├── core/        # auth.js, theme.js, utils.js
│       ├── components/  # auth-navbar.js, site.js
│       ├── services/    # realtime.js, cache-manager.js
│       ├── pages/       # Page-specific scripts
│       └── enums/       # UserRoleType.js
│
└── Snakk.AdminWeb/      # Admin panel (Blazor Server + Fluent UI)
    ├── Pages/           # Razor Pages (Dashboard, Auth)
    ├── Components/      # Blazor components (Fluent UI)
    └── Services/        # AdminApiClientService
```

---

## Common Patterns

### Live Activity Feed
- IActivityBroadcaster in Application.Services
- ActivityBroadcaster implementation in Api.Hubs
- Domain event handlers in Infrastructure.EventHandlers.Activity
- SignalR connects directly to Snakk.Realtime (port 5300) - NOT proxied through BFF

### File Naming Conventions

**All CSS, SCSS, JavaScript, TypeScript, and image files MUST use kebab-case:**
- ✅ `user-role-type.ts`
- ✅ `auth-navbar.js`
- ✅ `cache-manager.js`
- ✅ `space-detail.css`
- ✅ `space-detail.scss`
- ✅ `profile-avatar.png`
- ❌ `UserRoleType.ts` (PascalCase - wrong)
- ❌ `authNavbar.js` (camelCase - wrong)
- ❌ `spaceDetail.scss` (camelCase - wrong)
- ❌ `ProfileAvatar.png` (PascalCase - wrong)

**Exception:** C# files use PascalCase (e.g., `SnakkUrlHelper.cs`)

### JavaScript Patterns
- IIFE modules: `(function() { 'use strict'; ... })();`
- Window exports: `window.SnakkAuth`, `window.UserRoleType`
- Event delegation for dynamic content
- Custom events: `snakk:auth:token-set`, `snakk:nav:loaded`

---

## SDK Synchronization - MANDATORY

### Auto-Generated SDK from OpenAPI

**Snakk.Sdk is auto-generated from the API's OpenAPI specification.**

#### How It Works:

1. **Snakk.Api** exports OpenAPI spec to `src/core/Snakk.Sdk/openapi.json`
2. **Snakk.Sdk** uses NSwag to generate `SnakkApiClient.cs` from the spec during build
3. Both **Snakk.Web** and **Snakk.AdminWeb** use the generated SDK

#### Critical Rule:

**WHENEVER you modify/add/remove API endpoints, you MUST regenerate the SDK.**

### Regeneration Workflow:

```bash
# 1. Start Snakk.Api (if not already running)
dotnet run --project src/services/Snakk.Api

# 2. In a new terminal, fetch OpenAPI spec from running API
curl -k https://localhost:17100/openapi/v1.json -o src/core/Snakk.Sdk/openapi.json

# 3. Rebuild SDK (auto-generates client from openapi.json)
dotnet build src/core/Snakk.Sdk

# 4. Commit BOTH files
git add src/core/Snakk.Sdk/openapi.json
git add src/core/Snakk.Sdk/Generated/SnakkApiClient.cs
```

#### Checklist Before Committing API Changes:

- [ ] Modified/added/removed API endpoints?
- [ ] Exported updated `openapi.json`?
- [ ] Rebuilt Snakk.Sdk to regenerate client?
- [ ] Tested that Snakk.Web/AdminWeb still compile?
- [ ] Committed both `openapi.json` and `Generated/SnakkApiClient.cs`?

#### Files Involved:

- **`src/services/Snakk.Api`** - Exposes OpenAPI spec at `/openapi/v1.json` endpoint
- **`src/core/Snakk.Sdk/openapi.json`** - Static OpenAPI specification (committed to git)
- **`src/core/Snakk.Sdk/nswag.json`** - NSwag configuration for generation
- **`src/core/Snakk.Sdk/Generated/SnakkApiClient.cs`** - Auto-generated SDK client (committed to git)

#### Why Commit Generated Files?

Generated files (`openapi.json` and `SnakkApiClient.cs`) ARE committed to source control because:
- Reproducible builds without needing API running
- CI/CD can build projects without running Snakk.Api
- Clear audit trail of API changes in git history

---

## Known Issues & Fixes

### Entity Property Mismatches
- FollowDatabaseEntity: Use `FollowedUser`, `Space`, `Discussion`
- ReactionDatabaseEntity: Use `Type`, only has `Post` property
- UserDatabaseEntity: Use `DisplayName` (not Username)
- RefreshTokenDatabaseEntity: No `LastUsedAt` property

### API Response Formats
- Failed logins: `{ failures, suspiciousIps, total, page, pageSize }`
- All paginated responses need consistent structure

---

## When in Doubt

1. **Approval**: Did I ask for approval before making code changes?
2. **Architecture**: Does this follow Clean Architecture? Is BFF pattern respected?
3. **Security**: Is user content escaped? Are we avoiding XSS?
4. **JavaScript**: Are we calling `/bff/*` not `/api/*`?
5. **Technology**: Is the admin panel Blazor (NOT Next.js)?

If any answer is "no" or "unsure", **STOP and ask before proceeding**.

---

## Additional Documentation

For comprehensive architecture information, see:
- **`docs/ARCHITECTURE.md`** - Complete architecture documentation (current, accurate)
- **Project Memory** - `C:\Users\me\.claude\projects\c--Snakk\memory\MEMORY.md`

### Obsolete Documents (DO NOT USE)
- ❌ `docs/plans/admin-panel-architecture.md` - Describes Next.js (never implemented)
- ❌ `docs/plans/nextjs-admin-implementation-guide.md` - Describes Next.js (never implemented)

**Current Admin Panel**: Blazor Server + Microsoft Fluent UI (NOT Next.js)
