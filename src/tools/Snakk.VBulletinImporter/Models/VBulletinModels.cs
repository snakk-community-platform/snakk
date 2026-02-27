namespace Snakk.VBulletinImporter.Models;

public record VBUser(
    int UserId,
    string Username,
    string Email,
    int UserGroupId,
    int JoinDate,       // unix timestamp
    int LastActivity,   // unix timestamp
    int LastPost        // unix timestamp
);

public record VBForum(
    int ForumId,
    string Title,
    string? Description,
    int ParentId,       // -1 = category (→ Hub), >0 = forum (→ Space)
    int DisplayOrder
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
    int Visible,        // 1 = visible, 2 = soft-deleted
    int Sticky,
    int Open,           // 0 = closed/locked
    int FirstPostId
);

public record VBPost(
    int PostId,
    int ThreadId,
    int UserId,
    string Username,
    string PageText,    // BBCode content
    int DateLine,       // unix timestamp
    int LastEdit,       // unix timestamp, 0 = never edited
    int Visible         // 1 = visible, 2 = soft-deleted
);

public record VBPostVote(
    int PostId,
    int UserId,
    int DateLine        // unix timestamp
);
