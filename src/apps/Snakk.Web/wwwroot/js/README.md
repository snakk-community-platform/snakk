# JavaScript Organization

This directory contains all JavaScript files for the Snakk web application, organized into logical subdirectories.

## 📁 Directory Structure

```
js/
├── vendor/              → Third-party libraries (minified)
├── core/                → Core utilities used across the app
├── components/          → Reusable UI components
├── pages/               → Page-specific scripts
└── services/            → Background services & managers
```

## 📦 Vendor (Third-party Libraries)

**Location:** `vendor/`

Third-party JavaScript libraries that are **not** maintained by the Snakk team.

| File | Description | Version | Source |
|------|-------------|---------|--------|
| `htmx.min.js` | HTMX - HTML-over-the-wire | 1.9.x | https://htmx.org |
| `signalr.min.js` | SignalR Client - Real-time messaging | 7.x | https://docs.microsoft.com/signalr |

**When to add files here:**
- ✅ External libraries (jQuery, lodash, etc.)
- ✅ CDN downloads saved locally
- ✅ Minified third-party code

**When NOT to add files here:**
- ❌ Custom Snakk code
- ❌ Modified third-party code
- ❌ Internal utilities

---

## 🛠️ Core (Application Foundation)

**Location:** `core/`

Foundational utilities and services that are used throughout the entire application.

| File | Type | Description |
|------|------|-------------|
| `utils.js` | IIFE | Utility functions (escapeHtml, formatRelativeTime, etc.) |
| `auth.js` | IIFE | JWT authentication & token management |
| `theme.js` | IIFE | Dark/light theme switching |

**Characteristics:**
- ✅ Used across multiple pages
- ✅ No dependencies on other custom scripts
- ✅ Stateless utilities (mostly)
- ✅ Load early in page lifecycle

---

## 🧩 Components (Reusable UI)

**Location:** `components/`

Self-contained UI components that can be reused across different pages.

| File | Type | Description |
|------|------|-------------|
| `site.js` | ES6 Class | Hover popups for hubs/spaces/users (SnakkPopup) |
| `auth-navbar.js` | IIFE | Authentication navbar component |
| `search-focus.js` | IIFE | Search focus pane functionality |
| `sidebar-scrollbar.js` | IIFE | Sidebar scrollbar detection |

**Characteristics:**
- ✅ Self-contained functionality
- ✅ Can be used on multiple pages
- ✅ Manages its own state
- ✅ Often have init() methods

---

## 📄 Pages (Page-specific Logic)

**Location:** `pages/`

Scripts that are specific to individual pages or page types. Only loaded on the pages that need them.

| File | Type | Page | Description |
|------|------|------|-------------|
| `frontpage.js` | IIFE | `/` | Homepage discussions list |
| `frontpage-discussions.js` | ES6 Class | `/` | Endless scroll for discussions |
| `discussion-detail.js` | IIFE | `/discussions/{id}` | Discussion page with posts |
| `profile.js` | IIFE | `/users/{username}` | User profile page |
| `space-detail.js` | IIFE | `/spaces/{slug}` | Space detail page |
| `search-page.js` | IIFE | `/search` | Search results page |

**Characteristics:**
- ✅ Loaded only on specific pages
- ✅ Page-level initialization
- ✅ May depend on core utilities
- ✅ Can use services

---

## ⚙️ Services (Background Services)

**Location:** `services/`

Background services, managers, and batchers that run across the application lifecycle.

| File | Type | Description |
|------|------|-------------|
| `draft-manager.js` | ES6 Class | Auto-saves post/reply drafts (DraftManager) |
| `read-state-batcher.js` | ES6 Class | Batches read state updates (ReadStateBatcher) |
| `realtime.js` | IIFE | SignalR real-time notifications |
| `cache-manager.js` | IIFE | Cache management for discussions/posts |
| `follow-cache.js` | IIFE | Follow state caching |
| `read-history.js` | IIFE | Read history tracking |
| `search-history.js` | IIFE | Search history management |

**Characteristics:**
- ✅ Run in the background
- ✅ Manage application state
- ✅ Often use timers/intervals
- ✅ Have lifecycle methods (init, destroy, shutdown)

---

## 🏗️ Architecture Patterns

### IIFE Pattern (Immediately Invoked Function Expression)

**Used for:** Stateless utilities, simple UI components

```javascript
(function() {
    'use strict';

    function myFunction() {
        // implementation
    }

    // Export API
    window.MyModule = {
        myFunction
    };
})();
```

**When to use:**
- ✅ Simple, stateless logic
- ✅ Single responsibility
- ✅ Fire-and-forget setup
- ✅ No configuration needed

### ES6 Class Pattern

**Used for:** Stateful components, services with lifecycle

```javascript
class MyComponent {
    constructor(options = {}) {
        this.state = {};
    }

    init() {
        // initialization
    }

    destroy() {
        // cleanup
    }
}

// Export class and singleton
window.MyComponent = MyComponent;
window.MyComponentInstance = new MyComponent();
```

**When to use:**
- ✅ Stateful components
- ✅ Complex lifecycle (init, destroy)
- ✅ Needs configuration
- ✅ Multiple instances possible

---

## 🔗 Loading Order

Scripts should be loaded in this order to ensure dependencies are available:

1. **Vendor libraries** (htmx, signalr)
2. **Core utilities** (utils, auth, theme)
3. **Components** (site, auth-navbar, etc.)
4. **Services** (draft-manager, realtime, etc.)
5. **Page scripts** (discussion-detail, profile, etc.)

---

## ✨ Code Quality Standards

### Security
- ✅ Always use `escapeHtml()` for user-generated content
- ✅ Use `textContent` for plain text
- ✅ Use `<template>` elements for complex HTML
- ❌ Never use `innerHTML` with unsanitized user input

### Performance
- ✅ Use event delegation for dynamic content
- ✅ Debounce/throttle expensive operations
- ✅ Use `DocumentFragment` for batch DOM updates
- ✅ Use `requestAnimationFrame` for animations

### Maintainability
- ✅ Use ES6+ features (const, let, arrow functions, classes)
- ✅ Add JSDoc comments for functions
- ✅ Use meaningful variable names
- ✅ Keep files focused and single-purpose

---

## 📝 Adding New Files

When adding a new JavaScript file, follow this decision tree:

```
Is it third-party code?
├─ YES → Add to vendor/
└─ NO → Is it used across multiple pages?
    ├─ YES → Is it a UI component?
    │   ├─ YES → Add to components/
    │   └─ NO → Add to core/
    └─ NO → Is it a background service?
        ├─ YES → Add to services/
        └─ NO → Add to pages/
```

---

## 🔄 Migration Complete

All JavaScript files have been organized as of **February 2026**:
- ✅ Third-party libraries moved to `vendor/`
- ✅ Core utilities moved to `core/`
- ✅ UI components moved to `components/`
- ✅ Page scripts moved to `pages/`
- ✅ Services moved to `services/`
- ✅ All `.cshtml` references updated

---

## 📚 Additional Resources

- [HTMX Documentation](https://htmx.org/docs/)
- [SignalR Documentation](https://docs.microsoft.com/en-us/aspnet/core/signalr/)
- [MDN JavaScript Guide](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide)
