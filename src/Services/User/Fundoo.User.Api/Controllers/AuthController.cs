using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fundoo.User.Api.Contracts;
using Fundoo.User.Application.Abstractions;

namespace Fundoo.User.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _authService.RegisterAsync(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Password,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return Conflict(new
                {
                    isSuccess = false,
                    message = result.Message
                });
            }

            return StatusCode(StatusCodes.Status201Created, new
            {
                isSuccess = true,
                message = result.Message
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(15),
                cancellationToken);
            Console.WriteLine($"[Login] hit at {DateTime.Now:HH:mm:ss.fff}");

            var result = await _authService.LoginAsync(
                request.Email,
                request.Password,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return Unauthorized(new
                {
                    isSuccess = false,
                    message = result.Message
                });
            }

            if (result.RequiresTwoFactor)
            {
                return Ok(new
                {
                    isSuccess = true,
                    message = result.Message,
                    userId = result.UserId,
                    requiresTwoFactor = true,
                    accessToken = (string?)null
                });
            }

            return Ok(new
            {
                isSuccess = true,
                message = result.Message,
                userId = result.UserId,
                requiresTwoFactor = false,
                accessToken = result.AccessToken
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request,
            CancellationToken cancellationToken)
        {
            await _authService.ForgotPasswordAsync(
                request.Email,
                cancellationToken);

            return Ok(new
            {
                isSuccess = true,
                message = "If the account exists, password reset instructions will be sent to the registered email."
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request,
            CancellationToken cancellationToken)
        {
            var isReset = await _authService.ResetPasswordAsync(
                request.Email,
                request.Token,
                request.NewPassword,
                cancellationToken);

            if (!isReset)
            {
                return BadRequest(new
                {
                    isSuccess = false,
                    message = "The password reset request is invalid or has expired."
                });
            }

            return Ok(new
            {
                isSuccess = true,
                message = "Password has been reset successfully."
            });
        }

        [Authorize]
        [HttpPost("2fa/enable")]
        public async Task<IActionResult> Enable2FA(
            CancellationToken cancellationToken)
        {
            // Get user ID from claims (requires authorization)
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new
                {
                    isSuccess = false,
                    message = "User not authenticated."
                });
            }

            var result = await _authService.InitiateTwoFactorAsync(userId, cancellationToken);

            if (!result.IsSuccess)
            {
                return BadRequest(new
                {
                    isSuccess = false,
                    message = result.Message
                });
            }

            return Ok(new
            {
                isSuccess = true,
                message = result.Message
            });
        }

        [Authorize]
        [HttpPost("2fa/confirm")]
        public async Task<IActionResult> Confirm2FA(
            [FromBody] Confirm2FARequest request,
            CancellationToken cancellationToken)
        {
            // Get user ID from claims (requires authorization)
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new
                {
                    isSuccess = false,
                    message = "User not authenticated."
                });
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new
                {
                    isSuccess = false,
                    message = "OTP code is required."
                });
            }

            // Verify the OTP code and enable 2FA
            var result = await _authService.ConfirmTwoFactorAsync(userId, request.Code, string.Empty, cancellationToken);

            if (!result)
            {
                return BadRequest(new
                {
                    isSuccess = false,
                    message = "Failed to confirm 2FA setup. Invalid or expired OTP code."
                });
            }

            return Ok(new
            {
                isSuccess = true,
                message = "Two-factor authentication has been enabled successfully."
            });
        }

        [HttpPost("2fa/verify")]
        public async Task<IActionResult> Verify2FA(
            [FromBody] Verify2FARequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new
                {
                    isSuccess = false,
                    message = "Email and code are required."
                });
            }

            var result = await _authService.VerifyTwoFactorCodeAsync(
                request.Email,
                request.Code,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return Unauthorized(new
                {
                    isSuccess = false,
                    message = result.Message
                });
            }

            return Ok(new
            {
                isSuccess = true,
                message = result.Message,
                accessToken = result.AccessToken,
                userId = result.UserId
            });
        }

        [Authorize]
        [HttpPost("2fa/disable")]
        public async Task<IActionResult> Disable2FA(
            CancellationToken cancellationToken)
        {
            // Get user ID from claims (requires authorization)
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new
                {
                    isSuccess = false,
                    message = "User not authenticated."
                });
            }

            var result = await _authService.DisableTwoFactorAsync(userId, cancellationToken);

            if (!result)
            {
                return BadRequest(new
                {
                    isSuccess = false,
                    message = "Failed to disable 2FA."
                });
            }

            return Ok(new
            {
                isSuccess = true,
                message = "Two-factor authentication has been disabled."
            });
        }
    }
}