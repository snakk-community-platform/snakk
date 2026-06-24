# Cache Inventory

All HybridCache and IMemoryCache stores in the Snakk platform. Use this to answer "what is cached for X?" or "where do I need to add invalidation when I mutate Y?"

- **HybridCache** — distributed/shared cache, used for all durable data. Backed by Redis in production.
- **IMemoryCache** — process-local only, used for short-lived or per-process concerns.

TTL in HybridCache entries acts as a safety net unless noted as TTL-only.  
Entries marked **TTL-only** have no write-path invalidation — staleness is accepted by design.

---

## HybridCache Entries

### Auth & Identity

| Store | Key pattern | Data | TTL | Invalidation |
|---|---|---|---|---|
| **AuthVersionCache** | `authver:{userId}` | `long?` — user's AuthVersion column value | 5–5.5 min (jitter) | `InvalidateAsync(userId)` on any token revocation or role change. `SetAsync` used for immediate overwrite on refresh |
| **UserGrantsCacheService** | `user-grants:{userId}` | `UserGrants` — sets of SpaceIds, HubIds, CommunityIds a user has group access to | Configurable (`AccessCache:GrantsTtlMinutes`, default 5 min) | `Invalidate(userId)` on group membership change; `InvalidateAll()` clears all via `"user-grants"` tag |
| **UserGrantsCacheService** | `restricted-entities` | `RestrictedEntitySet` — all restricted Space/Hub/Community IDs | Configurable (`AccessCache:RestrictedEntitiesTtlMinutes`, default 5 min) | `InvalidateRestrictedCount()` on `SetIsRestrictedAsync` |
| **UserGrantsCacheService** | `adult-hiding-spaces` | `HashSet<int>` — space IDs that hide adult content | Configurable (`AccessCache:PlatformCountTtlSeconds`, default 30 s) | `InvalidateAdultHidingSpaces()` when `HideAdultDiscussionsFromLists` setting changes |
| **PermissionService** | `user_permissions_{userId}` | `List<PermissionDto>` — all permissions for the user's active roles | 5 min | `RemoveAsync` on role grant/revoke/temporary role changes. Also removes `manage_perms_user_{userId}` tag |
| **PermissionService** | `all_permissions` | `List<PermissionDto>` — full platform permissions catalog | 5 min | **TTL-only** — permissions table is seed-only, never mutated in production |
| **ManageScopeDataService** | `roles:global-admins` | `HashSet<string>` — public IDs of all active GlobalAdmin users | 24 h | `InvalidateGlobalAdminCacheAsync()` on admin role assignment/revocation |
| **ManagePermissionService** | `manage_perms_{userId}_{scopeType}_{scopePublicId}` | `ManagePermissionSet` — bitmask of manage operations allowed for user on scope | 5 min | `RemoveByTagAsync("manage_perms_user_{userId}")` on role changes, team updates, temp role grant/revoke |
| **GroupAccessService** | `group-access:{userPublicId\|"anon"}:{communityPublicId}:{hubPublicId}:{spacePublicId}` | `GroupAccessResult` — effective access level for a user on a scope | 3 min | `RemoveByTagAsync("group-access:s:{spacePublicId}")` / `h:` / `c:` on restriction or grant changes. Tagged `group-access:u:{userId}` (for authenticated users); swept by `UserGrantsCacheService.Invalidate(userId)` on group membership change |
| **UserGrpcService** | `user:slug-id:{slug}` | `string` — user's PublicId (empty string = not-found sentinel) | 24 h | `RemoveAsync` on slug change or not-found sentinel expiry |
| **UserGrpcService** | `user:old-slug:{oldSlug}` | `string` — current slug for a historical slug (redirect target) | 1 h | `RemoveAsync` after slug update |
| **UserVisitTracker** | `session-active:{userId}` | `bool` — guard preventing duplicate visit DB writes within the same window | 30 min | `RemoveAsync` in `ForceNewVisitAsync` |
| **UserRepositoryAdapter** | `last-visit-at:{userId}` | `DateTime?` — user's `LastVisitAt` column (unread cutoff timestamp) | 24 h | `RemoveAsync` in `UserVisitTracker.ForceNewVisitAsync` and `UserVisitTracker.UpdateVisitTimestampsAsync` (new-visit branch only) |

