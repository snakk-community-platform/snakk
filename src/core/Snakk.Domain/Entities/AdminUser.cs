namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;

public class AdminUser
{
    public AdminUserId PublicId { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public string DisplayName { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastModifiedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private AdminUser()
    {
    }
#pragma warning restore CS8618

    private AdminUser(
        AdminUserId publicId,
        string email,
        string passwordHash,
        string displayName,
        bool isActive,
        DateTime createdAt,
        DateTime? lastModifiedAt = null,
        DateTime? lastLoginAt = null)
    {
        PublicId = publicId;
        Email = email;
        PasswordHash = passwordHash;
        DisplayName = displayName;
        IsActive = isActive;
        CreatedAt = createdAt;
        LastModifiedAt = lastModifiedAt;
        LastLoginAt = lastLoginAt;
    }

    public static AdminUser Create(
        string email,
        string passwordHash,
        string displayName)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required", nameof(passwordHash));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required", nameof(displayName));

        return new AdminUser(
            AdminUserId.New(),
            email.ToLowerInvariant().Trim(),
            passwordHash,
            displayName.Trim(),
            isActive: true,
            createdAt: DateTime.UtcNow);
    }

    public void UpdatePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash is required", nameof(newPasswordHash));

        PasswordHash = newPasswordHash;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required", nameof(displayName));

        DisplayName = displayName.Trim();
        LastModifiedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }
}
