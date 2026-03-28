using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Snakk.Api.Helpers;
using Snakk.Api.Services;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Protos.Auth;
using Snakk.Shared.Enums;
using Snakk.Shared.Helpers;

namespace Snakk.Api.GrpcServices;

public class AuthGrpcService(
    AuthenticationUseCase authUseCase,
    IJwtTokenService jwtService,
    ICurrentUserService currentUser,
    SnakkDbContext context,
    ISettingsService settingsService,
    ILogger<AuthGrpcService> logger,
    IUserGrantsCacheService grantsCache,
    IDisplayNameHistoryRepository displayNameHistoryRepository,
    ITurnstileService turnstileService) : AuthService.AuthServiceBase
{
    public override async Task<AuthTokenResponse> Register(RegisterRequest request, ServerCallContext context)
    {
        if (!await turnstileService.VerifyAsync(request.HasTurnstileToken ? request.TurnstileToken : ""))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Captcha verification failed. Please try again."));

        var result = await authUseCase.RegisterWithEmailAsync(
            request.Email,
            request.Password,
            request.DisplayName,
            request.BaseUrl);

        if (!result.IsSuccess)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "Registration failed. Please check your details and try again."));

        var user = result.Value!;
        var roles = await GetUserRolesAsync(user.PublicId.Value);

        var jwt = jwtService.GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            user.OAuthProvider,
            roles.FirstOrDefault(),
            user.AvatarFileName);

        var refreshTokenResult = await authUseCase.CreateRefreshTokenAsync(user.PublicId);

        if (!refreshTokenResult.IsSuccess)
            throw new RpcException(new Status(StatusCode.Internal, "Failed to create refresh token"));

        await grantsCache.GetGrantsAsync(user.PublicId.Value);

        return new AuthTokenResponse
        {
            AccessToken = jwt,
            RefreshToken = refreshTokenResult.Value!.Value,
            Message = "Registration successful. Please check your email to verify your account.",
            User = new Protos.Auth.UserInfo
            {
                Id = user.PublicId.Value,
                Email = user.Email,
                DisplayName = user.DisplayName,
                EmailVerified = user.EmailVerified,
                Roles = { roles }
            }
        };
    }

    public override async Task<AuthTokenResponse> Login(LoginRequest request, ServerCallContext ctx)
    {
        if (!await turnstileService.VerifyAsync(request.HasTurnstileToken ? request.TurnstileToken : ""))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Captcha verification failed. Please try again."));

        var result = await authUseCase.LoginWithEmailAsync(request.Email, request.Password);

        if (!result.IsSuccess)
        {
            logger.LogInformation("Login failed for {Email} from {Ip}", request.Email, request.IpAddress);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid credentials"));
        }

        var user = result.Value!;
        var roles = await GetUserRolesAsync(user.PublicId.Value);

        var jwt = jwtService.GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            user.OAuthProvider,
            roles.FirstOrDefault(),
            user.AvatarFileName);

        var refreshTokenResult = await authUseCase.CreateRefreshTokenAsync(user.PublicId);

        if (!refreshTokenResult.IsSuccess)
            throw new RpcException(new Status(StatusCode.Internal, "Failed to create refresh token"));

        logger.LogInformation("Login succeeded for {UserId} from {Ip}", user.PublicId.Value, request.IpAddress);

        await grantsCache.GetGrantsAsync(user.PublicId.Value);

        return new AuthTokenResponse
        {
            AccessToken = jwt,
            RefreshToken = refreshTokenResult.Value!.Value,
            User = new Protos.Auth.UserInfo
            {
                Id = user.PublicId.Value,
                Email = user.Email,
                DisplayName = user.DisplayName,
                EmailVerified = user.EmailVerified,
                Roles = { roles }
            }
        };
    }

    public override async Task<Protos.Auth.MessageResponse> Logout(LogoutRequest request, ServerCallContext ctx)
    {
        var userId = currentUser.GetCurrentUserId();

        if (userId is not null)
            await authUseCase.RevokeRefreshTokensAsync(UserId.From(userId));

        return new Protos.Auth.MessageResponse { Message = "Logged out successfully" };
    }

    public override async Task<Protos.Auth.RefreshTokenResponse> RefreshToken(RefreshTokenRequest request, ServerCallContext ctx)
    {
        var result = await authUseCase.RefreshTokenAsync(request.RefreshToken);

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid refresh token"));

        var (user, newRefreshToken) = result.Value;
        var roles = await GetUserRolesAsync(user.PublicId.Value);

        var jwt = jwtService.GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            user.OAuthProvider,
            roles.FirstOrDefault(),
            user.AvatarFileName);

        return new Protos.Auth.RefreshTokenResponse
        {
            AccessToken = jwt,
            RefreshToken = newRefreshToken.Value
        };
    }

    public override async Task<Protos.Auth.MessageResponse> VerifyEmail(VerifyEmailRequest request, ServerCallContext ctx)
    {
        var result = await authUseCase.VerifyEmailAsync(request.Token);

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.InvalidArgument, result.Error ?? "Verification failed"));

        return new Protos.Auth.MessageResponse { Message = "Email verified successfully." };
    }

    public override Task<AuthStatusResponse> GetAuthStatus(GetAuthStatusRequest request, ServerCallContext ctx)
    {
        if (!currentUser.IsAuthenticated())
            return Task.FromResult(new AuthStatusResponse { IsAuthenticated = false });

        var userId = currentUser.GetCurrentUserId();

        return Task.FromResult(new AuthStatusResponse
        {
            IsAuthenticated = true,
            PublicId = userId,
            DisplayName = currentUser.GetCurrentUserDisplayName(),
            EmailVerified = currentUser.IsEmailVerified(),
            Role = currentUser.GetCurrentUserRole() ?? "",
            AvatarUrl = AvatarHelper.GetAvatarUrl(userId ?? "", AvatarEntityType.User, 0, currentUser.GetAvatarFileName())
        });
    }

    public override async Task<CurrentUserResponse> GetCurrentUser(GetCurrentUserRequest request, ServerCallContext ctx)
    {
        if (!currentUser.IsAuthenticated())
            return new CurrentUserResponse();

        var userIdValue = currentUser.GetCurrentUserId();

        if (userIdValue is null)
            return new CurrentUserResponse();

        var userId = UserId.From(userIdValue);
        var result = await authUseCase.GetUserByIdAsync(userId);

        if (!result.IsSuccess)
            return new CurrentUserResponse();

        var user = result.Value!;

        var response = new CurrentUserResponse
        {
            IsAuthenticated = true,
            PublicId = user.PublicId.Value,
            DisplayName = user.DisplayName,
            Email = user.Email,
            EmailVerified = user.EmailVerified,
            OauthProvider = user.OAuthProvider ?? "",
            AutoFollowOnReply = user.AutoFollowOnReply,
            Timezone = user.Timezone ?? "",
            IsDisplayNameLocked = user.IsDisplayNameLocked,
            HasPassword = user.PasswordHash is not null,
            AvatarUrl = AvatarHelper.GetAvatarUrl(user.PublicId.Value, AvatarEntityType.User, 0, user.AvatarFileName)
        };

        if (user.Bio is not null)
            response.Bio = user.Bio;

        if (user.FeedToken is not null)
            response.FeedToken = user.FeedToken;

        if (user.DisplayNameChangedAt.HasValue)
            response.DisplayNameChangedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                DateTime.SpecifyKind(user.DisplayNameChangedAt.Value, DateTimeKind.Utc));

        return response;
    }

    public override async Task<UpdateProfileResponse> UpdateProfile(UpdateProfileRequest request, ServerCallContext ctx)
    {
        if (!currentUser.IsAuthenticated())
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var userIdValue = currentUser.GetCurrentUserId();

        if (userIdValue is null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var userId = UserId.From(userIdValue);
        var result = await authUseCase.UpdateDisplayNameAsync(
            userId,
            request.DisplayName,
            request.HasPassword ? request.Password : null,
            request.HasTurnstileToken ? request.TurnstileToken : null);

        if (!result.IsSuccess)
            return new UpdateProfileResponse { Success = false, Message = result.Error ?? "Update failed" };

        // Generate new JWT with updated display name
        var userResult = await authUseCase.GetUserByIdAsync(userId);

        if (userResult.IsSuccess)
        {
            var user = userResult.Value!;
            var roles = await GetUserRolesAsync(user.PublicId.Value);

            var newToken = jwtService.GenerateToken(
                user.PublicId.Value,
                user.DisplayName,
                user.Email,
                user.EmailVerified,
                user.OAuthProvider,
                roles.FirstOrDefault(),
                user.AvatarFileName);

            return new UpdateProfileResponse
            {
                Success = true,
                Message = "Display name updated successfully",
                Token = newToken
            };
        }

        return new UpdateProfileResponse { Success = true, Message = "Display name updated successfully" };
    }

    public override async Task<Protos.Auth.MessageResponse> UpdatePreferences(UpdatePreferencesRequest request, ServerCallContext ctx)
    {
        if (!currentUser.IsAuthenticated())
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var userIdValue = currentUser.GetCurrentUserId();

        if (userIdValue is null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var userId = UserId.From(userIdValue);

        bool? autoFollowOnReply = request.HasAutoFollowOnReply ? request.AutoFollowOnReply : null;
        string? timezone = request.HasTimezone ? request.Timezone : null;
        string? bio = request.HasBio ? request.Bio : null;

        var result = await authUseCase.UpdatePreferencesAsync(userId, autoFollowOnReply, timezone, bio);

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.InvalidArgument, result.Error ?? "Update failed"));

        return new Protos.Auth.MessageResponse { Message = "Preferences updated successfully" };
    }

    public override async Task<OAuthCallbackResponse> OAuthCallback(OAuthCallbackRequest request, ServerCallContext ctx)
    {
        var result = await authUseCase.LoginWithOAuthAsync(
            request.Provider,
            request.ProviderUserId,
            request.Email,
            request.DisplayName);

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.InvalidArgument, result.Error ?? "OAuth login failed"));

        var user = result.Value!;
        var roles = await GetUserRolesAsync(user.PublicId.Value);

        var jwt = jwtService.GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            user.OAuthProvider,
            roles.FirstOrDefault(),
            user.AvatarFileName);

        var refreshTokenResult = await authUseCase.CreateRefreshTokenAsync(user.PublicId);

        if (!refreshTokenResult.IsSuccess)
            throw new RpcException(new Status(StatusCode.Internal, "Failed to create refresh token"));

        var isNewUser = (DateTime.UtcNow - user.CreatedAt).TotalSeconds < 30;

        logger.LogInformation(
            "OAuth {Provider} login for {Email} from {Ip} (new user: {IsNew})",
            request.Provider,
            request.Email,
            request.IpAddress,
            isNewUser);

        return new OAuthCallbackResponse
        {
            AccessToken = jwt,
            RefreshToken = refreshTokenResult.Value!.Value,
            IsNewUser = isNewUser,
            User = new Protos.Auth.UserInfo
            {
                Id = user.PublicId.Value,
                Email = user.Email,
                DisplayName = user.DisplayName,
                EmailVerified = user.EmailVerified,
                Roles = { roles }
            }
        };
    }

    public override async Task<PublicSettingsResponse> GetPublicSettings(
        GetPublicSettingsRequest request, ServerCallContext context)
    {
        var siteInfo = await settingsService.GetSiteInfoAsync();
        return new PublicSettingsResponse
        {
            Timezone = siteInfo.Timezone,
            SiteName = siteInfo.SiteName
        };
    }

    public override async Task<DisplayNameHistoryResponse> GetDisplayNameHistory(
        GetDisplayNameHistoryRequest request, ServerCallContext ctx)
    {
        if (!currentUser.IsAuthenticated())
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var userIdValue = currentUser.GetCurrentUserId();
        if (userIdValue is null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var history = await displayNameHistoryRepository.GetHistoryForUserAsync(userIdValue);

        var response = new DisplayNameHistoryResponse();
        foreach (var entry in history)
        {
            var item = new DisplayNameHistoryEntry
            {
                PreviousName = entry.PreviousName,
                NewName = entry.NewName,
                ChangedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.SpecifyKind(entry.ChangedAt, DateTimeKind.Utc))
            };
            response.Entries.Add(item);
        }

        return response;
    }

    public override async Task<FeedTokenResponse> GenerateFeedToken(GenerateFeedTokenRequest request, ServerCallContext ctx)
    {
        var userId = RequireAuth();
        var user = await authUseCase.GetUserByIdAsync(userId);
        if (!user.IsSuccess || user.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        var token = user.Value.GenerateFeedToken();
        await authUseCase.UpdateUserAsync(user.Value);

        return new FeedTokenResponse { Token = token };
    }

    public override async Task<Protos.Auth.MessageResponse> RevokeFeedToken(RevokeFeedTokenRequest request, ServerCallContext ctx)
    {
        var userId = RequireAuth();
        var user = await authUseCase.GetUserByIdAsync(userId);
        if (!user.IsSuccess || user.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        user.Value.RevokeFeedToken();
        await authUseCase.UpdateUserAsync(user.Value);

        return new Protos.Auth.MessageResponse { Message = "Feed token revoked" };
    }

    private UserId RequireAuth()
    {
        if (!currentUser.IsAuthenticated())
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var userId = currentUser.GetCurrentUserId();
        if (userId is null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        return UserId.From(userId);
    }

    // Shared helper to fetch roles for a user
    private async Task<List<string>> GetUserRolesAsync(string publicId) =>
        await context.UserRoles
            .Where(r => r.User.PublicId == publicId && r.RevokedAt == null)
            .Select(r => ((UserRoleTypeEnum)r.RoleId).ToString())
            .ToListAsync();
}
