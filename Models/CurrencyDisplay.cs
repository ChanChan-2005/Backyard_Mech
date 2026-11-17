using System.Globalization;

namespace Backyard_Mech.Models;

public static class CurrencyDisplay
{
    private static readonly CultureInfo CanadianEnglish = CultureInfo.GetCultureInfo("en-CA");

    public static string Cad(decimal? amount)
    {
        var value = amount ?? 0m;
        return value < 0m
            ? $"-CA${Math.Abs(value).ToString("N2", CanadianEnglish)}"
            : $"CA${value.ToString("N2", CanadianEnglish)}";
    }
}
