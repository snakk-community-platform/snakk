# Automatic Token Refresh - Snakk.Web

## Overview

Snakk.Web now has automatic token refresh functionality, ported from Admin's auth-check system. This ensures users stay logged in seamlessly without interruption.

## How It Works

### **Architecture**

```
Timer (5 min) → Check Expiry → Call SnakkAuth.refreshTokens() → BFF /bff/auth/refresh → API → New Tokens
```

### **Configuration**

- **Check Interval**: Every **5 minutes**
- **Initial Delay**: **1 minute** after page load
- **Refresh Threshold**: Refresh if less than **5 minutes** remaining

### **Smart Features**

1. **Proactive Refresh**: Refreshes BEFORE expiration, not after
2. **Page Visibility API**: Checks immediately when tab becomes visible
3. **Prevents Duplicate Refreshes**: Skips check if already refreshing
4. **Auto-Start**: Only starts if user has a token
5. **Auto-Stop**: Stops on logout
6. **Event-Driven**: Dispatches custom events for monitoring

## Events

The system dispatches these custom events:

| Event | When | Detail |
|-------|------|--------|
| `snakk:auth:token-ready` | System initialized | - |
| `snakk:auth:token-started` | Timer started | - |
| `snakk:auth:token-valid` | Token checked, still valid | - |
| `snakk:auth:token-refreshed` | Token successfully refreshed | - |
| `snakk:auth:token-refresh-failed` | Refresh failed | - |
| `snakk:auth:token-error` | Error during check | `{ error }` |
| `snakk:auth:token-stopped` | Timer stopped | - |

### **Usage Example**

```javascript
// Listen for successful refresh
document.addEventListener('snakk:auth:token-refreshed', () => {
    console.log('Token refreshed! User stays logged in.');
});

// Listen for failures
document.addEventListener('snakk:auth:token-refresh-failed', () => {
    console.log('Refresh failed - user will be redirected to login');
});

// Manual control
window.SnakkTokenRefresh.checkAndRefreshToken(); // Force check now
window.SnakkTokenRefresh.stopTokenRefresh();     // Stop timer
window.SnakkTokenRefresh.startTokenRefresh();    // Restart timer
```

## Flow Example

**User logs in and leaves browser tab open for 4 hours:**

```
T+0:00    User logs in
          ├─ Token stored in localStorage (expires in 8h)
          └─ Token refresh timer starts

T+1:00    First check (token valid for 7h, nothing happens)

T+6:00    Check runs (token valid for 2h, still above 5min threshold)

T+7:56    Check runs
          ├─ Token expires in 4 minutes (< 5 min threshold!)
          ├─ Calls window.SnakkAuth.refreshTokens()
          ├─ BFF calls API /auth/refresh
          ├─ New tokens stored in localStorage
          └─ Event: snakk:auth:token-refreshed

T+8:00    User interacts with page
          ├─ Uses NEW token (refreshed at T+7:56)
          └─ Everything works seamlessly!
```

## Files

| File | Purpose |
|------|---------|
| `Scripts/core/token-refresh.ts` | TypeScript source |
| `wwwroot/js/dist/core/token-refresh.js` | Compiled JavaScript |
| `Pages/Shared/_Layout.cshtml` | Loads script after auth.js |
| `Endpoints/BffApiEndpoints.cs` | BFF refresh endpoint |
| `Scripts/core/auth.ts` | Provides refreshTokens() function |

## Comparison with Admin

| Feature | Admin | Snakk.Web |
|---------|----------|-----------|
| **Storage** | HttpOnly cookies | localStorage |
| **Language** | Vanilla JS | TypeScript → JS |
| **Architecture** | Razor Pages → SDK → API | BFF → API |
| **Token Access** | Server-side only | JavaScript accessible |
| **Events** | `admin:session:*` | `snakk:auth:token-*` |
| **Implementation** | auth-check.js | token-refresh.ts |

## Security Notes

⚠️ **Important Differences from Admin:**

- **Admin**: Tokens in HttpOnly cookies (JavaScript can't access)
- **Snakk.Web**: Tokens in localStorage (JavaScript can access)

While localStorage is less secure than HttpOnly cookies, it's necessary for Snakk.Web's client-side rendering architecture. The BFF pattern provides some security by:
- API is firewalled (only accessible to ASP.NET apps)
- JavaScript only calls `/bff/*` endpoints
- BFF validates and sanitizes requests

## Maintenance

**When modifying:**
1. Edit `Scripts/core/token-refresh.ts` (NOT the .js file)
2. Run `npm run build:ts` to compile
3. Test in browser DevTools console

**To disable:**
```javascript
window.SnakkTokenRefresh.stopTokenRefresh();
```

**To restart:**
```javascript
window.SnakkTokenRefresh.startTokenRefresh();
```
