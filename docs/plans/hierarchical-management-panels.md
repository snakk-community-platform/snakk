# Hierarchical Management Panels Implementation Plan

## Overview

This plan outlines the implementation of three distinct management areas in Snakk:

1. **Platform Admin Panel** (`/admin`) - Site-wide administrators
2. **Community Management** (`/c/:slug/manage`) - Community owners and moderators
3. **Space Management** (`/c/:slug/s/:slug/manage`) - Space moderators

### Key Principle
**Separation of Concerns:** Platform admins manage the system, community owners manage their communities, space moderators manage their spaces. Each level is completely independent with clear permission boundaries.

---

## Architecture

### Permission Hierarchy

```
Platform Admins
└─ Access: /admin (system-wide control)
   └─ Cannot: Directly manage community/space settings (except via platform tools)

Community Owners/Moderators
└─ Access: /c/:slug/manage (their communities only)
   └─ Inherits: Can also manage all spaces within their community
   └─ Cannot: Access platform admin or other communities

Space Moderators
└─ Access: /c/:slug/s/:slug/manage (their spaces only)
   └─ Cannot: Access platform admin, manage community, or other spaces
```

### URL Structure

```
# Platform Admin (existing)
/admin
/admin/users
/admin/security
/admin/audit
/admin/settings
/admin/webhooks

# Community Management (new)
/c/:communitySlug/manage
/c/:communitySlug/manage/overview
/c/:communitySlug/manage/settings
/c/:communitySlug/manage/moderation
/c/:communitySlug/manage/spaces
/c/:communitySlug/manage/members
/c/:communitySlug/manage/roles
/c/:communitySlug/manage/webhooks
/c/:communitySlug/manage/rules
/c/:communitySlug/manage/analytics

# Space Management (new)
/c/:communitySlug/s/:spaceSlug/manage
/c/:communitySlug/s/:spaceSlug/manage/overview
/c/:communitySlug/s/:spaceSlug/manage/settings
/c/:communitySlug/s/:spaceSlug/manage/moderation
/c/:communitySlug/s/:spaceSlug/manage/rules
/c/:communitySlug/s/:spaceSlug/manage/thread-types
/c/:communitySlug/s/:spaceSlug/manage/analytics
```

---

## Backend Implementation

### Phase 1: Permission System Enhancement

#### 1.1 Database Schema Updates

**New Permissions Table Entries:**
```sql
-- Community-level permissions
INSERT INTO Permissions (Name, Description) VALUES
  ('community.manage', 'Manage community settings'),
  ('community.manage.settings', 'Edit community settings'),
  ('community.manage.moderators', 'Assign community moderators'),
  ('community.manage.spaces', 'Create and manage spaces'),
  ('community.manage.members', 'Manage community members'),
  ('community.manage.webhooks', 'Configure community webhooks'),
  ('community.manage.rules', 'Edit community rules'),
  ('community.moderate', 'Moderate community content');

-- Space-level permissions
INSERT INTO Permissions (Name, Description) VALUES
  ('space.manage', 'Manage space settings'),
  ('space.manage.settings', 'Edit space settings'),
  ('space.manage.rules', 'Edit space rules'),
  ('space.manage.thread-types', 'Configure thread types'),
  ('space.moderate', 'Moderate space content');
```

**Community-Space Relationship:**
- Communities already exist in the database
- Spaces (boards) already exist
- Need to add permission checks for management routes

#### 1.2 Permission Service Enhancement

**File:** `Snakk.Application/Services/IPermissionService.cs`

Add new methods:
```csharp
Task<bool> CanManageCommunityAsync(Guid userId, Guid communityId);
Task<bool> CanManageSpaceAsync(Guid userId, Guid spaceId);
Task<bool> HasCommunityPermissionAsync(Guid userId, Guid communityId, string permission);
Task<bool> HasSpacePermissionAsync(Guid userId, Guid spaceId, string permission);
Task<List<Guid>> GetManagedCommunitiesAsync(Guid userId);
Task<List<Guid>> GetManagedSpacesAsync(Guid userId);
```

**File:** `Snakk.Infrastructure/Services/PermissionService.cs`

Implementation logic:
- Check if user is community owner
- Check if user has community-wide moderator role
- Check if user has space moderator role
- Inherit permissions (community mods can manage all spaces in their community)

### Phase 2: Community Management DTOs

