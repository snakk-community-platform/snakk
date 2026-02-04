# Quiet Mode Feature Toggle

## Overview

Snakk can operate in two distinct modes:
- **Social Mode** (default): Full-featured social media enabled community
- **Quiet Mode**: Minimal distraction discussion forum

This document outlines the feature matrix, configuration approach, and implementation strategy for toggling between modes.

---

## Feature Matrix

### Features to DISABLE in Quiet Mode

| Feature | Social Mode | Quiet Mode | Implementation Notes |
|---------|-------------|------------|---------------------|
| **Following/Followers** | ✅ Enabled | ❌ Disabled | Hide follow buttons, followers list, following list |
| **Achievements/Gamification** | ✅ Enabled | ❌ Disabled | Hide achievement badges, progress bars, leaderboards |
| **Emoji Reactions** | ✅ Enabled | ❌ Disabled | Hide reaction buttons, keep simple upvote/like only |
| **Direct Messages** | ✅ Enabled | ❌ Disabled | Hide DM inbox, compose button |
| **User Profiles (Extended)** | ✅ Full Profile | 📊 Basic Info | Show only username, join date, post count |
| **Activity Feed** | ✅ Enabled | ❌ Disabled | Hide "Following" activity, keep only own activity |
| **Social Notifications** | ✅ All Types | 📊 Basic Only | Disable: "X followed you", "Achievement earned", "X reacted" |
| **Trending/Hot** | ✅ Enabled | ❌ Disabled | Remove trending algorithms, hot discussions |
| **User Stats Dashboard** | ✅ Enabled | ❌ Disabled | Hide stats page, charts, leaderboards |
| **@Mentions Autocomplete** | ✅ Enabled | ❌ Disabled | Still parse mentions, but no autocomplete UI |
| **Online Status Indicators** | ✅ Enabled | ❌ Disabled | Hide "online now" badges |
| **Read Receipts** | ✅ Enabled | ❌ Disabled | Don't track/show who viewed discussions |

### Features to KEEP in Quiet Mode

| Feature | Status | Notes |
|---------|--------|-------|
| **Discussion Threads** | ✅ Core Feature | Full threading, replies, nested conversations |
| **Basic Upvote/Like** | ✅ Enabled | Simple thumbs up/like button (no emoji variety) |
| **Search** | ✅ Enabled | Full text search across discussions |
| **Categories/Spaces** | ✅ Enabled | Topic organization remains essential |
| **Moderation Tools** | ✅ Enabled | Edit, delete, pin, lock discussions |
| **Notifications (Core)** | ✅ Enabled | Replies to your posts, mentions, moderation actions |
| **User Auth** | ✅ Enabled | Login, registration, profiles (basic) |
| **Markdown/Formatting** | ✅ Enabled | Code blocks, links, formatting |
| **Sorting Options** | ✅ Enabled | Sort by date, replies, etc. (not "hot" or "trending") |

---

## Configuration Schema

### appsettings.json

```json
{
  "FeatureFlags": {
    "QuietMode": {
      "Enabled": false,
      "DisabledFeatures": [
        "Following",
        "Achievements",
        "EmojiReactions",
        "DirectMessages",
        "ExtendedProfiles",
        "ActivityFeed",
        "SocialNotifications",
        "TrendingAlgorithms",
        "UserStats",
        "MentionAutocomplete",
        "OnlineStatus",
        "ReadReceipts"
      ]
    }
  }
}
```

### Feature Flag Service

```csharp
public interface IFeatureFlagService
{
    bool IsQuietModeEnabled();
    bool IsFeatureEnabled(string featureName);
}

public class FeatureFlagService : IFeatureFlagService
{
    private readonly IConfiguration _configuration;

    public FeatureFlagService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsQuietModeEnabled()
    {
        return _configuration.GetValue<bool>("FeatureFlags:QuietMode:Enabled");
    }

    public bool IsFeatureEnabled(string featureName)
    {
        if (!IsQuietModeEnabled()) return true;

        var disabledFeatures = _configuration
            .GetSection("FeatureFlags:QuietMode:DisabledFeatures")
            .Get<string[]>() ?? Array.Empty<string>();

        return !disabledFeatures.Contains(featureName);
    }
}
```

