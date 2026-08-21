using ChurchBooks.App.Models;
using FluentValidation;

namespace ChurchBooks.App.Validation;

public sealed class PersonDraftValidator : AbstractValidator<PersonDraft>
{
    public PersonDraftValidator()
    {
        RuleFor(static draft => draft.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(80).WithMessage("First name must be 80 characters or fewer.");

        RuleFor(static draft => draft.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(80).WithMessage("Last name must be 80 characters or fewer.");

        RuleFor(static draft => draft.PreferredName)
            .MaximumLength(80).WithMessage("Preferred name must be 80 characters or fewer.");

        RuleFor(static draft => draft.Email)
            .MaximumLength(160).WithMessage("Email must be 160 characters or fewer.")
            .EmailAddress().When(static draft => !string.IsNullOrWhiteSpace(draft.Email))
            .WithMessage("Enter a valid email address.");

        RuleFor(static draft => draft.Phone)
            .MaximumLength(40).WithMessage("Phone must be 40 characters or fewer.");

        RuleFor(static draft => draft.MemberNumber)
            .MaximumLength(40).WithMessage("Member number must be 40 characters or fewer.");

        RuleFor(static draft => draft)
            .Must(static draft => draft.IsMember || draft.IsDonor)
            .WithMessage("Choose Member, Donor, or both.");
    }
}
