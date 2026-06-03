namespace Snakk.Application.UseCases;

using Snakk.Application.DTOs.Gdpr;
using Snakk.Application.Events;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;

public class GdprUseCase(
    IUserRepository userRepository,
    ITokenService tokenService,
    ISecurityService securityService,
    IDomainEventDispatcher eventDispatcher,
    IGdprRepository gdprRepository)
{
    public async Task<Result> DeleteAccountAsync(string userId, CancellationToken ct = default)
    {
        var publicId = UserId.From(userId);

        var user = await userRepository.GetByPublicIdAsync(publicId, ct);
        if (user is null)
            return Result.Failure("User not found.");

        user.Anonymize();

        await userRepository.UpdateAsync(user, ct);

        await eventDispatcher.DispatchAsync(user.DomainEvents, ct);
        user.ClearDomainEvents();

        await tokenService.RevokeAllUserTokensAsync(publicId, "AccountDeleted", ct);

        await securityService.LogAuditAsync(
            action: "AccountDeleted",
            category: "GDPR",
            actorUserId: userId,
            details: $"{{\"deletedAt\":\"{DateTime.UtcNow:O}\"}}",
            success: true,
            severity: AuditLogSeverityEnum.Warning,
            ct: ct);

        return Result.Success();
    }

    public async Task<UserDataExportBundle?> ExportUserDataAsync(string userId, CancellationToken ct = default)
    {
        return await gdprRepository.ExportUserDataAsync(userId, ct);
    }
}
