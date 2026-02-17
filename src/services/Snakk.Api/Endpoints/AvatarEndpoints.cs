namespace Snakk.Api.Endpoints;

using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using System.Security.Claims;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Snakk.Api.Helpers;

public static class AvatarEndpoints
{
    public static void MapAvatarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/avatars")
            .WithTags("Avatars");

        group.MapGet("/{userId}", GetAvatarAsync)
            .WithName("GetAvatar");

        group.MapGet("/user/{userId}", GetAvatarAsync)
            .WithName("GetUserAvatar");

        group.MapGet("/{userId}/generated", GetGeneratedAvatar)
            .WithName("GetGeneratedAvatar");

        group.MapGet("/hub/{publicId}", GetHubAvatar)
            .WithName("GetHubAvatar");

        group.MapGet("/space/{publicId}", GetSpaceAvatar)
            .WithName("GetSpaceAvatar");

        group.MapGet("/community/{publicId}", GetCommunityAvatar)
            .WithName("GetCommunityAvatar");

        group.MapPost("/upload", UploadAvatarAsync)
            .WithName("UploadAvatar")
            .RequireAuthorization()
            .DisableAntiforgery();

        group.MapDelete("/", DeleteAvatarAsync)
            .WithName("DeleteAvatar")
            .RequireAuthorization();
    }

    private static async Task<IResult> GetAvatarAsync(
        string userId,
        string? type,
        HttpContext httpContext,
        IUserRepository userRepository,
        IWebHostEnvironment env)
    {
        // Strip .svg extension if present (for CDN-friendly URLs)
        var cleanUserId = userId.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
            ? userId[..^4]
            : userId;

        // Check if browser supports WebP
        var acceptHeader = httpContext.Request.Headers.Accept.ToString();
        var supportsWebP = acceptHeader.Contains("image/webp", StringComparison.OrdinalIgnoreCase);

        // Handle empty userId - return default avatar for unknown user
        if (string.IsNullOrWhiteSpace(cleanUserId))
        {
            return GetGeneratedAvatar("unknown", type, httpContext, env);
        }

        var user = await userRepository.GetByPublicIdAsync(UserId.From(cleanUserId));

        // If user has uploaded avatar, serve it (type parameter is ignored for uploaded avatars)
        if (user != null && !string.IsNullOrEmpty(user.AvatarFileName))
        {
            var avatarPath = Path.Combine(env.ContentRootPath, "avatars", user.AvatarFileName);
            if (File.Exists(avatarPath))
            {
                var extension = Path.GetExtension(user.AvatarFileName).ToLowerInvariant();

                // If browser supports WebP and image is not already WebP, convert on-the-fly
                if (supportsWebP && extension != ".webp" && (extension == ".jpg" || extension == ".jpeg" || extension == ".png"))
                {
                    // Check for cached WebP version
                    var webpCachePath = Path.Combine(env.ContentRootPath, "avatars", "webp-cache", $"{Path.GetFileNameWithoutExtension(user.AvatarFileName)}.webp");
                    byte[] webpData;

                    if (File.Exists(webpCachePath))
                    {
                        // Serve cached WebP
                        webpData = await File.ReadAllBytesAsync(webpCachePath);
                    }
                    else
                    {
                        // Convert to WebP and cache
                        Directory.CreateDirectory(Path.Combine(env.ContentRootPath, "avatars", "webp-cache"));

                        using var image = await Image.LoadAsync(avatarPath);
                        using var ms = new MemoryStream();
                        await image.SaveAsync(ms, new WebpEncoder { Quality = 85 });
                        webpData = ms.ToArray();

                        // Cache the WebP version
                        await File.WriteAllBytesAsync(webpCachePath, webpData);
                    }

                    httpContext.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                    return Results.File(webpData, "image/webp", enableRangeProcessing: true);
                }

                // Serve original format
                var contentType = extension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => "application/octet-stream"
                };

                // Set cache headers for CDN (1 year for immutable content)
                httpContext.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                return Results.File(
                    await File.ReadAllBytesAsync(avatarPath),
                    contentType,
                    enableRangeProcessing: true);
            }
        }

        // Fall back to pre-generated avatar file
        var generatedPath = Path.Combine(
            env.ContentRootPath,
            "avatars",
            "generated",
            "users",
            $"{cleanUserId}.svg"
        );

        if (File.Exists(generatedPath))
        {
            httpContext.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            httpContext.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            return Results.File(generatedPath, "image/svg+xml", enableRangeProcessing: true);
        }

        // If neither uploaded nor generated avatar exists, return 404
        return Results.NotFound(new { error = "Avatar not found" });
    }

    private static IResult GetGeneratedAvatar(
        string userId,
        string? type,
        HttpContext httpContext,
        IWebHostEnvironment env)
    {
        // Strip .svg extension if present
        var cleanUserId = userId.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
            ? userId[..^4]
            : userId;

        // Build file path
        var filePath = Path.Combine(
            env.ContentRootPath,
            "avatars",
            "generated",
            "users",
            $"{cleanUserId}.svg"
        );

        // Check if file exists
        if (!File.Exists(filePath))
        {
            return Results.NotFound(new { error = "Avatar not found" });
        }

        // Set aggressive cache headers
        httpContext.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        httpContext.Response.Headers.Append("X-Content-Type-Options", "nosniff");

        // Serve static file
        return Results.File(filePath, "image/svg+xml", enableRangeProcessing: true);
    }

    private static IResult GetHubAvatar(
        string publicId,
        string? type,
        HttpContext httpContext,
        IWebHostEnvironment env)
    {
        // Strip .svg extension if present
        var cleanId = publicId.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? publicId[..^4] : publicId;

        // Build file path
        var filePath = Path.Combine(
            env.ContentRootPath,
            "avatars",
            "generated",
            "hubs",
            $"{cleanId}.svg"
        );

        // Check if file exists
        if (!File.Exists(filePath))
        {
            return Results.NotFound(new { error = "Avatar not found" });
        }

        // Set aggressive cache headers
        httpContext.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        httpContext.Response.Headers.Append("X-Content-Type-Options", "nosniff");

        // Serve static file
        return Results.File(filePath, "image/svg+xml", enableRangeProcessing: true);
    }

    private static IResult GetSpaceAvatar(
        string publicId,
        string? type,
        HttpContext httpContext,
        IWebHostEnvironment env)
    {
        // Strip .svg extension if present
        var cleanId = publicId.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? publicId[..^4] : publicId;

        // Build file path
        var filePath = Path.Combine(
            env.ContentRootPath,
            "avatars",
            "generated",
            "spaces",
            $"{cleanId}.svg"
        );

        // Check if file exists
        if (!File.Exists(filePath))
        {
            return Results.NotFound(new { error = "Avatar not found" });
        }

        // Set aggressive cache headers
        httpContext.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        httpContext.Response.Headers.Append("X-Content-Type-Options", "nosniff");

        // Serve static file
        return Results.File(filePath, "image/svg+xml", enableRangeProcessing: true);
    }

    private static IResult GetCommunityAvatar(
        string publicId,
        string? type,
        HttpContext httpContext,
        IWebHostEnvironment env)
    {
        // Strip .svg extension if present
        var cleanId = publicId.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? publicId[..^4] : publicId;

        // Build file path
        var filePath = Path.Combine(
            env.ContentRootPath,
            "avatars",
            "generated",
            "communities",
            $"{cleanId}.svg"
        );

        // Check if file exists
        if (!File.Exists(filePath))
        {
            return Results.NotFound(new { error = "Avatar not found" });
        }

        // Set aggressive cache headers
        httpContext.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        httpContext.Response.Headers.Append("X-Content-Type-Options", "nosniff");

        // Serve static file
        return Results.File(filePath, "image/svg+xml", enableRangeProcessing: true);
    }

    private static async Task<IResult> UploadAvatarAsync(
        HttpContext httpContext,
        IUserRepository userRepository,
        IWebHostEnvironment env)
    {
        // Require authentication
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Results.Unauthorized();

        var userId = UserId.From(userIdClaim.Value);
        var user = await userRepository.GetByPublicIdAsync(userId);

        if (user == null)
            return Results.NotFound(new { error = "User not found" });

        var form = await httpContext.Request.ReadFormAsync();
        var file = form.Files.GetFile("avatar");

        if (file == null || file.Length == 0)
            return Results.BadRequest(new { error = "No file uploaded" });

        // Validate file size (max 2MB)
        if (file.Length > 2 * 1024 * 1024)
            return Results.BadRequest(new { error = "File too large. Maximum size is 2MB." });

        // Validate content type
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
            return Results.BadRequest(new { error = "Invalid file type. Allowed: JPEG, PNG, GIF, WebP" });

        // Validate file extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        if (!allowedExtensions.Contains(extension))
            return Results.BadRequest(new { error = "Invalid file extension" });

        // Validate file magic bytes
        if (!await FileValidationHelper.IsValidImageFileAsync(file, extension))
            return Results.BadRequest(new { error = "Invalid image file format" });

        // Create avatars directory if it doesn't exist
        var avatarsDir = Path.Combine(env.ContentRootPath, "avatars");
        Directory.CreateDirectory(avatarsDir);

        // Delete old avatar if exists
        if (!string.IsNullOrEmpty(user.AvatarFileName))
        {
            var oldPath = Path.Combine(avatarsDir, user.AvatarFileName);
            if (File.Exists(oldPath))
                File.Delete(oldPath);
        }

        // Generate unique filename
        var newFileName = $"{userId.Value}{extension}";
        var newPath = Path.Combine(avatarsDir, newFileName);

        // Save file
        await using (var stream = new FileStream(newPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Update user
        user.SetAvatarFileName(newFileName);
        await userRepository.UpdateAsync(user);

        return Results.Ok(new
        {
            message = "Avatar uploaded successfully",
            avatarUrl = $"/avatars/{userId.Value}"
        });
    }

    private static async Task<IResult> DeleteAvatarAsync(
        HttpContext httpContext,
        IUserRepository userRepository,
        IWebHostEnvironment env)
    {
        // Require authentication
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Results.Unauthorized();

        var userId = UserId.From(userIdClaim.Value);
        var user = await userRepository.GetByPublicIdAsync(userId);

        if (user == null)
            return Results.NotFound(new { error = "User not found" });

        // Delete file if exists
        if (!string.IsNullOrEmpty(user.AvatarFileName))
        {
            var avatarsDir = Path.Combine(env.ContentRootPath, "avatars");
            var avatarPath = Path.Combine(avatarsDir, user.AvatarFileName);
            if (File.Exists(avatarPath))
                File.Delete(avatarPath);
        }

        // Clear avatar from user
        user.ClearAvatar();
        await userRepository.UpdateAsync(user);

        return Results.Ok(new { message = "Avatar deleted. Using generated avatar." });
    }
}
