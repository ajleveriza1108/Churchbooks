using ChurchBooks.App.Models;
using FluentValidation;

namespace ChurchBooks.App.Validation;

public sealed class OfferingBatchDraftValidator : AbstractValidator<OfferingBatchDraft>
{
    public OfferingBatchDraftValidator()
    {
        RuleFor(static draft => draft.ServiceDate).NotNull().WithMessage("Service date is required.");
        RuleFor(static draft => draft.Name).NotEmpty().MaximumLength(120).WithMessage("Batch/service name is required and must be 120 characters or fewer.");
        RuleFor(static draft => draft.Reference).MaximumLength(80);
    }
}