**File:** `Snakk.Application/DTOs/CommunityManagement/`

Create DTOs:
```csharp
// CommunityManagementDto.cs
public record CommunityManagementDto
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string Slug { get; init; }
    public string? Description { get; init; }
    public string? AvatarUrl { get; init; }
    public string? CustomDomain { get; init; }
    public Guid OwnerId { get; init; }
    public UserDto Owner { get; init; }
    public DateTime CreatedAt { get; init; }
    public int MemberCount { get; init; }
    public int SpaceCount { get; init; }
    public int PostCount { get; init; }
}

// CommunitySettingsDto.cs
public record CommunitySettingsDto
{
    public Guid CommunityId { get; init; }
    public string Name { get; init; }
    public string Slug { get; init; }
    public string? Description { get; init; }
    public bool IsPrivate { get; init; }
    public bool RequireApproval { get; init; }
    public string? CustomDomain { get; init; }
    public string? WelcomeMessage { get; init; }
}

// UpdateCommunitySettingsDto.cs
public record UpdateCommunitySettingsDto
{
    public string Name { get; init; }
    public string? Description { get; init; }
    public bool IsPrivate { get; init; }
    public bool RequireApproval { get; init; }
    public string? CustomDomain { get; init; }
    public string? WelcomeMessage { get; init; }
}

// CommunityModeratorDto.cs
public record CommunityModeratorDto
{
    public Guid UserId { get; init; }
    public UserDto User { get; init; }
    public Guid RoleId { get; init; }
    public RoleDto Role { get; init; }
    public DateTime AssignedAt { get; init; }
    public Guid AssignedBy { get; init; }
}

// AddCommunityModeratorDto.cs
public record AddCommunityModeratorDto
{
    public Guid UserId { get; init; }
    public Guid RoleId { get; init; }
}
```

### Phase 3: Space Management DTOs

**File:** `Snakk.Application/DTOs/SpaceManagement/`

```csharp
// SpaceManagementDto.cs
public record SpaceManagementDto
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string Slug { get; init; }
    public string? Description { get; init; }
    public Guid CommunityId { get; init; }
    public DateTime CreatedAt { get; init; }
    public int ThreadCount { get; init; }
    public int PostCount { get; init; }
}

// SpaceSettingsDto.cs
public record SpaceSettingsDto
{
    public Guid SpaceId { get; init; }
    public string Name { get; init; }
    public string Slug { get; init; }
    public string? Description { get; init; }
    public bool IsPrivate { get; init; }
    public bool IsArchived { get; init; }
    public bool AllowThreads { get; init; }
    public bool AllowPolls { get; init; }
    public bool AllowEvents { get; init; }
}

// UpdateSpaceSettingsDto.cs
public record UpdateSpaceSettingsDto
{
    public string Name { get; init; }
    public string? Description { get; init; }
    public bool IsPrivate { get; init; }
    public bool IsArchived { get; init; }
    public bool AllowThreads { get; init; }
    public bool AllowPolls { get; init; }
    public bool AllowEvents { get; init; }
}

// SpaceModeratorDto.cs
public record SpaceModeratorDto
{
    public Guid UserId { get; init; }
    public UserDto User { get; init; }
    public DateTime AssignedAt { get; init; }
}
```

### Phase 4: Community Management Service

**File:** `Snakk.Application/Services/ICommunityManagementService.cs`

