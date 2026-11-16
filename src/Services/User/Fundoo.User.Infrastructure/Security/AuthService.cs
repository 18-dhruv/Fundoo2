using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entity;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace Fundoo.User.Infrastructure.Security
{
    public class AuthService : IAuthService, IExternalAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly ITwoFactorAuthService _twoFactorService;
        private readonly ITwoFactorOtpStore _twoFactorOtpStore;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher,
            ITokenService tokenService, ITwoFactorAuthService twoFactorService,
            ITwoFactorOtpStore twoFactorOtpStore, IHttpClientFactory httpClientFactory,
            ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _twoFactorService = twoFactorService;
            _twoFactorOtpStore = twoFactorOtpStore;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<AuthResult> RegisterAsync(string firstName, string lastName, string email, string password,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();

            var existingUser =
                await _userRepository.GetByNormalizedEmailAsync(
                    normalizedEmail,
                    cancellationToken);

            if (existingUser is not null)
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Email is already registered."
                };
            }

            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                Email = email.Trim(),
                NormalizedEmail = normalizedEmail,
                PasswordHash = _passwordHasher.Hash(password),
                IsActive = true,
                TwoFactorEnabled = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);

            var accessToken = _tokenService.CreateAccessToken(user);

            return new AuthResult
            {
                IsSuccess = true,
                Message = "Registration successful.",
                //AccessToken = accessToken,
                UserId = user.Id
            };
        }

        public async Task<AuthResult> LoginAsync(string email, string password,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();

            var user =
                await _userRepository.GetByNormalizedEmailAsync(
                    normalizedEmail,
                    cancellationToken);

            if (user is null ||
                !user.IsActive ||
                !_passwordHasher.Verify(password, user.PasswordHash))
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Invalid email or password."
                };
            }

            return await IssueOtpChallengeAsync(user, cancellationToken);
        }

        public async Task<AuthResult> LoginWithExternalProviderAsync(string provider, string providerSubject, string email,
            bool emailVerified, string firstName, string lastName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(provider) ||
                string.IsNullOrWhiteSpace(providerSubject) ||
                string.IsNullOrWhiteSpace(email) ||
                !emailVerified)
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = $"{provider} did not provide a verified email address."
                };
            }

            provider = provider.Trim();
            var externalLogin = await _userRepository.GetExternalLoginAsync(
                provider, providerSubject, cancellationToken);

            AppUser? user;
            if (externalLogin is not null)
            {
                user = await _userRepository.GetByIdAsync(externalLogin.UserId, cancellationToken);
            }
            else
            {
                var normalizedEmail = email.Trim().ToUpperInvariant();
                user = await _userRepository.GetByNormalizedEmailAsync(
                    normalizedEmail, cancellationToken);

                if (user is null)
                {
                    var unusablePassword = Convert.ToBase64String(
                        RandomNumberGenerator.GetBytes(48));
                    user = new AppUser
                    {
                        Id = Guid.NewGuid(),
                        FirstName = string.IsNullOrWhiteSpace(firstName) ? provider : firstName.Trim(),
                        LastName = lastName.Trim(),
                        Email = email.Trim(),
                        NormalizedEmail = normalizedEmail,
                        PasswordHash = _passwordHasher.Hash(unusablePassword),
                        IsActive = true,
                        TwoFactorEnabled = true,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    };

                    await _userRepository.AddAsync(user, cancellationToken);
                    await _userRepository.SaveChangesAsync(cancellationToken);
                }

                if (user.IsActive)
                {
                    await _userRepository.AddExternalLoginAsync(new ExternalLogin
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        Provider = provider,
                        ProviderSubject = providerSubject,
                        CreatedAtUtc = DateTime.UtcNow
                    }, cancellationToken);
                    await _userRepository.SaveChangesAsync(cancellationToken);
                }
            }

            if (user is null || !user.IsActive)
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = $"This {provider} account is not allowed to sign in."
                };
            }

            return await IssueOtpChallengeAsync(user, cancellationToken);
        }

        private async Task<AuthResult> IssueOtpChallengeAsync(AppUser user,
            CancellationToken cancellationToken)
        {
            await _twoFactorOtpStore.InvalidateAsync(user.Id, cancellationToken);

            var otpCode = _twoFactorService.GenerateOtpCode();
            const int expiryMinutes = 10;
            await _twoFactorOtpStore.IssueAsync(
                user.Id, otpCode, TimeSpan.FromMinutes(expiryMinutes), cancellationToken);

            var sent = false;
            try
            {
                var httpClient = _httpClientFactory.CreateClient("NotificationService");
                var request = new
                {
                    RecipientEmail = user.Email,
                    RecipientName = user.FirstName,
                    OtpCode = otpCode,
                    ExpiryMinutes = expiryMinutes
                };

                var response = await httpClient.PostAsJsonAsync(
                    "api/notifications/2fa-otp", request, cancellationToken);
                sent = response.IsSuccessStatusCode;
                if (!sent)
                {
                    _logger.LogWarning(
                        "2FA OTP email failed with status {StatusCode} for user {UserId}",
                        (int)response.StatusCode, user.Id);
                }
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Notification service request timed out for user {UserId}", user.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to send 2FA OTP for user {UserId}", user.Id);
            }

            if (!sent)
            {
                await _twoFactorOtpStore.InvalidateAsync(user.Id, cancellationToken);
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Unable to send the verification code. Please try signing in again."
                };
            }

            return new AuthResult
            {
                IsSuccess = true,
                Message = "Login successful. OTP has been sent to your email. Please verify with the code.",
                UserId = user.Id,
                RequiresTwoFactor = true
            };
        }

        public async Task ForgotPasswordAsync(string email,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();

            var user = await _userRepository.GetByNormalizedEmailAsync(
                normalizedEmail,
                cancellationToken);

            if (user is null || !user.IsActive)
                return;

            var rawToken = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(32));

            var tokenHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

            var now = DateTime.UtcNow;

            // Invalidate older, unused reset tokens.
            await _userRepository.InvalidatePasswordResetTokensAsync(
                user.Id,
                now,
                cancellationToken);

            var resetToken = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAtUtc = now.AddMinutes(15),
                UsedAtUtc = null
            };

            await _userRepository.AddPasswordResetTokenAsync(
                resetToken,
                cancellationToken);

            await _userRepository.SaveChangesAsync(cancellationToken);

            try
            {
                var httpClient = _httpClientFactory.CreateClient("NotificationService");

                var notificationRequest = new PasswordResetNotificationRequest
                {
                    RecipientEmail = user.Email,
                    RecipientName = user.FirstName,
                    ResetToken = rawToken
                };

                var response = await httpClient.PostAsJsonAsync(
                    "api/notifications/password-reset",
                    notificationRequest,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Notification service returned {StatusCode} for password reset of user {UserId}",
                        (int)response.StatusCode, user.Id);
                }
            }

            catch (OperationCanceledException ex)
      when (!cancellationToken.IsCancellationRequested)
            {
                // HttpClient timeout occurred (5 seconds).
                _logger.LogError(ex,
                    "Notification service request timed out for user {UserId}",
                    user.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Failed to send password reset email for user {UserId}",
                    user.Id);
            }
        }

        public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();

            var user = await _userRepository.GetByNormalizedEmailAsync(
                normalizedEmail,
                cancellationToken);

            if (user is null || !user.IsActive)
                return false;

            if (string.IsNullOrWhiteSpace(token) ||
                string.IsNullOrWhiteSpace(newPassword))
            {
                return false;
            }

            var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

            var resetToken = await _userRepository.GetPasswordResetTokenByHashAsync(
                    tokenHash,
                    cancellationToken);

            var now = DateTime.UtcNow;

            if (resetToken is null ||
                resetToken.UserId != user.Id ||
                resetToken.UsedAtUtc is not null ||
                resetToken.ExpiresAtUtc <= now)
            {
                return false;
            }

            var newPasswordHash = _passwordHasher.Hash(newPassword);

            await _userRepository.UpdatePasswordAsync(
                user,
                newPasswordHash,
                now,
                cancellationToken);

            resetToken.UsedAtUtc = now;

            await _userRepository.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task<AuthResult> VerifyTwoFactorCodeAsync(string email, string code,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();

            var user = await _userRepository.GetByNormalizedEmailAsync(
                normalizedEmail,
                cancellationToken);

            if (user is null || !user.IsActive)
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Account not found or inactive."
                };
            }

            var otpValidation = await _twoFactorOtpStore.ValidateAndConsumeAsync(
                user.Id, code, cancellationToken);
            if (otpValidation != OtpChallengeValidationResult.Success)
            {
                var message = otpValidation switch
                {
                    OtpChallengeValidationResult.AttemptsExceeded =>
                        "Maximum OTP attempts exceeded. Sign in again to request a new code.",
                    OtpChallengeValidationResult.InvalidCode => "Invalid OTP code.",
                    _ => "OTP has expired or is invalid."
                };

                return new AuthResult { IsSuccess = false, Message = message };
            }

            user.TwoFactorEnabled = true;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);

            var accessToken = _tokenService.CreateAccessToken(user);

            return new AuthResult
            {
                IsSuccess = true,
                Message = "OTP verified successfully.",
                AccessToken = accessToken,
                UserId = user.Id
            };
        }
    }
}
