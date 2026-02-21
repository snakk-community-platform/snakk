# Scoped Management Pages (/manage) - Implementation Plan

## Overview

Build scoped management pages ("admin islands") in Snakk.AdminWeb accessible via natural URLs:
- `/c/{communitySlug}/manage/` - Community management
- `/c/{communitySlug}/h/{hubSlug}/manage/` - Hub management
- `/c/{communitySlug}/h/{hubSlug}/s/{spaceSlug}/manage/` - Space management

Uses flat moderator roles with 7 granular permissions. Report reasons bubble down the hierarchy (Global → Community → Hub → Space).

---

## Phase 1: Permission Infrastructure & Auth Fixes

### 1a. Define Permission Constants
**File:** `src/core/Snakk.Shared/Enums/ManagePermissionEnum.cs` (NEW)
```csharp
public enum ManagePermissionEnum
{
    ViewDashboard = 1,
    ManageContent = 2,
    ManageReports = 3,
    ManageBans = 4,
    ManageSettings = 5,
    ManageTeam = 6,
    ManageWebhooks = 7
}
```

### 1b. Permission Derivation Service
**File:** `src/core/Snakk.Application/Services/IManagePermissionService.cs` (NEW)
- `Task<ManagePermissionSet> GetPermissionsForScopeAsync(Guid userId, string scopeType, Guid scopeId)`
- `Task<bool> HasPermissionAsync(Guid userId, string scopeType, Guid scopeId, ManagePermissionEnum permission)`

**File:** `src/core/Snakk.Infrastructure/Services/ManagePermissionService.cs` (NEW)
- Derives permissions from existing role types:
  - GlobalAdmin → ALL permissions everywhere
  - CommunityAdmin → ALL permissions at community scope
  - CommunityMod → ViewDashboard, ManageContent, ManageReports, ManageBans (no ManageSettings, ManageTeam, ManageWebhooks)
  - HubMod → Same as CommunityMod but at hub scope
  - SpaceMod → Same as CommunityMod but at space scope
- Uses existing PermissionService caching (5-min)

### 1c. Fix AdminWeb Authentication
**File:** `src/apps/Snakk.AdminWeb/Program.cs` (MODIFY)
- Keep JWT Bearer auth reading `.Snakk.Auth` cookie (this works for SSO)
- Remove any references to CookieAuthenticationDefaults

**File:** `src/apps/Snakk.AdminWeb/Pages/Auth/Login.cshtml.cs` (MODIFY)
- Remove role restriction (currently only allows GlobalAdmin/CommunityAdmin)
- Allow any authenticated user - scope access is checked per-page via permissions
- Redirect to appropriate manage page based on user's roles

### 1d. Seed Default Permissions
**File:** `src/core/Snakk.Infrastructure.Database/` - New migration (NEW)
- Seed the 7 permissions into the Permissions table
- No RolePermissions entries needed yet (derived from role type)

### Verification
- [ ] ManagePermissionEnum has 7 values
- [ ] ManagePermissionService correctly derives permissions from role types
- [ ] AdminWeb accepts any authenticated user with moderator roles
- [ ] Permissions table seeded with 7 records
- [ ] Solution compiles

---

## Phase 2: Gateway Routing & AdminWeb Path Configuration

### 2a. YARP Gateway Routes
**File:** `src/services/Snakk.Gateway/appsettings.json` (MODIFY)

Add 3 routes BEFORE the web catch-all route:
```json
"manage-community": {
  "ClusterId": "admin-cluster",
  "Match": { "Path": "/c/{slug}/manage/{**remainder}" }
},
"manage-hub": {
  "ClusterId": "admin-cluster",
  "Match": { "Path": "/c/{cslug}/h/{hslug}/manage/{**remainder}" }
},
"manage-space": {
  "ClusterId": "admin-cluster",
  "Match": { "Path": "/c/{cslug}/h/{hslug}/s/{sslug}/manage/{**remainder}" }
}
```

Also add route for Blazor static assets from manage pages:
```json
"admin-blazor": {
  "ClusterId": "admin-cluster",
  "Match": { "Path": "/_blazor/{**remainder}" }
},
"admin-framework": {
  "ClusterId": "admin-cluster",
  "Match": { "Path": "/_framework/{**remainder}" }
}
```

