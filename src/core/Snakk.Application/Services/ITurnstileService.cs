namespace Snakk.Application.Services;

public interface ITurnstileService
{
    Task<bool> VerifyAsync(string token, string? remoteIp = null);
}
