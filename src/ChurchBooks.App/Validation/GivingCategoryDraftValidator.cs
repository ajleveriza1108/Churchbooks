using ChurchBooks.App.Models;
using FluentValidation;

namespace ChurchBooks.App.Validation;

public sealed class GivingCategoryDraftValidator : AbstractValidator<GivingCategoryDraft>
{
    public GivingCategoryDraftValidator()
    {
        RuleFor(static draft => draft.Code)
            .NotEmpty().WithMessage("Category code is required.")
            .MaximumLength(24).WithMessage("Category code must be 24 characters or fewer.")
            .Matches("^[A-Za-z0-9][A-Za-z0-9._-]*$")
            .WithMessage("Use letters, numbers, period, underscore, or hyphen in the category code.");

        RuleFor(static draft => draft.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(100).WithMessage("Category name must be 100 characters or fewer.");

        RuleFor(static draft => draft.GroupName)
            .MaximumLength(100).WithMessage("Group name must be 100 characters or fewer.");

        RuleFor(static draft => draft.Description)
            .MaximumLength(400).WithMessage("Description must be 400 characters or fewer.");
    }
}
