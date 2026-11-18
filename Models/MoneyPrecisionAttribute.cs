using System.ComponentModel.DataAnnotations;

namespace Backyard_Mech.Models;

[AttributeUsage(AttributeTargets.Property)]
public sealed class MoneyPrecisionAttribute : ValidationAttribute
{
    public MoneyPrecisionAttribute()
    {
        ErrorMessage = "Enter a money amount with no more than two decimal places.";
    }

    public override bool IsValid(object? value) =>
        value is null || value is decimal amount && amount == decimal.Round(amount, 2);
}
