namespace Snakk.Application.DTOs.Responses;

public record TwoFactorSetupResponse(string Secret, string QrCodeUri, string Message);

public record TwoFactorEnableResponse(string Message, IEnumerable<string> BackupCodes, string Warning);

public record TwoFactorVerifyResponse(
    string Message,
    string AccessToken,
    bool RequiresTrustPrompt,
    string DeviceFingerprint,
    string DeviceName);

public record BackupCodesStatusResponse(int TotalCodes, int UnusedCodes, IEnumerable<object> Codes);

public record RegenerateBackupCodesResponse(string Message, IEnumerable<string> BackupCodes, string Warning);

public record TrustDeviceResponse(string Message, string ExpiresIn);
