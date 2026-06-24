// Helper: authenticate a k6 VU against Snakk.Auth's Razor Pages login form.
//
// Flow:
//   1. GET /auth/login — receives the page and sets a Razor antiforgery cookie
//      (.AspNetCore.Antiforgery.*) plus a hidden __RequestVerificationToken
//      input that must be echoed in the POST body.
//   2. POST /auth/login with form fields Input.Email + Input.Password +
//      __RequestVerificationToken. 200 on render-form-with-error, 302 to
//      "/" on success.
//   3. On 302, three auth cookies are set (.Snakk.Auth, .Snakk.Auth.Session,
//      .Snakk.Auth.Refresh). They live in the k6 cookie jar and get sent
//      automatically on subsequent requests for the same origin.
//
// Returns true on success, false otherwise. Logs to stderr if the form layout
// changed (token regex no longer matches) — k6's check() also surfaces it.

import http from 'k6/http';
import { check } from 'k6';
import { AUTH_BASE_URL, httpOptions } from './config.js';

const TOKEN_RE = /name="__RequestVerificationToken"[^>]*value="([^"]+)"/;

// Per-VU login cache. Module-level state is isolated per VU in k6, so this
// flag tracks "this VU has successfully logged in once". The gateway
// rate-limits /auth/login POST at 20/5min per source IP — without caching,
// VUs that re-login each iteration burn the quota in seconds and the rest
// of the run gets 429ed.
let alreadyLoggedIn = false;

// Clear k6's per-VU cookie jar so a re-login isn't short-circuited by the
// Login page detecting an existing valid `.Snakk.Auth` cookie and redirecting
// to "/" (which would mean no form, no antiforgery token, POST fails).
function clearAuthCookies() {
    const jar = http.cookieJar();
    const opts = { path: '/', expires: 'Thu, 01 Jan 1970 00:00:00 GMT' };
    jar.set(AUTH_BASE_URL, '.Snakk.Auth',         'expired', opts);
    jar.set(AUTH_BASE_URL, '.Snakk.Auth.Session', 'expired', opts);
    jar.set(AUTH_BASE_URL, '.Snakk.Auth.Refresh', 'expired', opts);
    jar.set(AUTH_BASE_URL, '.Snakk.Pref.RememberMe', 'expired', opts);
}

// Cached login — re-uses the existing session for the lifetime of the VU
// to avoid hammering the rate limiter. Call `forceRelogin()` if a test
// needs to deliberately exercise the login flow more than once.
export function login(user) {
    if (alreadyLoggedIn) return true;
    return doLogin(user);
}

export function forceRelogin(user) {
    alreadyLoggedIn = false;
    return doLogin(user);
}

function doLogin(user) {
    clearAuthCookies();

    const loginPage = http.get(`${AUTH_BASE_URL}/login`, {
        ...httpOptions, tags: { name: 'auth-login-get' }
    });
    const ok1 = check(loginPage, {
        'auth: GET /auth/login is 200': r => r.status === 200,
        'auth: login form contains antiforgery token': r => TOKEN_RE.test(r.body),
    });
    if (!ok1) return false;

    const token = TOKEN_RE.exec(loginPage.body)[1];

    const submission = http.post(
        `${AUTH_BASE_URL}/login`,
        {
            'Input.Email': user.email,
            'Input.Password': user.password,
            '__RequestVerificationToken': token,
        },
        { ...httpOptions, redirects: 0, tags: { name: 'auth-login-post' } }
    );

    // 302 = success (redirect to returnUrl). 200 = form re-rendered with error.
    const ok2 = check(submission, {
        'auth: login submission returned 302': r => r.status === 302,
    });
    if (ok2) alreadyLoggedIn = true;
    return ok2;
}
