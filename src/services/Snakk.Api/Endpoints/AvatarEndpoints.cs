namespace Snakk.Api.Endpoints;

using Snakk.Application.DTOs.Responses;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using System.Security.Claims;
using Snakk.Api.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

public static class AvatarEndpoints
{
    public static void MapAvatarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/avatars")
            .WithTags("Avatars");

        group.MapPost("/upload", UploadAvatarAsync)
            .WithName("UploadAvatar")
            .Produces<AvatarUploadResponse>()
            .RequireAuthorization()
            .DisableAntiforgery(); // File uploads via fetch() — CSRF mitigated by JWT auth

        group.MapDelete("/", DeleteAvatarAsync)
            .WithName("DeleteAvatar")
            .Produces<MessageResponse>()
            .RequireAuthorization();
    }

    private static async Task<IResult> UploadAvatarAsync(
        HttpContext httpContext,
        IUserRepository userRepository,
        IConfiguration configuration)
    {
        // Require authentication
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        var userId = UserId.From(userIdClaim.Value);
        var user = await userRepository.GetByPublicIdAsync(userId);

        if (user is null)
            return Results.NotFound(new { error = "User not found" });

        var form = await httpContext.Request.ReadFormAsync();
        var file = form.Files.GetFile("avatar");

        if (file is null || file.Length == 0)
            return Results.BadRequest(new { error = "No file uploaded" });

        // Validate file size (max 2MB)
        if (file.Length > 2 * 1024 * 1024)
            return Results.BadRequest(new { error = "File too large. Maximum size is 2MB." });

        // Validate content type
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };

        if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
            return Results.BadRequest(new { error = "Invalid file type. Allowed: JPEG, PNG, WebP" });

        // Validate file extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

        if (!allowedExtensions.Contains(extension))
            return Results.BadRequest(new { error = "Invalid file extension" });

        // Validate file magic bytes
        if (!await FileValidationHelper.IsValidImageFileAsync(file, extension))
            return Results.BadRequest(new { error = "Invalid image file format" });

        // Save to shared storage: {FileStorage:BasePath}/avatars/uploaded/
        var basePath = configuration["FileStorage:BasePath"] ?? "storage";
        var avatarsDir = Path.Combine(basePath, "avatars", "uploaded");
        Directory.CreateDirectory(avatarsDir);

        // Delete old avatar if exists
        if (!string.IsNullOrEmpty(user.AvatarFileName))
        {
            var oldPath = Path.Combine(avatarsDir, user.AvatarFileName);

            if (File.Exists(oldPath))
                File.Delete(oldPath);
        }

        // Process: resize to 256x256 max, encode as WebP
        var newFileName = $"{userId.Value}.webp";
        var newPath = Path.Combine(avatarsDir, newFileName);

        using var inputStream = file.OpenReadStream();
        using var image = await Image.LoadAsync(inputStream);

        if (image.Width > 256 || image.Height > 256)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(256, 256),
                Mode = ResizeMode.Max
            }));
        }

        await image.SaveAsWebpAsync(newPath, new WebpEncoder { Quality = 80 });

        // Update user
        user.SetAvatarFileName(newFileName);
        await userRepository.UpdateAsync(user);

        return TypedResults.Ok(new AvatarUploadResponse(
            "Avatar uploaded successfully",
            $"/avatars/uploaded/{newFileName}"));
    }

    private static async Task<IResult> DeleteAvatarAsync(
        HttpContext httpContext,
        IUserRepository userRepository,
        IConfiguration configuration)
    {
        // Require authentication
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        var userId = UserId.From(userIdClaim.Value);
        var user = await userRepository.GetByPublicIdAsync(userId);

        if (user is null)
            return Results.NotFound(new { error = "User not found" });

        // Delete file if exists
        if (!string.IsNullOrEmpty(user.AvatarFileName))
        {
            var basePath = configuration["FileStorage:BasePath"] ?? "storage";
            var avatarPath = Path.Combine(basePath, "avatars", "uploaded", user.AvatarFileName);

            if (File.Exists(avatarPath))
                File.Delete(avatarPath);
        }

        // Clear avatar from user
        user.ClearAvatar();
        await userRepository.UpdateAsync(user);

        return TypedResults.Ok(new MessageResponse("Avatar deleted. Using generated avatar."));
    }
}
