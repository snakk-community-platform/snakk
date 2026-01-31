namespace Snakk.Domain.ValueObjects;

public record RefreshToken
{
    public string Value { get; init; }
    public UserId UserId { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? RevokedAt { get; init; }
    public bool IsRevoked => RevokedAt != null;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;

    private RefreshToken(string value, UserId userId, DateTime expiresAt, DateTime createdAt, string? revokedAt = null)
    {
        Value = value;
        UserId = userId;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        RevokedAt = revokedAt;
    }

    public static RefreshToken Create(UserId userId, int expirationDays = 30)
    {
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) +
                   Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        return new RefreshToken(
            token,
            userId,
            DateTime.UtcNow.AddDays(expirationDays),
            DateTime.UtcNow
        );
    }

    public static RefreshToken Rehydrate(string value, UserId userId, DateTime expiresAt, DateTime createdAt, string? revokedAt = null)
    {
        return new RefreshToken(value, userId, expiresAt, createdAt, revokedAt);
    }

    public RefreshToken Revoke()
    {
        return this with { RevokedAt = DateTime.UtcNow.ToString("O") };
    }
}