---

### Settings & Configuration

| Store | Key pattern | Data | TTL | Invalidation |
|---|---|---|---|---|
| **SettingsService** | `settings_category_{category}` | `SettingsByCategoryResponse` — all settings for a category (General, Email, OAuth, Avatar, Content, RateLimiting, Registration, etc.) | 24 h | `RemoveAsync` in `UpdateSettingAsync` and `UpdateAllowedDisplayNameScriptsAsync` |
| **SettingsService** | `site-info` | `SiteInfoDto` — SiteName, SiteDescription, LogoUrl, Timezone, Language | 24 h | `RemoveAsync` in `UpdateSiteInfoAsync` |
| **CommunityDomainCacheService** | `domain:{normalizedDomain}` | `CommunityDomainLookupResult` — CommunitySlug, CommunityName, Timezone (or not-found) | Configurable (`Snakk:DomainCache:ExpirationMinutes`, default 15 min) | `InvalidateDomainAsync(domain)` |

---

### Community / Hub / Space Metadata

| Store | Key pattern | Data | TTL | Invalidation |
|---|---|---|---|---|
| **CommunityGrpcService** | `communities:public-list:{offset}:{pageSize}` | `PagedCommunityList` — paged list of public-listed communities with counts | 24 h | `RemoveByTagAsync("communities:public-list")` in `CommunityRepositoryAdapter.AddAsync`, `CommunityRepositoryAdapter.UpdateAsync`, `CommunityManagementService.UpdateSettingsAsync` |
| **CommunityDataService** | `community-meta:{publicId}` | `CommunityMetaDto` — HasRules, RulesRevision, TeamRevision, IsRestricted, Require2FA, VisibilityId | 5 min | `RemoveAsync` on rule update, visibility change, mod team change |
| **HubDataService** | `hub-meta:{publicId}` | `HubMetaDto` — HasRules, RulesRevision, ParentCommunityHasRules, TeamRevision, IsRestricted, Require2FA, AllowAnonymousReading, CommunitySlug | 5 min | `RemoveAsync` on rule update, visibility change, mod team change |
| **SpaceDataService** | `space-meta:{publicId}` | `SpaceMetaDto` — HasRules, RulesRevision, parent HasRules flags, TeamRevision, IsRestricted, AllowedTypes, slugs, DiscussionCount, ReplyCount, LatestDiscussion, Require2FA, AllowAnonymousReading | 5 min | `RemoveAsync` on rule update, visibility change, mod team change. Also removed by `DiscussionCreatedSpaceLatestCacheHandler` and `PostCreatedSpaceLatestCacheHandler` to keep `LatestDiscussion` in sync |
| **EntityHierarchyCacheService** | `hierarchy:d:{publicId}` | `DiscussionHierarchy` — SpaceId, HubId, CommunityId | 24 h | Null result evicts immediately; no write-path invalidation (hierarchy is immutable after creation) |
| **EntityHierarchyCacheService** | `hierarchy:s:{publicId}` | `SpaceHierarchy` — Id, HubId, CommunityId | 24 h | Same |
| **EntityHierarchyCacheService** | `hierarchy:h:{publicId}` | `HubHierarchy` — Id, CommunityId | 24 h | Same |
| **EntityHierarchyCacheService** | `hierarchy:c:{publicId}` | `int?` — community internal DB ID | 24 h | Same |

---

### Rules

| Store | Key pattern | Data | TTL | Invalidation |
|---|---|---|---|---|
| **RuleService** | `rules:{scopeType}:{scopePublicId\|"site"}` | `RulesDto` — list of rules for a scope (Site / Community / Hub / Space) | 24 h | `RemoveAsync` in every `Update*RulesAsync`. Cascades to community-meta / hub-meta / space-meta on hierarchy rule changes |
| **RuleService** | `site-rules-revision` | `string` — cache-busting revision hash (stored in SystemSettings) | 24 h | `RemoveAsync` in `UpdateSiteRulesAsync` |
| **RuleService** | `has-site-rules` | `bool` — whether any site-wide rules exist | 24 h | `RemoveAsync` in `UpdateSiteRulesAsync` |

---

### Content & Discussions