```csharp
public interface ICommunityManagementService
{
    // Overview
    Task<CommunityManagementDto> GetCommunityManagementOverviewAsync(Guid communityId, Guid requestingUserId);

    // Settings
    Task<CommunitySettingsDto> GetCommunitySettingsAsync(Guid communityId, Guid requestingUserId);
    Task UpdateCommunitySettingsAsync(Guid communityId, UpdateCommunitySettingsDto dto, Guid requestingUserId);

    // Moderators
    Task<List<CommunityModeratorDto>> GetCommunityModeratorsAsync(Guid communityId, Guid requestingUserId);
    Task AddCommunityModeratorAsync(Guid communityId, AddCommunityModeratorDto dto, Guid requestingUserId);
    Task RemoveCommunityModeratorAsync(Guid communityId, Guid userId, Guid requestingUserId);

    // Spaces
    Task<List<SpaceDto>> GetCommunitySpacesAsync(Guid communityId, Guid requestingUserId);
    Task<SpaceDto> CreateSpaceAsync(Guid communityId, CreateSpaceDto dto, Guid requestingUserId);
    Task ArchiveSpaceAsync(Guid communityId, Guid spaceId, Guid requestingUserId);

    // Members
    Task<PaginatedResult<CommunityMemberDto>> GetCommunityMembersAsync(
        Guid communityId, Guid requestingUserId, int page, int pageSize);
    Task RemoveCommunityMemberAsync(Guid communityId, Guid userId, Guid requestingUserId);

    // Webhooks
    Task<List<WebhookDto>> GetCommunityWebhooksAsync(Guid communityId, Guid requestingUserId);
    Task<WebhookDto> CreateCommunityWebhookAsync(Guid communityId, CreateWebhookDto dto, Guid requestingUserId);

    // Rules
    Task<List<CommunityRuleDto>> GetCommunityRulesAsync(Guid communityId, Guid requestingUserId);
    Task<CommunityRuleDto> CreateCommunityRuleAsync(Guid communityId, CreateRuleDto dto, Guid requestingUserId);
    Task UpdateCommunityRuleAsync(Guid communityId, Guid ruleId, UpdateRuleDto dto, Guid requestingUserId);
    Task DeleteCommunityRuleAsync(Guid communityId, Guid ruleId, Guid requestingUserId);
}
```

**File:** `Snakk.Infrastructure/Services/CommunityManagementService.cs`

Implementation with:
- Permission checks on every method
- Database queries via SnakkDbContext
- Domain event dispatching for audit trail
- Error handling for unauthorized access

### Phase 5: Space Management Service

**File:** `Snakk.Application/Services/ISpaceManagementService.cs`

```csharp
public interface ISpaceManagementService
{
    // Overview
    Task<SpaceManagementDto> GetSpaceManagementOverviewAsync(Guid spaceId, Guid requestingUserId);

    // Settings
    Task<SpaceSettingsDto> GetSpaceSettingsAsync(Guid spaceId, Guid requestingUserId);
    Task UpdateSpaceSettingsAsync(Guid spaceId, UpdateSpaceSettingsDto dto, Guid requestingUserId);

    // Moderators
    Task<List<SpaceModeratorDto>> GetSpaceModeratorsAsync(Guid spaceId, Guid requestingUserId);
    Task AddSpaceModeratorAsync(Guid spaceId, Guid userId, Guid requestingUserId);
    Task RemoveSpaceModeratorAsync(Guid spaceId, Guid userId, Guid requestingUserId);

    // Rules
    Task<List<SpaceRuleDto>> GetSpaceRulesAsync(Guid spaceId, Guid requestingUserId);
    Task<SpaceRuleDto> CreateSpaceRuleAsync(Guid spaceId, CreateRuleDto dto, Guid requestingUserId);

    // Thread Types
    Task<ThreadTypeConfigDto> GetThreadTypeConfigAsync(Guid spaceId, Guid requestingUserId);
    Task UpdateThreadTypeConfigAsync(Guid spaceId, ThreadTypeConfigDto dto, Guid requestingUserId);
}
```

**File:** `Snakk.Infrastructure/Services/SpaceManagementService.cs`

Implementation with permission checks and business logic.

### Phase 6: API Endpoints

**File:** `Snakk.Api/Endpoints/CommunityManagementEndpoints.cs`

```csharp
public static class CommunityManagementEndpoints
{
    public static void MapCommunityManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/communities/{communityId:guid}/manage")
            .RequireAuthorization();

        // Overview
        group.MapGet("/overview", GetOverview);

        // Settings
        group.MapGet("/settings", GetSettings);
        group.MapPut("/settings", UpdateSettings);

        // Moderators
        group.MapGet("/moderators", GetModerators);
        group.MapPost("/moderators", AddModerator);
        group.MapDelete("/moderators/{userId:guid}", RemoveModerator);

        // Spaces
        group.MapGet("/spaces", GetSpaces);
        group.MapPost("/spaces", CreateSpace);
        group.MapDelete("/spaces/{spaceId:guid}", ArchiveSpace);

        // Members
        group.MapGet("/members", GetMembers);
        group.MapDelete("/members/{userId:guid}", RemoveMember);

        // Webhooks
        group.MapGet("/webhooks", GetWebhooks);
        group.MapPost("/webhooks", CreateWebhook);

        // Rules
        group.MapGet("/rules", GetRules);
        group.MapPost("/rules", CreateRule);
        group.MapPut("/rules/{ruleId:guid}", UpdateRule);
        group.MapDelete("/rules/{ruleId:guid}", DeleteRule);
    }

    private static async Task<IResult> GetOverview(
        Guid communityId,
        ICommunityManagementService service,
        ClaimsPrincipal user)
    {
        var userId = user.GetUserId();
        var overview = await service.GetCommunityManagementOverviewAsync(communityId, userId);
        return Results.Ok(overview);
    }

    // ... other endpoint implementations
}
```

