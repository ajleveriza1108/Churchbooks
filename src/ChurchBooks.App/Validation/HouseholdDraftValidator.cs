using ChurchBooks.App.Models;
using FluentValidation;

namespace ChurchBooks.App.Validation;

public sealed class HouseholdDraftValidator : AbstractValidator<HouseholdDraft>
{
    public HouseholdDraftValidator()
    {
        RuleFor(static draft => draft.Name)
            .NotEmpty().WithMessage("Household name is required.")
            .MaximumLength(120).WithMessage("Household name must be 120 characters or fewer.");

        RuleFor(static draft => draft.StatementName)
            .MaximumLength(160).WithMessage("Statement name must be 160 characters or fewer.");
    }
}
