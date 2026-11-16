using Fundoo.User.Api.Authentication.Google;
using Fundoo.User.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Fundoo.User.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class ExternalAuthController(
    IExternalAuthService authService,
    IOptions<GoogleOAuthOptions> googleOptions) : ControllerBase
{
    [HttpGet("google")]
    public IActionResult GoogleLogin()
    {
        if (!googleOptions.Value.IsConfigured)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                detail: "Google OAuth is not configured. Set Authentication:Google:ClientId and ClientSecret.");
        }

        return Challenge(new AuthenticationProperties
        {
            RedirectUri = "/api/auth/google/callback"
        }, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback(CancellationToken cancellationToken)
    {
        var externalAuth = await HttpContext.AuthenticateAsync(
            GoogleAuthenticationExtensions.ExternalCookieScheme);
        await HttpContext.SignOutAsync(
            GoogleAuthenticationExtensions.ExternalCookieScheme);

        if (!externalAuth.Succeeded || externalAuth.Principal is null)
        {
            return Unauthorized(new
            {
                isSuccess = false,
                message = "Google sign-in was not completed."
            });
        }

        var principal = externalAuth.Principal;
        var providerSubject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
        var email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email");
        var verifiedEmailClaim = principal.Claims.FirstOrDefault(claim =>
            claim.Type.Equals("verified_email", StringComparison.OrdinalIgnoreCase) ||
            claim.Type.Equals("email_verified", StringComparison.OrdinalIgnoreCase) ||
            claim.Type.EndsWith("/email_verified", StringComparison.OrdinalIgnoreCase))?.Value;
        var emailVerified = bool.TryParse(verifiedEmailClaim, out var isEmailVerified) &&
            isEmailVerified;

        var result = await authService.LoginWithExternalProviderAsync(
            "Google",
            providerSubject ?? string.Empty,
            email ?? string.Empty,
            emailVerified,
            principal.FindFirstValue(ClaimTypes.GivenName) ?? "Google",
            principal.FindFirstValue(ClaimTypes.Surname) ?? string.Empty,
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
            userId = result.UserId,
            email,
            requiresTwoFactor = result.RequiresTwoFactor,
            accessToken = result.AccessToken
        });
    }
}