### 2b. AdminWeb Path Base
**File:** `src/apps/Snakk.AdminWeb/Program.cs` (MODIFY)
- Remove `UsePathBase("/admin")` for proxied debug mode
- Blazor will serve at root path `/`
- `<base href="/" />` in `_Host.cshtml` or equivalent

### 2c. AdminWeb Routing Setup
**File:** `src/apps/Snakk.AdminWeb/App.razor` (MODIFY)
- Ensure Router can handle `/c/{slug}/manage/...` routes
- Configure fallback page

### Verification
- [ ] Gateway routes manage paths to admin-cluster
- [ ] `/_blazor` and `/_framework` route to admin-cluster
- [ ] Existing `/admin/**` route still works for global admin pages
- [ ] AdminWeb serves Blazor content at manage URLs

---

## Phase 3: Manage Scope Context & Layout

### 3a. Manage Scope Context Service
**File:** `src/apps/Snakk.AdminWeb/Services/ManageScopeContext.cs` (NEW)
```csharp
public class ManageScopeContext
{
    public string ScopeType { get; set; } // "Community", "Hub", "Space"
    public Guid ScopeId { get; set; }
    public string ScopeName { get; set; }
    public string CommunitySlug { get; set; }
    public string? HubSlug { get; set; }
    public string? SpaceSlug { get; set; }
    public ManagePermissionSet Permissions { get; set; }

    // Breadcrumb data
    public string? CommunityName { get; set; }
    public string? HubName { get; set; }
    public string? SpaceName { get; set; }
}
```

**File:** `src/apps/Snakk.AdminWeb/Services/ManageScopeService.cs` (NEW)
- Resolves URL slugs → entity IDs via API calls
- Loads user's permissions for the resolved scope
- Validates user has at minimum `ViewDashboard` permission
- Returns 403 if unauthorized

### 3b. API Endpoints for Scope Resolution
**File:** `src/services/Snakk.Api/Endpoints/ManageContextEndpoints.cs` (NEW)
- `GET /api/manage/resolve?communitySlug=X&hubSlug=Y&spaceSlug=Z`
  - Returns: scope entity IDs, names, user's permissions for the scope
  - Validates slugs exist, returns 404 if not found

### 3c. Manage Layout Component
**File:** `src/apps/Snakk.AdminWeb/Components/Layout/ManageLayout.razor` (NEW)
- Left sidebar navigation (permission-aware - only show sections user can access)
- Breadcrumb bar showing: Community > Hub > Space > Section
- Scope context display (community/hub/space name + avatar)
- "Back to site" link (to Snakk.Web at the corresponding entity page)

Navigation items per scope type:

**Community scope:**
- Dashboard (ViewDashboard)
- Content Moderation (ManageContent)
- Reports (ManageReports)
- Bans (ManageBans)
- Moderation Log (ViewDashboard)
- Settings (ManageSettings)
- Report Reasons (ManageSettings)
- Team (ManageTeam)
- Webhooks (ManageWebhooks)

**Hub scope:**
- Dashboard (ViewDashboard)
- Content Moderation (ManageContent)
- Reports (ManageReports)
- Bans (ManageBans)
- Moderation Log (ViewDashboard)
- Settings (ManageSettings)
- Report Reasons (ManageSettings)
- Team (ManageTeam)
- Webhooks (ManageWebhooks)

**Space scope:**
- Dashboard (ViewDashboard)
- Content Moderation (ManageContent)
- Reports (ManageReports)
- Bans (ManageBans)
- Moderation Log (ViewDashboard)
- Settings (ManageSettings)
- Report Reasons (ManageSettings)
- Rules (ManageSettings)
- Team (ManageTeam)
- Webhooks (ManageWebhooks)

### 3d. Application DTOs for Manage Context
**File:** `src/core/Snakk.Application/DTOs/Management/ManageScopeDto.cs` (NEW)
- ScopeType, ScopeId, ScopeName, permissions list, breadcrumb data

### Verification
- [ ] ManageScopeService resolves slugs to entities
- [ ] ManageLayout renders with permission-filtered nav
- [ ] Unauthorized users see 403
- [ ] Breadcrumbs display correctly for all 3 scope levels
- [ ] Solution compiles

---