**File:** `Snakk.Api/Endpoints/SpaceManagementEndpoints.cs`

```csharp
public static class SpaceManagementEndpoints
{
    public static void MapSpaceManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/communities/{communityId:guid}/spaces/{spaceId:guid}/manage")
            .RequireAuthorization();

        // Overview
        group.MapGet("/overview", GetOverview);

        // Settings
        group.MapGet("/settings", GetSettings);
        group.MapPut("/settings", UpdateSettings);

        // Moderators
        group.MapGet("/moderators", GetModerators);
        group.MapPost("/moderators", AddModerator);
        group.MapDelete("/moderators/{userId:guid}", RemoveModerator);

        // Rules
        group.MapGet("/rules", GetRules);
        group.MapPost("/rules", CreateRule);

        // Thread Types
        group.MapGet("/thread-types", GetThreadTypeConfig);
        group.MapPut("/thread-types", UpdateThreadTypeConfig);
    }

    // ... endpoint implementations
}
```

**File:** `Snakk.Api/Program.cs`

Register endpoints:
```csharp
app.MapCommunityManagementEndpoints();
app.MapSpaceManagementEndpoints();
```

---

## Frontend Implementation

### Phase 1: Community Management Routes

**Directory Structure:**
```
src/clients/snakk-admin/src/app/
├── admin/                          # Existing platform admin
│   ├── layout.tsx
│   └── ...
├── c/
│   └── [communitySlug]/
│       ├── page.tsx                # Community view (existing)
│       ├── manage/                 # NEW: Community management
│       │   ├── layout.tsx          # Management layout
│       │   ├── page.tsx            # Redirect to overview
│       │   ├── overview/
│       │   │   └── page.tsx
│       │   ├── settings/
│       │   │   └── page.tsx
│       │   ├── moderation/
│       │   │   └── page.tsx
│       │   ├── spaces/
│       │   │   └── page.tsx
│       │   ├── members/
│       │   │   └── page.tsx
│       │   ├── roles/
│       │   │   └── page.tsx
│       │   ├── webhooks/
│       │   │   └── page.tsx
│       │   ├── rules/
│       │   │   └── page.tsx
│       │   └── analytics/
│       │       └── page.tsx
│       └── s/
│           └── [spaceSlug]/
│               ├── page.tsx        # Space view (existing)
│               └── manage/         # NEW: Space management
│                   ├── layout.tsx
│                   ├── page.tsx    # Redirect to overview
│                   ├── overview/
│                   │   └── page.tsx
│                   ├── settings/
│                   │   └── page.tsx
│                   ├── moderation/
│                   │   └── page.tsx
│                   ├── rules/
│                   │   └── page.tsx
│                   ├── thread-types/
│                   │   └── page.tsx
│                   └── analytics/
│                       └── page.tsx
```

### Phase 2: Management Layout Component

**File:** `src/clients/snakk-admin/src/components/management/management-layout.tsx`

```tsx
interface ManagementLayoutProps {
  title: string;
  subtitle?: string;
  backUrl: string;
  backLabel: string;
  navigation: {
    label: string;
    href: string;
    icon?: React.ComponentType;
  }[];
  children: React.ReactNode;
}

export function ManagementLayout({
  title,
  subtitle,
  backUrl,
  backLabel,
  navigation,
  children
}: ManagementLayoutProps) {
  return (
    <div className="flex min-h-screen">
      {/* Sidebar Navigation */}
      <aside className="w-64 border-r bg-muted/40">
        <div className="p-4">
          <Button variant="ghost" asChild className="w-full justify-start">
            <Link href={backUrl}>
              <ArrowLeft className="w-4 h-4 mr-2" />
              {backLabel}
            </Link>
          </Button>
        </div>

        <nav className="space-y-1 p-2">
          {navigation.map((item) => (
            <Button
              key={item.href}
              variant="ghost"
              asChild
              className="w-full justify-start"
            >
              <Link href={item.href}>
                {item.icon && <item.icon className="w-4 h-4 mr-2" />}
                {item.label}
              </Link>
            </Button>
          ))}
        </nav>
      </aside>

      {/* Main Content */}
      <main className="flex-1">
        <div className="border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
          <div className="container py-6">
            <h1 className="text-3xl font-bold">{title}</h1>
            {subtitle && (
              <p className="text-muted-foreground mt-1">{subtitle}</p>
            )}
          </div>
        </div>

        <div className="container py-6">
          {children}
        </div>
      </main>
    </div>
  );
}
```

