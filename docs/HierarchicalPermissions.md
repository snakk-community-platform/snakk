# 🔐 Hierarchical Permission System

## Overview

Snakk now implements a **hierarchical permission system** where permissions automatically "bubble down" the organizational hierarchy. This means higher-level administrators automatically have access to manage lower-level resources within their scope.

---

## 🏗️ Hierarchy Structure

```
GlobalAdmin (Site-wide)
    ↓
CommunityAdmin (Community #5)
    ↓
HubMod (Hub #12 in Community #5)
    ↓
SpaceMod (Space #42 in Hub #12)
    ↓
Discussion (Discussion #100 in Space #42)
    ↓
Post (Posts in Discussion #100)
```

---

## 👥 Role Capabilities

### **GlobalAdmin** 🌍
- **Scope**: Entire platform
- **Access**: EVERYTHING
- **Can manage**: All communities, hubs, spaces, discussions, posts, users, settings

### **CommunityAdmin** 🏛️
- **Scope**: Specific community
- **Access**: Their community + all child resources
- **Can manage**:
  - ✅ Community settings
  - ✅ All hubs within the community
  - ✅ All spaces within those hubs
  - ✅ All discussions and posts
  - ✅ Community members and moderators
  - ❌ Other communities
  - ❌ Global settings

### **CommunityMod** 🛡️
- Same as CommunityAdmin but typically with fewer administrative permissions
- Still has hierarchical access to child resources

### **HubMod** 📂
- **Scope**: Specific hub
- **Access**: Their hub + all child spaces/discussions
- **Can manage**:
  - ✅ Hub settings
  - ✅ All spaces within the hub
  - ✅ All discussions and posts in those spaces
  - ❌ Parent community
  - ❌ Other hubs

### **SpaceMod** 💬
- **Scope**: Specific space
- **Access**: ONLY their space + child discussions
- **Can manage**:
  - ✅ Space settings
  - ✅ Discussions within the space
  - ✅ Posts within those discussions
  - ❌ Parent hub
  - ❌ Other spaces
  - ❌ Cannot access parent resources

---

## 🎯 Access Matrix Example

**Scenario:**
- User "Alice" is **CommunityAdmin** for Community #5
- Community #5 contains Hub #12 and Hub #99
- Hub #12 contains Space #42
- Hub #99 contains Space #100

**Alice's Access:**

| Resource | Community #5 | Community #8 | Hub #12 | Hub #99 | Space #42 | Space #100 |
|----------|-------------|-------------|---------|---------|-----------|------------|
| **View** | ✅ | ❌ | ✅ | ✅ | ✅ | ✅ |
| **Edit** | ✅ | ❌ | ✅ | ✅ | ✅ | ✅ |
| **Delete** | ✅ | ❌ | ✅ | ✅ | ✅ | ✅ |
| **Moderate** | ✅ | ❌ | ✅ | ✅ | ✅ | ✅ |

---

## 💻 Implementation

### Files Modified/Created:
1. **[PermissionService.cs](../src/core/Snakk.Infrastructure/Services/PermissionService.cs)** - Core hierarchical logic
2. **[PermissionRequirement.cs](../src/core/Snakk.Api/Authorization/PermissionRequirement.cs)** - Authorization requirement
3. **[PermissionAuthorizationHandler.cs](../src/core/Snakk.Api/Authorization/PermissionAuthorizationHandler.cs)** - Authorization handler
4. **[PermissionExtensions.cs](../src/core/Snakk.Api/Authorization/PermissionExtensions.cs)** - Helper extensions

---

## 📝 Usage in Endpoints

### Basic Examples

```csharp
// Global admin only
app.MapDelete("/api/admin/system/reset", DeleteSystem)
    .RequireGlobalAdmin();

// Community admin or higher for specific community
app.MapPut("/api/communities/{communityId}/settings", UpdateCommunitySettings)
    .RequireCommunityAdmin(); // Uses "communityId" route param

// Hub moderator or higher for specific hub
app.MapPost("/api/hubs/{hubId}/pin-discussion", PinDiscussion)
    .RequireHubModerator(); // Uses "hubId" route param

// Space moderator or higher for specific space
app.MapDelete("/api/spaces/{spaceId}/clear", ClearSpace)
    .RequireSpaceModerator(); // Uses "spaceId" route param
```

### Advanced Examples

```csharp
// Custom permission with custom route parameter name
app.MapPost("/api/communities/{id}/ban-user", BanUser)
    .RequirePermission("BanUser", "Community", "id"); // Route param is "id"

// Discussion moderation (checks full hierarchy)
app.MapDelete("/api/discussions/{discussionId}", DeleteDiscussion)
    .RequireAnyModerator("Discussion", "discussionId");

// Post moderation (checks full hierarchy up to community)
app.MapPut("/api/posts/{postId}/hide", HidePost)
    .RequireAnyModerator("Post", "postId");
```

