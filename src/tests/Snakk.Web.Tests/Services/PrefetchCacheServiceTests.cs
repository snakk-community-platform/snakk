using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Snakk.Web.Services;

namespace Snakk.Web.Tests.Services;

/// <summary>
/// Concurrency tests for <see cref="PrefetchCacheService"/>. The service fronts expensive
/// gRPC→DB aggregations (e.g. trending contributors/spaces), so it MUST be single-flight:
/// under many concurrent callers for one key, the factory runs exactly once. Guards against
/// the cache-stampede regression where the non-atomic IMemoryCache.GetOrCreate let every
/// concurrent miss create its own Lazy and hit the DB (observed as ~50 recomputes per
/// TTL expiry under a 1000-VU load test).
/// </summary>
public class PrefetchCacheServiceTests
{
    private const int Concurrency = 64;

    private static PrefetchCacheService CreateSut() =>
        new(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<PrefetchCacheService>.Instance,
            new ConfigurationBuilder().Build(),
            new HttpContextAccessor());

    [Test]
    public async Task GetOrCreateSharedAsync_ManyConcurrentCallers_InvokesFactoryOnce()
    {
        var sut = CreateSut();
        var calls = 0;

        async Task<string?> Factory()
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(100); // hold the in-flight window open so callers genuinely overlap
            return "value";
        }

        var tasks = Enumerable.Range(0, Concurrency)
            .Select(_ => Task.Run(() => sut.GetOrCreateSharedAsync("trending:k", Factory, TimeSpan.FromMinutes(1))))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(results.All(r => r == "value")).IsTrue();
    }

    [Test]
    public async Task GetOrFetchAsync_ManyConcurrentCallers_InvokesFactoryOnce()
    {
        var sut = CreateSut();
        var calls = 0;

        async Task<string?> Factory()
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(100);
            return "value";
        }

        var tasks = Enumerable.Range(0, Concurrency)
            .Select(_ => Task.Run(() => sut.GetOrFetchAsync("trending:k", Factory)))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(results.All(r => r.Value == "value")).IsTrue();
    }

    [Test]
    public async Task Prefetch_ManyConcurrentCalls_SpawnsFactoryOnce()
    {
        var sut = CreateSut();
        var calls = 0;

        Func<Task<string>> factory = async () =>
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(100);
            return "value";
        };

        await Task.WhenAll(Enumerable.Range(0, Concurrency)
            .Select(_ => Task.Run(() => sut.Prefetch("trending:k", factory))));
        await Task.Delay(300); // let the single spawned fetch complete

        await Assert.That(calls).IsEqualTo(1);
    }
}
