using Fundoo.User.Application.Abstractions;
using System.Security.Cryptography;

namespace Fundoo.User.Infrastructure.Security
{
    public class TwoFactorAuthService : ITwoFactorAuthService
    {
        private const int OtpLength = 6;

        /// <summary>
        /// Generates a random 6-digit OTP code for email delivery
        /// </summary>
        public string GenerateOtpCode()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] data = new byte[4];
                rng.GetBytes(data);

                // Convert to unsigned int and get last 6 digits
                uint randomNumber = BitConverter.ToUInt32(data, 0);
                int otp = (int)(randomNumber % 1000000);

                return otp.ToString("D6");
            }
        }

        /// <summary>
        /// Generates a new 2FA secret (now just returns null since we're using email OTP instead of TOTP)
        /// </summary>
        public string GenerateTwoFactorSecret(string email, string issuer = "Fundoo")
        {
            // For email OTP, we don't need a secret
            return string.Empty;
        }

        /// <summary>
        /// Verifies TOTP code (deprecated - kept for backward compatibility)
        /// </summary>
        public bool VerifyTwoFactorCode(string secret, string code)
        {
            // This method is no longer used for email OTP
            return false;
        }

        /// <summary>
        /// Gets the setup key (deprecated - kept for backward compatibility)
        /// </summary>
        public string GetSetupKey(string email, string secret, string issuer = "Fundoo")
        {
            // Not used for email OTP
            return string.Empty;
        }
    }
}
