# BFF (Backend-for-Frontend) DTOs

## Purpose

BFF DTOs decouple the frontend JavaScript from internal API DTOs. This provides:

- **Stability**: API can evolve without breaking frontend
- **Flexibility**: BFF can reshape/aggregate data for UI needs
- **Security**: Filter out sensitive internal fields
- **Simplification**: Frontend-optimized data structures

## Pattern

### 1. Create BFF DTO (in `Models/Bff/`)

```csharp
namespace Snakk.Web.Models.Bff;

/// <summary>
/// BFF response for [feature] - decouples frontend from API structure
/// </summary>
public record BffSomeResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    // ... frontend-needed fields only
}
```

### 2. Map in BFF Endpoint (in `Endpoints/BffApiEndpoints.cs`)

```csharp
private static async Task<IResult> GetSomethingAsync(
    string id,
    SnakkApiClient apiClient)
{
    // Call API
    var apiResult = await apiClient.GetSomethingAsync(id);
    if (apiResult == null) return Results.NotFound();

    // Map API DTO → BFF DTO
    var bffResponse = new Models.Bff.BffSomeResponse
    {
        Id = apiResult.PublicId,
        Name = apiResult.Name
        // Map only what frontend needs
    };

    return Results.Ok(bffResponse);
}
```

### 3. Frontend Stays the Same

TypeScript/JavaScript continues to call `/bff/something` and gets the BFF DTO structure.
No changes needed unless you rename/restructure fields for better frontend DX.

## Benefits Examples

### Field Renaming
```csharp
// API has "PublicId", frontend prefers "id"
Id = apiResult.PublicId
```

### Field Filtering
```csharp
// API returns internal fields, BFF only exposes what UI needs
// (Don't include CreatedBy, InternalNotes, etc.)
```

### Aggregation
```csharp
// BFF can combine multiple API calls
var user = await apiClient.GetUserAsync(id);
var stats = await apiClient.GetUserStatsAsync(id);

return new BffUserProfileResponse
{
    // User fields
    Name = user.Name,
    // Stats fields
    PostCount = stats.PostCount
};
```

## Current Status

⚠️ **Partial Implementation**: Only auth endpoint uses proper BFF DTOs currently.

**Blocker**: Most API endpoints return anonymous objects (no `.Produces<T>()` metadata), so the SDK generates `Task` (void) methods instead of `Task<TDto>`. This prevents proper BFF mapping until API endpoints are fixed.

## Implementation Status

✅ **Fully Implemented (API + BFF + SDK):**
- Auth status (`BffAuthStatusResponse`)
  - API uses `TypedResults.Ok<AuthStatusResponse>`
  - SDK has typed `GetAuthStatusAsync() : Task<AuthStatusResponse>`
  - BFF maps to `BffAuthStatusResponse`

📦 **BFF DTOs Created (Pending API Fixes):**
- Notifications (`BffNotificationResponse`, etc.)
- User stats (`BffUserStatsResponse`)
- Entity stats (`BffHubStatsResponse`, `BffSpaceStatsResponse`, `BffCommunityStatsResponse`)

⏸️ **Blocked Until API Uses TypedResults:**
- All other endpoints currently return anonymous objects

## Migration Checklist

For each endpoint:
1. [ ] Create BFF DTO in `Models/Bff/`
2. [ ] Update endpoint in `BffApiEndpoints.cs` to map API → BFF DTO
3. [ ] Test that frontend still works (same field names = no JS changes needed)
4. [ ] (Optional) Rename fields for better frontend DX and update TypeScript interfaces

## Future Enhancements

- **AutoMapper**: Consider using AutoMapper if mapping becomes repetitive
- **Validation**: Add BFF-level validation before calling API
- **Caching**: Cache BFF responses independently of API caching
- **Rate Limiting**: Apply BFF-specific rate limits
