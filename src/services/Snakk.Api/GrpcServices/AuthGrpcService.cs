using Grpc.Core;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Snakk.Api.Endpoints;
using Snakk.Api.Helpers;
using Snakk.Api.Services;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Protos.Auth;
using Snakk.Shared.Enums;
using Snakk.Shared.Helpers;
using System.Security.Cryptography;

namespace Snakk.Api.GrpcServices;

public class AuthGrpcService(
    AuthenticationUseCase authUseCase,
    IJwtTokenService jwtService,
    ICurrentUserService currentUser,
    IAuthDataService authDataService,
    ISettingsService settingsService,
    ILogger<AuthGrpcService> logger,
    IUserGrantsCacheService grantsCache,
    IDisplayNameHistoryRepository displayNameHistoryRepository,
    ITurnstileService turnstileService,
    IConsentService consentService,
    IUserRepository userRepository,
    ITrustedDeviceService trustedDeviceService,
    IConfiguration configuration,
    ISessionManagementService sessionManagement,
    IMemoryCache cache,
    IDistributedCache distributedCache) : AuthService.AuthServiceBase
{
    public override async Task<AuthTokenResponse> Register(RegisterRequest request, ServerCallContext context)
    {
        if (!await turnstileService.VerifyAsync(request.HasTurnstileToken ? request.TurnstileToken : ""))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Captcha verification failed. Please try again."));

        await EnforceRegistrationGateAsync(request.HasInviteCode ? request.InviteCode : null);

        bool? allowAdult = request.HasAllowAdultContent ? request.AllowAdultContent : null;

        var result = await authUseCase.RegisterWithEmailAsync(
            request.Email,
            request.Password,
            request.DisplayName,
            request.BaseUrl,
            allowAdult);

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                result.Error ?? "Registration failed. Please check your details and try again."));

        var user = result.Value!;
        var roles = await authDataService.GetUserRolesAsync(user.PublicId.Value);

        var jwt = jwtService.GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            roles.FirstOrDefault(),
            user.AvatarFileName,
            authVersion: user.AuthVersion,
            twoFactorEnabled: user.TwoFactorEnabled,
            slug: user.Slug);

        var refreshTokenResult = await authUseCase.CreateRefreshTokenAsync(user.PublicId);

        if (!refreshTokenResult.IsSuccess)
            throw new RpcException(new Status(StatusCode.Internal, "Failed to create refresh token"));

        await grantsCache.GetGrantsAsync(user.PublicId.Value);

        return new AuthTokenResponse
        {
            AccessToken = jwt,
            RefreshToken = refreshTokenResult.Value!.Value,
            Message = "Registration successful. Please check your email to verify your account.",
            User = BuildUserInfo(user, roles)
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
        var roles = await authDataService.GetUserRolesAsync(user.PublicId.Value);

        if (configuration.GetValue<bool>("Features:TwoFactorEnabled", true))
        {
            // Check if 2FA is enabled — domain entity doesn't have this, so query via service
            var has2FA = await authDataService.GetTwoFactorEnabledAsync(user.PublicId.Value);

            if (has2FA)
            {
                var fingerprint = trustedDeviceService.GenerateDeviceFingerprint(request.UserAgent, request.IpAddress);
                var isTrusted = await trustedDeviceService.IsDeviceTrustedAsync(user.PublicId, fingerprint, ctx.CancellationToken);

                if (!isTrusted)
                {
                    logger.LogInformation("Login requires 2FA for {UserId} from {Ip}", user.PublicId.Value, request.IpAddress);
                    return new AuthTokenResponse
                    {
                        TwoFactorRequired = true,
                        Message = "Two-factor authentication required",
                        User = BuildUserInfo(user, roles)
                    };
                }

                logger.LogInformation("Trusted device bypassing 2FA for {UserId} from {Ip}", user.PublicId.Value, request.IpAddress);
            }
        }

        var refreshTokenResult = await authUseCase.CreateRefreshTokenAsync(user.PublicId);

        if (!refreshTokenResult.IsSuccess)
            throw new RpcException(new Status(StatusCode.Internal, "Failed to create refresh token"));

        // Store device info on the refresh token and retrieve session PublicId
        var rawToken = refreshTokenResult.Value!.Value;
        var tokenHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
        var ipAddress = string.IsNullOrEmpty(request.IpAddress) ? null : request.IpAddress;
        var userAgent = string.IsNullOrEmpty(request.UserAgent) ? null : request.UserAgent;

        var sessionPublicId = await authDataService.SetRefreshTokenSessionInfoAsync(tokenHash, ipAddress, userAgent);

        var jwt = jwtService.GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            roles.FirstOrDefault(),
            user.AvatarFileName,
            authVersion: user.AuthVersion,
            sessionId: sessionPublicId,
            twoFactorEnabled: user.TwoFactorEnabled,
            slug: user.Slug);

        logger.LogInformation("Login succeeded for {UserId} from {Ip}", user.PublicId.Value, request.IpAddress);

        await sessionManagement.LogLoginAsync(user.PublicId.Value, ipAddress, userAgent, success: true, ctx.CancellationToken);

        await grantsCache.GetGrantsAsync(user.PublicId.Value);

        return new AuthTokenResponse
        {
            AccessToken = jwt,
            RefreshToken = rawToken,
            User = BuildUserInfo(user, roles)
        };
    }

    public override async Task<Protos.Auth.MessageResponse> Logout(LogoutRequest request, ServerCallContext ctx)
    {
        var authHeader = ctx.RequestHeaders.GetValue("authorization") ?? "";
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            jwtService.RevokeToken(authHeader["Bearer ".Length..].Trim());

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
        var roles = await authDataService.GetUserRolesAsync(user.PublicId.Value);

        var newRawToken = newRefreshToken.Value;
        var newTokenHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(newRawToken)));
        var newSessionPublicId = await authDataService.GetRefreshTokenSessionIdAsync(newTokenHash);

        var jwt = jwtService.GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            roles.FirstOrDefault(),
            user.AvatarFileName,
            authVersion: user.AuthVersion,
            sessionId: newSessionPublicId,
            twoFactorEnabled: user.TwoFactorEnabled,
            slug: user.Slug);

        var needsConsent = !await consentService.HasAllRequiredConsentsAsync(user.PublicId.Value);

        return new Protos.Auth.RefreshTokenResponse
        {
            AccessToken = jwt,
            RefreshToken = newRefreshToken.Value,
            NeedsConsent = needsConsent
        };
    }

    public override async Task<AuthTokenResponse> ReloginWithRefreshToken(
        ReloginWithRefreshTokenRequest request, ServerCallContext ctx)
    {
        var result = await authUseCase.ReloginWithRefreshTokenAsync(request.RefreshToken, request.Password);

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.Unauthenticated, result.Error ?? "Relogin failed"));

        var (user, newRefreshToken) = result.Value;
        var roles = await authDataService.GetUserRolesAsync(user.PublicId.Value);

        var newRawToken = newRefreshToken.Value;
        var newTokenHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(newRawToken)));
        var newSessionPublicId = await authDataService.GetRefreshTokenSessionIdAsync(newTokenHash);

        var jwt = jwtService.GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            roles.FirstOrDefault(),
            user.AvatarFileName,
            authVersion: user.AuthVersion,
            sessionId: newSessionPublicId,
            twoFactorEnabled: user.TwoFactorEnabled,
            slug: user.Slug);

        return new AuthTokenResponse
        {
            AccessToken  = jwt,
            RefreshToken = newRefreshToken.Value,
            User = new UserInfo
            {
                Id             = user.PublicId.Value,
                DisplayName    = user.DisplayName,
                EmailVerified  = user.EmailVerified,
            }
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
        var user = await userRepository.GetCurrentUserSlimAsync(userId);

        if (user is null)
            return new CurrentUserResponse();

        var response = new CurrentUserResponse
        {
            IsAuthenticated = true,
            PublicId = user.PublicId,
            DisplayName = user.DisplayName,
            Email = user.Email,
            EmailVerified = user.EmailVerified,
            AutoFollowOnReply = user.AutoFollowOnReply,
            Timezone = user.Timezone ?? "",
            IsDisplayNameLocked = user.IsDisplayNameLocked,
            HasPassword = user.HasPassword,
            AvatarUrl = AvatarHelper.GetAvatarUrl(user.PublicId, AvatarEntityType.User, 0, user.AvatarFileName),
            AdultPreviewImageMode = user.AdultPreviewImageMode,
            HidePresence = user.HidePresence,
        };
        response.ConnectedProviders.AddRange(user.ConnectedProviders);

        if (user.AllowAdultContent.HasValue)
            response.AllowAdultContent = user.AllowAdultContent.Value;

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
        var sudoVerified = request.HasSudoToken &&
            await MeEndpoints.ValidateSudoTokenAsync(request.SudoToken, userIdValue, distributedCache, ctx.CancellationToken);
        var result = await authUseCase.UpdateDisplayNameAsync(
            userId,
            request.DisplayName,
            request.HasPassword ? request.Password : null,
            request.HasTurnstileToken ? request.TurnstileToken : null,
            sudoVerified);

        if (!result.IsSuccess)
            return new UpdateProfileResponse { Success = false, Message = result.Error ?? "Update failed" };

        cache.Remove($"display-name-history:{userIdValue}");

        // Generate new JWT with updated display name
        var userResult = await authUseCase.GetUserByIdAsync(userId);

        if (userResult.IsSuccess)
        {
            var user = userResult.Value!;
            var roles = await authDataService.GetUserRolesAsync(user.PublicId.Value);

            var newToken = jwtService.GenerateToken(
                user.PublicId.Value,
                user.DisplayName,
                user.Email,
                user.EmailVerified,
                roles.FirstOrDefault(),
                user.AvatarFileName,
                authVersion: user.AuthVersion,
                twoFactorEnabled: user.TwoFactorEnabled);

            return new UpdateProfileResponse
            {
                Success = true,
                Message = "Display name updated successfully",
                Token = newToken
            };
        }

        return new UpdateProfileResponse { Success = true, Message = "Display name updated successfully" };
    }

    public override async Task<SetEmailResponse> SetEmail(SetEmailRequest request, ServerCallContext ctx)
    {
        if (!currentUser.IsAuthenticated())
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var userIdValue = currentUser.GetCurrentUserId()
            ?? throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var userId = UserId.From(userIdValue);
        var result = await authUseCase.SetEmailAsync(userId, request.Email);

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.InvalidArgument, result.Error ?? "Failed to set email"));

        var userResult = await authUseCase.GetUserByIdAsync(userId);
        if (!userResult.IsSuccess)
            throw new RpcException(new Status(StatusCode.Internal, "Failed to retrieve updated user"));

        var user = userResult.Value!;
        var roles = await authDataService.GetUserRolesAsync(user.PublicId.Value);
        var newToken = jwtService.GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            roles.FirstOrDefault(),
            user.AvatarFileName,
            authVersion: user.AuthVersion,
            twoFactorEnabled: user.TwoFactorEnabled,
            slug: user.Slug);

        return new SetEmailResponse { Token = newToken };
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
        bool? allowAdultContent = request.HasAllowAdultContent ? request.AllowAdultContent : null;
        AdultPreviewImageModeEnum? adultPreviewImageMode = request.HasAdultPreviewImageMode
            ? (AdultPreviewImageModeEnum)request.AdultPreviewImageMode
            : null;
        bool? hidePresence = request.HasHidePresence ? request.HidePresence : null;

        var result = await authUseCase.UpdatePreferencesAsync(
            userId,
            autoFollowOnReply,
            timezone,
            bio,
            allowAdultContent,
            request.ResetAdultContentToAsk,
            adultPreviewImageMode,
            hidePresence);

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.InvalidArgument, result.Error ?? "Update failed"));

        return new Protos.Auth.MessageResponse { Message = "Preferences updated successfully" };
    }

    public override async Task<OAuthCallbackResponse> OAuthCallback(OAuthCallbackRequest request, ServerCallContext ctx)
    {
        var provider = request.Provider.ToLowerInvariant();

        // Gate: only check for users who don't already have an account
        var userAlreadyExists = await authDataService.OAuthConnectionExistsAsync(provider, request.ProviderUserId);
        if (!userAlreadyExists)
            await EnforceRegistrationGateAsync(request.HasInviteCode ? request.InviteCode : null);

        bool? allowAdult = request.HasAllowAdultContent ? request.AllowAdultContent : null;

        var result = await authUseCase.LoginWithOAuthAsync(
            provider,
            request.ProviderUserId,
            request.Email,
            request.DisplayName,
            allowAdult);

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.InvalidArgument, result.Error ?? "OAuth login failed"));

        var user = result.Value!;
        var roles = await authDataService.GetUserRolesAsync(user.PublicId.Value);
        var isNewUser = (DateTime.UtcNow - user.CreatedAt).TotalSeconds < 30;

        // Discord always requires 2FA; other providers check global flag and per-connection flag
        var globalTwoFAEnabled = configuration.GetValue<bool>("Features:TwoFactorEnabled", true);
        var perConnectionRequire2FA = !isNewUser && await authDataService.GetOAuthConnectionRequire2FAAsync(user.PublicId.Value, provider);

        var requires2FA = provider == "discord"
            || (globalTwoFAEnabled && await authDataService.GetTwoFactorEnabledAsync(user.PublicId.Value))
            || perConnectionRequire2FA;

        if (requires2FA)
        {
            var fingerprint = trustedDeviceService.GenerateDeviceFingerprint(request.UserAgent, request.IpAddress);
            var isTrusted = !isNewUser && await trustedDeviceService.IsDeviceTrustedAsync(user.PublicId, fingerprint, ctx.CancellationToken);

            if (!isTrusted)
            {
                logger.LogInformation("OAuth login requires 2FA for {Email} from {Ip}", request.Email, request.IpAddress);
                return new OAuthCallbackResponse
                {
                    TwoFactorRequired = true,
                    IsNewUser = isNewUser,
                    User = BuildUserInfo(user, roles)
                };
            }
        }

        var jwt = jwtService.GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            roles.FirstOrDefault(),
            user.AvatarFileName,
            authVersion: user.AuthVersion,
            twoFactorEnabled: user.TwoFactorEnabled,
            slug: user.Slug);

        var refreshTokenResult = await authUseCase.CreateRefreshTokenAsync(user.PublicId);

        if (!refreshTokenResult.IsSuccess)
            throw new RpcException(new Status(StatusCode.Internal, "Failed to create refresh token"));

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
            User = BuildUserInfo(user, roles)
        };
    }

    public override async Task<PublicSettingsResponse> GetPublicSettings(
        GetPublicSettingsRequest request, ServerCallContext context)
    {
        var siteInfoTask = settingsService.GetSiteInfoAsync();
        var regSettingsTask = settingsService.GetRegistrationSettingsAsync();
        await Task.WhenAll(siteInfoTask, regSettingsTask);
        return new PublicSettingsResponse
        {
            Timezone = siteInfoTask.Result.Timezone,
            SiteName = siteInfoTask.Result.SiteName,
            RegistrationMode = regSettingsTask.Result.Mode
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

        var key = $"display-name-history:{userIdValue}";
        if (cache.TryGetValue(key, out DisplayNameHistoryResponse? cached) && cached is not null)
            return cached;

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
        cache.Set(key, response, TimeSpan.FromMinutes(5));
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

    public override async Task<GenerateDiscordLinkTokenResponse> GenerateDiscordLinkToken(
        GenerateDiscordLinkTokenRequest request, ServerCallContext ctx)
    {
        var userId = RequireAuth();
        var (token, expiry) = await authDataService.SetDiscordLinkTokenAsync(userId.Value);

        return new GenerateDiscordLinkTokenResponse
        {
            Token = token,
            ExpiresAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(expiry)
        };
    }

    public override async Task<CompleteDiscordLinkResponse> CompleteDiscordLink(
        CompleteDiscordLinkRequest request, ServerCallContext ctx)
    {
        var (resultCode, userPublicId) = await authDataService.CompleteDiscordLinkAsync(
            request.LinkToken,
            request.DiscordUserId,
            request.DiscordUsername,
            request.HasDiscordAvatarHash ? request.DiscordAvatarHash : null);

        if (resultCode == "OK")
        {
            if (userPublicId is not null)
                cache.Remove($"discord-status:{userPublicId}");
            return new CompleteDiscordLinkResponse { Success = true };
        }

        return new CompleteDiscordLinkResponse { Success = false, ErrorCode = resultCode };
    }

    public override async Task<Protos.Auth.MessageResponse> UnlinkDiscord(
        UnlinkDiscordRequest request, ServerCallContext ctx)
    {
        var userId = RequireAuth();
        await authDataService.UnlinkDiscordAsync(userId.Value);

        cache.Remove($"discord-status:{userId.Value}");
        return new Protos.Auth.MessageResponse { Message = "Discord unlinked" };
    }

    public override async Task<DiscordStatusResponse> GetDiscordStatus(
        GetDiscordStatusRequest request, ServerCallContext ctx)
    {
        var userId = RequireAuth();
        var key = $"discord-status:{userId.Value}";
        if (cache.TryGetValue(key, out DiscordStatusResponse? cached) && cached is not null)
            return cached;

        var discord = await authDataService.GetDiscordStatusAsync(userId.Value);

        DiscordStatusResponse response;
        if (discord is null)
        {
            response = new DiscordStatusResponse { IsLinked = false };
        }
        else
        {
            response = new DiscordStatusResponse
            {
                IsLinked = true,
                DiscordUserId = discord.DiscordUserId,
                DiscordUsername = discord.DiscordUsername ?? ""
            };
            if (discord.DiscordAvatarHash is not null)
                response.DiscordAvatarHash = discord.DiscordAvatarHash;
        }
        cache.Set(key, response, TimeSpan.FromMinutes(2));
        return response;
    }

    public override async Task<Protos.Auth.MessageResponse> RequestPasswordReset(
        RequestPasswordResetRequest request, ServerCallContext ctx)
    {
        var ipAddress = string.IsNullOrEmpty(request.IpAddress) ? null : request.IpAddress;
        var userAgent = string.IsNullOrEmpty(request.UserAgent) ? null : request.UserAgent;

        await authUseCase.RequestPasswordResetAsync(
            request.Email, request.BaseUrl, ipAddress, userAgent, ctx.CancellationToken);

        return new Protos.Auth.MessageResponse { Message = "If an account with that email exists, a reset link has been sent." };
    }

    public override async Task<Protos.Auth.MessageResponse> ResetPassword(
        ResetPasswordRequest request, ServerCallContext ctx)
    {
        var ipAddress = string.IsNullOrEmpty(request.IpAddress) ? null : request.IpAddress;
        var userAgent = string.IsNullOrEmpty(request.UserAgent) ? null : request.UserAgent;

        var result = await authUseCase.CompletePasswordResetAsync(
            request.Token, request.NewPassword, ipAddress, userAgent, ctx.CancellationToken);

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.InvalidArgument, result.Error ?? "Password reset failed."));

        return new Protos.Auth.MessageResponse { Message = "Password reset successfully." };
    }

    public override async Task<GetOAuthConnectionsResponse> GetOAuthConnections(
        GetOAuthConnectionsRequest request, ServerCallContext ctx)
    {
        var userId = RequireAuth();
        var key = $"oauth-connections:{userId.Value}";
        if (cache.TryGetValue(key, out GetOAuthConnectionsResponse? cached) && cached is not null)
            return cached;

        var result = await authUseCase.GetOAuthConnectionsAsync(userId.Value, ctx.CancellationToken);
        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.Internal, result.Error ?? "Failed to get connections"));

        var response = new GetOAuthConnectionsResponse();
        foreach (var conn in result.Value!)
        {
            var info = new OAuthConnectionInfo
            {
                Provider = conn.Provider,
                ConnectedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.SpecifyKind(conn.ConnectedAt, DateTimeKind.Utc)),
                Require2Fa = conn.Require2FA
            };
            if (conn.LastLoginAt.HasValue)
                info.LastLoginAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.SpecifyKind(conn.LastLoginAt.Value, DateTimeKind.Utc));
            response.Connections.Add(info);
        }
        cache.Set(key, response, TimeSpan.FromMinutes(2));
        return response;
    }

    public override async Task<ConnectOAuthProviderResponse> ConnectOAuthProvider(
        ConnectOAuthProviderRequest request, ServerCallContext ctx)
    {
        // Identity is taken from the authenticated JWT only. The previous code path
        // honored a client-supplied request.UserPublicId when set, with the comment
        // that Snakk.Auth was a trusted server-to-server caller — but no
        // service-to-service auth gated the bypass, so any caller reaching the
        // gRPC surface could spoof the user. Snakk.Auth now forwards the validated
        // JWT cookie as a Bearer header instead. (CR-21 / HI-60 in
        // docs/SECURITY-AUDIT-2026-05-23.md.) The request.UserPublicId proto field
        // is left in place for wire-compat with older clients but the value is
        // ignored.
        var userPublicId = RequireAuth().Value;

        var result = await authUseCase.ConnectOAuthProviderAsync(
            userPublicId, request.Provider, request.ProviderUserId, ctx.CancellationToken);

        if (result.IsSuccess)
            cache.Remove($"oauth-connections:{userPublicId}");

        return new ConnectOAuthProviderResponse
        {
            Success = result.IsSuccess,
            ErrorCode = result.IsSuccess ? null : result.Error
        };
    }

    public override async Task<Protos.Auth.MessageResponse> DisconnectOAuthProvider(
        DisconnectOAuthProviderRequest request, ServerCallContext ctx)
    {
        var userId = RequireAuth();
        var result = await authUseCase.DisconnectOAuthProviderAsync(
            userId.Value, request.Provider, ctx.CancellationToken);

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Error ?? "Cannot disconnect"));

        cache.Remove($"oauth-connections:{userId.Value}");
        return new Protos.Auth.MessageResponse { Message = "Provider disconnected" };
    }

    public override async Task<Protos.Auth.MessageResponse> SetOAuthConnectionRequire2FA(
        SetOAuthConnectionRequire2FARequest request, ServerCallContext ctx)
    {
        var userId = RequireAuth();
        var result = await authUseCase.SetOAuthConnectionRequire2FAAsync(
            userId.Value, request.Provider, request.Require2Fa, ctx.CancellationToken);

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.InvalidArgument, result.Error ?? "Update failed"));

        cache.Remove($"oauth-connections:{userId.Value}");
        return new Protos.Auth.MessageResponse { Message = "2FA requirement updated" };
    }

    public override async Task<GenerateOAuthSudoNonceResponse> GenerateOAuthSudoNonce(
        GenerateOAuthSudoNonceRequest request, ServerCallContext ctx)
    {
        // As in ConnectOAuthProvider, identity must come from the authenticated JWT,
        // not from request.UserPublicId — see comment there.
        var userPublicId = RequireAuth().Value;

        // Verify the provider+providerUserId matches a connection for this user
        var connectionExists = await authDataService.VerifyOAuthConnectionOwnershipAsync(
            userPublicId, request.Provider, request.ProviderUserId, ctx.CancellationToken);

        if (!connectionExists)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "OAuth connection not found for user"));

        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var cacheKey = $"sudo-oauth-nonce:{userPublicId}:{nonce}";
        await distributedCache.SetStringAsync(cacheKey, "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) }, ctx.CancellationToken);

        return new GenerateOAuthSudoNonceResponse { Nonce = nonce };
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

    private async Task EnforceRegistrationGateAsync(string? inviteCode)
    {
        var settings = await settingsService.GetRegistrationSettingsAsync();

        if (settings.Mode == "Closed")
            throw new RpcException(new Status(StatusCode.PermissionDenied, "REGISTRATION_CLOSED"));

        if (settings.Mode == "InviteOnly")
        {
            if (string.IsNullOrWhiteSpace(inviteCode))
                throw new RpcException(new Status(StatusCode.PermissionDenied, "INVITE_CODE_REQUIRED"));
            if (!string.Equals(inviteCode.Trim(), settings.InviteCode.Trim(), StringComparison.Ordinal))
                throw new RpcException(new Status(StatusCode.PermissionDenied, "INVITE_CODE_INVALID"));
        }
    }

    // Builds a UserInfo proto with conditional assignment for nullable fields.
    // Email/DisplayName are proto3 `optional` — leave unset when null rather than coercing to "".
    private static Protos.Auth.UserInfo BuildUserInfo(Snakk.Domain.Entities.User user, List<string> roles)
    {
        var info = new Protos.Auth.UserInfo
        {
            Id = user.PublicId.Value,
            EmailVerified = user.EmailVerified,
            Roles = { roles }
        };

        if (user.Email is not null) info.Email = user.Email;
        if (user.DisplayName is not null) info.DisplayName = user.DisplayName;

        return info;
    }
}
