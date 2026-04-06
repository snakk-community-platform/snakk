namespace Snakk.Shared.Enums;

public enum PasswordResetRequestOutcomeEnum
{
    UserNotFound = 0,
    UserFound = 1,
    OAuthOnly = 2,
    RateLimited = 3,
    CaptchaFailed = 4
}
