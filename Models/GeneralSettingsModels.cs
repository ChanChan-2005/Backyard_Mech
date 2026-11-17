using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Backyard_Mech.Models;

public sealed class GarageSettings
{
    public int Id { get; set; } = 1;
    public string ValuesJson { get; set; } = "{}";
    public byte[]? Logo { get; set; }
    public string? LogoContentType { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
}

public class GeneralSettingsValues : IValidatableObject
{
    [Required, StringLength(160)] public string GarageName { get; set; } = "Bakyard Mech";
    [Required, StringLength(250)] public string Address { get; set; } = "";
    [Required, CanadianPhone, StringLength(30)] public string Phone { get; set; } = "";
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Required] public string WeekdayOpen { get; set; } = "08:00";
    [Required] public string WeekdayClose { get; set; } = "18:00";
    [Required] public string SaturdayOpen { get; set; } = "09:00";
    [Required] public string SaturdayClose { get; set; } = "14:00";
    public bool SundayEnabled { get; set; }
    public string SundayOpen { get; set; } = "09:00";
    public string SundayClose { get; set; } = "14:00";
    public bool EmailAppointmentReminders { get; set; }
    public bool LowStockAlerts { get; set; }
    public bool OverdueInvoiceReminders { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        foreach (var period in new[] {
            ("Monday - Friday", WeekdayOpen, WeekdayClose, nameof(WeekdayOpen)),
            ("Saturday", SaturdayOpen, SaturdayClose, nameof(SaturdayOpen)),
            ("Sunday", SundayOpen, SundayClose, nameof(SundayOpen)) })
        {
            if (period.Item1 == "Sunday" && !SundayEnabled) continue;
            if (!TimeOnly.TryParseExact(period.Item2, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
                !TimeOnly.TryParseExact(period.Item3, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end) || end <= start)
                yield return new ValidationResult($"{period.Item1}: closing time must be after opening time.", new[] { period.Item4 });
        }
    }
}

public sealed class GeneralSettingsForm : GeneralSettingsValues
{
    [Required, JsonIgnore] public string Expected { get; set; } = "";
    [JsonIgnore] public IFormFile? LogoUpload { get; set; }
    [JsonIgnore] public bool HasLogo { get; set; }
    [JsonIgnore] public bool CanEdit { get; set; }
}
