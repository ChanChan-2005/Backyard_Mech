using System.ComponentModel.DataAnnotations;

namespace Backyard_Mech.Models;

public sealed class ProfileSettingsViewModel
{
    public string UserName { get; set; } = string.Empty;
    public string LoginEmail { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool HasEmployeeProfile { get; set; }
    public bool HasPhoto { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
}

public sealed class UpdateOwnProfileForm
{
    [Required, StringLength(120)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(120)] public string LastName { get; set; } = string.Empty;
    [Phone, StringLength(40)] public string? Phone { get; set; }
    [EmailAddress, StringLength(256)] public string? ContactEmail { get; set; }
}

public sealed class ChangeOwnPasswordForm
{
    [Required] public string CurrentPassword { get; set; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 12)] public string NewPassword { get; set; } = string.Empty;
    [Required, Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = string.Empty;
}
