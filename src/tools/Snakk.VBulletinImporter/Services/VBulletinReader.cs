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
            SELECT userid, username, email, usergroupid, joindate, lastactivity, lastpost
            FROM user
            ORDER BY userid
            """, conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(new VBUser(
                UserId: reader.GetInt32(0),
                Username: reader.GetString(1),
                Email: reader.GetString(2),
                UserGroupId: reader.GetInt32(3),
                JoinDate: reader.GetInt32(4),
                LastActivity: reader.GetInt32(5),
                LastPost: reader.GetInt32(6)
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
            SELECT forumid, title, description, parentid, displayorder
            FROM forum
            ORDER BY parentid, displayorder
            """, conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            forums.Add(new VBForum(
                ForumId: reader.GetInt32(0),
                Title: reader.GetString(1),
                Description: reader.IsDBNull(2) ? null : reader.GetString(2),
                ParentId: reader.GetInt32(3),
                DisplayOrder: reader.GetInt32(4)
            ));
        }

        return forums;
    }

    public async Task<List<VBThread>> ReadThreadsAsync()
    {
        var threads = new List<VBThread>();
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        // Only visible (1) and soft-deleted (2)
        await using var cmd = new MySqlCommand("""
            SELECT threadid, title, forumid, postuserid, postusername,
                   dateline, lastpost, replycount, visible, sticky, open, firstpostid
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
                FirstPostId: reader.GetInt32(11)
            ));
        }

        return threads;
    }

    /// <summary>
    /// Streams posts in batches to avoid loading 3.5M records into memory at once.
    /// </summary>
    public async IAsyncEnumerable<List<VBPost>> ReadPostsBatchedAsync(int batchSize = 10000)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand("""
            SELECT postid, threadid, userid, username, pagetext, dateline, lastedit, visible
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
                Visible: reader.GetInt32(7)
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
    /// Reads only positive post votes (upvotes). The postvote table has no explicit
    /// vote direction column — a row existing means an upvote. Deleted votes have deleted=1.
    /// </summary>
    public async IAsyncEnumerable<List<VBPostVote>> ReadPositiveVotesBatchedAsync(int batchSize = 10000)
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
}
