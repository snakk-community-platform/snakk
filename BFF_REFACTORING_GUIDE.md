# BFF Pattern Refactoring Guide

This guide shows how to refactor JavaScript files to use the Backend-for-Frontend (BFF) pattern instead of calling the internal API directly.

## 📋 Refactoring Checklist

### Files to Refactor (Priority Order):

- [ ] 1. **services/realtime.js** - SignalR connection (CRITICAL - affects all real-time features)
- [ ] 2. **components/auth-navbar.js** - Auth status and logout (HIGH - affects all pages)
- [ ] 3. **pages/profile.js** - User stats and activity (9 API calls)
- [ ] 4. **pages/discussion-detail.js** - Posts, moderation, avatars (7 API calls)
- [ ] 5. **components/site.js** - Entity popup stats (5 API calls)
- [ ] 6. **pages/frontpage-discussions.js** - Discussion previews and avatars (2 API calls)
- [ ] 7. **pages/frontpage.js** - Discussion previews (1 API call)
- [ ] 8. **pages/space-detail.js** - Discussion previews (1 API call)

### Steps for Each File:

1. Identify all direct API calls
2. Create corresponding BFF endpoints in ASP.NET
3. Update JavaScript to call `/bff/*` instead
4. Remove `apiBaseUrl` or `snakkApiBaseUrl` references
5. Test functionality
6. Verify no direct API calls remain

---

## 🔧 Refactoring Pattern

### JavaScript Side (BEFORE):

```javascript
// ❌ Direct API call
const apiBaseUrl = window.snakkApiBaseUrl || 'https://localhost:7291';
const response = await fetch(`${apiBaseUrl}/api/users/${userId}/stats`);
const data = await response.json();
```

### JavaScript Side (AFTER):

```javascript
// ✅ BFF call
const response = await fetch(`/bff/users/${userId}/stats`);
const data = await response.json();
```

### ASP.NET BFF Endpoint:

```csharp
// In Snakk.Web/Program.cs or dedicated BFF endpoints file

app.MapGet("/bff/users/{userId}/stats", async (
    string userId,
    IHttpClientFactory httpClientFactory,
    HttpContext httpContext) =>
{
    // 1. Validate request
    if (string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest("User ID is required");

    // 2. Get JWT from request (already attached by auth.js interceptor)
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    // 3. Forward to internal API
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    var response = await httpClient.GetAsync($"/api/users/{userId}/stats");

    // 4. Return response
    if (!response.IsSuccessStatusCode)
        return Results.StatusCode((int)response.StatusCode);

    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
})
.RequireAuthorization(); // Optional: require authentication
```

### HttpClient Setup:

