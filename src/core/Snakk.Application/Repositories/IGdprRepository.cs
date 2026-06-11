namespace Snakk.Application.Repositories;

using Snakk.Application.DTOs.Gdpr;

public interface IGdprRepository
{
    Task<UserDataExportBundle?> ExportUserDataAsync(string publicId, CancellationToken ct = default);
}
