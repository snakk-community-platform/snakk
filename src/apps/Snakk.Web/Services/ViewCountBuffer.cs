namespace Snakk.Web.Services;

using System.Collections.Concurrent;

public class ViewCountBuffer
{
    private ConcurrentDictionary<(string DiscussionPublicId, string CountryCode), long> _counts = new();

    public void Record(string discussionPublicId, string countryCode)
    {
        _counts.AddOrUpdate((discussionPublicId, countryCode), 1L, (_, existing) => existing + 1);
    }

    public IReadOnlyDictionary<(string DiscussionPublicId, string CountryCode), long> DrainAndReset()
    {
        return Interlocked.Exchange(ref _counts, new ConcurrentDictionary<(string, string), long>());
    }
}
