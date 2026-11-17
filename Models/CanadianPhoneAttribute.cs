using System.ComponentModel.DataAnnotations;

namespace Backyard_Mech.Models;

// Shared by supplier, customer, employee and self-service profile forms.
public sealed class CanadianPhoneAttribute : RegularExpressionAttribute
{
    public CanadianPhoneAttribute()
        : base(@"(?:\+1)?[2-9][0-9]{2}[2-9][0-9]{6}|(?:\+1 )?\([2-9][0-9]{2}\) [2-9][0-9]{2}-[0-9]{4}")
    {
        ErrorMessage = "Use a Canadian number such as 4165550123, +14165550123, or (416) 555-0123.";
    }
}