---

## Implementation Examples

### Backend Guards

```csharp
// In UserController.cs
[HttpPost("follow")]
public async Task<IActionResult> FollowUser([FromBody] FollowRequest request)
{
    if (!_featureFlags.IsFeatureEnabled("Following"))
    {
        return BadRequest("Following is disabled in quiet mode");
    }

    // ... existing follow logic
}

// In AchievementService.cs
public async Task AwardAchievementAsync(UserId userId, AchievementId achievementId)
{
    if (!_featureFlags.IsFeatureEnabled("Achievements"))
    {
        return; // Silently skip achievement awards
    }

    // ... existing award logic
}

// In NotificationService.cs
public async Task CreateNotificationAsync(Notification notification)
{
    // Filter out social notifications in quiet mode
    if (!_featureFlags.IsFeatureEnabled("SocialNotifications"))
    {
        var socialTypes = new[] { "Follow", "Achievement", "Reaction" };
        if (socialTypes.Contains(notification.Type))
        {
            return; // Skip social notifications
        }
    }

    // ... existing notification logic
}
```

### UI Conditionals (Razor)

```cshtml
@inject IFeatureFlagService FeatureFlags

<!-- User Profile Header -->
<div class="profile-header">
    <h1>@Model.Username</h1>
    <p>Joined: @Model.JoinDate</p>

    @if (FeatureFlags.IsFeatureEnabled("Following"))
    {
        <button class="btn btn-primary" onclick="followUser()">Follow</button>
        <div class="stats">
            <span>@Model.FollowerCount Followers</span>
            <span>@Model.FollowingCount Following</span>
        </div>
    }

    @if (FeatureFlags.IsFeatureEnabled("Achievements"))
    {
        <div class="achievement-badges">
            @foreach (var achievement in Model.DisplayedAchievements)
            {
                <span class="badge">@achievement.Name</span>
            }
        </div>
    }
</div>

<!-- Discussion Post Reactions -->
<div class="post-actions">
    @if (FeatureFlags.IsFeatureEnabled("EmojiReactions"))
    {
        <!-- Full emoji reaction picker -->
        <div class="reaction-picker">
            <button data-emoji="👍">👍</button>
            <button data-emoji="❤️">❤️</button>
            <button data-emoji="🎉">🎉</button>
            <!-- ... more emojis -->
        </div>
    }
    else
    {
        <!-- Simple upvote only -->
        <button class="btn-upvote">
            👍 Upvote (@Model.UpvoteCount)
        </button>
    }
</div>

<!-- Sidebar Navigation -->
<nav id="sidebar">
    <a href="/discussions">Discussions</a>
    <a href="/spaces">Spaces</a>

    @if (FeatureFlags.IsFeatureEnabled("ActivityFeed"))
    {
        <a href="/activity">Activity Feed</a>
    }

    @if (FeatureFlags.IsFeatureEnabled("DirectMessages"))
    {
        <a href="/messages">
            Messages
            @if (Model.UnreadMessageCount > 0)
            {
                <span class="notification-badge">@Model.UnreadMessageCount</span>
            }
        </a>
    }

    @if (FeatureFlags.IsFeatureEnabled("UserStats"))
    {
        <a href="/stats">Statistics</a>
    }
</nav>
```

### JavaScript Feature Checks

```javascript
// search-focus.js or similar
const featureFlags = window.SNAKK_FEATURE_FLAGS || {};

function initializeMentionAutocomplete() {
    if (!featureFlags.MentionAutocomplete) {
        return; // Skip autocomplete initialization
    }

    // ... autocomplete logic
}

// Expose to global scope if needed from backend
// In _Layout.cshtml:
<script>
    window.SNAKK_FEATURE_FLAGS = {
        Following: @FeatureFlags.IsFeatureEnabled("Following").ToString().ToLower(),
        Achievements: @FeatureFlags.IsFeatureEnabled("Achievements").ToString().ToLower(),
        EmojiReactions: @FeatureFlags.IsFeatureEnabled("EmojiReactions").ToString().ToLower(),
        MentionAutocomplete: @FeatureFlags.IsFeatureEnabled("MentionAutocomplete").ToString().ToLower()
    };
</script>
```

