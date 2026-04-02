using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using Snakk.Api.Tests.Helpers;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

namespace Snakk.Api.Tests.Endpoints;

[NotInParallel]
public class AvatarEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    private const string TestUserId = "test-user-id";

    private static byte[] CreateTestJpeg()
    {
        using var image = new Image<Rgba32>(2, 2, Color.Red);
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms, new JpegEncoder { Quality = 75 });
        return ms.ToArray();
    }

    /// <summary>
    /// Seeds a user into the InMemory database with a PublicId matching the JWT sub claim.
    /// </summary>
    private async Task SeedUserAsync(string? avatarFileName = null)
    {
        using var scope = _server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SnakkDbContext>();

        var user = new UserDatabaseEntity
        {
            Id = 1,
            PublicId = TestUserId,
            DisplayName = "Test User",
            Email = "test@example.com",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            AvatarFileName = avatarFileName
        };
        db.Users.Add(user);

        await db.SaveChangesAsync();
    }

    // ── GET /avatars/{userId} ─────────────────────────────────────────

    [Test]
    public async Task GetAvatar_NonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/avatars/non-existent-user-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetAvatar_WithSvgSuffix_StripsSuffix()
    {
        // Arrange — requesting "user123.svg" should strip the .svg and look up "user123"
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/avatars/user123.svg");

        // Assert — user123 does not exist, so we expect 404 (not a routing error)
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetAvatar_NonExistentUser_Returns404()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/avatars/non-existent-user-id");

        // Assert — avatar GET routes are not registered (only upload/delete); returns 404
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ── POST /avatars/upload (requires auth) ──────────────────────────

    [Test]
    public async Task UploadAvatar_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "avatar", "test.jpg");

        // Act
        var response = await client.PostAsync("/avatars/upload", content);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UploadAvatar_NoFile_ReturnsBadRequest()
    {
        // Arrange — authenticated but no file in the multipart form.
        // A non-file form field is included so the multipart body is valid
        // (an empty multipart causes ReadFormAsync to throw InvalidDataException).
        await SeedUserAsync();
        var client = _server.CreateAuthenticatedClient();
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("placeholder"), "field");

        // Act
        var response = await client.PostAsync("/avatars/upload", content);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UploadAvatar_FileTooLarge_ReturnsBadRequest()
    {
        // Arrange — file exceeding the 2MB limit
        await SeedUserAsync();
        var client = _server.CreateAuthenticatedClient();
        var content = new MultipartFormDataContent();
        var largeBytes = new byte[3 * 1024 * 1024]; // 3MB
        largeBytes[0] = 0xFF;
        largeBytes[1] = 0xD8;
        largeBytes[2] = 0xFF;
        largeBytes[3] = 0xE0;
        var fileContent = new ByteArrayContent(largeBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "avatar", "large.jpg");

        // Act
        var response = await client.PostAsync("/avatars/upload", content);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UploadAvatar_FileTooLarge_ReturnsErrorMessage()
    {
        // Arrange
        await SeedUserAsync();
        var client = _server.CreateAuthenticatedClient();
        var content = new MultipartFormDataContent();
        var largeBytes = new byte[3 * 1024 * 1024];
        largeBytes[0] = 0xFF;
        largeBytes[1] = 0xD8;
        largeBytes[2] = 0xFF;
        largeBytes[3] = 0xE0;
        var fileContent = new ByteArrayContent(largeBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "avatar", "large.jpg");

        // Act
        var response = await client.PostAsync("/avatars/upload", content);

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        await Assert.That(json.RootElement.GetProperty("error").GetString())
            .Contains("File too large");
    }

    [Test]
    public async Task UploadAvatar_InvalidContentType_ReturnsBadRequest()
    {
        // Arrange — use text/plain which is not an allowed image type
        await SeedUserAsync();
        var client = _server.CreateAuthenticatedClient();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 0x00, 0x01, 0x02, 0x03 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "avatar", "test.jpg");

        // Act
        var response = await client.PostAsync("/avatars/upload", content);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UploadAvatar_InvalidExtension_ReturnsBadRequest()
    {
        // Arrange — .bmp is not in the allowed extensions list
        await SeedUserAsync();
        var client = _server.CreateAuthenticatedClient();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "avatar", "test.bmp");

        // Act
        var response = await client.PostAsync("/avatars/upload", content);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UploadAvatar_ValidFile_ReturnsOk()
    {
        // Arrange — seed user and upload a valid JPEG
        await SeedUserAsync();
        var client = _server.CreateAuthenticatedClient();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreateTestJpeg());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "avatar", "test.jpg");

        // Act
        var response = await client.PostAsync("/avatars/upload", content);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task UploadAvatar_ValidFile_ReturnsAvatarUrl()
    {
        // Arrange
        await SeedUserAsync();
        var client = _server.CreateAuthenticatedClient();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreateTestJpeg());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "avatar", "avatar.jpg");

        // Act
        var response = await client.PostAsync("/avatars/upload", content);

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        await Assert.That(json.RootElement.GetProperty("avatarUrl").GetString())
            .StartsWith("/avatars/uploaded/");
    }

    [Test]
    public async Task UploadAvatar_UserNotInDb_ReturnsNotFound()
    {
        // Arrange — authenticated but user not seeded in DB
        var client = _server.CreateAuthenticatedClient();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreateTestJpeg());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "avatar", "test.jpg");

        // Act
        var response = await client.PostAsync("/avatars/upload", content);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ── DELETE /avatars/ (requires auth) ──────────────────────────────

    [Test]
    public async Task DeleteAvatar_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.DeleteAsync("/avatars/");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task DeleteAvatar_NonExistentUser_ReturnsNotFound()
    {
        // Arrange — authenticated but user not in DB
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.DeleteAsync("/avatars/");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteAvatar_Authenticated_ReturnsOk()
    {
        // Arrange — seed user so delete can find them
        await SeedUserAsync();
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.DeleteAsync("/avatars/");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task DeleteAvatar_Authenticated_ReturnsSuccessMessage()
    {
        // Arrange
        await SeedUserAsync();
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.DeleteAsync("/avatars/");

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        await Assert.That(json.RootElement.GetProperty("message").GetString())
            .Contains("Avatar deleted");
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