### Phase 3: Community Management Layout

**File:** `src/clients/snakk-admin/src/app/c/[communitySlug]/manage/layout.tsx`

```tsx
import { ManagementLayout } from "@/components/management/management-layout";
import { Settings, Users, Shield, Grid, Webhook, FileText, BarChart } from "lucide-react";

export default async function CommunityManageLayout({
  children,
  params
}: {
  children: React.ReactNode;
  params: { communitySlug: string };
}) {
  // TODO: Fetch community data
  const community = await getCommunity(params.communitySlug);

  // TODO: Check permissions
  const canManage = await checkCanManageCommunity(community.id);
  if (!canManage) {
    redirect(`/c/${params.communitySlug}`);
  }

  const navigation = [
    {
      label: "Overview",
      href: `/c/${params.communitySlug}/manage/overview`,
      icon: BarChart
    },
    {
      label: "Settings",
      href: `/c/${params.communitySlug}/manage/settings`,
      icon: Settings
    },
    {
      label: "Moderation",
      href: `/c/${params.communitySlug}/manage/moderation`,
      icon: Shield
    },
    {
      label: "Spaces",
      href: `/c/${params.communitySlug}/manage/spaces`,
      icon: Grid
    },
    {
      label: "Members",
      href: `/c/${params.communitySlug}/manage/members`,
      icon: Users
    },
    {
      label: "Webhooks",
      href: `/c/${params.communitySlug}/manage/webhooks`,
      icon: Webhook
    },
    {
      label: "Rules",
      href: `/c/${params.communitySlug}/manage/rules`,
      icon: FileText
    },
    {
      label: "Analytics",
      href: `/c/${params.communitySlug}/manage/analytics`,
      icon: BarChart
    }
  ];

  return (
    <ManagementLayout
      title={`Managing: ${community.name}`}
      subtitle={community.description}
      backUrl={`/c/${params.communitySlug}`}
      backLabel="Back to Community"
      navigation={navigation}
    >
      {children}
    </ManagementLayout>
  );
}
```

### Phase 4: Space Management Layout

**File:** `src/clients/snakk-admin/src/app/c/[communitySlug]/s/[spaceSlug]/manage/layout.tsx`

```tsx
import { ManagementLayout } from "@/components/management/management-layout";
import { Settings, Shield, FileText, ListTree, BarChart } from "lucide-react";

export default async function SpaceManageLayout({
  children,
  params
}: {
  children: React.ReactNode;
  params: { communitySlug: string; spaceSlug: string };
}) {
  const space = await getSpace(params.communitySlug, params.spaceSlug);
  const canManage = await checkCanManageSpace(space.id);

  if (!canManage) {
    redirect(`/c/${params.communitySlug}/s/${params.spaceSlug}`);
  }

  const navigation = [
    {
      label: "Overview",
      href: `/c/${params.communitySlug}/s/${params.spaceSlug}/manage/overview`,
      icon: BarChart
    },
    {
      label: "Settings",
      href: `/c/${params.communitySlug}/s/${params.spaceSlug}/manage/settings`,
      icon: Settings
    },
    {
      label: "Moderation",
      href: `/c/${params.communitySlug}/s/${params.spaceSlug}/manage/moderation`,
      icon: Shield
    },
    {
      label: "Rules",
      href: `/c/${params.communitySlug}/s/${params.spaceSlug}/manage/rules`,
      icon: FileText
    },
    {
      label: "Thread Types",
      href: `/c/${params.communitySlug}/s/${params.spaceSlug}/manage/thread-types`,
      icon: ListTree
    },
    {
      label: "Analytics",
      href: `/c/${params.communitySlug}/s/${params.spaceSlug}/manage/analytics`,
      icon: BarChart
    }
  ];

  return (
    <ManagementLayout
      title={`Managing: ${space.name}`}
      subtitle={`in ${space.communityName}`}
      backUrl={`/c/${params.communitySlug}/s/${params.spaceSlug}`}
      backLabel="Back to Space"
      navigation={navigation}
    >
      {children}
    </ManagementLayout>
  );
}
```

