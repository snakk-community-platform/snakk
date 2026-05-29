using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;
using Snakk.VBulletinImporter.Models;

namespace Snakk.VBulletinImporter.Services;

/// <summary>
/// Writes imported data to Snakk's PostgreSQL database using Npgsql COPY for bulk performance.
/// Returns ID mappings (vBulletin ID → Snakk auto-increment ID) for FK resolution.
/// </summary>
public partial class SnakkWriter(string connectionString)
{
    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static DateTime FromUnix(int ts) => ts > 0 ? Epoch.AddSeconds(ts) : Epoch;
    private static DateTime? FromUnixNullable(int ts) => ts > 0 ? Epoch.AddSeconds(ts) : null;

    private static string NewUlid() => Ulid.NewUlid().ToString();

    private static string Sha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input.ToLowerInvariant().Trim()));
        return Convert.ToHexString(bytes).ToLower();
    }

    private static string? MapTimezone(string offset) => offset.Trim() switch
    {
        "-12" => "Etc/GMT+12",
        "-11" => "Etc/GMT+11",
        "-10" => "Pacific/Honolulu",
        "-9"  => "America/Anchorage",
        "-8"  => "America/Los_Angeles",
        "-7"  => "America/Denver",
        "-6"  => "America/Chicago",
        "-5"  => "America/New_York",
        "-4"  => "America/Halifax",
        "-3"  => "America/Sao_Paulo",
        "-2"  => "Etc/GMT+2",
        "-1"  => "Atlantic/Azores",
        "0"   => "UTC",
        "+1"  => "Europe/Oslo",
        "+2"  => "Europe/Helsinki",
        "+3"  => "Europe/Moscow",
        "+4"  => "Asia/Dubai",
        "+5"  => "Asia/Karachi",
        "+6"  => "Asia/Dhaka",
        "+7"  => "Asia/Bangkok",
        "+8"  => "Asia/Shanghai",
        "+9"  => "Asia/Tokyo",
        "+10" => "Australia/Sydney",
        "+11" => "Pacific/Noumea",
        "+12" => "Pacific/Auckland",
        _     => null
    };

    private static string? BuildTags(string prefixId, string tagList)
    {
        var tags = new List<string>();
        if (!string.IsNullOrWhiteSpace(prefixId))
            tags.Add(prefixId.Trim().ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(tagList))
            tags.AddRange(tagList.Split(',').Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0));
        return tags.Count > 0 ? string.Join(",", tags.Distinct()) : null;
    }

    private static string? BuildExcerpt(string content)
    {
        if (string.IsNullOrEmpty(content)) return null;
        var plain = MarkdownLinkRegex().Replace(content, "$1");
        plain = MarkdownFormatRegex().Replace(plain, "");
        plain = WhitespaceRegex().Replace(plain.Trim(), " ");
        if (plain.Length == 0) return null;
        return plain.Length > 200 ? plain[..197] + "..." : plain;
    }

    [GeneratedRegex(@"\[([^\]]+)\]\([^\)]+\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"\*{1,3}|`{1,3}|~~|>")]
    private static partial Regex MarkdownFormatRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex WhitespaceRegex();

    private static readonly Dictionary<int, int> VbGroupToSnakkRole = new()
    {
        [6] = 1, // Administrator → GlobalAdmin
        [5] = 2, // Super Moderators → CommunityAdmin
        [7] = 3, // Moderator → CommunityMod
    };

    // ─── Community ────────────────────────────────────────────────────────────

    /// <summary>Returns (Id, PublicId) of the created community.</summary>
    public async Task<(int Id, string PublicId)> WriteCommunityAsync()
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var publicId = NewUlid();
        var sql = @"
            INSERT INTO ""Community"" (""PublicId"", ""Slug"", ""Name"", ""Description"", ""CreatedAt"", ""VisibilityId"",
                                       ""ExposeToPlatformFeed"", ""IsAdultOnly"", ""IsRestricted"",
                                       ""HideAdultDiscussionsFromLists"", ""IsDeleted"", ""AvatarRevision"",
                                       ""HubCount"", ""SpaceCount"", ""DiscussionCount"", ""PostCount"", ""ReactionCount"")
            VALUES (@pid, @slug, @name, @desc, @created, 1, true, false, false, false, false, 0, 0, 0, 0, 0, 0)
            RETURNING ""Id""";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("pid", publicId);
        cmd.Parameters.AddWithValue("slug", "freakforum");
        cmd.Parameters.AddWithValue("name", "Freakforum");
        cmd.Parameters.AddWithValue("desc", "Imported from vBulletin (freakforum.nu / freak.no)");
        cmd.Parameters.AddWithValue("created", DateTime.UtcNow);

        var id = (int)(await cmd.ExecuteScalarAsync())!;
        return (id, publicId);
    }

    // ─── Users ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Bulk-inserts users via COPY (placeholder emails for mapping), then updates with real emails.
    /// Returns mapping: vBulletin userid → Snakk User.Id
    /// </summary>
    public async Task<Dictionary<int, int>> WriteUsersAsync(List<VBUser> users)
    {
        var mapping = new Dictionary<int, int>(users.Count);
        var importTime = DateTime.UtcNow;

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await ExecuteNonQuery(conn, @"DROP INDEX IF EXISTS ""IX_User_Email""");

        {
            var copySql = @"COPY ""User"" (""PublicId"", ""DisplayName"", ""Email"", ""PasswordHash"",
                            ""LegacyPasswordHash"", ""LegacyPasswordSalt"",
                            ""EmailVerified"", ""CreatedAt"", ""IsDeleted"", ""LastSeenAt"", ""LastLoginAt"",
                            ""AutoFollowOnReply"", ""TwoFactorEnabled"", ""AvatarRevision"",
                            ""NeedsProfileSetup"", ""AuthVersion"", ""AuthVersionUpdatedAt"",
                            ""ReplyCount"", ""DiscussionCount"", ""FollowerCount"", ""UnreadNotificationCount"",
                            ""FailedLoginAttempts"", ""IsDisplayNameLocked"", ""HidePresence"",
                            ""AdultPreviewImageMode"", ""Timezone"")
                FROM STDIN (FORMAT BINARY)";
            await using var writer = await conn.BeginBinaryImportAsync(copySql);

            foreach (var u in users)
            {
                var placeholderEmail = $"{u.UserId}@imported.freakforum.nu";

                await writer.StartRowAsync();
                await writer.WriteAsync(NewUlid(), NpgsqlDbType.Text);                              // PublicId
                await writer.WriteAsync(u.Username.Trim(), NpgsqlDbType.Text);                      // DisplayName
                await writer.WriteAsync(placeholderEmail, NpgsqlDbType.Text);                       // Email (placeholder)
                await writer.WriteNullAsync();                                                       // PasswordHash (null — use legacy)
                if (string.IsNullOrEmpty(u.Password))
                    await writer.WriteNullAsync();
                else
                    await writer.WriteAsync(u.Password, NpgsqlDbType.Text);                         // LegacyPasswordHash
                if (string.IsNullOrEmpty(u.Salt))
                    await writer.WriteNullAsync();
                else
                    await writer.WriteAsync(u.Salt, NpgsqlDbType.Text);                             // LegacyPasswordSalt
                await writer.WriteAsync(false, NpgsqlDbType.Boolean);                               // EmailVerified
                await writer.WriteAsync(FromUnix(u.JoinDate), NpgsqlDbType.TimestampTz);            // CreatedAt
                await writer.WriteAsync(false, NpgsqlDbType.Boolean);                               // IsDeleted
                var lastSeen = FromUnixNullable(u.LastActivity);
                if (lastSeen.HasValue) await writer.WriteAsync(lastSeen.Value, NpgsqlDbType.TimestampTz);
                else await writer.WriteNullAsync();                                                  // LastSeenAt
                var lastLogin = FromUnixNullable(u.LastPost);
                if (lastLogin.HasValue) await writer.WriteAsync(lastLogin.Value, NpgsqlDbType.TimestampTz);
                else await writer.WriteNullAsync();                                                  // LastLoginAt
                await writer.WriteAsync(true, NpgsqlDbType.Boolean);                                // AutoFollowOnReply
                await writer.WriteAsync(false, NpgsqlDbType.Boolean);                               // TwoFactorEnabled
                await writer.WriteAsync(0, NpgsqlDbType.Integer);                                   // AvatarRevision
                await writer.WriteAsync(true, NpgsqlDbType.Boolean);                                // NeedsProfileSetup
                await writer.WriteAsync(1L, NpgsqlDbType.Bigint);                                   // AuthVersion
                await writer.WriteAsync(importTime, NpgsqlDbType.TimestampTz);                      // AuthVersionUpdatedAt
                await writer.WriteAsync(u.Posts, NpgsqlDbType.Integer);                             // ReplyCount
                await writer.WriteAsync(0, NpgsqlDbType.Integer);                                   // DiscussionCount
                await writer.WriteAsync(0, NpgsqlDbType.Integer);                                   // FollowerCount
                await writer.WriteAsync(0, NpgsqlDbType.Integer);                                   // UnreadNotificationCount
                await writer.WriteAsync(0, NpgsqlDbType.Integer);                                   // FailedLoginAttempts
                await writer.WriteAsync(false, NpgsqlDbType.Boolean);                               // IsDisplayNameLocked
                await writer.WriteAsync(false, NpgsqlDbType.Boolean);                               // HidePresence
                await writer.WriteAsync(0, NpgsqlDbType.Integer);                                   // AdultPreviewImageMode
                var tz = MapTimezone(u.TimezoneOffset);
                if (tz != null) await writer.WriteAsync(tz, NpgsqlDbType.Text);
                else await writer.WriteNullAsync();                                                  // Timezone
            }

            await writer.CompleteAsync();
        }

        await ExecuteNonQuery(conn, @"CREATE INDEX IF NOT EXISTS ""IX_User_Email"" ON ""User"" (""Email"")");

        // Build vbUserId → snakkId mapping via placeholder email pattern
        var mapSql = @"SELECT ""Id"", ""Email"" FROM ""User"" WHERE ""Email"" LIKE '%@imported.freakforum.nu'";
        await using (var mapCmd = new NpgsqlCommand(mapSql, conn))
        await using (var mapReader = await mapCmd.ExecuteReaderAsync())
        {
            while (await mapReader.ReadAsync())
            {
                var snakkId = mapReader.GetInt32(0);
                var email = mapReader.GetString(1);
                var at = email.IndexOf('@');
                if (at > 0 && int.TryParse(email[..at], out var vbId))
                    mapping[vbId] = snakkId;
            }
        }

        // UPDATE real emails (deduplicated: lowest vbUserId wins on duplicate email)
        var emailUpdates = users
            .Where(u => !string.IsNullOrWhiteSpace(u.Email) && u.Email != "http://")
            .GroupBy(u => u.Email.Trim().ToLowerInvariant())
            .Select(g => g.OrderBy(u => u.UserId).First())
            .Where(u => mapping.ContainsKey(u.UserId))
            .ToList();

        if (emailUpdates.Count > 0)
        {
            await ExecuteNonQuery(conn, @"CREATE TEMP TABLE _email_upd (sid INT, email TEXT, hash TEXT) ON COMMIT DROP");
            await using var emailWriter = await conn.BeginBinaryImportAsync(
                @"COPY _email_upd (sid, email, hash) FROM STDIN (FORMAT BINARY)");
            foreach (var u in emailUpdates)
            {
                var realEmail = u.Email.Trim().ToLowerInvariant();
                await emailWriter.StartRowAsync();
                await emailWriter.WriteAsync(mapping[u.UserId], NpgsqlDbType.Integer);
                await emailWriter.WriteAsync(realEmail, NpgsqlDbType.Text);
                await emailWriter.WriteAsync(Sha256(realEmail), NpgsqlDbType.Text);
            }
            await emailWriter.CompleteAsync();
            await ExecuteNonQuery(conn, @"UPDATE ""User"" u SET ""Email"" = t.email, ""EmailHash"" = t.hash
                FROM _email_upd t WHERE u.""Id"" = t.sid");
        }

        return mapping;
    }

    // ─── User Roles ───────────────────────────────────────────────────────────

    public async Task WriteUserRolesAsync(List<VBUser> users, Dictionary<int, int> userMapping, int communityId)
    {
        var now = DateTime.UtcNow;
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var sql = @"INSERT INTO ""UserRole"" (""PublicId"", ""UserId"", ""RoleId"", ""CommunityId"", ""HubId"", ""SpaceId"",
                                              ""AssignedByUserId"", ""AssignedAt"")
                    VALUES (@pid, @uid, @role, @cid, NULL, NULL, @uid, @assigned) ON CONFLICT DO NOTHING";

        foreach (var u in users)
        {
            if (!userMapping.TryGetValue(u.UserId, out var snakkId)) continue;

            var groupIds = new HashSet<int> { u.UserGroupId };
            foreach (var part in u.MemberGroupIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part.Trim(), out var gid)) groupIds.Add(gid);

            foreach (var gid in groupIds)
            {
                if (!VbGroupToSnakkRole.TryGetValue(gid, out var roleId)) continue;
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("pid", NewUlid());
                cmd.Parameters.AddWithValue("uid", snakkId);
                cmd.Parameters.AddWithValue("role", roleId);
                cmd.Parameters.AddWithValue("cid", roleId == 1 ? DBNull.Value : communityId);
                cmd.Parameters.AddWithValue("assigned", now);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    // ─── User Social Links ────────────────────────────────────────────────────

    public async Task WriteUserSocialLinksAsync(List<VBUser> users, Dictionary<int, int> userMapping)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var sql = @"INSERT INTO ""UserSocialLink"" (""UserId"", ""Platform"", ""Username"")
                    VALUES (@uid, @platform, @username) ON CONFLICT DO NOTHING";

        foreach (var u in users)
        {
            if (!userMapping.TryGetValue(u.UserId, out var snakkId)) continue;
            foreach (var (platform, value) in new[] { ("website", u.Homepage), ("skype", u.Skype), ("icq", u.Icq) })
            {
                if (string.IsNullOrWhiteSpace(value) || value == "http://" || value == "https://") continue;
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("uid", snakkId);
                cmd.Parameters.AddWithValue("platform", platform);
                cmd.Parameters.AddWithValue("username", value.Trim());
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    // ─── Hubs ─────────────────────────────────────────────────────────────────

    public async Task<Dictionary<int, int>> WriteHubsAsync(List<VBForum> categories, int communityId)
    {
        var mapping = new Dictionary<int, int>(categories.Count);
        var slugs = new HashSet<string>();
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var sql = @"INSERT INTO ""Hub"" (""PublicId"", ""Slug"", ""CommunityId"", ""Name"", ""Description"", ""CreatedAt"",
                                         ""AllowAnonymousReading"", ""RequireEmailConfirmation"", ""IsRestricted"",
                                         ""IsAdultOnly"", ""IsDeleted"", ""AvatarRevision"",
                                         ""SpaceCount"", ""DiscussionCount"", ""PostCount"", ""ReactionCount"")
                    VALUES (@pid, @slug, @cid, @name, @desc, @created, true, false, false, false, false, 0, 0, 0, 0, 0)
                    RETURNING ""Id""";

        foreach (var cat in categories)
        {
            var slug = SlugGenerator.GenerateUnique(cat.Title, slugs);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("pid", NewUlid());
            cmd.Parameters.AddWithValue("slug", slug);
            cmd.Parameters.AddWithValue("cid", communityId);
            cmd.Parameters.AddWithValue("name", cat.Title);
            cmd.Parameters.AddWithValue("desc", (object?)cat.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("created", DateTime.UtcNow);
            mapping[cat.ForumId] = (int)(await cmd.ExecuteScalarAsync())!;
        }

        return mapping;
    }

    // ─── Spaces ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes spaces (forums). Flattens sub-forums under their grandparent hub.
    /// Returns (vbForumId→snakkSpaceId, snakkSpaceId→snakkHubId).
    /// </summary>
    public async Task<(Dictionary<int, int> VbToSnakk, Dictionary<int, int> SnakkToHub)> WriteSpacesAsync(
        List<VBForum> allForums, Dictionary<int, int> hubMapping)
    {
        var vbToSnakk = new Dictionary<int, int>();
        var snakkToHub = new Dictionary<int, int>();
        var slugs = new HashSet<string>();

        var forumById = allForums.ToDictionary(f => f.ForumId);
        var forumToHub = new Dictionary<int, int>();
        foreach (var f in allForums)
        {
            if (hubMapping.ContainsKey(f.ForumId)) continue;
            var current = f;
            for (var depth = 0; depth < 10; depth++)
            {
                if (hubMapping.TryGetValue(current.ParentId, out var resolvedHub))
                {
                    forumToHub[f.ForumId] = resolvedHub;
                    break;
                }
                if (!forumById.TryGetValue(current.ParentId, out var parent)) break;
                current = parent;
            }
        }

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var sql = @"INSERT INTO ""Space"" (""PublicId"", ""Slug"", ""HubId"", ""Name"", ""Description"", ""CreatedAt"",
                                           ""AllowAnonymousReading"", ""RequireEmailConfirmation"", ""IsRestricted"",
                                           ""IsAdultOnly"", ""AllowsAdultContent"", ""AutoParagraphEnabled"",
                                           ""IsDeleted"", ""AvatarRevision"",
                                           ""DiscussionCount"", ""PostCount"", ""ReactionCount"", ""FollowerCount"")
                    VALUES (@pid, @slug, @hid, @name, @desc, @created, true, false, @restricted, false, false, true, false, 0, 0, 0, 0, 0)
                    RETURNING ""Id""";

        foreach (var forum in allForums)
        {
            if (hubMapping.ContainsKey(forum.ForumId)) continue;
            if (!forumToHub.TryGetValue(forum.ForumId, out var hubId))
            {
                Console.WriteLine($"  WARN: Forum '{forum.Title}' (id={forum.ForumId}) could not be resolved to a hub. Skipping.");
                continue;
            }

            var slug = SlugGenerator.GenerateUnique(forum.Title, slugs);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("pid", NewUlid());
            cmd.Parameters.AddWithValue("slug", slug);
            cmd.Parameters.AddWithValue("hid", hubId);
            cmd.Parameters.AddWithValue("name", forum.Title);
            cmd.Parameters.AddWithValue("desc", (object?)forum.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("created", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("restricted", !forum.IsPublic);

            var spaceId = (int)(await cmd.ExecuteScalarAsync())!;
            vbToSnakk[forum.ForumId] = spaceId;
            snakkToHub[spaceId] = hubId;
        }

        return (vbToSnakk, snakkToHub);
    }

    // ─── Discussions ──────────────────────────────────────────────────────────

    public async Task<Dictionary<int, (int SnakkId, int SpaceId, int HubId, int CommunityId)>> WriteDiscussionsAsync(
        List<VBThread> threads,
        Dictionary<int, int> spaceMapping,
        Dictionary<int, int> spaceToHub,
        int communityId,
        Dictionary<int, int> userMapping)
    {
        var mapping = new Dictionary<int, (int, int, int, int)>();
        var slugs = new HashSet<string>();

        var valid = new List<(VBThread T, string PublicId, string Slug, int SpaceId, int HubId, int UserId)>();
        foreach (var t in threads)
        {
            if (!spaceMapping.TryGetValue(t.ForumId, out var spaceId)) continue;
            if (!spaceToHub.TryGetValue(spaceId, out var hubId)) continue;
            if (!userMapping.TryGetValue(t.PostUserId, out var userId)) continue;
            valid.Add((t, NewUlid(), SlugGenerator.GenerateUnique(t.Title, slugs), spaceId, hubId, userId));
        }

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        {
            var copySql = @"COPY ""Discussion"" (""PublicId"", ""Slug"", ""Title"", ""Type"", ""CreatedAt"",
                                                  ""IsDeleted"", ""DeletedAt"", ""LastActivityAt"",
                                                  ""IsPinned"", ""IsLocked"", ""IsAdultOnly"", ""WasNormalized"",
                                                  ""PostCount"", ""ReactionCount"", ""FollowerCount"",
                                                  ""TrendScore"", ""EngagementScore"", ""Tags"",
                                                  ""SpaceId"", ""HubId"", ""CommunityId"", ""CreatedByUserId"")
                            FROM STDIN (FORMAT BINARY)";
            await using var writer = await conn.BeginBinaryImportAsync(copySql);

            foreach (var (t, publicId, slug, spaceId, hubId, userId) in valid)
            {
                var created = FromUnix(t.DateLine);
                var isDeleted = t.Visible != 1;
                var type = t.PollId > 0 ? 2 : !string.IsNullOrEmpty(t.NewsUrl) ? 4 : 0;
                var tags = BuildTags(t.PrefixId, t.TagList);

                await writer.StartRowAsync();
                await writer.WriteAsync(publicId, NpgsqlDbType.Text);
                await writer.WriteAsync(slug, NpgsqlDbType.Text);
                await writer.WriteAsync(t.Title.Trim(), NpgsqlDbType.Text);
                await writer.WriteAsync(type, NpgsqlDbType.Integer);
                await writer.WriteAsync(created, NpgsqlDbType.TimestampTz);
                await writer.WriteAsync(isDeleted, NpgsqlDbType.Boolean);
                if (isDeleted) await writer.WriteAsync(created, NpgsqlDbType.TimestampTz);
                else await writer.WriteNullAsync();
                await writer.WriteAsync(FromUnix(t.LastPost), NpgsqlDbType.TimestampTz);
                await writer.WriteAsync(t.Sticky > 0, NpgsqlDbType.Boolean);
                await writer.WriteAsync(t.Open == 0, NpgsqlDbType.Boolean);
                await writer.WriteAsync(false, NpgsqlDbType.Boolean);
                await writer.WriteAsync(false, NpgsqlDbType.Boolean);
                await writer.WriteAsync(t.ReplyCount + 1, NpgsqlDbType.Integer);
                await writer.WriteAsync(0, NpgsqlDbType.Integer);
                await writer.WriteAsync(0, NpgsqlDbType.Integer);
                await writer.WriteAsync(0.0, NpgsqlDbType.Double);
                await writer.WriteAsync(t.ReplyCount + 1, NpgsqlDbType.Integer);
                if (tags != null) await writer.WriteAsync(tags, NpgsqlDbType.Text);
                else await writer.WriteNullAsync();
                await writer.WriteAsync(spaceId, NpgsqlDbType.Integer);
                await writer.WriteAsync(hubId, NpgsqlDbType.Integer);
                await writer.WriteAsync(communityId, NpgsqlDbType.Integer);
                await writer.WriteAsync(userId, NpgsqlDbType.Integer);
            }

            await writer.CompleteAsync();
        }

        var publicIdToVbId = valid.ToDictionary(v => v.PublicId, v => v.T.ThreadId);
        var publicIdToMeta = valid.ToDictionary(v => v.PublicId, v => (v.SpaceId, v.HubId));

        var mapSql = @"SELECT ""Id"", ""PublicId"" FROM ""Discussion"" WHERE ""PublicId"" = ANY(@pids)";
        await using var mapCmd = new NpgsqlCommand(mapSql, conn);
        mapCmd.Parameters.AddWithValue("pids", valid.Select(v => v.PublicId).ToArray());
        await using var reader = await mapCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var snakkId = reader.GetInt32(0);
            var publicId = reader.GetString(1);
            if (publicIdToVbId.TryGetValue(publicId, out var vbId) && publicIdToMeta.TryGetValue(publicId, out var meta))
                mapping[vbId] = (snakkId, meta.SpaceId, meta.HubId, communityId);
        }

        return mapping;
    }

    // ─── Discussion Type Extensions ───────────────────────────────────────────

    public async Task WriteDiscussionTypeLinksAsync(
        List<VBThread> threads,
        Dictionary<int, (int SnakkId, int SpaceId, int HubId, int CommunityId)> discussionMapping)
    {
        var linkThreads = threads
            .Where(t => !string.IsNullOrEmpty(t.NewsUrl) && t.PollId == 0 && discussionMapping.ContainsKey(t.ThreadId))
            .ToList();
        if (linkThreads.Count == 0) return;

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var sql = @"INSERT INTO ""DiscussionTypeLink"" (""DiscussionId"", ""Url"", ""IsInternal"")
                    VALUES (@did, @url, false) ON CONFLICT DO NOTHING";
        foreach (var t in linkThreads)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("did", discussionMapping[t.ThreadId].SnakkId);
            cmd.Parameters.AddWithValue("url", t.NewsUrl.Trim());
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // ─── Polls ────────────────────────────────────────────────────────────────

    public async Task<Dictionary<(int PollId, int OptionIdx), int>> WritePollsAsync(
        List<VBPoll> polls,
        Dictionary<int, (int SnakkId, int SpaceId, int HubId, int CommunityId)> discussionMapping)
    {
        var optionMapping = new Dictionary<(int, int), int>();
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var pollSql = @"INSERT INTO ""DiscussionTypePoll"" (""DiscussionId"", ""AllowMultipleChoices"", ""AllowChangeVote"", ""VotesVisible"", ""IsSegmented"")
                        VALUES (@did, @multi, false, true, false) RETURNING ""Id""";
        var optSql = @"INSERT INTO ""DiscussionTypePollOption"" (""PollId"", ""Text"", ""DisplayOrder"", ""VoteCount"")
                       VALUES (@pid, @text, @order, @votes) RETURNING ""Id""";

        foreach (var poll in polls)
        {
            if (!discussionMapping.TryGetValue(poll.ThreadId, out var disc)) continue;

            await using var pollCmd = new NpgsqlCommand(pollSql, conn);
            pollCmd.Parameters.AddWithValue("did", disc.SnakkId);
            pollCmd.Parameters.AddWithValue("multi", poll.AllowMultiple);
            var snakkPollId = (int)(await pollCmd.ExecuteScalarAsync())!;

            for (var i = 0; i < poll.Options.Length; i++)
            {
                var optText = poll.Options[i].Trim();
                if (string.IsNullOrEmpty(optText)) continue;

                await using var optCmd = new NpgsqlCommand(optSql, conn);
                optCmd.Parameters.AddWithValue("pid", snakkPollId);
                optCmd.Parameters.AddWithValue("text", optText.Length > 500 ? optText[..500] : optText);
                optCmd.Parameters.AddWithValue("order", i);
                optCmd.Parameters.AddWithValue("votes", i < poll.VoteCounts.Length ? poll.VoteCounts[i] : 0);
                var snakkOptionId = (int)(await optCmd.ExecuteScalarAsync())!;
                optionMapping[(poll.PollId, i + 1)] = snakkOptionId;
            }
        }

        return optionMapping;
    }

    public async Task WritePollVotesBatchAsync(
        List<VBPollVote> votes,
        Dictionary<(int PollId, int OptionIdx), int> optionMapping,
        Dictionary<int, int> userMapping)
    {
        var valid = new List<(int OptionId, int UserId, DateTime VotedAt)>();
        var seen = new HashSet<(int, int)>();

        foreach (var v in votes)
        {
            if (!optionMapping.TryGetValue((v.PollId, v.VoteOption), out var optionId)) continue;
            if (!userMapping.TryGetValue(v.UserId, out var userId)) continue;
            if (!seen.Add((optionId, userId))) continue;
            valid.Add((optionId, userId, FromUnix(v.VoteDate)));
        }

        if (valid.Count == 0) return;

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var copySql = @"COPY ""DiscussionTypePollVote"" (""OptionId"", ""UserId"", ""VotedAt"") FROM STDIN (FORMAT BINARY)";
        await using var writer = await conn.BeginBinaryImportAsync(copySql);
        foreach (var (optionId, userId, votedAt) in valid)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(optionId, NpgsqlDbType.Integer);
            await writer.WriteAsync(userId, NpgsqlDbType.Integer);
            await writer.WriteAsync(votedAt, NpgsqlDbType.TimestampTz);
        }
        await writer.CompleteAsync();
    }

    // ─── Posts ────────────────────────────────────────────────────────────────

    public async Task<Dictionary<int, int>> WritePostBatchAsync(
        List<VBPost> posts,
        Dictionary<int, (int SnakkId, int SpaceId, int HubId, int CommunityId)> discussionMapping,
        Dictionary<int, int> userMapping,
        HashSet<int> firstPostIds)
    {
        var mapping = new Dictionary<int, int>(posts.Count);

        var valid = new List<(VBPost P, string PublicId, int DiscussionId, int SpaceId, int HubId, int CommunityId, int UserId)>();
        foreach (var p in posts)
        {
            if (!discussionMapping.TryGetValue(p.ThreadId, out var disc)) continue;
            if (!userMapping.TryGetValue(p.UserId, out var userId)) continue;
            valid.Add((p, NewUlid(), disc.SnakkId, disc.SpaceId, disc.HubId, disc.CommunityId, userId));
        }

        if (valid.Count == 0) return mapping;

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        {
            var copySql = @"COPY ""Post"" (""PublicId"", ""Content"", ""RenderedContent"", ""CreatedAt"",
                                           ""IsDeleted"", ""DeletedAt"", ""EditedAt"",
                                           ""IsFirstPost"", ""HasCodeBlock"", ""RevisionCount"", ""ReactionCount"",
                                           ""IsUsersFirstPostInDiscussion"", ""IsUsersFirstPostInSpace"",
                                           ""IsOp"", ""IsNecro"", ""IsMilestone"", ""WasNormalized"",
                                           ""DiscussionId"", ""SpaceId"", ""HubId"", ""CommunityId"",
                                           ""CreatedByUserId"", ""PlainTextExcerpt"")
                            FROM STDIN (FORMAT BINARY)";
            await using var writer = await conn.BeginBinaryImportAsync(copySql);

            foreach (var (p, publicId, discussionId, spaceId, hubId, communityId, userId) in valid)
            {
                var created = FromUnix(p.DateLine);
                var isDeleted = p.Visible != 1;
                var isFirst = firstPostIds.Contains(p.PostId);
                var content = BbCodeConverter.Convert(p.PageText);

                await writer.StartRowAsync();
                await writer.WriteAsync(publicId, NpgsqlDbType.Text);
                await writer.WriteAsync(content, NpgsqlDbType.Text);
                await writer.WriteAsync(content, NpgsqlDbType.Text);
                await writer.WriteAsync(created, NpgsqlDbType.TimestampTz);
                await writer.WriteAsync(isDeleted, NpgsqlDbType.Boolean);
                if (isDeleted) await writer.WriteAsync(created, NpgsqlDbType.TimestampTz);
                else await writer.WriteNullAsync();
                if (p.LastEdit > 0) await writer.WriteAsync(FromUnix(p.LastEdit), NpgsqlDbType.TimestampTz);
                else await writer.WriteNullAsync();
                await writer.WriteAsync(isFirst, NpgsqlDbType.Boolean);
                await writer.WriteAsync(false, NpgsqlDbType.Boolean);
                await writer.WriteAsync(0, NpgsqlDbType.Integer);
                await writer.WriteAsync(0, NpgsqlDbType.Integer);
                await writer.WriteAsync(false, NpgsqlDbType.Boolean);
                await writer.WriteAsync(false, NpgsqlDbType.Boolean);
                await writer.WriteAsync(isFirst, NpgsqlDbType.Boolean);
                await writer.WriteAsync(false, NpgsqlDbType.Boolean);
                await writer.WriteAsync(false, NpgsqlDbType.Boolean);
                await writer.WriteAsync(false, NpgsqlDbType.Boolean);
                await writer.WriteAsync(discussionId, NpgsqlDbType.Integer);
                await writer.WriteAsync(spaceId, NpgsqlDbType.Integer);
                await writer.WriteAsync(hubId, NpgsqlDbType.Integer);
                await writer.WriteAsync(communityId, NpgsqlDbType.Integer);
                await writer.WriteAsync(userId, NpgsqlDbType.Integer);
                var excerpt = BuildExcerpt(content);
                if (excerpt != null) await writer.WriteAsync(excerpt, NpgsqlDbType.Text);
                else await writer.WriteNullAsync();
            }

            await writer.CompleteAsync();
        }

        var publicIdToVbId = valid.ToDictionary(v => v.PublicId, v => v.P.PostId);
        var mapSql = @"SELECT ""Id"", ""PublicId"" FROM ""Post"" WHERE ""PublicId"" = ANY(@pids)";
        await using var mapCmd = new NpgsqlCommand(mapSql, conn);
        mapCmd.Parameters.AddWithValue("pids", valid.Select(v => v.PublicId).ToArray());
        await using var mapReader = await mapCmd.ExecuteReaderAsync();
        while (await mapReader.ReadAsync())
        {
            var snakkId = mapReader.GetInt32(0);
            var pid = mapReader.GetString(1);
            if (publicIdToVbId.TryGetValue(pid, out var vbId)) mapping[vbId] = snakkId;
        }

        return mapping;
    }

    // ─── Reactions ────────────────────────────────────────────────────────────

    public async Task WriteReactionBatchAsync(
        List<VBPostVote> votes, Dictionary<int, int> postMapping, Dictionary<int, int> userMapping)
    {
        var valid = new List<(VBPostVote V, int PostId, int UserId)>();
        var seen = new HashSet<(int, int)>();

        foreach (var v in votes)
        {
            if (!postMapping.TryGetValue(v.PostId, out var postId)) continue;
            if (!userMapping.TryGetValue(v.UserId, out var userId)) continue;
            if (!seen.Add((postId, userId))) continue;
            valid.Add((v, postId, userId));
        }

        if (valid.Count == 0) return;

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var copySql = @"COPY ""Reaction"" (""PublicId"", ""PostId"", ""UserId"", ""TypeId"", ""CreatedAt"") FROM STDIN (FORMAT BINARY)";
        await using var writer = await conn.BeginBinaryImportAsync(copySql);
        foreach (var (v, postId, userId) in valid)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(NewUlid(), NpgsqlDbType.Text);
            await writer.WriteAsync(postId, NpgsqlDbType.Integer);
            await writer.WriteAsync(userId, NpgsqlDbType.Integer);
            await writer.WriteAsync(1, NpgsqlDbType.Integer);
            await writer.WriteAsync(FromUnix(v.DateLine), NpgsqlDbType.TimestampTz);
        }
        await writer.CompleteAsync();
    }

    // ─── Follows ──────────────────────────────────────────────────────────────

    public async Task WriteDiscussionFollowsAsync(
        List<VBSubscription> subscriptions,
        Dictionary<int, (int SnakkId, int SpaceId, int HubId, int CommunityId)> discussionMapping,
        Dictionary<int, int> userMapping)
    {
        var valid = new List<(int UserId, int DiscussionId, int LevelId)>();
        var seen = new HashSet<(int, int)>();

        foreach (var s in subscriptions)
        {
            if (!userMapping.TryGetValue(s.UserId, out var userId)) continue;
            if (!discussionMapping.TryGetValue(s.ThreadId, out var disc)) continue;
            if (!seen.Add((userId, disc.SnakkId))) continue;
            valid.Add((userId, disc.SnakkId, s.EmailUpdate > 0 ? 2 : 1));
        }

        if (valid.Count == 0) return;

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        var now = DateTime.UtcNow;

        var copySql = @"COPY ""Follow"" (""PublicId"", ""UserId"", ""TargetTypeId"", ""LevelId"", ""DiscussionId"", ""CreatedAt"") FROM STDIN (FORMAT BINARY)";
        await using var writer = await conn.BeginBinaryImportAsync(copySql);
        foreach (var (userId, discussionId, levelId) in valid)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(NewUlid(), NpgsqlDbType.Text);
            await writer.WriteAsync(userId, NpgsqlDbType.Integer);
            await writer.WriteAsync(1, NpgsqlDbType.Integer);
            await writer.WriteAsync(levelId, NpgsqlDbType.Integer);
            await writer.WriteAsync(discussionId, NpgsqlDbType.Integer);
            await writer.WriteAsync(now, NpgsqlDbType.TimestampTz);
        }
        await writer.CompleteAsync();
    }

    public async Task WriteUserFollowsAsync(List<VBUserFollow> follows, Dictionary<int, int> userMapping)
    {
        var valid = new List<(int UserId, int FollowedUserId)>();
        var seen = new HashSet<(int, int)>();

        foreach (var f in follows)
        {
            if (!userMapping.TryGetValue(f.UserId, out var userId)) continue;
            if (!userMapping.TryGetValue(f.RelationId, out var followedId)) continue;
            if (userId == followedId) continue;
            if (!seen.Add((userId, followedId))) continue;
            valid.Add((userId, followedId));
        }

        if (valid.Count == 0) return;

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        var now = DateTime.UtcNow;

        var copySql = @"COPY ""Follow"" (""PublicId"", ""UserId"", ""TargetTypeId"", ""LevelId"", ""FollowedUserId"", ""CreatedAt"") FROM STDIN (FORMAT BINARY)";
        await using var writer = await conn.BeginBinaryImportAsync(copySql);
        foreach (var (userId, followedId) in valid)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(NewUlid(), NpgsqlDbType.Text);
            await writer.WriteAsync(userId, NpgsqlDbType.Integer);
            await writer.WriteAsync(3, NpgsqlDbType.Integer);
            await writer.WriteAsync(1, NpgsqlDbType.Integer);
            await writer.WriteAsync(followedId, NpgsqlDbType.Integer);
            await writer.WriteAsync(now, NpgsqlDbType.TimestampTz);
        }
        await writer.CompleteAsync();
    }

    // ─── Denormalized Counts ──────────────────────────────────────────────────

    public async Task UpdateDenormalizedCountsAsync()
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        Console.WriteLine("  Discussion.PostCount...");
        await ExecuteNonQuery(conn, @"UPDATE ""Discussion"" d SET ""PostCount"" = COALESCE(sub.cnt, 0)
            FROM (SELECT ""DiscussionId"", COUNT(*) cnt FROM ""Post"" WHERE NOT ""IsDeleted"" GROUP BY ""DiscussionId"") sub
            WHERE d.""Id"" = sub.""DiscussionId""");

        Console.WriteLine("  Discussion.ReactionCount...");
        await ExecuteNonQuery(conn, @"UPDATE ""Discussion"" d SET ""ReactionCount"" = COALESCE(sub.cnt, 0)
            FROM (SELECT p.""DiscussionId"", COUNT(DISTINCT r.""UserId"") cnt FROM ""Reaction"" r
                  JOIN ""Post"" p ON p.""Id"" = r.""PostId"" AND NOT p.""IsDeleted""
                  GROUP BY p.""DiscussionId"") sub WHERE d.""Id"" = sub.""DiscussionId""");

        Console.WriteLine("  Discussion.EngagementScore + FollowerCount...");
        await ExecuteNonQuery(conn, @"UPDATE ""Discussion"" SET ""EngagementScore"" = ""PostCount"" + ""ReactionCount""");
        await ExecuteNonQuery(conn, @"UPDATE ""Discussion"" d SET ""FollowerCount"" = COALESCE(sub.cnt, 0)
            FROM (SELECT ""DiscussionId"", COUNT(*) cnt FROM ""Follow"" WHERE ""TargetTypeId"" = 1 GROUP BY ""DiscussionId"") sub
            WHERE d.""Id"" = sub.""DiscussionId""");

        Console.WriteLine("  Space counts...");
        await ExecuteNonQuery(conn, @"UPDATE ""Space"" s SET ""DiscussionCount"" = COALESCE(sub.cnt, 0)
            FROM (SELECT ""SpaceId"", COUNT(*) cnt FROM ""Discussion"" WHERE NOT ""IsDeleted"" GROUP BY ""SpaceId"") sub
            WHERE s.""Id"" = sub.""SpaceId""");
        await ExecuteNonQuery(conn, @"UPDATE ""Space"" s SET ""PostCount"" = COALESCE(sub.cnt, 0)
            FROM (SELECT d.""SpaceId"", COUNT(*) cnt FROM ""Post"" p
                  JOIN ""Discussion"" d ON d.""Id"" = p.""DiscussionId"" AND NOT d.""IsDeleted""
                  WHERE NOT p.""IsDeleted"" GROUP BY d.""SpaceId"") sub WHERE s.""Id"" = sub.""SpaceId""");
        await ExecuteNonQuery(conn, @"UPDATE ""Space"" s SET ""ReactionCount"" = COALESCE(sub.cnt, 0)
            FROM (SELECT d.""SpaceId"", COUNT(*) cnt FROM ""Reaction"" r
                  JOIN ""Post"" p ON p.""Id"" = r.""PostId"" AND NOT p.""IsDeleted""
                  JOIN ""Discussion"" d ON d.""Id"" = p.""DiscussionId"" AND NOT d.""IsDeleted""
                  GROUP BY d.""SpaceId"") sub WHERE s.""Id"" = sub.""SpaceId""");
        await ExecuteNonQuery(conn, @"UPDATE ""Space"" s SET ""FollowerCount"" = COALESCE(sub.cnt, 0)
            FROM (SELECT ""SpaceId"", COUNT(*) cnt FROM ""Follow"" WHERE ""TargetTypeId"" = 2 GROUP BY ""SpaceId"") sub
            WHERE s.""Id"" = sub.""SpaceId""");

        Console.WriteLine("  Hub counts...");
        await ExecuteNonQuery(conn, @"UPDATE ""Hub"" h
            SET ""SpaceCount"" = COALESCE(sub.sc, 0), ""DiscussionCount"" = COALESCE(sub.dc, 0),
                ""PostCount"" = COALESCE(sub.pc, 0), ""ReactionCount"" = COALESCE(sub.rc, 0)
            FROM (SELECT ""HubId"", COUNT(*) sc, SUM(""DiscussionCount"") dc,
                         SUM(""PostCount"") pc, SUM(""ReactionCount"") rc
                  FROM ""Space"" WHERE NOT ""IsDeleted"" GROUP BY ""HubId"") sub WHERE h.""Id"" = sub.""HubId""");

        Console.WriteLine("  Community counts...");
        await ExecuteNonQuery(conn, @"UPDATE ""Community"" c
            SET ""HubCount"" = COALESCE(sub.hc, 0), ""SpaceCount"" = COALESCE(sub.sc, 0),
                ""DiscussionCount"" = COALESCE(sub.dc, 0), ""PostCount"" = COALESCE(sub.pc, 0),
                ""ReactionCount"" = COALESCE(sub.rc, 0)
            FROM (SELECT ""CommunityId"", COUNT(*) hc, SUM(""SpaceCount"") sc,
                         SUM(""DiscussionCount"") dc, SUM(""PostCount"") pc, SUM(""ReactionCount"") rc
                  FROM ""Hub"" WHERE NOT ""IsDeleted"" GROUP BY ""CommunityId"") sub WHERE c.""Id"" = sub.""CommunityId""");

        Console.WriteLine("  User counts...");
        await ExecuteNonQuery(conn, @"UPDATE ""User"" u SET ""DiscussionCount"" = COALESCE(sub.cnt, 0)
            FROM (SELECT ""CreatedByUserId"", COUNT(*) cnt FROM ""Discussion"" WHERE NOT ""IsDeleted"" GROUP BY ""CreatedByUserId"") sub
            WHERE u.""Id"" = sub.""CreatedByUserId""");
        await ExecuteNonQuery(conn, @"UPDATE ""User"" u SET ""FollowerCount"" = COALESCE(sub.cnt, 0)
            FROM (SELECT ""FollowedUserId"", COUNT(*) cnt FROM ""Follow"" WHERE ""TargetTypeId"" = 3 GROUP BY ""FollowedUserId"") sub
            WHERE u.""Id"" = sub.""FollowedUserId""");

        Console.WriteLine("  Denormalized counts updated.");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static async Task ExecuteNonQuery(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.CommandTimeout = 900;
        await cmd.ExecuteNonQueryAsync();
    }
}
