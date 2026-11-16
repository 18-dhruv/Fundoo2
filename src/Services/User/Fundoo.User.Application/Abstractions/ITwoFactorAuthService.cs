namespace Fundoo.User.Application.Abstractions
{
    public enum OtpChallengeValidationResult
    {
        InvalidOrExpired,
        InvalidCode,
        AttemptsExceeded,
        Success
    }

    public interface ITwoFactorOtpStore
    {
        Task IssueAsync(Guid userId, string code, TimeSpan lifetime,
            CancellationToken cancellationToken = default);

        Task<OtpChallengeValidationResult> ValidateAndConsumeAsync(Guid userId, string code,
            CancellationToken cancellationToken = default);

        Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default);
    }

    public interface ITwoFactorAuthService
    {
        /// <summary>
        /// Generates a random 6-digit OTP code for email delivery
        /// </summary>
        string GenerateOtpCode();

        /// <summary>
        /// Generates a new 2FA secret (deprecated - kept for backward compatibility)
        /// </summary>
        string GenerateTwoFactorSecret(string email, string issuer = "Fundoo");

        /// <summary>
        /// Verifies TOTP code (deprecated - kept for backward compatibility)
        /// </summary>
        bool VerifyTwoFactorCode(string secret, string code);

        /// <summary>
        /// Gets the setup key (deprecated - kept for backward compatibility)
        /// </summary>
        string GetSetupKey(string email, string secret, string issuer = "Fundoo");
    }
}