## Phase 4: Dashboard, Reports & Moderation Log Pages

### 4a. Manage Dashboard
**Files:** (NEW)
- `src/apps/Snakk.AdminWeb/Components/Manage/Dashboard.razor`

**Community Dashboard:** Recent activity stats, active reports count, recent bans, team overview, child hubs summary
**Hub Dashboard:** Similar but scoped to hub + child spaces
**Space Dashboard:** Similar but scoped to space only

Uses existing API endpoints:
- `/api/moderation/stats` (may need scope filtering)
- `/api/moderation/reports` (with scope filter)
- `/api/moderation/log` (with scope filter)

### 4b. Reports Page
**File:** `src/apps/Snakk.AdminWeb/Components/Manage/Reports.razor` (NEW)
- List reports scoped to current entity (+ children for community/hub)
- Filter by status (Open, Resolved, Dismissed)
- Report detail view with resolve/dismiss actions
- Uses FluentDataGrid for table display

### 4c. Content Moderation Page
**File:** `src/apps/Snakk.AdminWeb/Components/Manage/ContentModeration.razor` (NEW)
- Flagged/reported content list
- Quick actions: delete post, delete discussion, lock discussion, edit post
- Content preview with context

### 4d. Moderation Log Page
**File:** `src/apps/Snakk.AdminWeb/Components/Manage/ModerationLog.razor` (NEW)
- Chronological log of all moderation actions in scope
- Filter by action type, moderator, date range
- Uses existing `/api/moderation/log` endpoint with scope filter

### 4e. Scope-Filtered API Additions
**File:** `src/services/Snakk.Api/Endpoints/ModerationEndpoints.cs` (MODIFY)
- Add optional `scopeType` and `scopeId` query parameters to existing endpoints
- Filter results to include only items within the requested scope (+ children)

### Verification
- [ ] Dashboard shows real data from API
- [ ] Reports page lists, filters, resolves/dismisses reports
- [ ] Content moderation page shows flagged content with actions
- [ ] Moderation log page shows filtered history
- [ ] All pages respect scope boundaries

---

## Phase 5: Bans Page

### 5a. Bans Management Page
**File:** `src/apps/Snakk.AdminWeb/Components/Manage/Bans.razor` (NEW)
- List active bans in scope
- Create new ban (user search, reason, duration, scope)
- Revoke ban
- Ban history for specific users
- Uses existing `/api/moderation/bans` endpoints with scope filter

### Verification
- [ ] Ban list displays correctly with scope filtering
- [ ] Can create new scoped bans
- [ ] Can revoke bans
- [ ] Ban history works

---

## Phase 6: Settings, Team, Report Reasons, Rules & Webhooks

### 6a. Settings Page
**File:** `src/apps/Snakk.AdminWeb/Components/Manage/Settings.razor` (NEW)

**Community Settings:** Name, description, avatar, visibility, posting rules, default sort
**Hub Settings:** Name, description, avatar, visibility
**Space Settings:** Name, description, avatar, visibility, posting rules, default sort

Uses existing DTOs: `CommunitySettingsDto`, `HubSettingsDto`, `SpaceSettingsDto`

### 6b. Team Management Page
**File:** `src/apps/Snakk.AdminWeb/Components/Manage/Team.razor` (NEW)
- List moderators for current scope
- Invite/add moderator (user search)
- Set permissions per moderator (checkbox grid of 7 permissions)
- Remove moderator
- Uses existing `/api/moderation/roles` endpoints

### 6c. Report Reasons Page (All Scopes)
**File:** `src/apps/Snakk.AdminWeb/Components/Manage/ReportReasons.razor` (NEW)
- **Inherited reasons display:** Show read-only list of reasons inherited from parent scopes
  - Space shows: Global + Community + Hub + Space-specific reasons
  - Hub shows: Global + Community + Hub-specific reasons
  - Community shows: Global + Community-specific reasons
- **Custom reasons CRUD:** Add/edit/delete reasons specific to this scope
- Visual distinction between inherited (read-only) and local (editable) reasons
- Toggle to enable/disable inherited reasons at this scope level

**API support needed:**
- `GET /api/moderation/report-reasons?scopeType=X&scopeId=Y&includeBubbled=true`
  - Returns all applicable reasons with source scope info
