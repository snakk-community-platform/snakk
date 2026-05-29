using MySqlConnector;
using Snakk.VBulletinImporter.Models;

namespace Snakk.VBulletinImporter.Services;

/// <summary>
/// Reads data from the vBulletin MySQL database using streaming DataReaders.
/// </summary>
public class VBulletinReader(string connectionString)
{
    public async Task<List<VBUser>> ReadUsersAsync()
    {
        var users = new List<VBUser>();
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand("""
            SELECT userid, username, email, password, salt, usergroupid, membergroupids,
                   joindate, lastactivity, lastpost, posts,
                   homepage, skype, icq, timezoneoffset
            FROM user
            ORDER BY userid
            """, conn);
        cmd.CommandTimeout = 120;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(new VBUser(
                UserId: reader.GetInt32(0),
                Username: reader.GetString(1),
                Email: reader.IsDBNull(2) ? "" : reader.GetString(2),
                Password: reader.IsDBNull(3) ? "" : reader.GetString(3),
                Salt: reader.IsDBNull(4) ? "" : reader.GetString(4),
                UserGroupId: reader.GetInt32(5),
                MemberGroupIds: reader.IsDBNull(6) ? "" : reader.GetString(6),
                JoinDate: reader.GetInt32(7),
                LastActivity: reader.GetInt32(8),
                LastPost: reader.GetInt32(9),
                Posts: reader.GetInt32(10),
                Homepage: reader.IsDBNull(11) ? "" : reader.GetString(11),
                Skype: reader.IsDBNull(12) ? "" : reader.GetString(12),
                Icq: reader.IsDBNull(13) ? "" : reader.GetString(13),
                TimezoneOffset: reader.IsDBNull(14) ? "" : reader.GetString(14)
            ));
        }

