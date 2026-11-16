using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

namespace Fundoo.User.Api.Authentication.Google;

public static class GoogleAuthenticationExtensions
{
    public const string ExternalCookieScheme = "GoogleExternal";

    public static AuthenticationBuilder AddFundooGoogleAuthentication(
        this AuthenticationBuilder authentication,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(GoogleOAuthOptions.SectionName);
        authentication.Services.Configure<GoogleOAuthOptions>(section);

        authentication.AddCookie(ExternalCookieScheme, options =>
        {
            options.Cookie.Name = ".Fundoo.Google.External";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            options.SlidingExpiration = false;
        });

        var googleOptions = section.Get<GoogleOAuthOptions>() ?? new GoogleOAuthOptions();
        if (!googleOptions.IsConfigured)
            return authentication;

        authentication.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
        {
            options.ClientId = googleOptions.ClientId;
            options.ClientSecret = googleOptions.ClientSecret;
            options.SignInScheme = ExternalCookieScheme;
            options.CallbackPath = "/signin-google";
            options.ClaimActions.MapJsonKey("verified_email", "verified_email");
            options.ClaimActions.MapJsonKey("email_verified", "email_verified");
        });

        return authentication;
    }
}