- `POST/PUT/DELETE /api/moderation/report-reasons` - CRUD for scope-specific reasons

**File:** `src/services/Snakk.Api/Endpoints/ReportReasonEndpoints.cs` (NEW or extend ModerationEndpoints)
**File:** `src/core/Snakk.Application/UseCases/ReportReasonUseCase.cs` (NEW)
- Bubbling logic: Load reasons from Global → Community → Hub → Space
- Each reason tagged with its source scope for display

### 6d. Rules Page (Space-only)
**File:** `src/apps/Snakk.AdminWeb/Components/Manage/Rules.razor` (NEW)
- Ordered list of space rules
- Add/edit/delete/reorder rules
- Uses existing `SpaceRulesDto`

### 6e. Webhooks Page (Placeholder)
**File:** `src/apps/Snakk.AdminWeb/Components/Manage/Webhooks.razor` (NEW)
- Placeholder page with "Coming soon" message
- Only visible to users with ManageWebhooks permission
- Structure ready for future implementation

### Verification
- [ ] Settings page loads and saves for all 3 scope types
- [ ] Team page lists moderators, can add/remove, set permissions
- [ ] Report reasons show inherited + local reasons correctly
- [ ] Report reasons bubble down: Global → Community → Hub → Space
- [ ] Rules page works for spaces
- [ ] Webhooks placeholder shows for authorized users
- [ ] Solution compiles

---

## Phase 7: SDK Regeneration & Integration Testing

### 7a. Regenerate SDK
- Export updated OpenAPI spec from running Snakk.Api
- Rebuild Snakk.Sdk to generate updated client
- Verify Snakk.Web and Snakk.AdminWeb compile with new SDK

### 7b. Add "Manage" Links in Snakk.Web
**Files in `src/apps/Snakk.Web/Pages/`:** (MODIFY)
- Community page: Add "Manage" link/button visible to moderators
- Hub page: Add "Manage" link/button visible to moderators
- Space page: Add "Manage" link/button visible to moderators
- Links point to `/c/{slug}/manage/`, `/c/{slug}/h/{slug}/manage/`, etc.

### 7c. Clean Up Dead Code in AdminWeb
- Remove `SnakkAdminApiClient.cs` (unused, not in DI)
- Remove `AdminAuthenticationMiddleware.cs` (not in pipeline)
- Remove mock data from existing pages

### Verification
- [ ] SDK regenerated and committed
- [ ] All projects compile
- [ ] Manage links visible in Snakk.Web for moderators
- [ ] Dead code removed from AdminWeb
- [ ] End-to-end: click "Manage" in Snakk.Web → arrives at manage page in AdminWeb

---

## Key Design Decisions

1. **No /members page** - Per user request
2. **Report reasons at ALL scopes** (community, hub, space) - Per user request
3. **Report reasons bubble down** (Global → Community → Hub → Space) - Per user request
4. **Flat moderator role** with 7 granular permissions
5. **Permissions derived from role type** initially (not customized per-moderator yet)
6. **`/manage` URL segment** (not `/admin` or `/moderation`)
7. **Blazor Server + Microsoft Fluent UI** for all manage pages
8. **Gateway routes manage paths** to AdminWeb cluster

## Files Summary

### New Files (~20)
- `ManagePermissionEnum.cs`, `IManagePermissionService.cs`, `ManagePermissionService.cs`
- `ManageScopeContext.cs`, `ManageScopeService.cs`, `ManageScopeDto.cs`
- `ManageContextEndpoints.cs`, `ReportReasonEndpoints.cs`, `ReportReasonUseCase.cs`
- `ManageLayout.razor`
- `Dashboard.razor`, `Reports.razor`, `ContentModeration.razor`, `ModerationLog.razor`
- `Bans.razor`, `Settings.razor`, `Team.razor`, `ReportReasons.razor`, `Rules.razor`, `Webhooks.razor`
- Database migration for permission seeding

### Modified Files (~8)
- `Program.cs` (AdminWeb - auth fix)
- `Login.cshtml.cs` (AdminWeb - role restriction removal)
- `App.razor` (AdminWeb - routing)
- `appsettings.json` (Gateway - routes)
- `ModerationEndpoints.cs` (API - scope filtering)
- Snakk.Web pages (manage links)
- SDK files (regenerated)
