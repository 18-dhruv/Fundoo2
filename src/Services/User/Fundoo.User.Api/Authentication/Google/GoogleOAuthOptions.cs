namespace Fundoo.User.Api.Authentication.Google;

public sealed class GoogleOAuthOptions
{
    public const string SectionName = "Authentication:Google";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !ClientId.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase) &&
        !ClientSecret.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
}
