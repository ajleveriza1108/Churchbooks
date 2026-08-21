using ChurchBooks.App.Models;
using FluentValidation;

namespace ChurchBooks.App.Validation;

public sealed class FundDraftValidator : AbstractValidator<FundDraft>
{
    public FundDraftValidator()
    {
        RuleFor(static draft => draft.Code)
            .NotEmpty().WithMessage("Fund code is required.")
            .MaximumLength(20).WithMessage("Fund code must be 20 characters or fewer.")
            .Matches("^[A-Za-z0-9][A-Za-z0-9._-]*$")
            .WithMessage("Use letters, numbers, period, underscore, or hyphen in the fund code.");

        RuleFor(static draft => draft.Name)
            .NotEmpty().WithMessage("Fund name is required.")
            .MinimumLength(2).WithMessage("Fund name must be at least 2 characters.")
            .MaximumLength(80).WithMessage("Fund name must be 80 characters or fewer.");

        RuleFor(static draft => draft.Purpose)
            .MaximumLength(400).WithMessage("Purpose must be 400 characters or fewer.");
    }
}
