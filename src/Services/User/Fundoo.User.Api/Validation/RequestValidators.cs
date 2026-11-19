using FluentValidation;
using Fundoo.User.Api.Contracts;

namespace Fundoo.User.Api.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().Length(2, 50)
            .Matches(@"^[A-Za-z]+(?:[ '-][A-Za-z]+)*$");
        RuleFor(x => x.LastName).NotEmpty().Length(2, 50)
            .Matches(@"^[A-Za-z]+(?:[ '-][A-Za-z]+)*$");
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches(@"^(?=.*[A-Za-z])(?=.*\d)(?=.*[^A-Za-z\d\s])\S{8,}$")
            .WithMessage("Password must contain at least one letter, one digit, and one special character, with no spaces.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .Matches(@"^(?=.*[A-Za-z])(?=.*\d)(?=.*[^A-Za-z\d\s])\S{8,}$")
            .WithMessage("Password must contain at least one letter, one digit, and one special character, with no spaces.");
    }
}

public sealed class Verify2FARequestValidator : AbstractValidator<Verify2FARequest>
{
    public Verify2FARequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Code).NotEmpty().Matches(@"^\d{6}$")
            .WithMessage("Code must be exactly six digits.");
    }
}
