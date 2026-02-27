using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Snakk.VBulletinImporter.Services;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json")
    .Build();

var mysqlConn = config.GetConnectionString("MySql")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:MySql");
var pgConn = config.GetConnectionString("PostgreSql")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:PostgreSql");

var reader = new VBulletinReader(mysqlConn);
var writer = new SnakkWriter(pgConn);
var totalTimer = Stopwatch.StartNew();

Console.WriteLine("=== Snakk vBulletin Importer ===\n");

// ─── Step 1: Community ───────────────────────────────────────────────
Console.WriteLine("[1/7] Creating community 'freakforum'...");
var communityId = await writer.WriteCommunityAsync();
Console.WriteLine($"  Community created (Id={communityId})\n");

// ─── Step 2: Users ───────────────────────────────────────────────────
Console.WriteLine("[2/7] Importing users...");
var sw = Stopwatch.StartNew();
var vbUsers = await reader.ReadUsersAsync();
Console.WriteLine($"  Read {vbUsers.Count:N0} users from MySQL ({sw.Elapsed.TotalSeconds:F1}s)");

sw.Restart();
var userMapping = await writer.WriteUsersAsync(vbUsers);
Console.WriteLine($"  Wrote {userMapping.Count:N0} users to PostgreSQL ({sw.Elapsed.TotalSeconds:F1}s)\n");

// ─── Step 3: Hubs (categories) ──────────────────────────────────────
Console.WriteLine("[3/7] Importing hubs (vBulletin categories)...");
var vbForums = await reader.ReadForumsAsync();
var categories = vbForums.Where(f => f.ParentId == -1).OrderBy(f => f.DisplayOrder).ToList();
var forums = vbForums.Where(f => f.ParentId > 0).OrderBy(f => f.DisplayOrder).ToList();
Console.WriteLine($"  Found {categories.Count} categories → hubs, {forums.Count} forums → spaces");

var hubMapping = await writer.WriteHubsAsync(categories, communityId);
Console.WriteLine($"  Wrote {hubMapping.Count} hubs\n");

// ─── Step 4: Spaces (forums) ────────────────────────────────────────
Console.WriteLine("[4/7] Importing spaces (vBulletin forums)...");
var spaceMapping = await writer.WriteSpacesAsync(forums, hubMapping);
Console.WriteLine($"  Wrote {spaceMapping.Count} spaces\n");

// ─── Step 5: Discussions (threads) ──────────────────────────────────
Console.WriteLine("[5/7] Importing discussions (vBulletin threads)...");
sw.Restart();
var vbThreads = await reader.ReadThreadsAsync();
Console.WriteLine($"  Read {vbThreads.Count:N0} threads from MySQL ({sw.Elapsed.TotalSeconds:F1}s)");

// Collect first post IDs for marking IsFirstPost
var firstPostIds = new HashSet<int>(vbThreads.Where(t => t.FirstPostId > 0).Select(t => t.FirstPostId));

sw.Restart();
var discussionMapping = await writer.WriteDiscussionsAsync(vbThreads, spaceMapping, userMapping);
Console.WriteLine($"  Wrote {discussionMapping.Count:N0} discussions to PostgreSQL ({sw.Elapsed.TotalSeconds:F1}s)\n");

// ─── Step 6: Posts ──────────────────────────────────────────────────
Console.WriteLine("[6/7] Importing posts (vBulletin posts)...");
sw.Restart();
var postMapping = new Dictionary<int, int>();
var totalPosts = 0;
var batchNum = 0;

await foreach (var batch in reader.ReadPostsBatchedAsync(10_000))
{
    batchNum++;
    var batchMapping = await writer.WritePostBatchAsync(batch, discussionMapping, userMapping, firstPostIds);
    foreach (var kvp in batchMapping)
        postMapping[kvp.Key] = kvp.Value;

    totalPosts += batch.Count;
    Console.Write($"\r  Progress: {totalPosts:N0} posts processed ({batchNum} batches, {sw.Elapsed.TotalSeconds:F0}s)");
}
Console.WriteLine($"\n  Wrote {postMapping.Count:N0} posts to PostgreSQL ({sw.Elapsed.TotalSeconds:F1}s)\n");

// ─── Step 7: Reactions (positive votes → ThumbsUp) ──────────────────
Console.WriteLine("[7/7] Importing reactions (positive post votes → ThumbsUp)...");
sw.Restart();
var totalVotes = 0;
batchNum = 0;

await foreach (var batch in reader.ReadPositiveVotesBatchedAsync(10_000))
{
    batchNum++;
    await writer.WriteReactionBatchAsync(batch, postMapping, userMapping);
    totalVotes += batch.Count;
    Console.Write($"\r  Progress: {totalVotes:N0} votes processed ({batchNum} batches, {sw.Elapsed.TotalSeconds:F0}s)");
}
Console.WriteLine($"\n  Processed {totalVotes:N0} votes ({sw.Elapsed.TotalSeconds:F1}s)\n");

// ─── Final: Update denormalized counts ──────────────────────────────
Console.WriteLine("Updating denormalized counts...");
sw.Restart();
await writer.UpdateDenormalizedCountsAsync();
Console.WriteLine($"  Done ({sw.Elapsed.TotalSeconds:F1}s)\n");

totalTimer.Stop();
Console.WriteLine($"=== Import complete in {totalTimer.Elapsed.TotalMinutes:F1} minutes ===");
Console.WriteLine($"  Users:       {userMapping.Count:N0}");
Console.WriteLine($"  Hubs:        {hubMapping.Count}");
Console.WriteLine($"  Spaces:      {spaceMapping.Count}");
Console.WriteLine($"  Discussions: {discussionMapping.Count:N0}");
Console.WriteLine($"  Posts:       {postMapping.Count:N0}");
Console.WriteLine($"  Reactions:   {totalVotes:N0}");
