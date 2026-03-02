using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Snakk.Api.Tests.Helpers;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Api.Tests.Endpoints;

public class AdminModerationEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    // ==================== Ban User ====================

    [Test]
    public async Task BanUser_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var body = JsonContent.Create(new { reason = "Test ban", duration = (int?)null });

        // Act
        var response = await client.PostAsync("/admin/users/some-user-id/ban", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task BanUser_WithNonAdmin_ReturnsForbidden()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();
        var body = JsonContent.Create(new { reason = "Test ban", duration = (int?)null });

        // Act
        var response = await client.PostAsync("/admin/users/some-user-id/ban", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task BanUser_WithAdmin_ValidUser_ReturnsOk()
    {
        // Arrange
        var adminUserId = "admin-user-ban-ok";
        var targetUserId = "target-user-ban-ok";
        var adminDbId = await SeedUserAsync(adminUserId, "Admin User");
        await SeedUserAsync(targetUserId, "Target User");
        await SeedGlobalAdminRoleAsync(adminDbId);

        var client = _server.CreateAuthenticatedClient(userId: adminUserId, role: "Admin");
        var body = JsonContent.Create(new { reason = "Spamming", duration = 7 });

        // Act
        var response = await client.PostAsync($"/admin/users/{targetUserId}/ban", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("publicId", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("banType", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("bannedAt", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("reason", out _)).IsTrue();
    }

    [Test]
    public async Task BanUser_NonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var adminUserId = "admin-user-ban-404";
        await SeedUserAsync(adminUserId, "Admin User");

        var client = _server.CreateAuthenticatedClient(userId: adminUserId, role: "Admin");
        var body = JsonContent.Create(new { reason = "Spamming", duration = (int?)null });

        // Act
        var response = await client.PostAsync("/admin/users/nonexistent-user/ban", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task BanUser_PermanentBan_ReturnsOkWithNullExpiry()
    {
        // Arrange
        var adminUserId = "admin-user-perm-ban";
        var targetUserId = "target-user-perm-ban";
        var adminDbId = await SeedUserAsync(adminUserId, "Admin User");
        await SeedUserAsync(targetUserId, "Target User");
        await SeedGlobalAdminRoleAsync(adminDbId);

        var client = _server.CreateAuthenticatedClient(userId: adminUserId, role: "Admin");
        var body = JsonContent.Create(new { reason = "Severe violation", duration = (int?)null });

        // Act
        var response = await client.PostAsync($"/admin/users/{targetUserId}/ban", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("reason").GetString()).IsEqualTo("Severe violation");
    }

    // ==================== Unban User ====================

    [Test]
    public async Task UnbanUser_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.DeleteAsync("/admin/users/some-user-id/ban");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UnbanUser_WithNonAdmin_ReturnsForbidden()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.DeleteAsync("/admin/users/some-user-id/ban");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task UnbanUser_NonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var adminUserId = "admin-user-unban-404";
        await SeedUserAsync(adminUserId, "Admin User");

        var client = _server.CreateAuthenticatedClient(userId: adminUserId, role: "Admin");

        // Act
        var response = await client.DeleteAsync("/admin/users/nonexistent-user/ban");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UnbanUser_UserNotBanned_ReturnsBadRequest()
    {
        // Arrange
        var adminUserId = "admin-user-unban-nobanned";
        var targetUserId = "target-user-unban-nobanned";
        await SeedUserAsync(adminUserId, "Admin User");
        await SeedUserAsync(targetUserId, "Target User");

        var client = _server.CreateAuthenticatedClient(userId: adminUserId, role: "Admin");

        // Act
        var response = await client.DeleteAsync($"/admin/users/{targetUserId}/ban");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        await Assert.That(json.RootElement.GetProperty("error").GetString()).IsEqualTo("User is not currently banned");
    }

    [Test]
    public async Task UnbanUser_WithAdmin_WhenBanned_ReturnsNoContent()
    {
        // Arrange
        var adminUserId = "admin-user-unban-ok";
        var targetUserId = "target-user-unban-ok";
        var adminDbId = await SeedUserAsync(adminUserId, "Admin User");
        var targetDbId = await SeedUserAsync(targetUserId, "Target User");
        await SeedGlobalAdminRoleAsync(adminDbId);
        await SeedActiveBanAsync(targetDbId, targetUserId, adminDbId);

        var client = _server.CreateAuthenticatedClient(userId: adminUserId, role: "Admin");

        // Act
        var response = await client.DeleteAsync($"/admin/users/{targetUserId}/ban");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
    }

    // ==================== Update User Role ====================

    [Test]
    public async Task UpdateUserRole_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var body = JsonContent.Create(new { role = "Admin" });

        // Act
        var response = await client.PutAsync("/admin/users/some-user-id/role", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UpdateUserRole_WithNonAdmin_ReturnsForbidden()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();
        var body = JsonContent.Create(new { role = "Admin" });

        // Act
        var response = await client.PutAsync("/admin/users/some-user-id/role", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task UpdateUserRole_WithAdmin_ToAdmin_ReturnsOk()
    {
        // Arrange
        var adminUserId = "admin-user-role-ok";
        var targetUserId = "target-user-role-ok";
        var adminDbId = await SeedUserAsync(adminUserId, "Admin User");
        await SeedUserAsync(targetUserId, "Target User");
        await SeedGlobalAdminRoleAsync(adminDbId);

        var client = _server.CreateAuthenticatedClient(userId: adminUserId, role: "Admin");
        var body = JsonContent.Create(new { role = "Admin" });

        // Act
        var response = await client.PutAsync($"/admin/users/{targetUserId}/role", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("publicId", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("role", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("assignedAt", out _)).IsTrue();
    }

    [Test]
    public async Task UpdateUserRole_NonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var adminUserId = "admin-user-role-404";
        await SeedUserAsync(adminUserId, "Admin User");

        var client = _server.CreateAuthenticatedClient(userId: adminUserId, role: "Admin");
        var body = JsonContent.Create(new { role = "Admin" });

        // Act
        var response = await client.PutAsync("/admin/users/nonexistent-user/role", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdateUserRole_WithModeratorRole_ReturnsBadRequest()
    {
        // Arrange
        var adminUserId = "admin-user-role-mod";
        var targetUserId = "target-user-role-mod";
        await SeedUserAsync(adminUserId, "Admin User");
        await SeedUserAsync(targetUserId, "Target User");

        var client = _server.CreateAuthenticatedClient(userId: adminUserId, role: "Admin");
        var body = JsonContent.Create(new { role = "Moderator" });

        // Act
        var response = await client.PutAsync($"/admin/users/{targetUserId}/role", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        await Assert.That(json.RootElement.GetProperty("error").GetString())
            .IsEqualTo("Community moderator role requires a community scope. Use platform admin instead.");
    }

    [Test]
    public async Task UpdateUserRole_WithInvalidRole_ReturnsBadRequest()
    {
        // Arrange
        var adminUserId = "admin-user-role-invalid";
        var targetUserId = "target-user-role-invalid";
        await SeedUserAsync(adminUserId, "Admin User");
        await SeedUserAsync(targetUserId, "Target User");

        var client = _server.CreateAuthenticatedClient(userId: adminUserId, role: "Admin");
        var body = JsonContent.Create(new { role = "SuperDuperAdmin" });

        // Act
        var response = await client.PutAsync($"/admin/users/{targetUserId}/role", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        await Assert.That(json.RootElement.GetProperty("error").GetString())
            .IsEqualTo("Invalid role. Must be 'Admin' or 'User'");
    }

    // ==================== Get Reports ====================

    [Test]
    public async Task GetReports_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/moderation/reports?page=1");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetReports_WithNonAdmin_ReturnsForbidden()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/admin/moderation/reports?page=1");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GetReports_WithAdmin_ReturnsPagedResults()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/moderation/reports?page=1");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("reports", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("total", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("page", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("pageSize", out _)).IsTrue();
        await Assert.That(json.RootElement.GetProperty("page").GetInt32()).IsEqualTo(1);
        await Assert.That(json.RootElement.GetProperty("pageSize").GetInt32()).IsEqualTo(20);
    }

    // ==================== Get Report ====================

    [Test]
    public async Task GetReport_ExistingId_ReturnsDetails()
    {
        // Arrange
        var reporterUserId = "reporter-user-get";
        var reportedUserId = "reported-user-get";
        var reporterDbId = await SeedUserAsync(reporterUserId, "Reporter User");
        var reportedDbId = await SeedUserAsync(reportedUserId, "Reported User");
        var reportPublicId = "report-detail-get";
        await SeedReportAsync(reportPublicId, reporterDbId, reportedDbId);

        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync($"/admin/moderation/reports/{reportPublicId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("id").GetString()).IsEqualTo(reportPublicId);
        await Assert.That(json.RootElement.TryGetProperty("reporterId", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("reporterUsername", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("status", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("createdAt", out _)).IsTrue();
    }

    [Test]
    public async Task GetReport_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/moderation/reports/nonexistent-report-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Resolve Report ====================

    [Test]
    public async Task ResolveReport_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var body = JsonContent.Create(new { resolutionNote = "Resolved" });

        // Act
        var response = await client.PostAsync("/admin/moderation/reports/some-id/resolve", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ResolveReport_NonExistentReport_ReturnsBadRequest()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var body = JsonContent.Create(new { resolutionNote = "Resolved" });

        // Act
        var response = await client.PostAsync("/admin/moderation/reports/nonexistent-id/resolve", body);

        // Assert
        // ModerationUseCase.ResolveReportAsync returns Result.Failure("Report not found") -> BadRequest
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    // ==================== Dismiss Report ====================

    [Test]
    public async Task DismissReport_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var body = JsonContent.Create(new { resolutionNote = "Not valid" });

        // Act
        var response = await client.PostAsync("/admin/moderation/reports/some-id/dismiss", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task DismissReport_NonExistentReport_ReturnsBadRequest()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var body = JsonContent.Create(new { resolutionNote = "Not a real issue" });

        // Act
        var response = await client.PostAsync("/admin/moderation/reports/nonexistent-id/dismiss", body);

        // Assert
        // ModerationUseCase.ResolveReportAsync returns Result.Failure("Report not found") -> BadRequest
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    // ==================== Moderation Log ====================

    [Test]
    public async Task GetModerationLog_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/moderation/log?page=1");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetModerationLog_WithAdmin_ReturnsPagedResults()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/moderation/log?page=1");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("actions", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("total", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("page", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("pageSize", out _)).IsTrue();
        await Assert.That(json.RootElement.GetProperty("page").GetInt32()).IsEqualTo(1);
        await Assert.That(json.RootElement.GetProperty("pageSize").GetInt32()).IsEqualTo(20);
    }

    // ==================== Active Bans ====================

    [Test]
    public async Task GetActiveBans_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/moderation/bans?page=1");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetActiveBans_WithNonAdmin_ReturnsForbidden()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/admin/moderation/bans?page=1");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GetActiveBans_WithAdmin_ReturnsPagedResults()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/moderation/bans?page=1");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("bans", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("total", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("page", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("pageSize", out _)).IsTrue();
    }

    // ==================== Helpers ====================

    /// <summary>
    /// Seeds a user into the in-memory database and returns the database Id.
    /// </summary>
    private async Task<int> SeedUserAsync(string publicId, string displayName)
    {
        using var scope = _server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SnakkDbContext>();

        var user = new UserDatabaseEntity
        {
            PublicId = publicId,
            DisplayName = displayName,
            Email = $"{publicId}@test.com",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }

    /// <summary>
    /// Seeds a GlobalAdmin role for the given user so that ModerationRepository
    /// permission checks (CanModerateAsync / CanAdministerAsync) succeed.
    /// </summary>
    private async Task SeedGlobalAdminRoleAsync(int userId)
    {
        using var scope = _server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SnakkDbContext>();

        var role = new UserRoleDatabaseEntity
        {
            PublicId = $"role-admin-{userId}-{Guid.NewGuid():N}",
            UserId = userId,
            RoleId = (int)UserRoleTypeEnum.GlobalAdmin,
            AssignedByUserId = userId,
            AssignedAt = DateTime.UtcNow
        };

        db.UserRoles.Add(role);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds an active (non-expired, not-unbanned) ban for the given user.
    /// </summary>
    private async Task SeedActiveBanAsync(int userId, string userPublicId, int bannedByUserId)
    {
        using var scope = _server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SnakkDbContext>();

        var ban = new UserBanDatabaseEntity
        {
            PublicId = $"ban-{userPublicId}-{Guid.NewGuid():N}",
            UserId = userId,
            BanTypeId = 1, // ReadWrite
            Reason = "Test ban",
            BannedAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = null, // Permanent
            BannedByUserId = bannedByUserId,
            UnbannedAt = null
        };

        db.UserBans.Add(ban);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds a pending report between two users into the in-memory database.
    /// </summary>
    private async Task SeedReportAsync(string reportPublicId, int reporterDbId, int reportedDbId)
    {
        using var scope = _server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SnakkDbContext>();

        var report = new ReportDatabaseEntity
        {
            PublicId = reportPublicId,
            ReporterUserId = reporterDbId,
            ReportedUserId = reportedDbId,
            Details = "Test report details",
            StatusId = (int)ReportStatusEnum.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.Reports.Add(report);
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
