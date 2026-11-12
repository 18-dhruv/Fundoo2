using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.User.Application.Abstractions
{
    public interface IAuthService
    {
        Task<AuthResult> RegisterAsync(string firstName, string lastName, string email, string password,
        CancellationToken cancellationToken = default);

        Task<AuthResult> LoginAsync(string email, string password,
            CancellationToken cancellationToken = default);

        Task<AuthResult> VerifyTwoFactorCodeAsync(string email, string code,
            CancellationToken cancellationToken = default);

        Task<TwoFactorSetupResult> InitiateTwoFactorAsync(Guid userId,
            CancellationToken cancellationToken = default);

        Task<bool> ConfirmTwoFactorAsync(Guid userId, string code, string secret,
            CancellationToken cancellationToken = default);

        Task<bool> DisableTwoFactorAsync(Guid userId,
            CancellationToken cancellationToken = default);

        Task ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);

        Task<bool> ResetPasswordAsync(string email, string token, string newPassword,
            CancellationToken cancellationToken = default);
    }
    public class AuthResult
    {
        public bool IsSuccess { get; init; }

        public string Message { get; init; } = string.Empty;

        public string? AccessToken { get; init; }

        public Guid? UserId { get; init; }

        public bool RequiresTwoFactor { get; init; }
    }

    public class TwoFactorSetupResult
    {
        public bool IsSuccess { get; init; }

        public string Message { get; init; } = string.Empty;

        public string? Secret { get; init; }

        public string? QrCodeSetupKey { get; init; }
    }
}
