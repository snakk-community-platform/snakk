namespace Snakk.Domain.Entities;

public class UserOAuthConnection
{
    public int Id { get; private set; }
    public string UserPublicId { get; private set; } = null!;
    public string Provider { get; private set; } = null!;
    public string ProviderUserId { get; private set; } = null!;
    public DateTime ConnectedAt { get; private set; }
    public bool Require2FA { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    private UserOAuthConnection() { }

    public static UserOAuthConnection Create(string userPublicId, string provider, string providerUserId)
    {
        if (string.IsNullOrWhiteSpace(userPublicId))
            throw new ArgumentException("User public ID cannot be empty", nameof(userPublicId));
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider cannot be empty", nameof(provider));
        if (string.IsNullOrWhiteSpace(providerUserId))
            throw new ArgumentException("Provider user ID cannot be empty", nameof(providerUserId));

        return new UserOAuthConnection
        {
            UserPublicId = userPublicId,
            Provider = provider.ToLowerInvariant(),
            ProviderUserId = providerUserId,
            ConnectedAt = DateTime.UtcNow,
            Require2FA = false
        };
    }

    public static UserOAuthConnection Rehydrate(
        int id,
        string userPublicId,
        string provider,
        string providerUserId,
        DateTime connectedAt,
        bool require2FA,
        DateTime? lastLoginAt) =>
        new()
        {
            Id = id,
            UserPublicId = userPublicId,
            Provider = provider,
            ProviderUserId = providerUserId,
            ConnectedAt = connectedAt,
            Require2FA = require2FA,
            LastLoginAt = lastLoginAt
        };

    public void SetRequire2FA(bool require) => Require2FA = require;

    public void RecordLogin() => LastLoginAt = DateTime.UtcNow;
}
