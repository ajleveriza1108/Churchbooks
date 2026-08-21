using System.Globalization;
using ChurchBooks.App.Models;
using FluentValidation;

namespace ChurchBooks.App.Validation;

public sealed class OfferingLineDraftValidator : AbstractValidator<OfferingLineDraft>
{
    public OfferingLineDraftValidator()
    {
        RuleFor(static draft => draft.GivingCategory).NotNull().WithMessage("Select a giving category.");
        RuleFor(static draft => draft.Fund).NotNull().WithMessage("Select a fund.");
        RuleFor(static draft => draft.AmountText).Must(BePositiveMoney).WithMessage("Enter an amount greater than zero with at most two decimal places.");
    }

    private static bool BePositiveMoney(string? text)
    {
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)) return false;
        return amount > 0m && decimal.Round(amount, 2, MidpointRounding.ToEven) == amount;
    }
}
