namespace Snakk.Application.Services;

public interface IRevocationCache
{
    void RevokeUser(string userId);
    bool IsUserRevoked(string userId);
}