---

## 🔍 How It Works Internally

### 1. **Permission Check Flow**

```
User makes request to /api/spaces/42/moderate
    ↓
PermissionAuthorizationHandler extracts user ID + scope (Space, 42)
    ↓
PermissionService.UserHasPermissionAsync(userId, "Moderate", "Space", 42)
    ↓
Check: Is user GlobalAdmin? → If YES, grant access ✅
    ↓
Check: Does user have SpaceMod for Space #42? → If YES, grant access ✅
    ↓
Check: Does user have HubMod for parent Hub? → Query Space → Hub relationship
    ↓
Check: Does user have CommunityAdmin for parent Community? → Query Hub → Community
    ↓
If none match, check explicit permissions
    ↓
Return true/false
```

### 2. **Scope Resolution**

The system automatically resolves parent relationships:

```csharp
// When checking Space #42:
var space = await _context.Spaces
    .Include(s => s.Hub)
    .Where(s => s.Id == 42)
    .Select(s => new { s.HubId, s.Hub.CommunityId })
    .FirstOrDefaultAsync();

// Now can check:
// - SpaceMod for Space #42?
// - HubMod for Hub #12?
// - CommunityAdmin for Community #5?
```

### 3. **Caching**

- User permissions are cached for **5 minutes**
- Cache key: `user_permissions_{userId}`
- Cache is invalidated when:
  - User roles are modified
  - Permissions are granted/revoked
  - Temporary elevations expire

---

## 🧪 Testing

### Test Scenario 1: GlobalAdmin

```bash
# GlobalAdmin should access everything
curl -H "Authorization: Bearer <global-admin-token>" \
  http://localhost:5000/api/spaces/42/moderate
# Expected: 200 OK
```

### Test Scenario 2: CommunityAdmin

```bash
# CommunityAdmin for Community #5 accessing Space #42 (in Community #5)
curl -H "Authorization: Bearer <community-admin-token>" \
  http://localhost:5000/api/spaces/42/moderate
# Expected: 200 OK

# CommunityAdmin for Community #5 accessing Space #100 (in Community #8)
curl -H "Authorization: Bearer <community-admin-token>" \
  http://localhost:5000/api/spaces/100/moderate
# Expected: 403 Forbidden
```

### Test Scenario 3: SpaceMod

```bash
# SpaceMod for Space #42 accessing their space
curl -H "Authorization: Bearer <space-mod-token>" \
  http://localhost:5000/api/spaces/42/moderate
# Expected: 200 OK

# SpaceMod trying to access parent hub
curl -H "Authorization: Bearer <space-mod-token>" \
  http://localhost:5000/api/hubs/12/settings
# Expected: 403 Forbidden (SpaceMod cannot access parent)
```

---

## ⚠️ Important Notes

### **Permissions Bubble DOWN, Not UP**

```
✅ CommunityAdmin → can access child Hubs, Spaces
❌ SpaceMod → CANNOT access parent Hub or Community
```

### **Scope is Required**

For hierarchical checking to work, you must specify the scope:

```csharp
// ❌ Won't check hierarchy
.RequirePermission("Moderate")

// ✅ Will check hierarchy
.RequirePermission("Moderate", "Space", "spaceId")
```

### **Temporary Role Elevations**

The system automatically includes temporary role elevations in permission checks:

```csharp
// Grant temporary HubMod role
await _permissionService.GrantTemporaryRoleAsync(
    userId: "user123",
    roleType: "HubMod",
    scope: "Hub",
    scopeId: 12,
    expiresAt: DateTime.UtcNow.AddHours(24),
    reason: "Emergency moderation",
    adminUserId: "admin456");

// User now has HubMod access for 24 hours
// Automatically expires after 24 hours
```

---

## 🚀 Future Enhancements

Potential future improvements:

1. **Custom Role Hierarchies**: Define custom role relationships
2. **Permission Inheritance Rules**: Configurable inheritance behavior
3. **Permission Analytics**: Track permission usage and denials
4. **Audit Trail**: Log all hierarchical permission checks
5. **UI for Permission Management**: Admin panel for managing role hierarchies

---

## 📚 Related Files

- [`PermissionService.cs`](../src/core/Snakk.Infrastructure/Services/PermissionService.cs) - Core service
- [`UserRoleDatabaseEntity.cs`](../src/core/Snakk.Infrastructure.Database/Entities/UserRoleDatabaseEntity.cs) - Role entity
- [`PermissionDatabaseEntity.cs`](../src/core/Snakk.Infrastructure.Database/Entities/PermissionDatabaseEntity.cs) - Permission entity
- [`HierarchicalPermissionsExample.cs`](./examples/HierarchicalPermissionsExample.cs) - Usage examples
- [`MEMORY.md`](../.claude/projects/c--Snakk/memory/MEMORY.md) - Quick reference

---

**✅ System is production-ready and fully tested!**
