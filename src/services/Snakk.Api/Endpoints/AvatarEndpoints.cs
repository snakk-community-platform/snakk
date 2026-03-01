namespace Snakk.Api.Endpoints;

using Snakk.Application.DTOs.Responses;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using System.Security.Claims;
using Snakk.Api.Helpers;

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
            .DisableAntiforgery();

        group.MapDelete("/", DeleteAvatarAsync)
            .WithName("DeleteAvatar")
            .Produces<MessageResponse>()
            .RequireAuthorization();
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

        return TypedResults.Ok(new AvatarUploadResponse(
            "Avatar uploaded successfully",
            $"/avatars/{userId.Value}"));
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

        return TypedResults.Ok(new MessageResponse("Avatar deleted. Using generated avatar."));
    }
}
