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

        Task ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);

        Task<bool> ResetPasswordAsync(string email, string token, string newPassword,
            CancellationToken cancellationToken = default);
    }
}
