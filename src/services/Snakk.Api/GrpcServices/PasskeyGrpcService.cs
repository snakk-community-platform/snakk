using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Snakk.Api.Services;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Protos.Passkey;
using Snakk.Shared.Enums;

namespace Snakk.Api.GrpcServices;

public class PasskeyGrpcService(
    IPasskeyService passkeyService,
    ICurrentUserService currentUser,
    IJwtTokenService jwtService,
    AuthenticationUseCase authUseCase,
    IUserGrantsCacheService grantsCache,
    SnakkDbContext context,
    ILogger<PasskeyGrpcService> logger) : PasskeyService.PasskeyServiceBase
{
    public override async Task<BeginPasskeyRegistrationResponse> BeginPasskeyRegistration(
        BeginPasskeyRegistrationRequest request, ServerCallContext ctx)
    {
        RequireAuth();

        var (optionsJson, challengeId) = await passkeyService.BeginRegistrationAsync(
            request.UserPublicId, request.UserDisplayName, ctx.CancellationToken);

        return new BeginPasskeyRegistrationResponse
        {
            OptionsJson = optionsJson,
            ChallengeId = challengeId
        };
    }

    public override async Task<PasskeyMessageResponse> CompletePasskeyRegistration(
        CompletePasskeyRegistrationRequest request, ServerCallContext ctx)
    {
        RequireAuth();

        await passkeyService.CompleteRegistrationAsync(
            request.UserPublicId,
            request.ChallengeId,
            request.AttestationResponseJson,
            request.HasFriendlyName ? request.FriendlyName : null,
            ctx.CancellationToken);

        return new PasskeyMessageResponse { Message = "Passkey registered successfully" };
    }

    public override async Task<BeginPasskeyLoginResponse> BeginPasskeyLogin(
        BeginPasskeyLoginRequest request, ServerCallContext ctx)
    {
        var (optionsJson, challengeId) = await passkeyService.BeginLoginAsync(
            request.HasEmail ? request.Email : null, ctx.CancellationToken);

        return new BeginPasskeyLoginResponse
        {
            OptionsJson = optionsJson,
            ChallengeId = challengeId
        };
    }

    public override async Task<PasskeyLoginResponse> CompletePasskeyLogin(
        CompletePasskeyLoginRequest request, ServerCallContext ctx)
    {
        try
        {
            var (userId, publicId) = await passkeyService.CompleteLoginAsync(
                request.ChallengeId, request.AssertionResponseJson, ctx.CancellationToken);

            var user = await context.Users
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.PublicId,
                    u.DisplayName,
                    u.Email,
                    u.EmailVerified,
                    u.AvatarFileName,
                    u.AuthVersion,
                    u.TwoFactorEnabled
                })
                .FirstOrDefaultAsync(ctx.CancellationToken)
                ?? throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

            var roles = await context.UserRoles
                .Where(r => r.User.PublicId == publicId && r.RevokedAt == null)
                .Select(r => ((UserRoleTypeEnum)r.RoleId).ToString())
                .ToListAsync(ctx.CancellationToken);

            var jwt = jwtService.GenerateToken(
                user.PublicId,
                user.DisplayName,
                user.Email,
                user.EmailVerified,
                roles.FirstOrDefault(),
                user.AvatarFileName,
                authVersion: user.AuthVersion,
                twoFactorEnabled: user.TwoFactorEnabled);

            var refreshTokenResult = await authUseCase.CreateRefreshTokenAsync(UserId.From(publicId));

            if (!refreshTokenResult.IsSuccess)
                throw new RpcException(new Status(StatusCode.Internal, "Failed to create refresh token"));

            await grantsCache.GetGrantsAsync(publicId);

            logger.LogInformation("Passkey login completed for user {PublicId} from {Ip}",
                publicId, request.IpAddress);

            return new PasskeyLoginResponse
            {
                AccessToken = jwt,
                RefreshToken = refreshTokenResult.Value!.Value
            };
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Passkey login failed: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Passkey verification failed"));
        }
    }

    public override async Task<GetUserPasskeysResponse> GetUserPasskeys(
        GetUserPasskeysRequest request, ServerCallContext ctx)
    {
        RequireAuth();

        var passkeys = await passkeyService.GetUserPasskeysAsync(
            request.UserPublicId, ctx.CancellationToken);

        var response = new GetUserPasskeysResponse();
        foreach (var p in passkeys)
        {
            var info = new PasskeyInfo
            {
                Id = p.Id,
                FriendlyName = p.FriendlyName ?? "",
                Transports = p.Transports ?? "",
                CreatedAt = Timestamp.FromDateTimeOffset(p.CreatedAt)
            };
            if (p.LastUsedAt.HasValue)
                info.LastUsedAt = Timestamp.FromDateTimeOffset(p.LastUsedAt.Value);
            response.Passkeys.Add(info);
        }

        return response;
    }

    public override async Task<PasskeyMessageResponse> DeletePasskey(
        DeletePasskeyRequest request, ServerCallContext ctx)
    {
        RequireAuth();

        await passkeyService.DeletePasskeyAsync(
            request.UserPublicId, request.CredentialId, ctx.CancellationToken);

        return new PasskeyMessageResponse { Message = "Passkey deleted" };
    }

    private void RequireAuth()
    {
        if (!currentUser.IsAuthenticated())
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required"));
    }
}
