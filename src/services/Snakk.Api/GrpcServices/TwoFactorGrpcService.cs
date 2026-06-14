using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using Snakk.Api.Services;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain;
using Snakk.Domain.ValueObjects;
using Snakk.Protos.TwoFactor;

namespace Snakk.Api.GrpcServices;

public class TwoFactorGrpcService(
    ITwoFactorAuthService twoFactorService,
    ITotpService totpService,
    ITwoFactorSecretProtector secretProtector,
    IJwtTokenService jwtService,
    AuthenticationUseCase authUseCase,
    ICurrentUserService currentUser,
    ITrustedDeviceService trustedDeviceService,
    IAuthDataService authDataService,
    ITwoFactorDataService twoFactorDataService,
    IUserGrantsCacheService grantsCache,
    ILogger<TwoFactorGrpcService> logger) : TwoFactorService.TwoFactorServiceBase
{
    public override async Task<SetupTwoFactorResponse> SetupTwoFactor(
        SetupTwoFactorRequest request, ServerCallContext ctx)
    {
        var ct = ctx.CancellationToken;
        var userId = RequireAuth();

        try
        {
            var setup = await twoFactorService.SetupTwoFactorAsync(userId.Value, ct);

            return new SetupTwoFactorResponse
            {
                Secret = setup.Secret,
                QrCodeUri = setup.QrCodeUrl
            };
        }
        catch (DomainException ex)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in SetupTwoFactor for user {UserId}", userId);
            throw new RpcException(new Status(StatusCode.Internal, "An error occurred. Please try again."));
        }
    }

    public override async Task<EnableTwoFactorResponse> EnableTwoFactor(
        EnableTwoFactorRequest request, ServerCallContext ctx)
    {
        var ct = ctx.CancellationToken;
        var userId = RequireAuth();

        var (success, backupCodes, error) = await twoFactorService.EnableTwoFactorAsync(userId.Value, request.Code, ct);

        if (!success)
            throw new RpcException(new Status(StatusCode.InvalidArgument, error ?? "Failed to enable 2FA"));

        var response = new EnableTwoFactorResponse { Success = true };
        response.BackupCodes.AddRange(backupCodes);

        return response;
    }

    public override async Task<DisableTwoFactorResponse> DisableTwoFactor(
        DisableTwoFactorRequest request, ServerCallContext ctx)
    {
        var ct = ctx.CancellationToken;
        var userId = RequireAuth();

        if (string.IsNullOrWhiteSpace(request.TotpCode))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "A valid 2FA code is required to disable 2FA"));

        var (isValid, _) = await twoFactorService.VerifyTwoFactorCodeAsync(userId.Value, request.TotpCode, ct: ct);

        if (!isValid)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid 2FA code"));

        var success = await twoFactorService.DisableTwoFactorAsync(userId.Value, request.Password, ct);

        if (!success)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid password or 2FA not enabled"));

        return new DisableTwoFactorResponse { Success = true };
    }

    public override async Task<VerifyTwoFactorLoginResponse> VerifyTwoFactorLogin(
        VerifyTwoFactorLoginRequest request, ServerCallContext ctx)
    {
        var ct = ctx.CancellationToken;

        // Hash matches EmailProtector.ComputeHash: SHA256(UTF8(email.Trim().ToLowerInvariant())) → hex lower
        var emailHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(request.Email.Trim().ToLowerInvariant()));
        var emailHash = Convert.ToHexStringLower(emailHashBytes);

        var userData = await twoFactorDataService.GetUserForTwoFactorLoginByEmailHashAsync(emailHash, ct);

        if (userData is null)
        {
            logger.LogWarning("VerifyTwoFactorLogin: user not found or 2FA not enabled for email {Email}", request.Email);
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid request"));
        }

        var isValid = false;

        // Try TOTP code first
        if (!string.IsNullOrEmpty(userData.TwoFactorSecret))
        {
            try
            {
                var decryptedSecret = secretProtector.Unprotect(userData.TwoFactorSecret);
                isValid = totpService.VerifyCode(decryptedSecret, request.Code);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to decrypt 2FA secret for user {UserId}", userData.PublicId);
                throw new RpcException(new Status(StatusCode.Internal, "2FA configuration error. Please re-enable 2FA."));
            }
        }

        // If TOTP fails, try backup codes
        if (!isValid)
        {
            var unusedBackupCodes = userData.BackupCodes
                .Where(bc => !bc.IsUsed)
                .ToList();

            foreach (var backupCode in unusedBackupCodes)
            {
                if (totpService.VerifyBackupCode(request.Code, backupCode.CodeHash))
                {
                    await twoFactorDataService.ConsumeBackupCodeAsync(backupCode.Id, usedIp: null, ct);
                    isValid = true;
                    break;
                }
            }
        }

        if (!isValid)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid 2FA code"));

        // Generate tokens
        var roles = await authDataService.GetUserRolesAsync(userData.PublicId, ct);

        var accessToken = jwtService.GenerateToken(
            userData.PublicId,
            userData.DisplayName,
            userData.Email,
            userData.EmailVerified,
            roles.FirstOrDefault(),
            userData.AvatarFileName,
            authVersion: userData.AuthVersion,
            twoFactorEnabled: userData.TwoFactorEnabled);

        var refreshTokenResult = await authUseCase.CreateRefreshTokenAsync(UserId.From(userData.PublicId));

        if (!refreshTokenResult.IsSuccess)
            throw new RpcException(new Status(StatusCode.Internal, "Failed to create refresh token"));

        logger.LogInformation("2FA login verified for {UserId}", userData.PublicId);

        if (request.TrustDevice && !string.IsNullOrEmpty(request.IpAddress))
        {
            var fingerprint = trustedDeviceService.GenerateDeviceFingerprint(request.UserAgent, request.IpAddress);
            var deviceName = trustedDeviceService.GetDeviceName(request.UserAgent);
            await trustedDeviceService.TrustDeviceAsync(UserId.From(userData.PublicId), fingerprint, deviceName, request.IpAddress, 30, ct);
        }

        await grantsCache.GetGrantsAsync(userData.PublicId, ct);

        return new VerifyTwoFactorLoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenResult.Value!.Value
        };
    }

    public override async Task<GetTwoFactorStatusResponse> GetTwoFactorStatus(
        GetTwoFactorStatusRequest request, ServerCallContext ctx)
    {
        var ct = ctx.CancellationToken;
        var userId = RequireAuth();

        var status = await twoFactorService.GetTwoFactorStatusAsync(userId.Value, ct);

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
        var ct = ctx.CancellationToken;
        var userId = RequireAuth();

        try
        {
            var status = await twoFactorService.GetBackupCodesStatusAsync(userId.Value, ct);

            return new GetBackupCodesStatusResponse
            {
                TotalCodes = status.TotalCount,
                UnusedCodes = status.TotalCount - status.UsedCount
            };
        }
        catch (DomainException ex)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in GetBackupCodesStatus for user {UserId}", userId);
            throw new RpcException(new Status(StatusCode.Internal, "An error occurred. Please try again."));
        }
    }

    public override async Task<RegenerateBackupCodesResponse> RegenerateBackupCodes(
        RegenerateBackupCodesRequest request, ServerCallContext ctx)
    {
        var ct = ctx.CancellationToken;
        var userId = RequireAuth();

        try
        {
            var backupCodes = await twoFactorService.RegenerateBackupCodesAsync(userId.Value, request.Password, ct);

            var response = new RegenerateBackupCodesResponse { Success = true };
            response.BackupCodes.AddRange(backupCodes);

            return response;
        }
        catch (DomainException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in RegenerateBackupCodes for user {UserId}", userId);
            throw new RpcException(new Status(StatusCode.Internal, "An error occurred. Please try again."));
        }
    }

    public override async Task<GetTrustedDevicesResponse> GetTrustedDevices(
        GetTrustedDevicesRequest request, ServerCallContext ctx)
    {
        var ct = ctx.CancellationToken;
        var userId = RequireAuth();

        var devices = await trustedDeviceService.GetTrustedDevicesAsync(userId, ct);
        var response = new GetTrustedDevicesResponse();
        foreach (var d in devices)
        {
            response.Devices.Add(new TrustedDeviceItem
            {
                PublicId = d.PublicId,
                DeviceName = d.DeviceName,
                TrustedAt = d.TrustedAt.ToString("O"),
                ExpiresAt = d.ExpiresAt?.ToString("O") ?? "",
                LastUsedAt = d.LastUsedAt?.ToString("O") ?? "",
                LastUsedIp = d.LastUsedIp ?? "",
                IsActive = d.IsActive
            });
        }
        return response;
    }

    public override async Task<RevokeDeviceResponse> RevokeDevice(
        RevokeDeviceRequest request, ServerCallContext ctx)
    {
        var ct = ctx.CancellationToken;
        var userId = RequireAuth();

        var success = await trustedDeviceService.RevokeDeviceForUserAsync(
            request.DeviceId, userId.Value, "User requested revocation", ct);

        if (!success)
            throw new RpcException(new Status(StatusCode.NotFound, "Device not found"));

        return new RevokeDeviceResponse { Success = true };
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
}
