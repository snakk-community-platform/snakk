# Client-Side Caching & Optimization Guide

This document describes the client-side caching and optimization features implemented in Snakk.

## Overview

Four main optimization systems have been implemented:

1. **Follow Status Cache** - Reduces API calls for follow status checks
2. **Draft Auto-Save** - Prevents data loss for reply/post content
3. **Search History** - Tracks searches for suggestions and analytics
4. **Read State Batcher** - Batches read state updates to reduce API load

## 1. Follow Status Cache

**File:** `wwwroot/js/follow-cache.js`
**Storage:** localStorage with 5-minute TTL
**Purpose:** Cache followed spaces, discussions, and users to avoid repeated API calls

### Usage

```javascript
// Check if a discussion is followed (returns cached value or null)
const isFollowing = SnakkFollowCache.isDiscussionFollowed(discussionId);
if (isFollowing !== null) {
    // Use cached value
    updateUI(isFollowing);
} else {
    // Cache miss, fetch from API
    const result = await fetchFollowStatus(discussionId);
    SnakkFollowCache.setDiscussionFollowed(discussionId, result);
}

// After toggling follow status (mutation)
await toggleFollowAPI(discussionId);
SnakkFollowCache.setDiscussionFollowed(discussionId, newState);

// Sync all follows from API (on login or when stale)
if (SnakkFollowCache.isSyncStale()) {
    await SnakkFollowCache.syncFollowedDiscussions();
}
```

### API

- `isSpaceFollowed(spaceId)` - Check cached space follow status
- `isDiscussionFollowed(discussionId)` - Check cached discussion follow status
- `isUserFollowed(userId)` - Check cached user follow status
- `setSpaceFollowed(spaceId, isFollowing)` - Update cache after mutation
- `setDiscussionFollowed(discussionId, isFollowing)` - Update cache after mutation
- `setUserFollowed(userId, isFollowing)` - Update cache after mutation
- `syncFollowedSpaces()` - Fetch and cache all from API
- `syncFollowedDiscussions()` - Fetch and cache all from API
- `syncFollowedUsers()` - Fetch and cache all from API
- `getFollowedSpaces()` - Get all followed space IDs from cache
- `getFollowedDiscussions()` - Get all followed discussion IDs from cache
- `getFollowedUsers()` - Get all followed user IDs from cache
- `invalidateSpace(spaceId)` - Remove from cache (force refresh)
- `clearAllCaches()` - Clear all follow caches

## 2. Draft Auto-Save

**File:** `wwwroot/js/draft-manager.js`
**Storage:** localStorage
**Purpose:** Auto-save reply/post content every 5 seconds to prevent data loss

### Usage

```javascript
const textarea = document.getElementById('post-content-input');
const discussionId = 'disc_123';

// Restore draft on page load
const restored = SnakkDraftManager.restoreDraft(discussionId, textarea);
if (restored) {
    console.log('Draft restored');
}

// Start auto-save (saves every 5 seconds)
SnakkDraftManager.startAutoSave(discussionId, textarea, () => {
    // Optional: return replyToPostId if replying to specific post
    return document.getElementById('reply-to-post-id')?.value || null;
});

// Clear draft on successful post
form.addEventListener('submit', async (e) => {
    // ... submit logic
    SnakkDraftManager.clearDraftOnSuccess(discussionId, replyToPostId);
});
```

### Features

- Auto-saves every 5 seconds
- Saves on textarea blur
- Saves on page unload
- Shows "Draft restored" indicator
- Drafts expire after 7 days
- Empty drafts are automatically deleted

### API

- `getDraft(discussionId, replyToPostId)` - Get saved draft
- `saveDraft(discussionId, content, replyToPostId)` - Manually save draft
- `deleteDraft(discussionId, replyToPostId)` - Delete draft
- `startAutoSave(discussionId, textarea, getReplyToPostId)` - Start auto-save timer
- `stopAutoSave()` - Stop auto-save timer
- `restoreDraft(discussionId, textarea, replyToPostId)` - Restore draft to textarea
- `clearDraftOnSuccess(discussionId, replyToPostId)` - Clear after successful post
- `getDraftCount()` - Get count of saved drafts
- `getAllDraftsList()` - Get all drafts with metadata
- `clearAllDrafts()` - Clear all drafts
- `pruneOldDrafts()` - Remove drafts older than 7 days

## 3. Search History

**File:** `wwwroot/js/search-history.js`
**Storage:** localStorage
**Purpose:** Track search queries, filters, and clicked results for suggestions

### Usage

