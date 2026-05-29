using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Snakk.VBulletinImporter.Services;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var mysqlConn = config.GetConnectionString("MySql")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:MySql");
var pgConn = config.GetConnectionString("PostgreSql")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:PostgreSql");

var reader = new VBulletinReader(mysqlConn);
var writer = new SnakkWriter(pgConn);
var totalTimer = Stopwatch.StartNew();
var sw = Stopwatch.StartNew();

Console.WriteLine("=== Snakk vBulletin Importer ===\n");

// ─── Step 1: Community ──────────────────────────────────────────────────────
Console.WriteLine("[1/14] Creating community 'freakforum'...");
var (communityId, communityPublicId) = await writer.WriteCommunityAsync();
Console.WriteLine($"  Community created (Id={communityId}, PublicId={communityPublicId})\n");

// ─── Step 2: Users ──────────────────────────────────────────────────────────
Console.WriteLine("[2/14] Importing users...");
sw.Restart();
var vbUsers = await reader.ReadUsersAsync();
Console.WriteLine($"  Read {vbUsers.Count:N0} users from MySQL ({sw.Elapsed.TotalSeconds:F1}s)");

sw.Restart();
var userMapping = await writer.WriteUsersAsync(vbUsers);
Console.WriteLine($"  Wrote {userMapping.Count:N0} users to PostgreSQL ({sw.Elapsed.TotalSeconds:F1}s)\n");

// ─── Step 3: User Roles ─────────────────────────────────────────────────────
Console.WriteLine("[3/14] Assigning user roles (Administrators / Super Moderators / Moderators)...");
sw.Restart();
await writer.WriteUserRolesAsync(vbUsers, userMapping, communityId);
Console.WriteLine($"  Done ({sw.Elapsed.TotalSeconds:F1}s)\n");

// ─── Step 4: User Social Links ──────────────────────────────────────────────
Console.WriteLine("[4/14] Importing social links (homepage / skype / icq)...");
sw.Restart();
await writer.WriteUserSocialLinksAsync(vbUsers, userMapping);
Console.WriteLine($"  Done ({sw.Elapsed.TotalSeconds:F1}s)\n");

// ─── Step 5: Hubs (vBulletin categories) ────────────────────────────────────
Console.WriteLine("[5/14] Importing hubs (vBulletin categories)...");
var vbForums = await reader.ReadForumsAsync();
var categories = vbForums.Where(f => f.ParentId == -1).OrderBy(f => f.DisplayOrder).ToList();
Console.WriteLine($"  Found {categories.Count} categories → hubs, {vbForums.Count(f => f.ParentId != -1)} forums → spaces");

sw.Restart();
var hubMapping = await writer.WriteHubsAsync(categories, communityId);
Console.WriteLine($"  Wrote {hubMapping.Count} hubs ({sw.Elapsed.TotalSeconds:F1}s)\n");

// ─── Step 6: Spaces (vBulletin forums, with sub-forum flattening) ────────────
Console.WriteLine("[6/14] Importing spaces (vBulletin forums)...");
sw.Restart();
var (spaceMapping, spaceToHub) = await writer.WriteSpacesAsync(vbForums, hubMapping);
Console.WriteLine($"  Wrote {spaceMapping.Count} spaces ({sw.Elapsed.TotalSeconds:F1}s)\n");

// ─── Step 7: Discussions (vBulletin threads) ─────────────────────────────────
Console.WriteLine("[7/14] Importing discussions (vBulletin threads)...");
sw.Restart();
var vbThreads = await reader.ReadThreadsAsync();
Console.WriteLine($"  Read {vbThreads.Count:N0} threads from MySQL ({sw.Elapsed.TotalSeconds:F1}s)");

var firstPostIds = new HashSet<int>(vbThreads.Where(t => t.FirstPostId > 0).Select(t => t.FirstPostId));

sw.Restart();
var discussionMapping = await writer.WriteDiscussionsAsync(vbThreads, spaceMapping, spaceToHub, communityId, userMapping);
Console.WriteLine($"  Wrote {discussionMapping.Count:N0} discussions ({sw.Elapsed.TotalSeconds:F1}s)\n");

// ─── Step 8: Discussion Type Extensions (links + polls) ──────────────────────
Console.WriteLine("[8/14] Importing discussion type extensions (link + poll metadata)...");
sw.Restart();

await writer.WriteDiscussionTypeLinksAsync(vbThreads, discussionMapping);
var linkCount = vbThreads.Count(t => !string.IsNullOrEmpty(t.NewsUrl) && t.PollId == 0 && discussionMapping.ContainsKey(t.ThreadId));
Console.WriteLine($"  Wrote {linkCount} link discussions");

var vbPolls = await reader.ReadPollsAsync();
var pollOptionMapping = await writer.WritePollsAsync(vbPolls, discussionMapping);
Console.WriteLine($"  Wrote {vbPolls.Count} polls with {pollOptionMapping.Count} options ({sw.Elapsed.TotalSeconds:F1}s)\n");