### Phase 5: API Client Functions

**File:** `src/clients/snakk-admin/src/lib/api/community-management.ts`

```typescript
import { apiClient } from "./client";
import type {
  CommunityManagementDto,
  CommunitySettingsDto,
  UpdateCommunitySettingsDto,
  CommunityModeratorDto,
  AddCommunityModeratorDto,
  // ... other types
} from "./types";

export const communityManagementApi = {
  // Overview
  getOverview: (communityId: string) =>
    apiClient.get<CommunityManagementDto>(
      `/communities/${communityId}/manage/overview`
    ),

  // Settings
  getSettings: (communityId: string) =>
    apiClient.get<CommunitySettingsDto>(
      `/communities/${communityId}/manage/settings`
    ),

  updateSettings: (communityId: string, data: UpdateCommunitySettingsDto) =>
    apiClient.put<CommunitySettingsDto>(
      `/communities/${communityId}/manage/settings`,
      data
    ),

  // Moderators
  getModerators: (communityId: string) =>
    apiClient.get<CommunityModeratorDto[]>(
      `/communities/${communityId}/manage/moderators`
    ),

  addModerator: (communityId: string, data: AddCommunityModeratorDto) =>
    apiClient.post<CommunityModeratorDto>(
      `/communities/${communityId}/manage/moderators`,
      data
    ),

  removeModerator: (communityId: string, userId: string) =>
    apiClient.delete(
      `/communities/${communityId}/manage/moderators/${userId}`
    ),

  // ... other methods
};
```

**File:** `src/clients/snakk-admin/src/lib/api/space-management.ts`

```typescript
import { apiClient } from "./client";
import type {
  SpaceManagementDto,
  SpaceSettingsDto,
  UpdateSpaceSettingsDto,
  // ... other types
} from "./types";

export const spaceManagementApi = {
  getOverview: (communityId: string, spaceId: string) =>
    apiClient.get<SpaceManagementDto>(
      `/communities/${communityId}/spaces/${spaceId}/manage/overview`
    ),

  getSettings: (communityId: string, spaceId: string) =>
    apiClient.get<SpaceSettingsDto>(
      `/communities/${communityId}/spaces/${spaceId}/manage/settings`
    ),

  updateSettings: (
    communityId: string,
    spaceId: string,
    data: UpdateSpaceSettingsDto
  ) =>
    apiClient.put<SpaceSettingsDto>(
      `/communities/${communityId}/spaces/${spaceId}/manage/settings`,
      data
    ),

  // ... other methods
};
```

### Phase 6: Example Page Implementation

**File:** `src/clients/snakk-admin/src/app/c/[communitySlug]/manage/settings/page.tsx`

