namespace Snakk.Application.DTOs.Responses;

public record RegisterResponse(
    string Message,
    string AccessToken,
    string RefreshToken,
    RegisterUserInfo User);

public record RegisterUserInfo(
    string Id,
    string Email,
    string DisplayName,
    bool EmailVerified);

public record CurrentUserResponse(
    string PublicId,
    string DisplayName,
    string Email,
    bool EmailVerified,
    IReadOnlyList<string> ConnectedProviders,
    bool AutoFollowOnReply,
    string? Timezone = null,
    bool HasPassword = false);

public record UpdateProfileResponse(string Message, string? Token = null);

public record AvatarUploadResponse(string Message, string AvatarUrl);