// ─── Step 9: Poll Votes ──────────────────────────────────────────────────────
Console.WriteLine("[9/14] Importing poll votes...");
sw.Restart();
var totalPollVotes = 0;
await foreach (var batch in reader.ReadPollVotesBatchedAsync(10_000))
{
    await writer.WritePollVotesBatchAsync(batch, pollOptionMapping, userMapping);
    totalPollVotes += batch.Count;
    Console.Write($"\r  Progress: {totalPollVotes:N0} poll votes ({sw.Elapsed.TotalSeconds:F0}s)");
}
Console.WriteLine($"\n  Done ({sw.Elapsed.TotalSeconds:F1}s)\n");

// ─── Step 10: Posts ──────────────────────────────────────────────────────────
Console.WriteLine("[10/14] Importing posts (vBulletin posts)...");
sw.Restart();
var postMapping = new Dictionary<int, int>();
var totalPosts = 0;
var batchNum = 0;

await foreach (var batch in reader.ReadPostsBatchedAsync(10_000))
{
    batchNum++;
    var batchMap = await writer.WritePostBatchAsync(batch, discussionMapping, userMapping, firstPostIds);
    foreach (var kvp in batchMap) postMapping[kvp.Key] = kvp.Value;
    totalPosts += batch.Count;
    Console.Write($"\r  Progress: {totalPosts:N0} posts ({batchNum} batches, {sw.Elapsed.TotalSeconds:F0}s)");
}
Console.WriteLine($"\n  Wrote {postMapping.Count:N0} posts ({sw.Elapsed.TotalSeconds:F1}s)\n");

// ─── Step 11: Reactions (positive post votes → Agree) ────────────────────────
Console.WriteLine("[11/14] Importing reactions (post votes → Agree)...");
sw.Restart();
var totalVotes = 0;
batchNum = 0;
await foreach (var batch in reader.ReadPositiveVotesBatchedAsync(10_000))
{
    batchNum++;
    await writer.WriteReactionBatchAsync(batch, postMapping, userMapping);
    totalVotes += batch.Count;
    Console.Write($"\r  Progress: {totalVotes:N0} votes ({batchNum} batches, {sw.Elapsed.TotalSeconds:F0}s)");
}
Console.WriteLine($"\n  Processed {totalVotes:N0} votes ({sw.Elapsed.TotalSeconds:F1}s)\n");

// ─── Step 12: Thread Follows (subscribethread → Discussion follows) ───────────
Console.WriteLine("[12/14] Importing thread follows (subscribethread)...");
sw.Restart();
var vbSubscriptions = await reader.ReadSubscriptionsAsync();
Console.WriteLine($"  Read {vbSubscriptions.Count:N0} subscriptions");
await writer.WriteDiscussionFollowsAsync(vbSubscriptions, discussionMapping, userMapping);
Console.WriteLine($"  Done ({sw.Elapsed.TotalSeconds:F1}s)\n");

// ─── Step 13: User Follows (userlist buddies → User follows) ─────────────────
Console.WriteLine("[13/14] Importing user follows (buddy list)...");
sw.Restart();
var vbUserFollows = await reader.ReadUserFollowsAsync();
Console.WriteLine($"  Read {vbUserFollows.Count:N0} buddy relationships");
await writer.WriteUserFollowsAsync(vbUserFollows, userMapping);
Console.WriteLine($"  Done ({sw.Elapsed.TotalSeconds:F1}s)\n");

// ─── Step 14: Denormalized counts ────────────────────────────────────────────
Console.WriteLine("[14/14] Updating denormalized counts...");
sw.Restart();
await writer.UpdateDenormalizedCountsAsync();
Console.WriteLine($"  Done ({sw.Elapsed.TotalSeconds:F1}s)\n");

// ─── Summary ─────────────────────────────────────────────────────────────────
totalTimer.Stop();
Console.WriteLine($"=== Import complete in {totalTimer.Elapsed.TotalMinutes:F1} minutes ===");
Console.WriteLine($"  Users:          {userMapping.Count:N0}");
Console.WriteLine($"  Hubs:           {hubMapping.Count}");
Console.WriteLine($"  Spaces:         {spaceMapping.Count}");
Console.WriteLine($"  Discussions:    {discussionMapping.Count:N0}");
Console.WriteLine($"  Posts:          {postMapping.Count:N0}");
Console.WriteLine($"  Reactions:      {totalVotes:N0}");
Console.WriteLine($"  Polls:          {vbPolls.Count}");
Console.WriteLine($"  Poll votes:     {totalPollVotes:N0}");
Console.WriteLine($"  Thread follows: {vbSubscriptions.Count:N0}");
Console.WriteLine($"  User follows:   {vbUserFollows.Count:N0}");
