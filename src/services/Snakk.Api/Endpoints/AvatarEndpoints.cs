namespace Snakk.Api.Endpoints;

using Snakk.Application.DTOs.Responses;
using Snakk.Application.Services;
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

        // Entity avatars (community, hub, space) — requires mod/admin permissions
        group.MapPost("/upload/{entityType}/{entityId}", UploadEntityAvatarAsync)
            .WithName("UploadEntityAvatar")
            .Produces<AvatarUploadResponse>()
            .RequireAuthorization()
            .DisableAntiforgery();

        group.MapDelete("/{entityType}/{entityId}", DeleteEntityAvatarAsync)
            .WithName("DeleteEntityAvatar")
            .Produces<MessageResponse>()
            .RequireAuthorization();
    }

    private static async Task<IResult> UploadAvatarAsync(
        HttpContext httpContext,
        IUserRepository userRepository,
        IFileStorage fileStorage)
    {
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

        if (file.Length > 2 * 1024 * 1024)
            return Results.BadRequest(new { error = "File too large. Maximum size is 2MB." });

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
            return Results.BadRequest(new { error = "Invalid file type. Allowed: JPEG, PNG, WebP" });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(extension))
            return Results.BadRequest(new { error = "Invalid file extension" });

        if (!await FileValidationHelper.IsValidImageFileAsync(file, extension))
            return Results.BadRequest(new { error = "Invalid image file format" });

        // Delete old avatar + thumbnail from storage
        if (!string.IsNullOrEmpty(user.AvatarFileName))
        {
            try { await fileStorage.DeleteAsync($"avatars/uploaded/{user.AvatarFileName}"); }
            catch { /* best-effort cleanup */ }
        }
        if (!string.IsNullOrEmpty(user.AvatarThumbnailFileName))
        {
            try { await fileStorage.DeleteAsync($"avatars/uploaded/{user.AvatarThumbnailFileName}"); }
            catch { /* best-effort cleanup */ }
        }
        if (!string.IsNullOrEmpty(user.AvatarMicroFileName))
        {
            try { await fileStorage.DeleteAsync($"avatars/uploaded/{user.AvatarMicroFileName}"); }
            catch { /* best-effort cleanup */ }
        }

        // Process: resize to 256x256 max, encode as WebP
        var nextRevision = user.AvatarRevision + 1;
        var newFileName = $"{userId.Value}_r{nextRevision}.webp";
        var thumbFileName = $"{userId.Value}_r{nextRevision}_thumb.webp";

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

        // Save full-size avatar
        using var outputStream = new MemoryStream();
        await image.SaveAsWebpAsync(outputStream, new WebpEncoder { Quality = 80 });
        outputStream.Position = 0;
        await fileStorage.SaveAsync(
            $"avatars/uploaded/{newFileName}",
            outputStream,
            "public, max-age=31536000, immutable");

        // Generate and save 80x80 thumbnail
        using var thumbImage = image.Clone(x => x.Resize(new ResizeOptions
        {
            Size = new Size(80, 80),
            Mode = ResizeMode.Max
        }));
        using var thumbStream = new MemoryStream();
        await thumbImage.SaveAsWebpAsync(thumbStream, new WebpEncoder { Quality = 75 });
        thumbStream.Position = 0;
        await fileStorage.SaveAsync(
            $"avatars/uploaded/{thumbFileName}",
            thumbStream,
            "public, max-age=31536000, immutable");

        // Generate and save 26x26 micro thumbnail
        var microFileName = $"{userId.Value}_r{nextRevision}_micro.webp";
        using var microImage = image.Clone(x => x.Resize(new ResizeOptions
        {
            Size = new Size(26, 26),
            Mode = ResizeMode.Max
        }));
        using var microStream = new MemoryStream();
        await microImage.SaveAsWebpAsync(microStream, new WebpEncoder { Quality = 70 });
        microStream.Position = 0;
        await fileStorage.SaveAsync(
            $"avatars/uploaded/{microFileName}",
            microStream,
            "public, max-age=31536000, immutable");

        user.SetAvatarFileName(newFileName, thumbFileName, microFileName);
        await userRepository.UpdateAsync(user);

        return TypedResults.Ok(new AvatarUploadResponse(
            "Avatar uploaded successfully",
            fileStorage.GetPublicUrl($"avatars/uploaded/{newFileName}")));
    }

    private static async Task<IResult> DeleteAvatarAsync(
        HttpContext httpContext,
        IUserRepository userRepository,
        IFileStorage fileStorage)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim is null)
            return Results.Unauthorized();

        var userId = UserId.From(userIdClaim.Value);
        var user = await userRepository.GetByPublicIdAsync(userId);
        if (user is null)
            return Results.NotFound(new { error = "User not found" });

        if (!string.IsNullOrEmpty(user.AvatarFileName))
        {
            try { await fileStorage.DeleteAsync($"avatars/uploaded/{user.AvatarFileName}"); }
            catch { /* best-effort cleanup */ }
        }
        if (!string.IsNullOrEmpty(user.AvatarThumbnailFileName))
        {
            try { await fileStorage.DeleteAsync($"avatars/uploaded/{user.AvatarThumbnailFileName}"); }
            catch { /* best-effort cleanup */ }
        }
        if (!string.IsNullOrEmpty(user.AvatarMicroFileName))
        {
            try { await fileStorage.DeleteAsync($"avatars/uploaded/{user.AvatarMicroFileName}"); }
            catch { /* best-effort cleanup */ }
        }

        user.ClearAvatar();
        await userRepository.UpdateAsync(user);

        return TypedResults.Ok(new MessageResponse("Avatar deleted. Using generated avatar."));
    }

    private static async Task<IResult> UploadEntityAvatarAsync(
        string entityType,
        string entityId,
        HttpContext httpContext,
        IPermissionService permissionService,
        ICommunityRepository communityRepository,
        IHubRepository hubRepository,
        ISpaceRepository spaceRepository,
        IFileStorage fileStorage)
    {
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        // Validate entity type and check permissions
        var (permissionName, scope) = entityType.ToLowerInvariant() switch
        {
            "community" => ("ManageCommunity", "community"),
            "hub" => ("ManageHub", "hub"),
            "space" => ("ManageSpace", "space"),
            _ => (null as string, null as string)
        };

        if (permissionName is null)
            return Results.BadRequest(new { error = "Invalid entity type. Allowed: community, hub, space" });

        if (!await permissionService.UserHasPermissionAsync(userId, permissionName, scope, entityId))
            return Results.Forbid();

        // Read and validate file
        var form = await httpContext.Request.ReadFormAsync();
        var file = form.Files.GetFile("avatar");

        if (file is null || file.Length == 0)
            return Results.BadRequest(new { error = "No file uploaded" });

        if (file.Length > 2 * 1024 * 1024)
            return Results.BadRequest(new { error = "File too large. Maximum size is 2MB." });

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
            return Results.BadRequest(new { error = "Invalid file type. Allowed: JPEG, PNG, WebP" });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(extension))
            return Results.BadRequest(new { error = "Invalid file extension" });

        if (!await FileValidationHelper.IsValidImageFileAsync(file, extension))
            return Results.BadRequest(new { error = "Invalid image file format" });

        // Load entity and get current avatar info
        switch (entityType.ToLowerInvariant())
        {
            case "community":
            {
                var entity = await communityRepository.GetByPublicIdAsync(CommunityId.From(entityId));
                if (entity is null) return Results.NotFound();

                var (newFileName, thumbFileName, microFileName) = await ProcessAndSaveAvatarAsync(
                    file, fileStorage, entityType, entityId, entity.AvatarRevision,
                    entity.AvatarFileName, entity.AvatarThumbnailFileName, entity.AvatarMicroFileName);

                entity.SetAvatarFileName(newFileName, thumbFileName, microFileName);
                await communityRepository.UpdateAsync(entity);

                return TypedResults.Ok(new AvatarUploadResponse(
                    "Avatar uploaded successfully",
                    fileStorage.GetPublicUrl($"avatars/uploaded/{entityType}/{newFileName}")));
            }
            case "hub":
            {
                var entity = await hubRepository.GetByPublicIdAsync(HubId.From(entityId));
                if (entity is null) return Results.NotFound();

                var (newFileName, thumbFileName, microFileName) = await ProcessAndSaveAvatarAsync(
                    file, fileStorage, entityType, entityId, entity.AvatarRevision,
                    entity.AvatarFileName, entity.AvatarThumbnailFileName, entity.AvatarMicroFileName);

                entity.SetAvatarFileName(newFileName, thumbFileName, microFileName);
                await hubRepository.UpdateAsync(entity);

                return TypedResults.Ok(new AvatarUploadResponse(
                    "Avatar uploaded successfully",
                    fileStorage.GetPublicUrl($"avatars/uploaded/{entityType}/{newFileName}")));
            }
            case "space":
            {
                var entity = await spaceRepository.GetByPublicIdAsync(SpaceId.From(entityId));
                if (entity is null) return Results.NotFound();

                var (newFileName, thumbFileName, microFileName) = await ProcessAndSaveAvatarAsync(
                    file, fileStorage, entityType, entityId, entity.AvatarRevision,
                    entity.AvatarFileName, entity.AvatarThumbnailFileName, entity.AvatarMicroFileName);

                entity.SetAvatarFileName(newFileName, thumbFileName, microFileName);
                await spaceRepository.UpdateAsync(entity);

                return TypedResults.Ok(new AvatarUploadResponse(
                    "Avatar uploaded successfully",
                    fileStorage.GetPublicUrl($"avatars/uploaded/{entityType}/{newFileName}")));
            }
            default:
                return Results.BadRequest(new { error = "Invalid entity type" });
        }
    }

    private static async Task<(string FileName, string ThumbnailFileName, string MicroFileName)> ProcessAndSaveAvatarAsync(
        IFormFile file,
        IFileStorage fileStorage,
        string entityType,
        string entityId,
        int currentRevision,
        string? oldAvatarFileName,
        string? oldThumbnailFileName,
        string? oldMicroFileName = null)
    {
        // Delete old avatar + thumbnail + micro from storage
        if (!string.IsNullOrEmpty(oldAvatarFileName))
        {
            try { await fileStorage.DeleteAsync($"avatars/uploaded/{entityType}/{oldAvatarFileName}"); }
            catch { /* best-effort cleanup */ }
        }
        if (!string.IsNullOrEmpty(oldThumbnailFileName))
        {
            try { await fileStorage.DeleteAsync($"avatars/uploaded/{entityType}/{oldThumbnailFileName}"); }
            catch { /* best-effort cleanup */ }
        }
        if (!string.IsNullOrEmpty(oldMicroFileName))
        {
            try { await fileStorage.DeleteAsync($"avatars/uploaded/{entityType}/{oldMicroFileName}"); }
            catch { /* best-effort cleanup */ }
        }

        // Process: resize to 256x256 max, encode as WebP
        var nextRevision = currentRevision + 1;
        var newFileName = $"{entityId}_r{nextRevision}.webp";
        var thumbFileName = $"{entityId}_r{nextRevision}_thumb.webp";

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

        // Save full-size avatar
        using var outputStream = new MemoryStream();
        await image.SaveAsWebpAsync(outputStream, new WebpEncoder { Quality = 80 });
        outputStream.Position = 0;
        await fileStorage.SaveAsync(
            $"avatars/uploaded/{entityType}/{newFileName}",
            outputStream,
            "public, max-age=31536000, immutable");

        // Generate and save 80x80 thumbnail
        using var thumbImage = image.Clone(x => x.Resize(new ResizeOptions
        {
            Size = new Size(80, 80),
            Mode = ResizeMode.Max
        }));
        using var thumbStream = new MemoryStream();
        await thumbImage.SaveAsWebpAsync(thumbStream, new WebpEncoder { Quality = 75 });
        thumbStream.Position = 0;
        await fileStorage.SaveAsync(
            $"avatars/uploaded/{entityType}/{thumbFileName}",
            thumbStream,
            "public, max-age=31536000, immutable");

        // Generate and save 26x26 micro thumbnail
        var microFileName = $"{entityId}_r{nextRevision}_micro.webp";
        using var microImage = image.Clone(x => x.Resize(new ResizeOptions
        {
            Size = new Size(26, 26),
            Mode = ResizeMode.Max
        }));
        using var microStream = new MemoryStream();
        await microImage.SaveAsWebpAsync(microStream, new WebpEncoder { Quality = 70 });
        microStream.Position = 0;
        await fileStorage.SaveAsync(
            $"avatars/uploaded/{entityType}/{microFileName}",
            microStream,
            "public, max-age=31536000, immutable");

        return (newFileName, thumbFileName, microFileName);
    }

    private static async Task<IResult> DeleteEntityAvatarAsync(
        string entityType,
        string entityId,
        HttpContext httpContext,
        IPermissionService permissionService,
        ICommunityRepository communityRepository,
        IHubRepository hubRepository,
        ISpaceRepository spaceRepository,
        IFileStorage fileStorage)
    {
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        var (permissionName, scope) = entityType.ToLowerInvariant() switch
        {
            "community" => ("ManageCommunity", "community"),
            "hub" => ("ManageHub", "hub"),
            "space" => ("ManageSpace", "space"),
            _ => (null as string, null as string)
        };

        if (permissionName is null)
            return Results.BadRequest(new { error = "Invalid entity type" });

        if (!await permissionService.UserHasPermissionAsync(userId, permissionName, scope, entityId))
            return Results.Forbid();

        switch (entityType.ToLowerInvariant())
        {
            case "community":
            {
                var entity = await communityRepository.GetByPublicIdAsync(CommunityId.From(entityId));
                if (entity is null) return Results.NotFound();

                if (!string.IsNullOrEmpty(entity.AvatarFileName))
                {
                    try { await fileStorage.DeleteAsync($"avatars/uploaded/community/{entity.AvatarFileName}"); }
                    catch { /* best-effort */ }
                }
                if (!string.IsNullOrEmpty(entity.AvatarThumbnailFileName))
                {
                    try { await fileStorage.DeleteAsync($"avatars/uploaded/community/{entity.AvatarThumbnailFileName}"); }
                    catch { /* best-effort */ }
                }
                if (!string.IsNullOrEmpty(entity.AvatarMicroFileName))
                {
                    try { await fileStorage.DeleteAsync($"avatars/uploaded/community/{entity.AvatarMicroFileName}"); }
                    catch { /* best-effort */ }
                }

                entity.ClearAvatar();
                await communityRepository.UpdateAsync(entity);
                break;
            }
            case "hub":
            {
                var entity = await hubRepository.GetByPublicIdAsync(HubId.From(entityId));
                if (entity is null) return Results.NotFound();

                if (!string.IsNullOrEmpty(entity.AvatarFileName))
                {
                    try { await fileStorage.DeleteAsync($"avatars/uploaded/hub/{entity.AvatarFileName}"); }
                    catch { /* best-effort */ }
                }
                if (!string.IsNullOrEmpty(entity.AvatarThumbnailFileName))
                {
                    try { await fileStorage.DeleteAsync($"avatars/uploaded/hub/{entity.AvatarThumbnailFileName}"); }
                    catch { /* best-effort */ }
                }
                if (!string.IsNullOrEmpty(entity.AvatarMicroFileName))
                {
                    try { await fileStorage.DeleteAsync($"avatars/uploaded/hub/{entity.AvatarMicroFileName}"); }
                    catch { /* best-effort */ }
                }

                entity.ClearAvatar();
                await hubRepository.UpdateAsync(entity);
                break;
            }
            case "space":
            {
                var entity = await spaceRepository.GetByPublicIdAsync(SpaceId.From(entityId));
                if (entity is null) return Results.NotFound();

                if (!string.IsNullOrEmpty(entity.AvatarFileName))
                {
                    try { await fileStorage.DeleteAsync($"avatars/uploaded/space/{entity.AvatarFileName}"); }
                    catch { /* best-effort */ }
                }
                if (!string.IsNullOrEmpty(entity.AvatarThumbnailFileName))
                {
                    try { await fileStorage.DeleteAsync($"avatars/uploaded/space/{entity.AvatarThumbnailFileName}"); }
                    catch { /* best-effort */ }
                }
                if (!string.IsNullOrEmpty(entity.AvatarMicroFileName))
                {
                    try { await fileStorage.DeleteAsync($"avatars/uploaded/space/{entity.AvatarMicroFileName}"); }
                    catch { /* best-effort */ }
                }

                entity.ClearAvatar();
                await spaceRepository.UpdateAsync(entity);
                break;
            }
        }

        return TypedResults.Ok(new MessageResponse("Avatar deleted. Using generated avatar."));
    }
}