```javascript
// When user performs a search
SnakkSearchHistory.addSearchQuery('react hooks', { type: 'discussion' });

// When user clicks a result
SnakkSearchHistory.recordResultClick('react hooks', 'disc_123', 'discussion');

// Get recent searches for autocomplete
const recent = SnakkSearchHistory.getRecentQueries(5);
// ["react hooks", "typescript error", "deploy help", ...]

// Get suggestions based on partial input
const suggestions = SnakkSearchHistory.getSuggestions('reac', 5);
// ["react hooks", "react context", ...]

// Get popular searches
const popular = SnakkSearchHistory.getPopularQueries(10);
// [{ query: "react hooks", clickCount: 5, lastClickedAt: 1234567890 }, ...]
```

### Features

- Tracks last 20 searches
- Records click counts per query
- Tracks which results were clicked (for ranking)
- Provides autocomplete suggestions
- Automatically prunes searches older than 90 days

### API

- `addSearchQuery(query, filters)` - Add search to history
- `recordResultClick(query, resultId, resultType)` - Track clicked result
- `getRecentQueries(limit)` - Get recent search queries
- `getPopularQueries(limit)` - Get queries sorted by click count
- `getSuggestions(partial, limit)` - Get autocomplete suggestions
- `getCommonFilters()` - Get frequently used filters
- `removeQuery(query)` - Remove specific query
- `clearSearchHistory()` - Clear all history
- `getQueryEntry(query)` - Get full entry with metadata
- `pruneOldSearches()` - Remove searches older than 90 days

## 4. Read State Batcher

**File:** `wwwroot/js/read-state-batcher.js`
**Storage:** localStorage (for pending updates)
**Purpose:** Batch read state updates to reduce API calls from 1/second to 1/30sec

### Usage

```javascript
// Initialize on page load
const isAuthenticated = true;
SnakkReadStateBatcher.init(isAuthenticated);

// Update read state (buffered, not sent immediately)
SnakkReadStateBatcher.updateReadState(discussionId, lastReadPostId);

// Automatic flush every 30 seconds
// Also flushes on:
// - Page unload (using sendBeacon for reliability)
// - Tab/window hidden (visibility change)
// - Idle time (using requestIdleCallback)

// Force immediate flush (if needed)
await SnakkReadStateBatcher.forceFlush();
```

### Features

- Batches updates in memory
- Flushes every 30 seconds automatically
- **Flushes on page unload** using `sendBeacon` API
- Flushes when tab/window hidden
- Persists pending updates to localStorage (survives crashes)
- Retries failed requests
- Reduces API calls by ~97% (1/sec → 1/30sec)

### API

- `init(isAuthenticated)` - Initialize the batcher
- `updateReadState(discussionId, postId)` - Buffer a read state update
- `flush()` - Send all pending updates to server
- `forceFlush()` - Force immediate flush (async)
- `getPendingCount()` - Get count of pending updates
- `getPendingUpdates()` - Get all pending updates
- `clearPendingUpdates()` - Clear all pending (for testing)
- `shutdown()` - Stop batcher and cleanup

## Integration Examples

### Discussion Page (Complete Example)

```javascript
// Initialize all systems
document.addEventListener('DOMContentLoaded', () => {
    const discussionId = 'disc_123';
    const isAuth = true;

    // 1. Initialize read state batcher
    SnakkReadStateBatcher.init(isAuth);

    // 2. Initialize draft auto-save
    const textarea = document.getElementById('post-content-input');
    SnakkDraftManager.restoreDraft(discussionId, textarea);
    SnakkDraftManager.startAutoSave(discussionId, textarea, getReplyToPostId);

    // 3. Use follow cache
    loadFollowStatus(discussionId);

    // 4. Track read state (batched)
    window.addEventListener('scroll', () => {
        const lastVisiblePostId = getLastVisiblePost();
        SnakkReadStateBatcher.updateReadState(discussionId, lastVisiblePostId);
    });
});

async function loadFollowStatus(discussionId) {
    // Check cache first
    const cached = SnakkFollowCache.isDiscussionFollowed(discussionId);
    if (cached !== null) {
        updateUI(cached);
        return; // Skip API call!
    }

    // Cache miss, fetch from API
    const result = await fetch(`/api/follows/${discussionId}`).then(r => r.json());
    SnakkFollowCache.setDiscussionFollowed(discussionId, result.isFollowing);
    updateUI(result.isFollowing);
}

async function toggleFollow(discussionId) {
    // Optimistic update
    const newState = !currentState;
    updateUI(newState);
    SnakkFollowCache.setDiscussionFollowed(discussionId, newState);

    // API call
    try {
        await fetch(`/api/follows/${discussionId}`, { method: 'POST' });
    } catch (err) {
        // Revert on error
        updateUI(!newState);
        SnakkFollowCache.setDiscussionFollowed(discussionId, !newState);
    }
}
```