```csharp
// In Program.cs

builder.Services.AddHttpClient("InternalApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["InternalApiUrl"] ?? "https://localhost:7291");
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

---

## 📝 File-by-File Refactoring Instructions

### 1. services/realtime.js (CRITICAL)

**Lines to Change:**
- Line 5: Remove `const apiBaseUrl = window.snakkApiBaseUrl || 'https://localhost:7291';`
- Line 9: Change `.withUrl(\`${apiBaseUrl}/realtime\`)` to `.withUrl('/bff/realtime')`

**ASP.NET BFF Endpoint:**
```csharp
// SignalR requires special mapping
app.MapHub<RealTimeHub>("/bff/realtime");

// Move RealTimeHub from Api project to Snakk.Web
// Or configure SignalR to proxy to internal API hub
```

**Alternative (Proxy Pattern):**
```csharp
// If hub stays in API, create a proxy hub in Snakk.Web
public class BffRealTimeHub : Hub
{
    private readonly IHubContext<RealTimeHub> _apiHub;

    public BffRealTimeHub(IHubContext<RealTimeHub> apiHub)
    {
        _apiHub = apiHub;
    }

    // Proxy methods to internal API hub
    public async Task Subscribe(string channel)
    {
        await _apiHub.Clients.Group(channel).SendAsync("subscribed", channel);
    }
}
```

---

### 2. components/auth-navbar.js (HIGH)

**Lines to Change:**
- Line 16: Change `fetch(\`${window.apiBaseUrl}/auth/status\`)` to `fetch('/bff/auth/status')`
- Line 86: Change `src="${window.apiBaseUrl}/avatars/${user.publicId}"` to `src="/bff/avatars/${user.publicId}"`
- Line 194: Change `fetch(\`${window.apiBaseUrl}/auth/logout\`)` to `fetch('/bff/auth/logout')`

**ASP.NET BFF Endpoints:**
```csharp
// Auth status
app.MapGet("/bff/auth/status", async (HttpContext httpContext, IHttpClientFactory httpClientFactory) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    if (string.IsNullOrWhiteSpace(token))
        return Results.Unauthorized();

    var httpClient = httpClientFactory.CreateClient("InternalApi");
    httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    var response = await httpClient.GetAsync("/auth/status");
    var content = await response.Content.ReadAsStringAsync();

    return Results.Content(content, "application/json");
});

// Logout
app.MapPost("/bff/auth/logout", async (HttpContext httpContext, IHttpClientFactory httpClientFactory) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    var httpClient = httpClientFactory.CreateClient("InternalApi");
    httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    var response = await httpClient.PostAsync("/auth/logout", null);

    return response.IsSuccessStatusCode ? Results.Ok() : Results.StatusCode((int)response.StatusCode);
});

// Avatar proxy
app.MapGet("/bff/avatars/{userId}", async (string userId, IHttpClientFactory httpClientFactory) =>
{
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    var response = await httpClient.GetAsync($"/avatars/{userId}");

    var bytes = await response.Content.ReadAsByteArrayAsync();
    var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/png";

    return Results.File(bytes, contentType);
});
```

---

### 3. pages/profile.js (9 API calls)

**Lines to Change:**
- Line 36: `/api/users/${userId}/stats` → `/bff/users/${userId}/stats`
- Line 63: `/api/search/discussions` → `/bff/search/discussions`
- Line 96: `/api/search/posts` → `/bff/search/posts`
- Line 131: `/api/search/discussions` → `/bff/search/discussions`
- Line 171: `/api/search/posts` → `/bff/search/posts`
- Line 225: `/api/users/${userId}/activity-history` → `/bff/users/${userId}/activity-history`
- Line 330: `/api/search/discussions` → `/bff/search/discussions`
- Line 370: `/api/auth/status` → `/bff/auth/status`
- Line 392: `/api/users/${userId}/follow-status` → `/bff/users/${userId}/follow-status`
- Line 424: `/api/users/${targetUserId}/follow` → `/bff/users/${targetUserId}/follow`

**Remove**: All `window.apiBaseUrl` references

**ASP.NET BFF Endpoints:**
```csharp
// User stats
app.MapGet("/bff/users/{userId}/stats", async (string userId, IHttpClientFactory httpClientFactory, HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    var response = await httpClient.GetAsync($"/api/users/{userId}/stats");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});

// Search discussions
app.MapGet("/bff/search/discussions", async (
    [FromQuery] string? authorPublicId,
    [FromQuery] int? pageSize,
    IHttpClientFactory httpClientFactory,
    HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    var queryParams = new List<string>();
    if (authorPublicId != null) queryParams.Add($"authorPublicId={authorPublicId}");
    if (pageSize != null) queryParams.Add($"pageSize={pageSize}");
    var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";

    var response = await httpClient.GetAsync($"/api/search/discussions{queryString}");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});

// Search posts
app.MapGet("/bff/search/posts", async (
    [FromQuery] string? authorPublicId,
    [FromQuery] int? pageSize,
    IHttpClientFactory httpClientFactory,
    HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    var queryParams = new List<string>();
    if (authorPublicId != null) queryParams.Add($"authorPublicId={authorPublicId}");
    if (pageSize != null) queryParams.Add($"pageSize={pageSize}");
    var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";

    var response = await httpClient.GetAsync($"/api/search/posts{queryString}");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});

// Activity history
app.MapGet("/bff/users/{userId}/activity-history", async (
    string userId,
    [FromQuery] int days,
    IHttpClientFactory httpClientFactory,
    HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    var response = await httpClient.GetAsync($"/api/users/{userId}/activity-history?days={days}");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});

// Follow status
app.MapGet("/bff/users/{userId}/follow-status", async (
    string userId,
    [FromQuery] string currentUserId,
    IHttpClientFactory httpClientFactory,
    HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    var response = await httpClient.GetAsync($"/api/users/{userId}/follow-status?currentUserId={currentUserId}");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});

// Follow user
app.MapPost("/bff/users/{userId}/follow", async (
    string userId,
    IHttpClientFactory httpClientFactory,
    HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    var response = await httpClient.PostAsync($"/api/users/{userId}/follow", null);
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
}).RequireAuthorization();
```

---

### 4. pages/discussion-detail.js (7 API calls)

**Lines to Change:**
- Line 262: `/api/posts/${postId}/edit` → `/bff/posts/${postId}/edit`
- Line 887: `/api/discussions/${discussionId}/posts` → `/bff/discussions/${discussionId}/posts`
- Line 1039: Avatar URL → `/bff/avatars/${post.author.publicId}`
- Line 1094: `/api/moderation/reports/reasons` → `/bff/moderation/reports/reasons`

**Remove**: All `apiBaseUrl` constants (lines 421, 455, 503, 580, 883, 1008, 1091)

**ASP.NET BFF Endpoints:**
```csharp
// Edit post
app.MapPost("/bff/posts/{postId}/edit", async (
    string postId,
    [FromQuery] string userId,
    [FromQuery] string content,
    IHttpClientFactory httpClientFactory,
    HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    var response = await httpClient.PostAsync(
        $"/api/posts/{postId}/edit?userId={userId}&content={Uri.EscapeDataString(content)}", null);
    var result = await response.Content.ReadAsStringAsync();
    return Results.Content(result, "application/json");
}).RequireAuthorization();

// Get discussion posts
app.MapGet("/bff/discussions/{discussionId}/posts", async (
    string discussionId,
    [FromQuery] int offset,
    [FromQuery] int pageSize,
    IHttpClientFactory httpClientFactory,
    HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    var response = await httpClient.GetAsync(
        $"/api/discussions/{discussionId}/posts?offset={offset}&pageSize={pageSize}");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});

// Moderation report reasons
app.MapGet("/bff/moderation/reports/reasons", async (
    [FromQuery] string? reportType,
    IHttpClientFactory httpClientFactory,
    HttpContext httpContext) =>
{
    var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    var queryString = reportType != null ? $"?reportType={reportType}" : "";
    var response = await httpClient.GetAsync($"/api/moderation/reports/reasons{queryString}");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});
```

---

### 5. components/site.js (5 API calls)

**Lines to Change:**
- Line 12: Remove `this.apiBaseUrl` property
- Line 29: Remove `getApiBase()` method or simplify to return nothing
- Lines 121-133: Change all `/api/*` to `/bff/*`

**ASP.NET BFF Endpoints:**
```csharp
// Hub stats
app.MapGet("/bff/hubs/{publicId}/stats", async (string publicId, IHttpClientFactory httpClientFactory) =>
{
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    var response = await httpClient.GetAsync($"/api/hubs/{publicId}/stats");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});

// Space stats
app.MapGet("/bff/spaces/{publicId}/stats", async (string publicId, IHttpClientFactory httpClientFactory) =>
{
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    var response = await httpClient.GetAsync($"/api/spaces/{publicId}/stats");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});

// Community stats
app.MapGet("/bff/communities/{publicId}/stats", async (string publicId, IHttpClientFactory httpClientFactory) =>
{
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    var response = await httpClient.GetAsync($"/api/communities/{publicId}/stats");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});

// User stats
app.MapGet("/bff/users/{publicId}/stats", async (string publicId, IHttpClientFactory httpClientFactory) =>
{
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    var response = await httpClient.GetAsync($"/api/users/{publicId}/stats");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});

// Discussion stats
app.MapGet("/bff/discussions/{publicId}/stats", async (string publicId, IHttpClientFactory httpClientFactory) =>
{
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    var response = await httpClient.GetAsync($"/api/discussions/{publicId}/stats");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});
```

---

### 6. pages/frontpage-discussions.js (2 API calls)

**Lines to Change:**
- Line 98: `/discussions/${discussionId}/preview` → `/bff/discussions/${discussionId}/preview`
- Line 183: `/avatars/space/${discussion.space.publicId}.svg` → `/bff/avatars/space/${discussion.space.publicId}.svg`

**Remove**: `config.apiBaseUrl` references

**ASP.NET BFF Endpoints:**
```csharp
// Discussion preview
app.MapGet("/bff/discussions/{discussionId}/preview", async (
    string discussionId,
    IHttpClientFactory httpClientFactory) =>
{
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    var response = await httpClient.GetAsync($"/discussions/{discussionId}/preview");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});

// Space avatar
app.MapGet("/bff/avatars/space/{publicId}.svg", async (
    string publicId,
    IHttpClientFactory httpClientFactory) =>
{
    var httpClient = httpClientFactory.CreateClient("InternalApi");
    var response = await httpClient.GetAsync($"/avatars/space/{publicId}.svg");
    var bytes = await response.Content.ReadAsByteArrayAsync();
    return Results.File(bytes, "image/svg+xml");
});
```

---

### 7. pages/frontpage.js (1 API call)

**Lines to Change:**
- Line 88: Remove `const apiBaseUrl = window.SnakkConfig?.apiBaseUrl || '';`
- Line 110: `/discussions/${discussionId}/preview` → `/bff/discussions/${discussionId}/preview`

**ASP.NET BFF Endpoint:** (Same as frontpage-discussions.js above)

---

### 8. pages/space-detail.js (1 API call)

**Lines to Change:**
- Line 415: `/discussions/${discussionId}/preview` → `/bff/discussions/${discussionId}/preview`

**Remove**: `config.apiBaseUrl` reference

**ASP.NET BFF Endpoint:** (Same as frontpage-discussions.js above)

---

## 🚀 Implementation Strategy

### Phase 1: Setup (Do First)
1. Add HttpClient configuration in Program.cs
2. Create BFF endpoints folder/file structure
3. Test one simple endpoint (e.g., `/bff/auth/status`)

### Phase 2: Critical Path (High Priority)
1. Refactor **realtime.js** (SignalR connection)
2. Refactor **auth-navbar.js** (authentication)
3. Test thoroughly - these affect all pages

### Phase 3: User-Facing Features
1. Refactor **profile.js** (most API calls)
2. Refactor **discussion-detail.js**
3. Test user journeys

### Phase 4: Supporting Features
1. Refactor **site.js** (hover popups)
2. Refactor **frontpage-discussions.js**
3. Refactor **frontpage.js**
4. Refactor **space-detail.js**

### Phase 5: Cleanup
1. Remove all `apiBaseUrl` from _Layout.cshtml
2. Remove `window.snakkApiBaseUrl` global
3. Search codebase for any remaining `/api/` or `apiBaseUrl` references
4. Add CSP headers to block direct API access

---

## ✅ Testing Checklist

After refactoring each file:

- [ ] No console errors in browser
- [ ] Features work as expected
- [ ] No network requests to internal API in DevTools
- [ ] JWT tokens still attached to requests
- [ ] CSRF tokens included for POST/PUT/DELETE
- [ ] Error handling works correctly
- [ ] Loading states display properly

---

## 🔍 Verification Script

Run this in browser console to detect direct API calls:

```javascript
// Override fetch to detect direct API calls
const originalFetch = window.fetch;
window.fetch = function(...args) {
    const url = typeof args[0] === 'string' ? args[0] : args[0].url;
    if (url.includes('/api/') && !url.includes('/bff/')) {
        console.error('❌ DIRECT API CALL DETECTED:', url);
        debugger; // Pause to see stack trace
    }
    return originalFetch(...args);
};

// Also monitor XHR
const originalOpen = XMLHttpRequest.prototype.open;
XMLHttpRequest.prototype.open = function(method, url, ...rest) {
    if (url.includes('/api/') && !url.includes('/bff/')) {
        console.error('❌ DIRECT API CALL DETECTED (XHR):', url);
        debugger;
    }
    return originalOpen.call(this, method, url, ...rest);
};

console.log('✅ API call monitoring enabled. Direct API calls will trigger errors.');
```

---

## 📚 Additional Resources

- [CLAUDE.md](CLAUDE.md) - Enforcement rules for future development
- [src/clients/Snakk.Web/wwwroot/js/SECURITY.md](src/clients/Snakk.Web/wwwroot/js/SECURITY.md) - Security patterns including BFF
- [src/clients/Snakk.Web/wwwroot/js/README.md](src/clients/Snakk.Web/wwwroot/js/README.md) - JavaScript organization

---

## 🎯 Success Criteria

Refactoring is complete when:

✅ All JavaScript files call `/bff/*` instead of `/api/*`
✅ No `apiBaseUrl` or `snakkApiBaseUrl` references in JavaScript
✅ No direct fetch calls to internal API URLs
✅ All BFF endpoints created and tested
✅ HttpClient configured for internal API communication
✅ CSP headers block direct API access (optional but recommended)
✅ All features work as before
✅ No console errors
✅ Verification script passes

---

**Next Step**: Start with Phase 1 (Setup), then work through each file systematically.
