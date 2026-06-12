namespace Snakk.Application.Services;

/// <summary>
/// Service for Time-based One-Time Password (TOTP) operations used in 2FA
/// </summary>
public interface ITotpService
{
    /// <summary>
    /// Generates a new secret key for TOTP
    /// </summary>
    /// <returns>Base32-encoded secret key</returns>
    string GenerateSecret();

    /// <summary>
    /// Generates a QR code URI for TOTP setup (compatible with Google Authenticator, Authy, etc.)
    /// </summary>
    /// <param name="secret">Base32-encoded secret key</param>
    /// <param name="accountName">Account name (e.g., user email or username)</param>
    /// <param name="issuer">Issuer name (e.g., "Snakk")</param>
    /// <returns>otpauth:// URI for QR code generation</returns>
    string GenerateQrCodeUri(string secret, string accountName, string issuer = "Snakk");

    /// <summary>
    /// Verifies a TOTP code against a secret
    /// </summary>
    /// <param name="secret">Base32-encoded secret key</param>
    /// <param name="code">6-digit code from authenticator app</param>
    /// <param name="window">Time window tolerance (default: 1 = 30 seconds before/after)</param>
    /// <returns>True if code is valid, false otherwise</returns>
    bool VerifyCode(string secret, string code, int window = 1);

    /// <summary>
    /// Verifies a TOTP code and reports which 30-second time-step matched. The step lets
    /// callers prevent replay of a still-valid code within the verification window.
    /// </summary>
    /// <param name="matchedStep">The matched Unix time-step (seconds / 30), or -1 if no match.</param>
    /// <returns>True if code is valid, false otherwise</returns>
    bool TryVerifyCode(string secret, string code, out long matchedStep, int window = 1);

    /// <summary>
    /// Generates recovery/backup codes for 2FA
    /// </summary>
    /// <param name="count">Number of codes to generate (default: 10)</param>
    /// <returns>List of backup codes (8 characters each)</returns>
    List<string> GenerateBackupCodes(int count = 10);

    /// <summary>
    /// Hashes a backup code for secure storage
    /// </summary>
    string HashBackupCode(string code);

    /// <summary>
    /// Verifies a backup code against its hash
    /// </summary>
    bool VerifyBackupCode(string code, string hash);
}
