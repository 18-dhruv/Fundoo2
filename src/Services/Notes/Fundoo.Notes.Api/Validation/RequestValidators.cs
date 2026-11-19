using FluentValidation;
using Fundoo.Notes.Api.Contracts.Requests;

namespace Fundoo.Notes.Api.Validation;

public sealed class CreateNoteRequestValidator : AbstractValidator<CreateNoteRequest>
{
    public CreateNoteRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Color).MaximumLength(50).When(x => x.Color is not null);
    }
}

public sealed class UpdateNoteRequestValidator : AbstractValidator<UpdateNoteRequest>
{
    public UpdateNoteRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Color).MaximumLength(50).When(x => x.Color is not null);
    }
}

public sealed class CreateLabelRequestValidator : AbstractValidator<CreateLabelRequest>
{
    public CreateLabelRequestValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
}

public sealed class UpdateLabelRequestValidator : AbstractValidator<UpdateLabelRequest>
{
    public UpdateLabelRequestValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
}
