namespace Fundoo.User.Application.Abstractions
{
    public interface ITwoFactorAuthService
    {
        /// <summary>
        /// Generates a random 6-digit OTP code for email delivery
        /// </summary>
        string GenerateOtpCode();

        /// <summary>
        /// Verifies a 6-digit OTP code sent via email
        /// </summary>
        bool VerifyOtpCode(string otpCode, string providedCode);

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
