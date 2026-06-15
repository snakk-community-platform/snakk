namespace Snakk.Application.Services;

public interface IUserVisitTracker
{
    Task TrackVisitAsync(string userId, CancellationToken ct = default);
    Task ForceNewVisitAsync(string userId, CancellationToken ct = default);
}
