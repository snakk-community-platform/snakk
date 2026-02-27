using System.Security.Cryptography;
using Npgsql;
using NpgsqlTypes;
using Snakk.VBulletinImporter.Models;

namespace Snakk.VBulletinImporter.Services;

/// <summary>
/// Writes imported data to Snakk's PostgreSQL database using Npgsql COPY for bulk performance.
/// Returns ID mappings (vBulletin ID → Snakk auto-increment ID) for FK resolution.
/// </summary>
public class SnakkWriter(string connectionString)
{
    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static DateTime FromUnix(int timestamp) =>
        timestamp > 0 ? Epoch.AddSeconds(timestamp) : Epoch;

    private static string NewUlid() => Ulid.NewUlid().ToString();

    private static string RandomPasswordHash()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return $"$IMPORTED${Convert.ToBase64String(bytes)}";
    }

    /// <summary>
    /// Creates the single "freakforum" community. Returns its auto-generated Id.
    /// </summary>
    public async Task<int> WriteCommunityAsync()
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var sql = @"
            INSERT INTO ""Community"" (""PublicId"", ""Slug"", ""Name"", ""Description"", ""CreatedAt"", ""VisibilityId"", ""ExposeToPlatformFeed"",
                                       ""IsDeleted"", ""AvatarRevision"", ""HubCount"", ""SpaceCount"", ""DiscussionCount"", ""PostCount"")
            VALUES (@pid, @slug, @name, @desc, @created, 1, true, false, 0, 0, 0, 0, 0)
            RETURNING ""Id""";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("pid", NewUlid());
        cmd.Parameters.AddWithValue("slug", "freakforum");
        cmd.Parameters.AddWithValue("name", "Freakforum");
        cmd.Parameters.AddWithValue("desc", "Imported from vBulletin (freakforum.nu / freak.no)");
        cmd.Parameters.AddWithValue("created", DateTime.UtcNow);

        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Writes users via COPY. Returns mapping: vBulletin userid → Snakk User.Id
    /// </summary>
    public async Task<Dictionary<int, int>> WriteUsersAsync(List<VBUser> users)
    {
        var mapping = new Dictionary<int, int>(users.Count);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // Temporarily drop the unique email index (vBulletin has many users with empty/duplicate emails)
        await ExecuteNonQuery(conn, @"DROP INDEX IF EXISTS ""IX_User_Email""");

        {
            var copySql = @"COPY ""User"" (""PublicId"", ""DisplayName"", ""Email"", ""PasswordHash"", ""EmailVerified"", ""CreatedAt"",
                            ""IsDeleted"", ""LastSeenAt"", ""PreferEndlessScroll"", ""AutoFollowOnReply"",
                            ""TwoFactorEnabled"", ""AvatarRevision"")
               FROM STDIN (FORMAT BINARY)";
            await using var writer = await conn.BeginBinaryImportAsync(copySql);

            foreach (var u in users)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(NewUlid(), NpgsqlDbType.Text);                          // PublicId
                await writer.WriteAsync(u.Username, NpgsqlDbType.Text);                         // DisplayName
                await writer.WriteAsync($"{u.UserId}@imported.freakforum.nu", NpgsqlDbType.Text); // Email
                await writer.WriteAsync(RandomPasswordHash(), NpgsqlDbType.Text);               // PasswordHash
                await writer.WriteAsync(false, NpgsqlDbType.Boolean);                           // EmailVerified
                await writer.WriteAsync(FromUnix(u.JoinDate), NpgsqlDbType.TimestampTz);       // CreatedAt
                await writer.WriteAsync(false, NpgsqlDbType.Boolean);                           // IsDeleted
                await writer.WriteAsync(FromUnix(u.LastActivity), NpgsqlDbType.TimestampTz);    // LastSeenAt
                await writer.WriteAsync(true, NpgsqlDbType.Boolean);                            // PreferEndlessScroll
                await writer.WriteAsync(true, NpgsqlDbType.Boolean);                            // AutoFollowOnReply
                await writer.WriteAsync(false, NpgsqlDbType.Boolean);                           // TwoFactorEnabled
                await writer.WriteAsync(0, NpgsqlDbType.Integer);                               // AvatarRevision
            }

            await writer.CompleteAsync();
        } // Dispose writer — exits COPY state

        // Recreate email index (non-unique — imported users have placeholder emails)
        await ExecuteNonQuery(conn, @"CREATE INDEX IF NOT EXISTS ""IX_User_Email"" ON ""User"" (""Email"")");

        // Build the ID mapping by querying back by email pattern
        var mapSql = @"SELECT ""Id"", ""Email"" FROM ""User"" WHERE ""Email"" LIKE '%@imported.freakforum.nu'";
        await using var mapCmd = new NpgsqlCommand(mapSql, conn);
        await using var mapReader = await mapCmd.ExecuteReaderAsync();

        while (await mapReader.ReadAsync())
        {
            var snakkId = mapReader.GetInt32(0);
            var email = mapReader.GetString(1);
            var atIndex = email.IndexOf('@');
            if (atIndex > 0 && int.TryParse(email[..atIndex], out var vbUserId))
            {
                mapping[vbUserId] = snakkId;
            }
        }

        return mapping;
    }

    /// <summary>
    /// Writes hubs (vBulletin categories). Returns mapping: vBulletin forumid → Snakk Hub.Id
    /// </summary>
    public async Task<Dictionary<int, int>> WriteHubsAsync(List<VBForum> categories, int communityId)
    {
        var mapping = new Dictionary<int, int>(categories.Count);
        var slugs = new HashSet<string>();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var sql = @"
            INSERT INTO ""Hub"" (""PublicId"", ""Slug"", ""CommunityId"", ""Name"", ""Description"", ""CreatedAt"",
                                 ""AllowAnonymousReading"", ""RequireEmailConfirmation"", ""IsDeleted"",
                                 ""AvatarRevision"", ""SpaceCount"", ""DiscussionCount"", ""PostCount"")
            VALUES (@pid, @slug, @cid, @name, @desc, @created, true, false, false, 0, 0, 0, 0)
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

            var id = (int)(await cmd.ExecuteScalarAsync())!;
            mapping[cat.ForumId] = id;
        }

        return mapping;
    }

    /// <summary>
    /// Writes spaces (vBulletin forums under categories). Returns mapping: vBulletin forumid → Snakk Space.Id
    /// </summary>
    public async Task<Dictionary<int, int>> WriteSpacesAsync(List<VBForum> forums, Dictionary<int, int> hubMapping)
    {
        var mapping = new Dictionary<int, int>(forums.Count);
        var slugs = new HashSet<string>();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var sql = @"
            INSERT INTO ""Space"" (""PublicId"", ""Slug"", ""HubId"", ""Name"", ""Description"", ""CreatedAt"",
                                   ""AllowAnonymousReading"", ""RequireEmailConfirmation"", ""IsDeleted"",
                                   ""AvatarRevision"", ""DiscussionCount"", ""PostCount"")
            VALUES (@pid, @slug, @hid, @name, @desc, @created, true, false, false, 0, 0, 0)
            RETURNING ""Id""";

        foreach (var forum in forums)
        {
            if (!hubMapping.TryGetValue(forum.ParentId, out var hubId))
            {
                Console.WriteLine($"  WARN: Forum '{forum.Title}' (id={forum.ForumId}) has parent {forum.ParentId} which is not a category/hub. Skipping.");
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

            var id = (int)(await cmd.ExecuteScalarAsync())!;
            mapping[forum.ForumId] = id;
        }

        return mapping;
    }

    /// <summary>
    /// Writes discussions (vBulletin threads) via COPY. Returns mapping: vBulletin threadid → Snakk Discussion.Id
    /// </summary>
    public async Task<Dictionary<int, int>> WriteDiscussionsAsync(
        List<VBThread> threads,
        Dictionary<int, int> spaceMapping,
        Dictionary<int, int> userMapping)
    {
        var mapping = new Dictionary<int, int>(threads.Count);
        var slugs = new HashSet<string>();

        // Filter to threads whose forum maps to a space and whose user exists
        var validThreads = new List<(VBThread Thread, string PublicId, string Slug, int SpaceId, int UserId)>();

        foreach (var t in threads)
        {
            if (!spaceMapping.TryGetValue(t.ForumId, out var spaceId))
                continue;
            if (!userMapping.TryGetValue(t.PostUserId, out var userId))
                continue;

            var slug = SlugGenerator.GenerateUnique(t.Title, slugs);
            validThreads.Add((t, NewUlid(), slug, spaceId, userId));
        }

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        {
            var copySql = @"COPY ""Discussion"" (""PublicId"", ""Slug"", ""Title"", ""CreatedAt"", ""IsDeleted"", ""DeletedAt"",
                                                 ""LastActivityAt"", ""IsPinned"", ""IsLocked"", ""PostCount"", ""ReactionCount"",
                                                 ""SpaceId"", ""CreatedByUserId"")
                            FROM STDIN (FORMAT BINARY)";
            await using var writer = await conn.BeginBinaryImportAsync(copySql);

            foreach (var (t, publicId, slug, spaceId, userId) in validThreads)
            {
                var created = FromUnix(t.DateLine);
                var lastActivity = FromUnix(t.LastPost);
                var isDeleted = t.Visible == 2;

                await writer.StartRowAsync();
                await writer.WriteAsync(publicId, NpgsqlDbType.Text);                   // PublicId
                await writer.WriteAsync(slug, NpgsqlDbType.Text);                       // Slug
                await writer.WriteAsync(t.Title, NpgsqlDbType.Text);                    // Title
                await writer.WriteAsync(created, NpgsqlDbType.TimestampTz);             // CreatedAt
                await writer.WriteAsync(isDeleted, NpgsqlDbType.Boolean);               // IsDeleted
                if (isDeleted)
                    await writer.WriteAsync(created, NpgsqlDbType.TimestampTz);         // DeletedAt
                else
                    await writer.WriteNullAsync();
                await writer.WriteAsync(lastActivity, NpgsqlDbType.TimestampTz);        // LastActivityAt
                await writer.WriteAsync(t.Sticky > 0, NpgsqlDbType.Boolean);            // IsPinned
                await writer.WriteAsync(t.Open == 0, NpgsqlDbType.Boolean);             // IsLocked
                await writer.WriteAsync(t.ReplyCount + 1, NpgsqlDbType.Integer);        // PostCount
                await writer.WriteAsync(0, NpgsqlDbType.Integer);                       // ReactionCount
                await writer.WriteAsync(spaceId, NpgsqlDbType.Integer);                 // SpaceId
                await writer.WriteAsync(userId, NpgsqlDbType.Integer);                  // CreatedByUserId
            }

            await writer.CompleteAsync();
        } // Dispose writer — exits COPY state

        // Build mapping: read back by PublicId
        var publicIdToVbId = validThreads.ToDictionary(v => v.PublicId, v => v.Thread.ThreadId);

        var mapSql = @"SELECT ""Id"", ""PublicId"" FROM ""Discussion"" WHERE ""PublicId"" = ANY(@pids)";
        await using var mapCmd = new NpgsqlCommand(mapSql, conn);
        mapCmd.Parameters.AddWithValue("pids", validThreads.Select(v => v.PublicId).ToArray());
        await using var reader = await mapCmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var snakkId = reader.GetInt32(0);
            var publicId = reader.GetString(1);
            if (publicIdToVbId.TryGetValue(publicId, out var vbThreadId))
            {
                mapping[vbThreadId] = snakkId;
            }
        }

        return mapping;
    }

    /// <summary>
    /// Writes a batch of posts via COPY. Returns mapping: vBulletin postid → Snakk Post.Id
    /// </summary>
    public async Task<Dictionary<int, int>> WritePostBatchAsync(
        List<VBPost> posts,
        Dictionary<int, int> discussionMapping,
        Dictionary<int, int> userMapping,
        HashSet<int> firstPostIds)
    {
        var mapping = new Dictionary<int, int>(posts.Count);

        var validPosts = new List<(VBPost Post, string PublicId, int DiscussionId, int UserId)>();
        foreach (var p in posts)
        {
            if (!discussionMapping.TryGetValue(p.ThreadId, out var discussionId))
                continue;
            if (!userMapping.TryGetValue(p.UserId, out var userId))
                continue;

            validPosts.Add((p, NewUlid(), discussionId, userId));
        }

        if (validPosts.Count == 0)
            return mapping;

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        {
            var copySql = @"COPY ""Post"" (""PublicId"", ""Content"", ""CreatedAt"", ""IsDeleted"", ""DeletedAt"",
                                           ""EditedAt"", ""IsFirstPost"", ""RevisionCount"",
                                           ""DiscussionId"", ""CreatedByUserId"")
                            FROM STDIN (FORMAT BINARY)";
            await using var writer = await conn.BeginBinaryImportAsync(copySql);

            foreach (var (p, publicId, discussionId, userId) in validPosts)
            {
                var created = FromUnix(p.DateLine);
                var isDeleted = p.Visible == 2;
                var isFirst = firstPostIds.Contains(p.PostId);
                var content = BbCodeConverter.Convert(p.PageText);

                await writer.StartRowAsync();
                await writer.WriteAsync(publicId, NpgsqlDbType.Text);                   // PublicId
                await writer.WriteAsync(content, NpgsqlDbType.Text);                    // Content
                await writer.WriteAsync(created, NpgsqlDbType.TimestampTz);             // CreatedAt
                await writer.WriteAsync(isDeleted, NpgsqlDbType.Boolean);               // IsDeleted
                if (isDeleted)
                    await writer.WriteAsync(created, NpgsqlDbType.TimestampTz);         // DeletedAt
                else
                    await writer.WriteNullAsync();
                if (p.LastEdit > 0)
                    await writer.WriteAsync(FromUnix(p.LastEdit), NpgsqlDbType.TimestampTz); // EditedAt
                else
                    await writer.WriteNullAsync();
                await writer.WriteAsync(isFirst, NpgsqlDbType.Boolean);                 // IsFirstPost
                await writer.WriteAsync(0, NpgsqlDbType.Integer);                       // RevisionCount
                await writer.WriteAsync(discussionId, NpgsqlDbType.Integer);            // DiscussionId
                await writer.WriteAsync(userId, NpgsqlDbType.Integer);                  // CreatedByUserId
            }

            await writer.CompleteAsync();
        } // Dispose writer — exits COPY state

        // Build mapping by PublicId
        var publicIdToVbId = validPosts.ToDictionary(v => v.PublicId, v => v.Post.PostId);

        var mapSql = @"SELECT ""Id"", ""PublicId"" FROM ""Post"" WHERE ""PublicId"" = ANY(@pids)";
        await using var mapCmd = new NpgsqlCommand(mapSql, conn);
        mapCmd.Parameters.AddWithValue("pids", validPosts.Select(v => v.PublicId).ToArray());
        await using var mapReader = await mapCmd.ExecuteReaderAsync();

        while (await mapReader.ReadAsync())
        {
            var snakkId = mapReader.GetInt32(0);
            var publicId = mapReader.GetString(1);
            if (publicIdToVbId.TryGetValue(publicId, out var vbPostId))
            {
                mapping[vbPostId] = snakkId;
            }
        }

        return mapping;
    }

    /// <summary>
    /// Writes a batch of reactions (positive votes → ThumbsUp) via COPY.
    /// </summary>
    public async Task WriteReactionBatchAsync(
        List<VBPostVote> votes,
        Dictionary<int, int> postMapping,
        Dictionary<int, int> userMapping)
    {
        var validVotes = new List<(VBPostVote Vote, int PostId, int UserId)>();
        var seen = new HashSet<(int, int)>(); // Deduplicate (postId, userId) pairs

        foreach (var v in votes)
        {
            if (!postMapping.TryGetValue(v.PostId, out var postId))
                continue;
            if (!userMapping.TryGetValue(v.UserId, out var userId))
                continue;

            var key = (postId, userId);
            if (!seen.Add(key))
                continue; // Skip duplicate

            validVotes.Add((v, postId, userId));
        }

        if (validVotes.Count == 0)
            return;

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var copySql = @"COPY ""Reaction"" (""PublicId"", ""PostId"", ""UserId"", ""TypeId"", ""CreatedAt"")
                        FROM STDIN (FORMAT BINARY)";
        await using var writer = await conn.BeginBinaryImportAsync(copySql);

        foreach (var (v, postId, userId) in validVotes)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(NewUlid(), NpgsqlDbType.Text);                  // PublicId
            await writer.WriteAsync(postId, NpgsqlDbType.Integer);                  // PostId
            await writer.WriteAsync(userId, NpgsqlDbType.Integer);                  // UserId
            await writer.WriteAsync(1, NpgsqlDbType.Integer);                       // TypeId (ThumbsUp = 1)
            await writer.WriteAsync(FromUnix(v.DateLine), NpgsqlDbType.TimestampTz); // CreatedAt
        }

        await writer.CompleteAsync();
    }

    /// <summary>
    /// Updates denormalized counts on Community, Hub, Space, and Discussion.
    /// </summary>
    public async Task UpdateDenormalizedCountsAsync()
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // Discussion.PostCount — JOIN-based (single scan of Post table)
        Console.WriteLine("  Updating Discussion.PostCount...");
        await ExecuteNonQuery(conn, @"
            UPDATE ""Discussion"" d
            SET ""PostCount"" = COALESCE(sub.cnt, 0)
            FROM (
                SELECT ""DiscussionId"", COUNT(*) as cnt
                FROM ""Post""
                WHERE NOT ""IsDeleted""
                GROUP BY ""DiscussionId""
            ) sub
            WHERE d.""Id"" = sub.""DiscussionId""");

        // Discussion.ReactionCount — JOIN-based
        Console.WriteLine("  Updating Discussion.ReactionCount...");
        await ExecuteNonQuery(conn, @"
            UPDATE ""Discussion"" d
            SET ""ReactionCount"" = COALESCE(sub.cnt, 0)
            FROM (
                SELECT p.""DiscussionId"", COUNT(DISTINCT r.""UserId"") as cnt
                FROM ""Reaction"" r
                JOIN ""Post"" p ON p.""Id"" = r.""PostId"" AND NOT p.""IsDeleted""
                GROUP BY p.""DiscussionId""
            ) sub
            WHERE d.""Id"" = sub.""DiscussionId""");

        // Space.DiscussionCount
        Console.WriteLine("  Updating Space counts...");
        await ExecuteNonQuery(conn, @"
            UPDATE ""Space"" s
            SET ""DiscussionCount"" = COALESCE(sub.cnt, 0)
            FROM (
                SELECT ""SpaceId"", COUNT(*) as cnt
                FROM ""Discussion""
                WHERE NOT ""IsDeleted""
                GROUP BY ""SpaceId""
            ) sub
            WHERE s.""Id"" = sub.""SpaceId""");

        // Space.PostCount
        await ExecuteNonQuery(conn, @"
            UPDATE ""Space"" s
            SET ""PostCount"" = COALESCE(sub.cnt, 0)
            FROM (
                SELECT d.""SpaceId"", COUNT(*) as cnt
                FROM ""Post"" p
                JOIN ""Discussion"" d ON d.""Id"" = p.""DiscussionId"" AND NOT d.""IsDeleted""
                WHERE NOT p.""IsDeleted""
                GROUP BY d.""SpaceId""
            ) sub
            WHERE s.""Id"" = sub.""SpaceId""");

        // Hub counts (from already-updated Space counts)
        Console.WriteLine("  Updating Hub counts...");
        await ExecuteNonQuery(conn, @"
            UPDATE ""Hub"" h
            SET ""SpaceCount"" = COALESCE(sub.sc, 0),
                ""DiscussionCount"" = COALESCE(sub.dc, 0),
                ""PostCount"" = COALESCE(sub.pc, 0)
            FROM (
                SELECT ""HubId"",
                       COUNT(*) as sc,
                       SUM(""DiscussionCount"") as dc,
                       SUM(""PostCount"") as pc
                FROM ""Space""
                WHERE NOT ""IsDeleted""
                GROUP BY ""HubId""
            ) sub
            WHERE h.""Id"" = sub.""HubId""");

        // Community counts (from already-updated Hub counts)
        Console.WriteLine("  Updating Community counts...");
        await ExecuteNonQuery(conn, @"
            UPDATE ""Community"" c
            SET ""HubCount"" = COALESCE(sub.hc, 0),
                ""SpaceCount"" = COALESCE(sub.sc, 0),
                ""DiscussionCount"" = COALESCE(sub.dc, 0),
                ""PostCount"" = COALESCE(sub.pc, 0)
            FROM (
                SELECT ""CommunityId"",
                       COUNT(*) as hc,
                       SUM(""SpaceCount"") as sc,
                       SUM(""DiscussionCount"") as dc,
                       SUM(""PostCount"") as pc
                FROM ""Hub""
                WHERE NOT ""IsDeleted""
                GROUP BY ""CommunityId""
            ) sub
            WHERE c.""Id"" = sub.""CommunityId""");

        Console.WriteLine("  Denormalized counts updated.");
    }

    private static async Task ExecuteNonQuery(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.CommandTimeout = 900; // 15 minutes for large tables
        await cmd.ExecuteNonQueryAsync();
    }
}
