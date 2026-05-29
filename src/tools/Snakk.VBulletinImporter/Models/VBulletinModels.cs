namespace Snakk.VBulletinImporter.Models;

public record VBUser(
    int UserId,
    string Username,
    string Email,
    string Password,        // vBulletin MD5 hash: MD5(MD5(plaintext) + salt)
    string Salt,            // 3-char salt
    int UserGroupId,
    string MemberGroupIds,  // comma-separated secondary group IDs
    int JoinDate,           // unix timestamp
    int LastActivity,       // unix timestamp
    int LastPost,           // unix timestamp
    int Posts,              // post count
    string Homepage,
    string Skype,
    string Icq,
    string TimezoneOffset   // e.g. "+1", "-5", "0"
);

public record VBForum(
    int ForumId,
    string Title,
    string? Description,
    int ParentId,       // -1 = category (→ Hub), >0 = forum (→ Space)
    int DisplayOrder,
    bool IsPublic       // forum.ispublic bit
);

public record VBThread(
    int ThreadId,
    string Title,
    int ForumId,
    int PostUserId,
    string PostUsername,
    int DateLine,       // unix timestamp
    int LastPost,       // unix timestamp
    int ReplyCount,
    int Visible,        // 1 = visible, 2 = soft-deleted, 0 = modqueue
    int Sticky,
    int Open,           // 0 = closed/locked
    int FirstPostId,
    int PollId,         // 0 = no poll
    string NewsUrl,     // non-empty = link-type thread
    string PrefixId,    // tag prefix label (e.g. "answered", "wip")
    string TagList      // comma-separated tags from vBulletin tag system
);

public record VBPost(
    int PostId,
    int ThreadId,
    int UserId,
    string Username,
    string PageText,    // BBCode content
    int DateLine,       // unix timestamp
    int LastEdit,       // unix timestamp, 0 = never edited
    int Visible,        // 1 = visible, 2 = soft-deleted
    int ParentId        // threaded reply parent (0 = top-level)
);

public record VBPostVote(
    int PostId,
    int UserId,
    int DateLine        // unix timestamp
);

public record VBPoll(
    int PollId,
    int ThreadId,           // resolved via JOIN with thread table
    string Question,
    string[] Options,       // split on "|||"
    int[] VoteCounts,       // split on "|||", parallel array to Options
    bool AllowMultiple,
    int TimeoutDays,        // 0 = never closes
    int DateLine            // unix timestamp
);

public record VBPollVote(
    int PollId,
    int UserId,
    int VoteOption,         // 1-based index into the Options array
    int VoteDate            // unix timestamp
);

public record VBSubscription(
    int UserId,
    int ThreadId,
    int EmailUpdate         // 0 = no email, >0 = email on reply
);

public record VBUserFollow(
    int UserId,
    int RelationId
);
