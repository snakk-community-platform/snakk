using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Snakk.Api.Services;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Protos.TwoFactor;
using Snakk.Shared.Enums;

namespace Snakk.Api.GrpcServices;

public class TwoFactorGrpcService(
    ITwoFactorAuthService twoFactorService,
    ITotpService totpService,
    ITwoFactorSecretProtector secretProtector,
    IJwtTokenService jwtService,
    AuthenticationUseCase authUseCase,
    ICurrentUserService currentUser,
    SnakkDbContext context,
    IUserGrantsCacheService grantsCache,
    ILogger<TwoFactorGrpcService> logger) : TwoFactorService.TwoFactorServiceBase
{
    public override async Task<SetupTwoFactorResponse> SetupTwoFactor(
        SetupTwoFactorRequest request, ServerCallContext ctx)
    {
        var userId = RequireAuth();

        try
        {
            var setup = await twoFactorService.SetupTwoFactorAsync(userId.Value);

            return new SetupTwoFactorResponse
            {
                Secret = setup.Secret,
                QrCodeUri = setup.QrCodeUrl
            };
        }
        catch (InvalidOperationException ex)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
    }

    public override async Task<EnableTwoFactorResponse> EnableTwoFactor(
        EnableTwoFactorRequest request, ServerCallContext ctx)
    {
        var userId = RequireAuth();

        var (success, backupCodes, error) = await twoFactorService.EnableTwoFactorAsync(userId.Value, request.Code);

        if (!success)
            throw new RpcException(new Status(StatusCode.InvalidArgument, error ?? "Failed to enable 2FA"));

        var response = new EnableTwoFactorResponse { Success = true };
        response.BackupCodes.AddRange(backupCodes);

        return response;
    }

    public override async Task<DisableTwoFactorResponse> DisableTwoFactor(
        DisableTwoFactorRequest request, ServerCallContext ctx)
    {
        var userId = RequireAuth();

        if (string.IsNullOrWhiteSpace(request.TotpCode))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "A valid 2FA code is required to disable 2FA"));

        var (isValid, _) = await twoFactorService.VerifyTwoFactorCodeAsync(userId.Value, request.TotpCode);

        if (!isValid)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid 2FA code"));

        var success = await twoFactorService.DisableTwoFactorAsync(userId.Value, request.Password);

        if (!success)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid password or 2FA not enabled"));

        return new DisableTwoFactorResponse { Success = true };
    }

    public override async Task<VerifyTwoFactorLoginResponse> VerifyTwoFactorLogin(
        VerifyTwoFactorLoginRequest request, ServerCallContext ctx)
    {
        var user = await context.Users
            .Include(u => u.TwoFactorBackupCodes)
            .Include(u => u.Roles.Where(r => r.RevokedAt == null))
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || !user.TwoFactorEnabled)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid request"));

        var isValid = false;

        // Try TOTP code first
        if (!string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            var decryptedSecret = secretProtector.Unprotect(user.TwoFactorSecret);
            isValid = totpService.VerifyCode(decryptedSecret, request.Code);
        }

        // If TOTP fails, try backup codes
        if (!isValid)
        {
            var unusedBackupCodes = user.TwoFactorBackupCodes
                .Where(bc => !bc.IsUsed)
                .ToList();

            foreach (var backupCode in unusedBackupCodes)
            {
                if (totpService.VerifyBackupCode(request.Code, backupCode.CodeHash))
                {
                    backupCode.IsUsed = true;
                    backupCode.UsedAt = DateTime.UtcNow;
                    backupCode.UsedIp = null;
                    await context.SaveChangesAsync();

                    isValid = true;
                    break;
                }
            }
        }

        if (!isValid)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid 2FA code"));

        // Generate tokens
        var roles = await GetUserRolesAsync(user.PublicId);

        var accessToken = jwtService.GenerateToken(
            user.PublicId,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            user.OAuthProvider,
            roles.FirstOrDefault(),
            user.AvatarFileName);

        var refreshTokenResult = await authUseCase.CreateRefreshTokenAsync(UserId.From(user.PublicId));

        if (!refreshTokenResult.IsSuccess)
            throw new RpcException(new Status(StatusCode.Internal, "Failed to create refresh token"));

        logger.LogInformation("2FA login verified for {UserId}", user.PublicId);

        await grantsCache.GetGrantsAsync(user.PublicId);

        return new VerifyTwoFactorLoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenResult.Value!.Value
        };
    }

    public override async Task<GetTwoFactorStatusResponse> GetTwoFactorStatus(
        GetTwoFactorStatusRequest request, ServerCallContext ctx)
    {
        var userId = RequireAuth();

        var status = await twoFactorService.GetTwoFactorStatusAsync(userId.Value);

        if (status is null)
            throw new RpcException(new Status(StatusCode.NotFound, "2FA status not found"));

        return new GetTwoFactorStatusResponse
        {
            IsEnabled = status.IsEnabled,
            UnusedBackupCodes = status.TotalBackupCodes - status.UsedBackupCodesCount
        };
    }

    public override async Task<GetBackupCodesStatusResponse> GetBackupCodesStatus(
        GetBackupCodesStatusRequest request, ServerCallContext ctx)
    {
        var userId = RequireAuth();

        try
        {
            var status = await twoFactorService.GetBackupCodesStatusAsync(userId.Value);

            return new GetBackupCodesStatusResponse
            {
                TotalCodes = status.TotalCount,
                UnusedCodes = status.TotalCount - status.UsedCount
            };
        }
        catch (InvalidOperationException ex)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
    }

    public override async Task<RegenerateBackupCodesResponse> RegenerateBackupCodes(
        RegenerateBackupCodesRequest request, ServerCallContext ctx)
    {
        var userId = RequireAuth();

        try
        {
            var backupCodes = await twoFactorService.RegenerateBackupCodesAsync(userId.Value, request.Password);

            var response = new RegenerateBackupCodesResponse { Success = true };
            response.BackupCodes.AddRange(backupCodes);

            return response;
        }
        catch (InvalidOperationException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
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

    private async Task<List<string>> GetUserRolesAsync(string publicId) =>
        await context.UserRoles
            .Where(r => r.User.PublicId == publicId && r.RevokedAt == null)
            .Select(r => ((UserRoleTypeEnum)r.RoleId).ToString())
            .ToListAsync();
}