---

## Decision Points

### 1. Reaction Strategy

**Option A**: Disable all reactions (pure discussion-only)
**Option B**: Keep simple upvote/like, disable emoji reactions (RECOMMENDED)
**Option C**: Keep reactions but hide reaction counts/leaderboards

**Recommendation**: Option B - A single upvote button provides minimal social feedback while keeping focus on content.

### 2. Mentions Handling

**Option A**: Disable @mentions completely
**Option B**: Keep mentions but disable autocomplete UI (RECOMMENDED)
**Option C**: Keep mentions with full functionality

**Recommendation**: Option B - Mentions are useful for discussion flow, but autocomplete encourages social discovery.

### 3. Profile Visibility

**Option A**: Hide all user profiles
**Option B**: Show minimal profiles (username, join date, post count) (RECOMMENDED)
**Option C**: Show full profiles but hide social stats

**Recommendation**: Option B - Basic identity is necessary for trust, but extended social features encourage stalking/comparison.

### 4. Notification Types

Keep in Quiet Mode:
- Replies to your posts
- @Mentions of your username
- Moderation actions on your content

Disable in Quiet Mode:
- "X started following you"
- "Achievement unlocked"
- "X reacted to your post with ❤️"
- "Trending discussion in your space"

### 5. Search and Discovery

**Disable**: "Trending", "Hot", "Popular users"
**Keep**: Text search, category browsing, "Recent" and "Most replies" sorting

---

## Migration Path

### Phase 1: Infrastructure (Current)
- [x] Create FeatureFlagService
- [ ] Add appsettings.json configuration
- [ ] Register service in DI container
- [ ] Add feature flag injection to controllers/services

### Phase 2: Backend Guards
- [ ] Add guards to follow/unfollow endpoints
- [ ] Add guards to achievement system
- [ ] Add guards to DM endpoints
- [ ] Filter notifications by type
- [ ] Disable trending algorithms

### Phase 3: UI Updates
- [ ] Add feature checks to navigation
- [ ] Conditionally render reaction pickers
- [ ] Hide/show profile sections
- [ ] Update _Layout.cshtml script injection
- [ ] Add CSS classes for hidden features

### Phase 4: Testing
- [ ] Test with QuietMode = false (all features work)
- [ ] Test with QuietMode = true (social features hidden)
- [ ] Verify no JavaScript errors when features disabled
- [ ] Check database writes don't occur for disabled features
- [ ] Validate notification filtering

---

## Performance Considerations

- Feature flag checks are lightweight (in-memory configuration reads)
- Consider caching `IsQuietModeEnabled()` result per request
- No database queries needed for feature flags
- UI conditionals prevent unnecessary DOM/JavaScript loading

---

## Admin Controls (Future)

Potential admin panel for runtime toggling:

```
[Admin Panel] > [Feature Flags]

☐ Quiet Mode (Minimal Distraction Forum)

  When enabled, the following features are disabled:
  ☑ Following/Followers
  ☑ Achievements & Gamification
  ☑ Emoji Reactions (keeps simple upvote)
  ☑ Direct Messages
  ☑ Extended User Profiles
  ☑ Activity Feed
  ☑ Social Notifications
  ☑ Trending Algorithms

  [Save Changes]
```

---

## Notes

- Quiet mode is a **deployment-level** setting, not per-user
- Changing modes does NOT delete existing data (achievements, follows, etc.)
- Re-enabling social mode restores all functionality immediately
- Consider user communication strategy if toggling an existing community
- May want gradual rollout (disable features one at a time) rather than all-at-once

---

## Related Files

- `c:\Snakk\src\core\Snakk.Application\Services\FeatureFlagService.cs` (to be created)
- `c:\Snakk\src\core\Snakk.Api\appsettings.json`
- `c:\Snakk\src\clients\Snakk.Web\Pages\Shared\_Layout.cshtml`
- All controller files that implement social features
- `c:\Snakk\src\core\Snakk.Application\Services\NotificationService.cs`

---

**Last Updated**: 2026-02-03
**Status**: Planning / Not Implemented
**Priority**: Low (nice-to-have feature for deployment flexibility)