### Search Page Example

```html
<input type="text" id="search-input" placeholder="Search..." />
<div id="search-suggestions"></div>

<script>
const searchInput = document.getElementById('search-input');
const suggestionsDiv = document.getElementById('search-suggestions');

// Show suggestions on input
searchInput.addEventListener('input', (e) => {
    const query = e.target.value;
    if (query.length < 2) {
        suggestionsDiv.innerHTML = '';
        return;
    }

    const suggestions = SnakkSearchHistory.getSuggestions(query, 5);
    suggestionsDiv.innerHTML = suggestions
        .map(s => `<div class="suggestion">${s}</div>`)
        .join('');
});

// Track search
searchForm.addEventListener('submit', (e) => {
    const query = searchInput.value;
    SnakkSearchHistory.addSearchQuery(query, { type: 'all' });
});

// Track click
resultLinks.forEach(link => {
    link.addEventListener('click', () => {
        SnakkSearchHistory.recordResultClick(
            currentQuery,
            link.dataset.id,
            link.dataset.type
        );
    });
});
</script>
```

## Performance Impact

### Before Optimization
- **Follow status checks**: 1 API call per discussion/space/user shown (N queries)
- **Read state updates**: 1 API call per second while scrolling (~60/minute)
- **Lost drafts**: Common on browser crash or accidental navigation
- **No search suggestions**: Users re-type same queries

### After Optimization
- **Follow status checks**: 1 API call per entity, cached for 5 minutes (~95% reduction)
- **Read state updates**: 1 API call per 30 seconds (~97% reduction)
- **Draft recovery**: 100% (auto-saved every 5 seconds + on unload)
- **Search UX**: Instant suggestions from history

### Total Impact
- **~60-90% reduction** in API calls for typical user session
- **Zero data loss** for draft content
- **Faster perceived performance** (instant cache hits)
- **Better UX** (search suggestions, draft restoration)

## Browser Support

All features use modern browser APIs with graceful fallbacks:

- **localStorage**: Supported in all modern browsers
- **sendBeacon**: Used for reliable page unload, falls back to sync XHR
- **requestIdleCallback**: Used for idle flush, optional enhancement
- **Visibility API**: Used for tab hidden flush, optional enhancement

## Storage Limits

- Follow Cache: ~100 items × 3 types = ~300 KB
- Drafts: Max 50 × ~2 KB each = ~100 KB
- Search History: 20 queries × ~500 B = ~10 KB
- Read States: ~50 pending × ~100 B = ~5 KB
- Read History: 50 discussions × ~500 B = ~25 KB

**Total: ~440 KB** (well within 5-10 MB localStorage limits)

## Debugging

```javascript
// Check follow cache status
console.table(SnakkFollowCache.getFollowedDiscussions());

// Check draft count
console.log('Drafts:', SnakkDraftManager.getDraftCount());

// Check pending read states
console.log('Pending:', SnakkReadStateBatcher.getPendingCount());

// Check search history
console.table(SnakkSearchHistory.getRecentQueries(10));

// Force flush read states
await SnakkReadStateBatcher.forceFlush();

// Clear all caches (for testing)
SnakkFollowCache.clearAllCaches();
SnakkDraftManager.clearAllDrafts();
SnakkSearchHistory.clearSearchHistory();
SnakkReadStateBatcher.clearPendingUpdates();
```

## Backend Requirements

### New Endpoints Needed

1. **Batch Read State Update**
   - `POST /bff/read-states/batch`
   - Body: `{ updates: [{ discussionId, postId, timestamp }] }`
   - Response: `{ success: true, updated: 5 }`

2. **Batch Follow Status Check** (optional optimization)
   - `POST /bff/follows/check-batch`
   - Body: `{ discussionIds: [...], spaceIds: [...], userIds: [...] }`
   - Response: `{ discussions: {}, spaces: {}, users: {} }`

3. **Follow Lists** (for cache sync)
   - `GET /bff/follows/spaces` → `{ items: ['space_1', 'space_2'] }`
   - `GET /bff/follows/discussions` → `{ items: ['disc_1', 'disc_2'] }`
   - `GET /bff/follows/users` → `{ items: ['user_1', 'user_2'] }`

All other features work with existing endpoints.
