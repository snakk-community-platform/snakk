namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

public class TrustedDeviceService : ITrustedDeviceService
{
    private readonly SnakkDbContext _context;

    public TrustedDeviceService(SnakkDbContext context)
    {
        _context = context;
    }

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
        var browser = "Unknown Browser";
        var os = "Unknown OS";

        // Detect browser
        if (userAgent.Contains("Chrome") && !userAgent.Contains("Edg"))
            browser = "Chrome";
        else if (userAgent.Contains("Firefox"))
            browser = "Firefox";
        else if (userAgent.Contains("Safari") && !userAgent.Contains("Chrome"))
            browser = "Safari";
        else if (userAgent.Contains("Edg"))
            browser = "Edge";
        else if (userAgent.Contains("Opera") || userAgent.Contains("OPR"))
            browser = "Opera";

        // Detect OS
        if (userAgent.Contains("Windows"))
            os = "Windows";
        else if (userAgent.Contains("Mac OS"))
            os = "macOS";
        else if (userAgent.Contains("Linux"))
            os = "Linux";
        else if (userAgent.Contains("Android"))
            os = "Android";
        else if (userAgent.Contains("iOS") || userAgent.Contains("iPhone") || userAgent.Contains("iPad"))
            os = "iOS";

        return $"{browser} on {os}";
    }

    public async Task<bool> IsDeviceTrustedAsync(UserId userId, string deviceFingerprint)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        if (user == null) return false;

        var now = DateTime.UtcNow;
        return await _context.TrustedDevices
            .AnyAsync(d =>
                d.UserId == user.Id &&
                d.DeviceFingerprint == deviceFingerprint &&
                d.RevokedAt == null &&
                (d.ExpiresAt == null || d.ExpiresAt > now));
    }

    public async Task TrustDeviceAsync(UserId userId, string deviceFingerprint, string deviceName, string ipAddress, int? expirationDays = null)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        if (user == null)
            throw new InvalidOperationException($"User {userId} not found");

        // Check if device is already trusted
        var existing = await _context.TrustedDevices
            .FirstOrDefaultAsync(d =>
                d.UserId == user.Id &&
                d.DeviceFingerprint == deviceFingerprint &&
                d.RevokedAt == null);

        if (existing != null)
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

            _context.TrustedDevices.Add(device);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<TrustedDeviceDto>> GetTrustedDevicesAsync(UserId userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        if (user == null)
            return new List<TrustedDeviceDto>();

        var now = DateTime.UtcNow;
        var devices = await _context.TrustedDevices
            .Where(d => d.UserId == user.Id && d.RevokedAt == null)
            .OrderByDescending(d => d.LastUsedAt ?? d.TrustedAt)
            .ToListAsync();

        return devices.Select(d => new TrustedDeviceDto
        {
            PublicId = d.PublicId,
            DeviceName = d.DeviceName,
            TrustedAt = d.TrustedAt,
            ExpiresAt = d.ExpiresAt,
            LastUsedAt = d.LastUsedAt,
            LastUsedIp = d.LastUsedIp,
            IsActive = d.ExpiresAt == null || d.ExpiresAt > now
        }).ToList();
    }

    public async Task RevokeDeviceAsync(string devicePublicId, string reason)
    {
        var device = await _context.TrustedDevices
            .FirstOrDefaultAsync(d => d.PublicId == devicePublicId);

        if (device != null && device.RevokedAt == null)
        {
            device.RevokedAt = DateTime.UtcNow;
            device.RevocationReason = reason;
            await _context.SaveChangesAsync();
        }
    }

    public async Task RevokeAllDevicesAsync(UserId userId, string reason)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        if (user == null) return;

        var devices = await _context.TrustedDevices
            .Where(d => d.UserId == user.Id && d.RevokedAt == null)
            .ToListAsync();

        foreach (var device in devices)
        {
            device.RevokedAt = DateTime.UtcNow;
            device.RevocationReason = reason;
        }

        await _context.SaveChangesAsync();
    }
}
