namespace Fundoo.User.Application.Abstractions;

public interface IExternalAuthService
{
    Task<AuthResult> LoginWithExternalProviderAsync(
        string provider,
        string providerSubject,
        string email,
        bool emailVerified,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default);
}
