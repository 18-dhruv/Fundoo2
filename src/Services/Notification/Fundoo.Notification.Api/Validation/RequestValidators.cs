using FluentValidation;
using Fundoo.Notification.Application.DTO;

namespace Fundoo.Notification.Api.Validation;

public sealed class PasswordResetNotificationRequestValidator : AbstractValidator<PasswordResetNotificationRequest>
{
    public PasswordResetNotificationRequestValidator()
    {
        RuleFor(x => x.RecipientEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ResetToken).NotEmpty();
    }
}

public sealed class TwoFactorOtpNotificationRequestValidator : AbstractValidator<TwoFactorOtpNotificationRequest>
{
    public TwoFactorOtpNotificationRequestValidator()
    {
        RuleFor(x => x.RecipientEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OtpCode).Matches(@"^\d{6}$");
        RuleFor(x => x.ExpiryMinutes).InclusiveBetween(1, 60);
    }
}

public sealed class WelcomeNotificationRequestValidator : AbstractValidator<WelcomeNotificationRequest>
{
    public WelcomeNotificationRequestValidator()
    {
        RuleFor(x => x.RecipientEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(100);
    }
}

public sealed class NoteActivityNotificationRequestValidator : AbstractValidator<NoteActivityNotificationRequest>
{
    public NoteActivityNotificationRequestValidator()
    {
        RuleFor(x => x.RecipientEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.NoteId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EventType).Must(value => value is "Created" or "Updated")
            .WithMessage("Event type must be Created or Updated.");
    }
}
