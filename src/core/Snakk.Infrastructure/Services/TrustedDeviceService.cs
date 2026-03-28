namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

public class TrustedDeviceService(SnakkDbContext context) : ITrustedDeviceService
{
    public string GenerateDeviceFingerprint(string userAgent, string ipAddress)
    {
        // Combine User-Agent and IP, then hash for privacy
        var combined = $"{userAgent}|{ipAddress}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
        return Convert.ToBase64String(hashBytes);
    }

    public string GetDeviceName(string userAgent)
    {
        // Parse User-Agent to get friendly name
        var browser = userAgent switch
        {
            _ when userAgent.Contains("Edg") => "Edge",
            _ when userAgent.Contains("Chrome") => "Chrome",
            _ when userAgent.Contains("Firefox") => "Firefox",
            _ when userAgent.Contains("Safari") && !userAgent.Contains("Chrome") => "Safari",
            _ when userAgent.Contains("Opera") || userAgent.Contains("OPR") => "Opera",
            _ => "Unknown Browser"
        };

        // Detect OS
        var os = userAgent switch
        {
            _ when userAgent.Contains("Windows") => "Windows",
            _ when userAgent.Contains("Mac OS") => "macOS",
            _ when userAgent.Contains("Linux") => "Linux",
            _ when userAgent.Contains("Android") => "Android",
            _ when userAgent.Contains("iOS") || userAgent.Contains("iPhone") || userAgent.Contains("iPad") => "iOS",
            _ => "Unknown OS"
        };

        return $"{browser} on {os}";
    }

    public async Task<bool> IsDeviceTrustedAsync(UserId userId, string deviceFingerprint)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user is null) return false;

        var now = DateTime.UtcNow;

        return await context.TrustedDevices
            .AnyAsync(d =>
                d.UserId == user.Id
                && d.DeviceFingerprint == deviceFingerprint
                && d.RevokedAt == null
                && (d.ExpiresAt == null || d.ExpiresAt > now));
    }

    public async Task TrustDeviceAsync(
        UserId userId,
        string deviceFingerprint,
        string deviceName,
        string ipAddress,
        int? expirationDays = null)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user is null)
            throw new InvalidOperationException($"User {userId} not found");

        // Check if device is already trusted
        var existing = await context.TrustedDevices
            .FirstOrDefaultAsync(d =>
                d.UserId == user.Id
                && d.DeviceFingerprint == deviceFingerprint
                && d.RevokedAt == null);

        if (existing is not null)
        {
            // Update existing trust
            existing.LastUsedAt = DateTime.UtcNow;
            existing.LastUsedIp = ipAddress;

            if (expirationDays.HasValue)
                existing.ExpiresAt = DateTime.UtcNow.AddDays(expirationDays.Value);
        }
        else
        {
            // Create new trusted device
            var device = new TrustedDeviceDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                UserId = user.Id,
                DeviceFingerprint = deviceFingerprint,
                DeviceName = deviceName,
                TrustedAt = DateTime.UtcNow,
                ExpiresAt = expirationDays.HasValue ? DateTime.UtcNow.AddDays(expirationDays.Value) : null,
                LastUsedAt = DateTime.UtcNow,
                LastUsedIp = ipAddress
            };

            context.TrustedDevices.Add(device);
        }

        await context.SaveChangesAsync();
    }

    public async Task<List<TrustedDeviceDto>> GetTrustedDevicesAsync(UserId userId)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user is null)
            return [];

        var now = DateTime.UtcNow;

        var devices = await context.TrustedDevices
            .Where(d =>
                d.UserId == user.Id
                && d.RevokedAt == null)
            .OrderByDescending(d => d.LastUsedAt ?? d.TrustedAt)
            .ToListAsync();

        return devices
            .Select(d => new TrustedDeviceDto
            {
                PublicId = d.PublicId,
                DeviceName = d.DeviceName,
                TrustedAt = d.TrustedAt,
                ExpiresAt = d.ExpiresAt,
                LastUsedAt = d.LastUsedAt,
                LastUsedIp = d.LastUsedIp,
                IsActive = d.ExpiresAt == null || d.ExpiresAt > now
            })
            .ToList();
    }

    public async Task RevokeDeviceAsync(string devicePublicId, string reason)
    {
        var device = await context.TrustedDevices
            .FirstOrDefaultAsync(d => d.PublicId == devicePublicId);

        if (device is not null && device.RevokedAt is null)
        {
            device.RevokedAt = DateTime.UtcNow;
            device.RevocationReason = reason;
            await context.SaveChangesAsync();
        }
    }

    public async Task<bool> RevokeDeviceForUserAsync(string devicePublicId, string userId, string reason)
    {
        var device = await context.TrustedDevices
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.PublicId == devicePublicId);

        if (device is null || device.User.PublicId != userId || device.RevokedAt is not null)
            return false;

        device.RevokedAt = DateTime.UtcNow;
        device.RevocationReason = reason;
        await context.SaveChangesAsync();
        return true;
    }

    public async Task RevokeAllDevicesAsync(UserId userId, string reason)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user is null) return;

        var devices = await context.TrustedDevices
            .Where(d =>
                d.UserId == user.Id
                && d.RevokedAt == null)
            .ToListAsync();

        foreach (var device in devices)
        {
            device.RevokedAt = DateTime.UtcNow;
            device.RevocationReason = reason;
        }

        await context.SaveChangesAsync();
    }
}