| Store | Key pattern | Data | TTL | Invalidation |
|---|---|---|---|---|
| **SearchRepository** | `space-latest-discussion:{internalSpaceId}` | `SpaceLatestDiscussion` — PublicId, Title, Slug, LastActivityAt, AuthorPublicId, PostCount | 24 h | `RemoveAsync` by `DiscussionCreatedSpaceLatestCacheHandler`, `PostCreatedSpaceLatestCacheHandler`, `AdminContentService` (soft-delete/delete), `ModerationRepository.DeleteDiscussionAsync` |
| **SearchRepository / SaveRepository / DiscussionRepositoryAdapter** | `space-display:{internalSpaceId}` | `SpaceDisplay` — Slug, Name, Description, AvatarFileName, HubSlug, HubName, CommunitySlug, CommunityName | 365 days | `RemoveAsync` by `CommunityRepositoryAdapter.SaveAsync` (all child spaces), `HubRepositoryAdapter.SaveAsync` (all child spaces), `SpaceRepositoryAdapter.SaveAsync` (specific space). **Gap:** no invalidation on space deletion |
| **SearchRepository** | `preview:poll:{discussionPublicId}` | `DiscussionPreviewDto` — poll options, vote counts, configuration | 24 h | `RemoveAsync` in `PollService.VoteAsync` / `RemoveVoteAsync` |
| **SearchRepository** | `preview:debate:{discussionPublicId}` | `DiscussionPreviewDto` — debate positions | 24 h | `RemoveAsync` in `DiscussionTypeQueryService.SetPostDebatePositionAsync` |
| **SearchRepository** | `preview:iama:{discussionPublicId}` | `DiscussionPreviewDto` — IAmA official answer, best questions, phase | 24 h | `RemoveAsync` in `DiscussionTypeQueryService.MarkIamaOfficialAnswerAsync`, `SetIamaBestQuestionsAsync`, `TransitionIamaPhaseAsync` |
| **SearchRepository** | `preview:link:{discussionPublicId}` | `DiscussionPreviewDto` — link URL, title, description | 24 h | `RemoveAsync` in `AdminContentService.SoftDeleteDiscussionByPublicIdAsync`, `DeleteDiscussionAsync`, and `ModerationRepository.ModeratorDeleteDiscussionAsync` on deletion |
| **SearchRepository** | `preview:images:{discussionPublicId}` | `DiscussionPreviewDto` — image list | 24 h | Same deletion invalidation as `preview:link:` |
| **DiscussionReadStateRepositoryAdapter** | `read-state:{userId}:{discussionId}` | `DiscussionReadState` — LastReadPostId, LastReadAt | 24 h | `RemoveAsync` in `SaveAsync` and `BatchSaveAsync` after every write |
| **DiscussionGrpcService** | `unread-count:{userId}:{lastVisitAt.Ticks}` | `int` — count of discussions with `LastActivityAt > lastVisitAt` | 2 min | **TTL-only for new-discussion staleness** (new discussions can't be tracked). Key includes `lastVisitAt.Ticks`, so it naturally rotates on each new visit — fresh count on every visit boundary without explicit removal |
| **PollService** | `poll:{discussionPublicId}` | `CachedPollData` — user-independent poll snapshot (raw/unmasked options, vote counts, ClosesAt). `IsClosed` and secret masking computed at serve time. | 24 h | `RemoveAsync` in `VoteAsync` / `RemoveVoteAsync`. Also removes `preview:poll:{id}` |

---

### Moderation

| Store | Key pattern | Data | TTL | Invalidation |
|---|---|---|---|---|
| **ModerationRepository** | `user-mod-roles:{userPublicId}` | `List<UserModRoleDto>` — active moderator roles for a user | 24 h | No direct key removal — evicted by `RemoveByTagAsync("manage_perms_user_{userId}")` tag sweep on team changes |
| **ModerationGrpcService** | `moderators:{scopeType}:{scopePublicId}` | `GetModeratorsResponse` — list of moderators for a scope | 5 min | `RemoveByTagAsync("moderators")` on role assignment or revocation |

---

### Banners

| Store | Key pattern | Data | TTL | Invalidation |
|---|---|---|---|---|
| **BannerGrpcService** | `banners:community:{entityId}` | `BannerList` — active banners | 5 min | `RemoveAsync` on banner create/update/delete in `ManageGrpcService` |
| **BannerGrpcService** | `banners:hub:{entityId}` | `BannerList` | 5 min | Same |
| **BannerGrpcService** | `banners:space:{entityId}` | `BannerList` | 5 min | Same |

---

### Activity Sparklines

Sparkline data comes from `ActivityDailySnapshot`, which is written exclusively by `ActivitySnapshotWorker` (runs hourly). After each refresh, the worker calls `cache.RemoveByTagAsync("sparkline")` to sweep all entries tagged `"sparkline"`. Both entry types below carry that tag.

| Store | Key pattern | Data | TTL | Invalidation |
|---|---|---|---|---|
| **StatisticsGrpcService** | `sparkline:{entityType}:{publicId\|"platform"}:{days}` | `SparklineResponse` — per-entity daily activity sparkline (posts + discussions per day) | 24 h | `RemoveByTagAsync("sparkline")` in `ActivitySnapshotWorker` after each hourly snapshot refresh |
| **StatisticsGrpcService** | `sparkline:{entityType}:batch:{sortedIds}:{days}` | `SparklineBatchResponse` — batch sparklines for a sorted, comma-joined set of public IDs | 24 h | Same — `RemoveByTagAsync("sparkline")` |

---

### Statistics & Trends (all TTL-only)

| Store | Key pattern | Data | TTL |
|---|---|---|---|
| **StatisticsGrpcService** | `stats:platform` | `PlatformStats` — hub/space/discussion/reply counts | 60 s |
| **StatisticsGrpcService** | `trending:{hubId}:{spaceId}:{communityId}:{limit}:{userId}:{allowAdult}` | `TopActiveDiscussionsList` | 2 min |
| **StatisticsGrpcService** | `stats:top-spaces-today:{hubId}:{communityId}:{limit}` | `TopActiveSpacesList` | 2 min |
| **StatisticsGrpcService** | `stats:top-contributors-today:{hubId}:{spaceId}:{communityId}:{limit}` | `TopContributorsList` | 2 min |
| **StatisticsGrpcService** | `stats:latest-spaces:{hubId}:{communityId}:{limit}` | `LatestSpacesList` | 2 min |
| **StatisticsGrpcService** | `stats:latest-contributors:{hubId}:{spaceId}:{communityId}:{limit}` | `LatestContributorsList` | 2 min |
| **StatisticsGrpcService** | `stats:trending-spaces:{hubId}:{communityId}:{limit}` | `TopActiveSpacesList` | 5 min |
| **StatisticsGrpcService** | `stats:trending-contributors:{hubId}:{spaceId}:{communityId}:{limit}` | `TopContributorsList` | 5 min |
| **StatisticsGrpcService** | `stats:top-spaces-period:{period}:{hubId}:{communityId}:{limit}` | `TopActiveSpacesList` | 5 min |
| **StatisticsGrpcService** | `stats:top-contributors-period:{period}:{hubId}:{spaceId}:{communityId}:{limit}` | `TopContributorsList` | 5 min |
| **VolumeWindowService** | `volume-window:posts` | `TimeSpan` — adaptive trending window for posts | 1 h |
| **VolumeWindowService** | `volume-window:discussions` | `TimeSpan` — adaptive trending window for discussions | 1 h |

---

### Admin

| Store | Key pattern | Data | TTL | Invalidation |
|---|---|---|---|---|
| **AdminContentService** | `admin_content_overview` | `ContentOverviewDto` — community/hub/space/discussion/post counts | 5 min | **TTL-only** — too many mutation paths to cover; 5-min staleness accepted |
| **AdminContentService** | `admin_content_overview_extended` | `ContentOverviewExtendedDto` — same + pinned/locked counts | 5 min | **TTL-only** — same rationale |
| **AdminUserService** | `admin_user_{userId}` | `AdminUserDto` — PublicId, DisplayName, Email, CreatedAt | 5 min | **TTL-only** — 5-min staleness accepted for admin detail view |

---

### Web App (Snakk.Web)

| Store | Key pattern | Data | TTL | Invalidation |
|---|---|---|---|---|
| **FollowedSpacesCacheService** | `followed-spaces:{userId}` | `List<string>` — public IDs of followed spaces | 2 min | `InvalidateAsync(userId)` on follow/unfollow |

---

## IDistributedCache Entries (Valkey direct)

Keys written directly via `IDistributedCache` (not HybridCache). Backed by Valkey in production.

### View Tracking Deduplication

| Store | Key pattern | Data | TTL | Invalidation |
|---|---|---|---|---|
| **BffApiEndpoints** | `views:dedup:user:{userPublicId}:{discussionPublicId}` | `byte[]` sentinel — marks that this user already had their view counted for this discussion | 24 h | TTL-only (dedup window by design) |
| **BffApiEndpoints** | `views:dedup:ip:{ipHash}:{discussionPublicId}` | `byte[]` sentinel — marks that this IP (SHA-256 prefix) already had their view counted for this discussion | 24 h | TTL-only (dedup window by design) |

---

## Raw Valkey Keys (IConnectionMultiplexer — atomic counters)

Keys written directly via `IConnectionMultiplexer` using `INCR`/`DECR` (string counters) or `SADD` (set). NOT prefixed by `InstanceName` (`snakk:` prefix is included manually). All flushed to the database by `ActivitySnapshotWorker` on each hourly tick.

### Buffered Post Counts

| Key pattern | Data | Written by | Flushed by |
|---|---|---|---|
| `snakk:counter:post:discussion:{discussionPublicId}` | `long` — net delta of post creates/deletes since last flush | `CounterService.IncrementPostCountAsync` / `DecrementPostCountAsync` (INCR/DECR) | `CounterRepository.FlushPostCountsAsync` — GETDEL all keys, then runs 4 SQL UPDATE statements (Discussion + EngagementScore recompute, Space, Hub, Community via JOIN) |

**Flush SQL summary:**
- `Discussion`: `PostCount += delta`, `EngagementScore = (PostCount + delta) + ReactionCount`  
- `Space/Hub/Community`: `PostCount += SUM(delta)` grouped by hierarchy via `JOIN "Discussion"`

---

### Buffered Follower Counts

| Key pattern | Data | Written by | Flushed by |
|---|---|---|---|
| `snakk:counter:follower:space:{spacePublicId}` | `long` — net delta of follows/unfollows since last flush | `CounterService.IncrementSpaceFollowerCountAsync` / `DecrementSpaceFollowerCountAsync` | `CounterRepository.FlushFollowerCountsAsync` — GETDEL, then `UPDATE "Space" SET "FollowerCount" += delta WHERE "PublicId" = pid` (UNNEST) |
| `snakk:counter:follower:discussion:{discussionPublicId}` | `long` — net delta | `CounterService.IncrementDiscussionFollowerCountAsync` / `DecrementDiscussionFollowerCountAsync` | `CounterRepository.FlushFollowerCountsAsync` — `UPDATE "Discussion" SET "FollowerCount" += delta` |
| `snakk:counter:follower:user:{userPublicId}` | `long` — net delta | `CounterService.IncrementUserFollowerCountAsync` / `DecrementUserFollowerCountAsync` | `CounterRepository.FlushFollowerCountsAsync` — `UPDATE "User" SET "FollowerCount" += delta` |

---

### Buffered User Activity Counts

| Key pattern | Data | Written by | Flushed by |
|---|---|---|---|
| `snakk:counter:user-discussions:{userPublicId}` | `long` — net delta of discussions created/deleted since last flush | `CounterService.IncrementUserDiscussionCountAsync` / `DecrementUserDiscussionCountAsync` | `CounterRepository.FlushUserCountsAsync` — GETDEL, then `UPDATE "User" SET "DiscussionCount" += delta` (UNNEST) |
| `snakk:counter:user-replies:{userPublicId}` | `long` — net delta of replies created/deleted since last flush | `CounterService.IncrementUserReplyCountAsync` / `DecrementUserReplyCountAsync` | `CounterRepository.FlushUserCountsAsync` — `UPDATE "User" SET "ReplyCount" += delta` |

---

### Buffered Discussion Counts

| Key pattern | Data | Written by | Flushed by |
|---|---|---|---|
| `snakk:counter:discussions:space:{spacePublicId}` | `long` — net delta of discussions created/deleted since last flush | `CounterService.IncrementDiscussionCountAsync` / `DecrementDiscussionCountAsync` (INCR/DECR) | `CounterRepository.FlushDiscussionCountsAsync` — GETDEL, then 3 SQL UPDATEs: `Space.DiscussionCount` (direct UNNEST), `Hub.DiscussionCount` (JOIN Space), `Community.DiscussionCount` (JOIN Space→Hub) |

---

### Buffered Reaction Counts

| Key pattern | Data | Written by | Flushed by |
|---|---|---|---|
| `snakk:counter:reaction:post:{postPublicId}` | `long` — net delta of reactions added/removed since last flush | `CounterService.IncrementReactionCountAsync` / `DecrementReactionCountAsync` (INCR/DECR) | `CounterRepository.FlushReactionCountsAsync` — GETDEL, then 5 SQL UPDATEs: `Post.ReactionCount` (direct UNNEST), `Discussion.ReactionCount + EngagementScore` (JOIN Post), `Space.ReactionCount` (JOIN Post→Discussion), `Hub.ReactionCount`, `Community.ReactionCount` |

**Note:** `EngagementScore` recompute in the reaction flush uses `PostCount + (ReactionCount + delta)`. Post-count flush always runs before reaction-count flush in `ActivitySnapshotWorker`, so `PostCount` is already up-to-date when `EngagementScore` is recalculated.

---

### Trending Dirty Set

| Key | Type | Data | Written by | Flushed by |
|---|---|---|---|---|
| `snakk:counter:trend:dirty` | Redis SET | Set of discussion public IDs that need TrendScore recalculation | `PostCreatedTrendingHandler` (SADD on every new post), `ReactionAddedTrendingHandler` / `ReactionRemovedTrendingHandler` (SADD after resolving post→discussion) | `CounterRepository.FlushTrendScoresAsync` — SPOP up to 1000 members, then calls `TrendScoreCalculator.RecalculateAsync` per discussion (2 SELECTs + 1 UPDATE per discussion) |

**Note:** Redis SET semantics guarantee each discussion ID appears at most once regardless of how many events fired. The fixed 48-hour window is used at flush time (not the adaptive `VolumeWindowService` window) — acceptable for batch recalculation.

**Note:** Keys persist until flush. If the flush worker is down for an extended period, deltas accumulate. There is no TTL — the worker is the only consumer.

---

### Pre-computed Stats Rollup

Written by `StatsRollupWorker` every 2 minutes via `ValkeyStatsRollupRepository`. Read by `StatisticsGrpcService` via `IStatsRollupRepository`. On cold start (key absent) the worker populates immediately instead of waiting 30 seconds.

| Key | Type | Data | TTL | Written by |
|---|---|---|---|---|
| `snakk:stats:platform-stats` | String (JSON array) | `[StatsRollupRow]` — single row with `PlatformStats` | 5 min | `StatsRollupWorker` via `ValkeyStatsRollupRepository.ReplaceAllAsync` |
| `snakk:stats:trending-spaces` | String (JSON array) | `[StatsRollupRow]` — top 20 trending spaces globally | 5 min | Same |
| `snakk:stats:trending-contributors` | String (JSON array) | `[StatsRollupRow]` — top 20 trending contributors globally | 5 min | Same |
| `snakk:stats:top-spaces-today` | String (JSON array) | `[StatsRollupRow]` — top 20 most active spaces today | 5 min | Same |
| `snakk:stats:top-contributors-today` | String (JSON array) | `[StatsRollupRow]` — top 20 contributors today | 5 min | Same |
| `snakk:stats:latest-active-spaces` | String (JSON array) | `[StatsRollupRow]` — 20 most recently active spaces | 5 min | Same |
| `snakk:stats:latest-contributors` | String (JSON array) | `[StatsRollupRow]` — 20 most recently active contributors | 5 min | Same |
| `snakk:stats:top-spaces-period:{period}` | String (JSON array) | `[StatsRollupRow]` — top 20 spaces for period (`day`/`week`/`month`/`year`/`all_time`) | 5 min | Same |
| `snakk:stats:top-contributors-period:{period}` | String (JSON array) | `[StatsRollupRow]` — top 20 contributors for period | 5 min | Same |

TTL (5 min) is a safety net only — the worker refreshes every 2 minutes. If Valkey is cold, `GetRowsAsync` returns an empty list until the first worker run completes.

---

## IMemoryCache Entries (process-local only)

| Store | Key pattern | Data | TTL | Invalidation |
|---|---|---|---|---|
| **PasskeyService** | `passkey:reg:{challengeId}` | JSON string — FIDO2 registration challenge options | 5 min | `Remove` immediately on challenge consumption in `CompleteRegistrationAsync` |
| **PasskeyService** | `passkey:login:{challengeId}` | JSON string — FIDO2 login challenge options | 5 min | `Remove` immediately on challenge consumption in `CompleteLoginAsync` |
| **AuthGrpcService** | `display-name-history:{userId}` | `DisplayNameHistoryResponse` | 5 min | `Remove` in `UpdateDisplayName` handler |
| **AuthGrpcService** | `discord-status:{userId}` | `DiscordStatusResponse` — IsLinked, DiscordUserId, DiscordUsername | 2 min | `Remove` in `CompleteDiscordLink` / `UnlinkDiscord` |
| **AuthGrpcService** | `oauth-connections:{userId}` | `GetOAuthConnectionsResponse` — list of OAuth connections | Short-lived | `Remove` in `ConnectOAuthProvider` / `DisconnectOAuthProvider` |
| **PrefetchCacheService** | `prefetch:{cacheKey}` | `Task<T>` — in-flight or completed prefetch task | Default 5 s (configurable `PrefetchCache:PrefetchTtlSeconds`) | Faulted entries removed explicitly; no write-path invalidation |
| **PrefetchCacheService** | `shared:{cacheKey}` | `Lazy<Task<T?>>` — coalesced in-flight factory | Default 10 s (configurable `PrefetchCache:SharedTtlSeconds`) | Faulted entries removed on exception |
| **PartialRenderCache** | *(opaque internal key)* | Rendered HTML fragments for `DiscussionItem` / `Post` partials | ~10 s | No explicit removal; private `MemoryCache` with `SizeLimit = 2048` |

---

## Cache Write-Path Invalidation Gaps

Entries where a mutation can result in stale data beyond the TTL window. Known and accepted gaps:

| Entry | Gap | Risk |
|---|---|---|
| `space-display:{id}` | No invalidation on space deletion | Space deletion is not yet a feature — add `RemoveAsync` here when it is implemented |
| `all_permissions` | Permissions catalog is seed-only; no mutation path | No real gap — catalog never changes in production |
| `admin_content_overview*` | Too many mutation paths to cover | Accepted: 5-min-stale admin counts |
| `admin_user_{userId}` | Mutation paths not covered | Accepted: 5-min-stale admin user detail |
| `hierarchy:*` | Hierarchy is immutable after entity creation | No real gap — hierarchy never changes |
| `volume-window:*` | Aggregate score; by design TTL-only | Accepted: 1-h-stale window |
| `stats:*` | Aggregate statistics; by design TTL-only | Accepted: 60 s–5 min staleness |

---

## Cache Key Quick Reference

| Key prefix | Service | Notes |
|---|---|---|
| `authver:` | AuthVersionCache | Per-user |
| `user-grants:` | UserGrantsCacheService | Per-user, tagged `"user-grants"` |
| `restricted-entities` | UserGrantsCacheService | Global |
| `adult-hiding-spaces` | UserGrantsCacheService | Global |
| `user_permissions_` | PermissionService | Per-user |
| `all_permissions` | PermissionService | Global |
| `manage_perms_` | ManagePermissionService | Per-user + scope, tagged `"manage_perms_user_{userId}"` |
| `group-access:` | GroupAccessService | Per user+scope, tagged per community/hub/space and per user (`group-access:u:{userId}`) |
| `roles:global-admins` | ManageScopeDataService | Global |
| `user:slug-id:` | UserGrpcService | Per-slug |
| `user:old-slug:` | UserGrpcService | Per-historical-slug |
| `session-active:` | UserVisitTracker | Per-user |
| `last-visit-at:` | UserRepositoryAdapter | Per-user, write-invalidated by UserVisitTracker |
| `unread-count:` | DiscussionGrpcService | Per-user + lastVisitAt tick, TTL-only |
| `settings_category_` | SettingsService | Per-category |
| `site-info` | SettingsService | Global |
| `domain:` | CommunityDomainCacheService | Per-domain |
| `community-meta:` | CommunityDataService | Per-community |
| `hub-meta:` | HubDataService | Per-hub |
| `space-meta:` | SpaceDataService | Per-space |
| `hierarchy:d/s/h/c:` | EntityHierarchyCacheService | Per-entity |
| `rules:` | RuleService | Per-scope |
| `site-rules-revision` | RuleService | Global |
| `has-site-rules` | RuleService | Global |
| `space-latest-discussion:` | SearchRepository | Per-space (integer ID) |
| `space-display:` | SearchRepository / SaveRepository / DiscussionRepositoryAdapter | Per-space (integer ID), shared key |
| `preview:` | SearchRepository | Per-discussion + type |
| `read-state:` | DiscussionReadStateRepositoryAdapter | Per-user + discussion |
| `poll:` | PollService | Per-discussion |
| `user-mod-roles:` | ModerationRepository | Per-user, tagged `"manage_perms_user_{userId}"` |
| `moderators:` | ModerationGrpcService | Per-scope, tagged `"moderators"` |
| `banners:` | BannerGrpcService | Per-scope |
| `sparkline:` | StatisticsGrpcService | Per-entity + days, and batch variant; tagged `"sparkline"`, write-invalidated by `ActivitySnapshotWorker` |
| `communities:public-list:` | CommunityGrpcService | Per-offset+pageSize, tagged `"communities:public-list"` |
| `stats:` / `trending:` | StatisticsGrpcService | Aggregate, TTL-only |
| `volume-window:` | VolumeWindowService | Aggregate, TTL-only |
| `admin_content_overview*` | AdminContentService | Aggregate, TTL-only |
| `admin_user_` | AdminUserService | Per-user, TTL-only |
| `followed-spaces:` | FollowedSpacesCacheService | Per-user (Snakk.Web) |
| `snakk:stats:` | StatsRollupWorker / ValkeyStatsRollupRepository | Raw Valkey; pre-computed global stats, refreshed every 2 min, TTL 5 min |
| `snakk:counter:post:discussion:` | CounterService / CounterRepository | Raw Valkey (IConnectionMultiplexer); net delta flushed hourly by ActivitySnapshotWorker |
| `snakk:counter:follower:space:` | CounterService / CounterRepository | Raw Valkey; Space.FollowerCount delta, flushed hourly |
| `snakk:counter:follower:discussion:` | CounterService / CounterRepository | Raw Valkey; Discussion.FollowerCount delta, flushed hourly |
| `snakk:counter:follower:user:` | CounterService / CounterRepository | Raw Valkey; User.FollowerCount delta, flushed hourly |
| `snakk:counter:user-discussions:` | CounterService / CounterRepository | Raw Valkey; User.DiscussionCount delta, flushed hourly |
| `snakk:counter:user-replies:` | CounterService / CounterRepository | Raw Valkey; User.ReplyCount delta, flushed hourly |
| `snakk:counter:discussions:space:` | CounterService / CounterRepository | Raw Valkey; Space/Hub/Community.DiscussionCount delta, flushed hourly |
| `snakk:counter:reaction:post:` | CounterService / CounterRepository | Raw Valkey; Post + Discussion + Space/Hub/Community.ReactionCount delta, flushed hourly |
| `snakk:counter:trend:dirty` | Trending handlers / CounterRepository | Raw Valkey SET; discussion IDs needing TrendScore recalculation, popped hourly |
| `passkey:` | PasskeyService | Per-challenge (IMemoryCache) |
| `display-name-history:` | AuthGrpcService | Per-user (IMemoryCache) |
| `discord-status:` | AuthGrpcService | Per-user (IMemoryCache) |
| `oauth-connections:` | AuthGrpcService | Per-user (IMemoryCache) |
| `prefetch:` / `shared:` | PrefetchCacheService | Request-scoped (IMemoryCache) |
| *(partial renders)* | PartialRenderCache | Private MemoryCache, ~10 s |
