namespace Snakk.Web.Services;

public sealed class ProfilerIdBag
{
    private readonly List<string> _ids = [];

    public void Add(string ids) => _ids.Add(ids);

    public string? Joined => _ids.Count > 0 ? string.Join(',', _ids) : null;
}