        return users;
    }

    public async Task<List<VBForum>> ReadForumsAsync()
    {
        var forums = new List<VBForum>();
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand("""
            SELECT forumid, title, description, parentid, displayorder, ispublic
            FROM forum
            ORDER BY parentid, displayorder
            """, conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            // ispublic is a BIT(1) column — read as byte/ulong
            var isPublicRaw = reader.GetValue(5);
            var isPublic = isPublicRaw switch
            {
                byte b => b != 0,
                ulong ul => ul != 0,
                long l => l != 0,
                int i => i != 0,
                bool bl => bl,
                _ => true
            };

            forums.Add(new VBForum(
                ForumId: reader.GetInt32(0),
                Title: reader.GetString(1),
                Description: reader.IsDBNull(2) ? null : reader.GetString(2),
                ParentId: reader.GetInt32(3),
                DisplayOrder: reader.GetInt32(4),
                IsPublic: isPublic
            ));
        }

        return forums;
    }

    public async Task<List<VBThread>> ReadThreadsAsync()
    {
        var threads = new List<VBThread>();
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand("""
            SELECT threadid, title, forumid, postuserid, postusername,
                   dateline, lastpost, replycount, visible, sticky, open, firstpostid,
                   pollid, newsurl, prefixid, taglist
            FROM thread
            WHERE visible IN (1, 2)
            ORDER BY threadid
            """, conn);
        cmd.CommandTimeout = 300;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            threads.Add(new VBThread(
                ThreadId: reader.GetInt32(0),
                Title: reader.GetString(1),
                ForumId: reader.GetInt32(2),
                PostUserId: reader.GetInt32(3),
                PostUsername: reader.GetString(4),
                DateLine: reader.GetInt32(5),
                LastPost: reader.GetInt32(6),
                ReplyCount: reader.GetInt32(7),
                Visible: reader.GetInt32(8),
                Sticky: reader.GetInt32(9),
                Open: reader.GetInt32(10),
                FirstPostId: reader.GetInt32(11),
                PollId: reader.GetInt32(12),
                NewsUrl: reader.IsDBNull(13) ? "" : reader.GetString(13),
                PrefixId: reader.IsDBNull(14) ? "" : reader.GetString(14),
                TagList: reader.IsDBNull(15) ? "" : reader.GetString(15)
            ));
        }

        return threads;
    }

    /// <summary>
    /// Streams posts in batches to avoid loading 3.5M records into memory at once.
    /// </summary>
    public async IAsyncEnumerable<List<VBPost>> ReadPostsBatchedAsync(int batchSize = 10_000)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand("""
            SELECT postid, threadid, userid, username, pagetext, dateline, lastedit, visible, parentid
            FROM post
            WHERE visible IN (1, 2)
            ORDER BY postid
            """, conn);
        cmd.CommandTimeout = 600;

        await using var reader = await cmd.ExecuteReaderAsync();
        var batch = new List<VBPost>(batchSize);

        while (await reader.ReadAsync())
        {
            batch.Add(new VBPost(
                PostId: reader.GetInt32(0),
                ThreadId: reader.GetInt32(1),
                UserId: reader.GetInt32(2),
                Username: reader.GetString(3),
                PageText: reader.IsDBNull(4) ? "" : reader.GetString(4),
                DateLine: reader.GetInt32(5),
                LastEdit: reader.GetInt32(6),
                Visible: reader.GetInt32(7),
                ParentId: reader.GetInt32(8)
            ));

            if (batch.Count >= batchSize)
            {
                yield return batch;
                batch = new List<VBPost>(batchSize);
            }
        }

        if (batch.Count > 0)
            yield return batch;
    }

    /// <summary>
    /// Reads only active (non-deleted) post votes.
    /// </summary>
    public async IAsyncEnumerable<List<VBPostVote>> ReadPositiveVotesBatchedAsync(int batchSize = 10_000)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand("""
            SELECT postid, userid, dateline
            FROM postvote
            WHERE deleted = 0
            ORDER BY postid, userid
            """, conn);
        cmd.CommandTimeout = 300;

        await using var reader = await cmd.ExecuteReaderAsync();
        var batch = new List<VBPostVote>(batchSize);

        while (await reader.ReadAsync())
        {
            batch.Add(new VBPostVote(
                PostId: reader.GetInt32(0),
                UserId: reader.GetInt32(1),
                DateLine: reader.GetInt32(2)
            ));

            if (batch.Count >= batchSize)
            {
                yield return batch;
                batch = new List<VBPostVote>(batchSize);
            }
        }

        if (batch.Count > 0)
            yield return batch;
    }

    /// <summary>
    /// Reads all polls, joining to thread to resolve threadid.
    /// Options and vote counts are stored as "|||"-delimited strings in the poll table.
    /// </summary>
    public async Task<List<VBPoll>> ReadPollsAsync()
    {
        var polls = new List<VBPoll>();
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand("""
            SELECT p.pollid, t.threadid, p.question, p.options, p.votes,
                   p.multiple, p.timeout, p.dateline
            FROM poll p
            JOIN thread t ON t.pollid = p.pollid
            WHERE t.pollid > 0
            """, conn);
        cmd.CommandTimeout = 60;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var optionsStr = reader.IsDBNull(3) ? "" : reader.GetString(3);
            var votesStr = reader.IsDBNull(4) ? "" : reader.GetString(4);

            var options = optionsStr.Split("|||", StringSplitOptions.None);
            var voteParts = votesStr.Split("|||", StringSplitOptions.None);
            var voteCounts = new int[options.Length];
            for (var i = 0; i < options.Length && i < voteParts.Length; i++)
                voteCounts[i] = int.TryParse(voteParts[i].Trim(), out var n) ? n : 0;

            polls.Add(new VBPoll(
                PollId: reader.GetInt32(0),
                ThreadId: reader.GetInt32(1),
                Question: reader.GetString(2),
                Options: options,
                VoteCounts: voteCounts,
                AllowMultiple: reader.GetInt32(5) != 0,
                TimeoutDays: reader.GetInt32(6),
                DateLine: reader.GetInt32(7)
            ));
        }

        return polls;
    }

    /// <summary>
    /// Streams poll votes in batches.
    /// voteoption is 1-based index into the poll's options array.
    /// </summary>
    public async IAsyncEnumerable<List<VBPollVote>> ReadPollVotesBatchedAsync(int batchSize = 10_000)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand("""
            SELECT pollid, userid, voteoption, votedate
            FROM pollvote
            WHERE userid IS NOT NULL AND voteoption > 0
            ORDER BY pollid, userid
            """, conn);
        cmd.CommandTimeout = 120;

        await using var reader = await cmd.ExecuteReaderAsync();
        var batch = new List<VBPollVote>(batchSize);

        while (await reader.ReadAsync())
        {
            if (reader.IsDBNull(1)) continue;

            batch.Add(new VBPollVote(
                PollId: reader.GetInt32(0),
                UserId: reader.GetInt32(1),
                VoteOption: reader.GetInt32(2),
                VoteDate: reader.GetInt32(3)
            ));

            if (batch.Count >= batchSize)
            {
                yield return batch;
                batch = new List<VBPollVote>(batchSize);
            }
        }

        if (batch.Count > 0)
            yield return batch;
    }

    /// <summary>
    /// Reads thread subscriptions (→ Discussion Follows).
    /// subscribethread has no dateline column so CreatedAt will be set to import time.
    /// </summary>
    public async Task<List<VBSubscription>> ReadSubscriptionsAsync()
    {
        var subs = new List<VBSubscription>();
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand("""
            SELECT userid, threadid, emailupdate
            FROM subscribethread
            WHERE canview = 1
            ORDER BY userid
            """, conn);
        cmd.CommandTimeout = 60;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            subs.Add(new VBSubscription(
                UserId: reader.GetInt32(0),
                ThreadId: reader.GetInt32(1),
                EmailUpdate: reader.GetInt32(2)
            ));
        }

        return subs;
    }

    /// <summary>
    /// Reads confirmed buddy relationships (→ User Follows).
    /// </summary>
    public async Task<List<VBUserFollow>> ReadUserFollowsAsync()
    {
        var follows = new List<VBUserFollow>();
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand("""
            SELECT userid, relationid
            FROM userlist
            WHERE type = 'buddy' AND friend = 'yes'
            ORDER BY userid
            """, conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            follows.Add(new VBUserFollow(
                UserId: reader.GetInt32(0),
                RelationId: reader.GetInt32(1)
            ));
        }

        return follows;
    }
}
