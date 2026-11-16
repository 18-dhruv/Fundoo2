namespace Fundoo.User.Application.Abstractions;

public sealed class AuthResult
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? AccessToken { get; init; }
    public Guid? UserId { get; init; }
    public bool RequiresTwoFactor { get; init; }
}
