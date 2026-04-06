using Snakk.Shared.Enums;

namespace Snakk.Application.Repositories;

public interface IPasswordResetRequestRepository
{
    Task LogAsync(
        string emailHash,
        string ipAddress,
        PasswordResetRequestOutcomeEnum outcome);

    /// <summary>
    /// Counts requests from a given IP address within a rolling time window.
    /// Used for rate limiting.
    /// </summary>
    Task<int> CountByIpInWindowAsync(string ipAddress, TimeSpan window);
}
