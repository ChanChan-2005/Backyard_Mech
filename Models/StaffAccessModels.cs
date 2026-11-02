using System.ComponentModel.DataAnnotations;

namespace Backyard_Mech.Models;

public sealed class UserPermissionOverride
{
    public int UserPermissionOverrideId { get; set; }
    public int UserId { get; set; }
    public int PermissionId { get; set; }
    public bool IsAllowed { get; set; }
}
public sealed class StaffAccountForm
{
    [Range(1,int.MaxValue)] public int EmployeeProfileId { get; set; }
    [Required,EmailAddress,StringLength(256)] public string Email { get; set; } = "";
    [Required,StringLength(120)] public string UserName { get; set; } = "";
    [Required,StringLength(128,MinimumLength=12)] public string Password { get; set; } = "";
    [Required,StringLength(50)] public string RoleKey { get; set; } = "";
}
public sealed class UserAccessForm
{
    [Range(1,int.MaxValue)] public int UserId { get; set; }
    [Required] public string Expected { get; set; } = "";
    [Required,StringLength(50)] public string RoleKey { get; set; } = "";
    public bool IsActive { get; set; }
    public List<PermissionChoice> Permissions { get; set; } = new();
    [Required,StringLength(500)] public string Reason { get; set; } = "";
}
public sealed class PermissionChoice
{
    public int PermissionId { get; set; }
    [Required,RegularExpression("Inherit|Allow|Deny")] public string Mode { get; set; } = "Inherit";
}
public sealed class StaffAccountsPage
{
    public List<EmployeeProfile> Profiles { get; set; } = new();
    public List<AppUser> Accounts { get; set; } = new();
    public List<AppRole> Roles { get; set; } = new();
    public List<Permission> Permissions { get; set; } = new();
    public List<UserPermissionOverride> Overrides { get; set; } = new();
    public HashSet<string> Defaults { get; set; } = new();
    public Dictionary<string,string[]> RoleDefaults { get; set; } = new();
    public AppUser? Selected { get; set; }
    public string Expected { get; set; } = "";
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
}
public sealed class AvailabilityExceptionForm
{
    public int Id { get; set; }
    public string? Expected { get; set; }
    [Range(1,int.MaxValue)] public int EmployeeProfileId { get; set; }
    [Required] public DateTime? Start { get; set; }
    [Required] public DateTime? End { get; set; }
    public bool IsAvailable { get; set; }
    [Required,StringLength(500)] public string Reason { get; set; } = "";
}
public sealed record StaffExceptionRow(EmployeeAvailabilityException Record, string Name, DateTime Start, DateTime End, string Expected);
public sealed class StaffAvailabilityPage
{
    public List<EmployeeProfile> Profiles { get; set; } = new();
    public List<StaffExceptionRow> Rows { get; set; } = new();
    public bool CanEdit { get; set; }
    public string TimeZone { get; set; } = "";
}