```tsx
"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { communityManagementApi } from "@/lib/api/community-management";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useForm } from "react-hook-form";
import { toast } from "sonner";

export default function CommunitySettingsPage({
  params
}: {
  params: { communitySlug: string };
}) {
  const queryClient = useQueryClient();

  // Fetch settings
  const { data: settings, isLoading } = useQuery({
    queryKey: ["community-settings", params.communitySlug],
    queryFn: async () => {
      // TODO: Get community ID from slug
      const communityId = await getCommunityId(params.communitySlug);
      return communityManagementApi.getSettings(communityId);
    }
  });

  // Update mutation
  const updateMutation = useMutation({
    mutationFn: (data: UpdateCommunitySettingsDto) => {
      return communityManagementApi.updateSettings(settings!.communityId, data);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["community-settings"] });
      toast.success("Settings updated successfully");
    },
    onError: () => {
      toast.error("Failed to update settings");
    }
  });

  const form = useForm<UpdateCommunitySettingsDto>({
    values: settings
  });

  if (isLoading) {
    return <div>Loading...</div>; // TODO: Add skeleton
  }

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>General Settings</CardTitle>
          <CardDescription>
            Manage your community's basic information
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form
            onSubmit={form.handleSubmit((data) => updateMutation.mutate(data))}
            className="space-y-4"
          >
            <div>
              <Label htmlFor="name">Community Name</Label>
              <Input
                id="name"
                {...form.register("name", { required: true })}
              />
            </div>

            <div>
              <Label htmlFor="description">Description</Label>
              <Textarea
                id="description"
                {...form.register("description")}
                rows={4}
              />
            </div>

            <div className="flex items-center justify-between">
              <div>
                <Label htmlFor="isPrivate">Private Community</Label>
                <p className="text-sm text-muted-foreground">
                  Only members can view content
                </p>
              </div>
              <Switch
                id="isPrivate"
                checked={form.watch("isPrivate")}
                onCheckedChange={(checked) =>
                  form.setValue("isPrivate", checked)
                }
              />
            </div>

            <div className="flex items-center justify-between">
              <div>
                <Label htmlFor="requireApproval">Require Approval</Label>
                <p className="text-sm text-muted-foreground">
                  New members must be approved
                </p>
              </div>
              <Switch
                id="requireApproval"
                checked={form.watch("requireApproval")}
                onCheckedChange={(checked) =>
                  form.setValue("requireApproval", checked)
                }
              />
            </div>

            <Button type="submit" disabled={updateMutation.isPending}>
              {updateMutation.isPending ? "Saving..." : "Save Changes"}
            </Button>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Custom Domain</CardTitle>
          <CardDescription>
            Configure a custom domain for your community
          </CardDescription>
        </CardHeader>
        <CardContent>
          {/* TODO: Custom domain configuration UI */}
        </CardContent>
      </Card>
    </div>
  );
}
```

### Phase 7: Entry Point Buttons

**File:** `src/clients/snakk-admin/src/components/community/community-header.tsx`

Add management button:
```tsx
{canManage && (
  <Button asChild variant="outline">
    <Link href={`/c/${community.slug}/manage`}>
      <Settings className="w-4 h-4 mr-2" />
      Manage Community
    </Link>
  </Button>
)}
```

**File:** `src/clients/snakk-admin/src/components/space/space-header.tsx`

Add management button:
```tsx
{canManage && (
  <Button asChild variant="ghost" size="sm">
    <Link href={`/c/${communitySlug}/s/${space.slug}/manage`}>
      <Settings className="w-4 h-4" />
    </Link>
  </Button>
)}
```

---

## Database Migrations

### Migration 1: Community Rules Table

```csharp
public partial class AddCommunityRules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CommunityRules",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CommunityId = table.Column<Guid>(nullable: false),
                Title = table.Column<string>(maxLength: 200, nullable: false),
                Description = table.Column<string>(maxLength: 2000, nullable: false),
                Order = table.Column<int>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CommunityRules", x => x.Id);
                table.ForeignKey(
                    name: "FK_CommunityRules_Communities_CommunityId",
                    column: x => x.CommunityId,
                    principalTable: "Communities",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CommunityRules_CommunityId",
            table: "CommunityRules",
            column: "CommunityId");
    }
}
```

### Migration 2: Space Rules Table

```csharp
public partial class AddSpaceRules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SpaceRules",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                SpaceId = table.Column<Guid>(nullable: false),
                Title = table.Column<string>(maxLength: 200, nullable: false),
                Description = table.Column<string>(maxLength: 2000, nullable: false),
                Order = table.Column<int>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SpaceRules", x => x.Id);
                table.ForeignKey(
                    name: "FK_SpaceRules_Spaces_SpaceId",
                    column: x => x.SpaceId,
                    principalTable: "Spaces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SpaceRules_SpaceId",
            table: "SpaceRules",
            column: "SpaceId");
    }
}
```

### Migration 3: Space Moderators Table

```csharp
public partial class AddSpaceModerators : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SpaceModerators",
            columns: table => new
            {
                SpaceId = table.Column<Guid>(nullable: false),
                UserId = table.Column<Guid>(nullable: false),
                AssignedAt = table.Column<DateTime>(nullable: false),
                AssignedBy = table.Column<Guid>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SpaceModerators", x => new { x.SpaceId, x.UserId });
                table.ForeignKey(
                    name: "FK_SpaceModerators_Spaces_SpaceId",
                    column: x => x.SpaceId,
                    principalTable: "Spaces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_SpaceModerators_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SpaceModerators_UserId",
            table: "SpaceModerators",
            column: "UserId");
    }
}
```

### Migration 4: Space Thread Type Configuration

