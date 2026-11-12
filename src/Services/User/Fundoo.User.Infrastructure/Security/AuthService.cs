using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entity;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace Fundoo.User.Infrastructure.Security
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly ITwoFactorAuthService _twoFactorService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher, ITokenService tokenService, ITwoFactorAuthService twoFactorService, IHttpClientFactory httpClientFactory, ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _twoFactorService = twoFactorService;
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

            // Check if user has 2FA enabled
            if (user.TwoFactorEnabled)
            {
                // Generate and send OTP to user's email
                string otpCode = _twoFactorService.GenerateOtpCode();
                var expiryMinutes = 10;
                var otpToken = new OtpToken
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    OtpCode = otpCode,
                    Attempts = 0,
                    CreatedAtUtc = DateTime.UtcNow,
                    ExpiresAtUtc = DateTime.UtcNow.AddMinutes(expiryMinutes),
                    IsVerified = false
                };

                await _userRepository.AddOtpTokenAsync(otpToken, cancellationToken);
                await _userRepository.SaveChangesAsync(cancellationToken);

                // Send OTP to email
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
                        "api/notifications/2fa-otp",
                        request,
                        cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("2FA OTP email failed to send for user {UserId}", user.Id);
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Notification service request failed for user {UserId}", user.Id);
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogError(ex, "Notification service request timed out for user {UserId}", user.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send 2FA OTP email for user {UserId}", user.Id);
                }

                return new AuthResult
                {
                    IsSuccess = true,
                    Message = "Login successful. OTP has been sent to your email. Please verify with the code.",
                    UserId = user.Id,
                    RequiresTwoFactor = true,
                    AccessToken = null
                };
            }

            var accessToken = _tokenService.CreateAccessToken(user);

            return new AuthResult
            {
                IsSuccess = true,
                Message = "Login successful.",
                AccessToken = accessToken,
                UserId = user.Id,
                RequiresTwoFactor = false
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

            if (user is null || !user.IsActive || !user.TwoFactorEnabled)
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "2FA is not enabled for this account."
                };
            }

            // Get the latest valid OTP for this user
            var otpToken = await _userRepository.GetLatestOtpTokenAsync(user.Id, cancellationToken);

            if (otpToken is null || otpToken.IsVerified || otpToken.ExpiresAtUtc <= DateTime.UtcNow)
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "OTP has expired or is invalid."
                };
            }

            if (otpToken.Attempts >= 3)
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Maximum OTP attempts exceeded. Request a new code."
                };
            }

            // Verify the OTP code
            if (!_twoFactorService.VerifyOtpCode(otpToken.OtpCode, code))
            {
                otpToken.Attempts++;
                await _userRepository.SaveChangesAsync(cancellationToken);

                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Invalid OTP code."
                };
            }

            // Mark OTP as verified
            otpToken.IsVerified = true;
            otpToken.VerifiedAtUtc = DateTime.UtcNow;
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
        

        public async Task<TwoFactorSetupResult> InitiateTwoFactorAsync(Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

            if (user is null || !user.IsActive)
            {
                return new TwoFactorSetupResult
                {
                    IsSuccess = false,
                    Message = "User not found or inactive."
                };
            }

            // Generate a new 6-digit OTP
            string otpCode = _twoFactorService.GenerateOtpCode();

            // Create OTP token entry
            var otpToken = new OtpToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OtpCode = otpCode,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10), // 10 minute expiry
                Attempts = 0,
                IsVerified = false
            };

            await _userRepository.AddOtpTokenAsync(otpToken, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);

            // Send OTP to email
            try
            {
                var httpClient = _httpClientFactory.CreateClient("NotificationService");

                var notificationRequest = new
                {
                    RecipientEmail = user.Email,
                    RecipientName = user.FirstName,
                    OtpCode = otpCode,
                    ExpiryMinutes = 10
                };

                var response = await httpClient.PostAsJsonAsync(
                    "api/notifications/2fa-otp",
                    notificationRequest,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Notification service returned {StatusCode} for 2FA OTP of user {UserId}",
                        (int)response.StatusCode, userId);
                }
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Notification service request timed out for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send 2FA OTP email for user {UserId}", userId);
            }

            return new TwoFactorSetupResult
            {
                IsSuccess = true,
                Message = "OTP has been sent to your registered email. It expires in 10 minutes.",
                Secret = otpCode  // Return OTP in response (client can display message showing last digits)
            };
        }

        public async Task<bool> ConfirmTwoFactorAsync(Guid userId, string code, string secret,
            CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

            if (user is null || !user.IsActive)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            // Get the latest OTP token for this user
            var otpToken = await _userRepository.GetLatestOtpTokenAsync(userId, cancellationToken);

            if (otpToken is null || otpToken.IsVerified || otpToken.ExpiresAtUtc <= DateTime.UtcNow)
            {
                return false;
            }

            if (otpToken.Attempts >= 3)
            {
                return false;
            }

            // Verify the OTP code
            if (!_twoFactorService.VerifyOtpCode(otpToken.OtpCode, code))
            {
                otpToken.Attempts++;
                await _userRepository.SaveChangesAsync(cancellationToken);
                return false;
            }

            // Mark OTP as verified
            otpToken.IsVerified = true;
            otpToken.VerifiedAtUtc = DateTime.UtcNow;

            // Enable 2FA on user account (for email OTP, we don't store a secret, just mark it enabled)
            user.TwoFactorEnabled = true;
            user.UpdatedAtUtc = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task<bool> DisableTwoFactorAsync(Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

            if (user is null || !user.IsActive)
            {
                return false;
            }

            user.TwoFactorEnabled = false;
            user.TwoFactorSecret = null;
            user.UpdatedAtUtc = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}