```csharp
public partial class AddSpaceThreadTypeConfig : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "AllowThreads",
            table: "Spaces",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "AllowPolls",
            table: "Spaces",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "AllowEvents",
            table: "Spaces",
            nullable: false,
            defaultValue: false);
    }
}
```

---

## Implementation Phases

### Phase 1: Backend Foundation (Week 1)
1. Database migrations
2. Permission service enhancements
3. DTOs for community and space management
4. Service interfaces

### Phase 2: Community Management Backend (Week 2)
1. CommunityManagementService implementation
2. API endpoints for community management
3. Unit tests
4. Integration tests

### Phase 3: Space Management Backend (Week 2)
1. SpaceManagementService implementation
2. API endpoints for space management
3. Unit tests
4. Integration tests

### Phase 4: Frontend Foundation (Week 3)
1. ManagementLayout component
2. API client functions
3. Route structure
4. Permission checks on frontend

### Phase 5: Community Management UI (Week 3-4)
1. Overview page
2. Settings page
3. Moderation page
4. Spaces management page
5. Members page
6. Webhooks page
7. Rules page
8. Analytics page

### Phase 6: Space Management UI (Week 4)
1. Overview page
2. Settings page
3. Moderation page
4. Rules page
5. Thread types configuration page
6. Analytics page

### Phase 7: Polish & Testing (Week 5)
1. E2E tests
2. UI/UX refinements
3. Loading states and skeletons
4. Error handling
5. Documentation
6. Performance optimization

---

## Security Considerations

### Permission Checks
- **Every API endpoint** must verify the user has permission to manage the community/space
- **Frontend permission checks** to hide UI elements, but always enforce on backend
- **Audit logging** for all management actions

### CORS & CSRF
- Ensure API endpoints are protected against CSRF
- Validate Origin headers for custom domain communities

### Rate Limiting
- Implement rate limiting on management endpoints
- Prevent abuse of moderation actions

---

## Testing Strategy

### Unit Tests
- Permission service methods
- Service layer business logic
- DTO validation

### Integration Tests
- API endpoint authorization
- Database operations
- Service integration

### E2E Tests
- Community owner managing settings
- Space moderator managing space
- Permission denial scenarios
- Navigation flows

---

## Success Metrics

1. **Permission Accuracy**: 100% of management actions properly authorized
2. **Response Time**: Management pages load < 500ms
3. **Error Rate**: < 1% of management actions fail
4. **User Adoption**: Community owners actively use management panels
5. **Audit Coverage**: 100% of management actions logged

---

## Future Enhancements

### Phase 8: Advanced Features
- **Bulk moderation actions**
- **Scheduled posts and announcements**
- **Advanced analytics dashboards**
- **Role templates and presets**
- **Community exports and backups**
- **Multi-community operations** (for users managing multiple communities)

### Phase 9: Mobile Support
- Responsive design for mobile management
- Progressive Web App (PWA) features
- Mobile-optimized workflows

---

## Open Questions

1. **Custom domain verification:** How should communities verify domain ownership?
2. **Community transfer:** Should we allow transferring ownership? What's the workflow?
3. **Space archival:** Should archived spaces be visible or completely hidden?
4. **Analytics scope:** What metrics are most important for community/space managers?
5. **Webhook event types:** Which events should be available for community-level webhooks?
6. **Role hierarchy:** Can space mods override community rules, or are community rules absolute?

---

## Dependencies

### Backend
- Existing permission system
- Audit logging system
- Webhook infrastructure (from admin panel)
- Community and space database entities

### Frontend
- shadcn/ui components
- TanStack Query for data fetching
- Next.js 14 App Router
- React Hook Form for forms

---

## Documentation Needed

1. **For Community Owners:**
   - How to manage community settings
   - How to assign moderators
   - How to create and configure spaces
   - Best practices for community governance

2. **For Space Moderators:**
   - How to manage space settings
   - How to configure thread types
   - Moderation guidelines

3. **For Developers:**
   - API documentation
   - Permission system documentation
   - How to add new management features

---

## Conclusion

This hierarchical management system provides:
- **Clear separation** between platform admins, community owners, and space moderators
- **Scalable architecture** that can grow with the platform
- **Consistent UX** across all management levels
- **Robust permissions** to prevent unauthorized access
- **Audit trail** for accountability

The phased implementation allows for incremental delivery and testing, ensuring each level of management works correctly before moving to the next.
