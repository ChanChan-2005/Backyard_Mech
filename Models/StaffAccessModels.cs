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
    [Range(1,int.MaxValue)] public int? EmployeeProfileId { get; set; }
    [Required,EmailAddress,StringLength(256)] public string Email { get; set; } = "";
    [Required,StringLength(120)] public string UserName { get; set; } = "";
    [Required,StringLength(128,MinimumLength=12)] public string Password { get; set; } = "";
    [Required,Compare(nameof(Password),ErrorMessage="Passwords do not match.")] [StringLength(128)] public string ConfirmPassword { get; set; } = "";
    [Required,StringLength(50)] public string RoleKey { get; set; } = "";
    public List<PermissionChoice> Permissions { get; set; } = new();
}
public sealed class UserAccessForm
{
    [Range(1,int.MaxValue)] public int UserId { get; set; }
    [Required] [StringLength(100)] public string Expected { get; set; } = "";
    public bool UpdateDetails { get; set; }
    [EmailAddress, StringLength(256)] public string? Email { get; set; }
    [StringLength(120)] public string? UserName { get; set; }
    [Required,StringLength(50)] public string RoleKey { get; set; } = "";
    public bool IsActive { get; set; }
    public List<PermissionChoice> Permissions { get; set; } = new();
}
public sealed class StaffPasswordResetForm
{
    [Range(1,int.MaxValue)] public int UserId { get; set; }
    [Required,StringLength(100)] public string Expected { get; set; } = "";
}
public sealed class AccountStatusForm
{
    [Range(1,int.MaxValue)] public int UserId { get; set; }
    [Required,StringLength(100)] public string Expected { get; set; } = "";
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
    public HashSet<string> Defaults { get; set; } = new();
    public Dictionary<string,string[]> RoleDefaults { get; set; } = new();
    public Dictionary<int,string> ExpectedByAccount { get; set; } = new();
    public AppUser? Selected { get; set; }
    public string Expected { get; set; } = "";
    public bool CanCreate { get; set; }
    public bool CanCreateGarageOwner { get; set; }
    public bool CanEdit { get; set; }
    public bool CanActivateAccounts { get; set; }
    public bool CanDeactivateAccounts { get; set; }
    public bool CanBlockAccounts { get; set; }
    public bool CanUnblockAccounts { get; set; }
    public bool CanResetPassword { get; set; }
    public string? ResetPasswordPreview { get; set; }
    public string? ResetPasswordValue { get; set; }
}
public sealed class AvailabilityExceptionForm
{
    public int Id { get; set; }
    [StringLength(100)] public string? Expected { get; set; }
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

public sealed class WeeklyAvailabilityDayForm
{
    public int DayOfWeek { get; set; }
    public string DayName { get; set; } = "";
    public bool IsAvailable { get; set; }
    public string StartTime { get; set; } = "08:00";
    public string EndTime { get; set; } = "17:00";
    public string BreakStart { get; set; } = "";
    public string BreakEnd { get; set; } = "";
}

public sealed class EmployeeAvailabilityPage
{
    public int EmployeeProfileId { get; set; }
    public string EmployeeName { get; set; } = "";
    public List<WeeklyAvailabilityDayForm> Days { get; set; } = new();
    public bool CanEdit { get; set; }
}

public sealed class AuditLogReportPage
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? NameOrEmail { get; set; }
    public string? Action { get; set; }
    public string? Entity { get; set; }
    public int? EmployeeProfileId { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public IReadOnlyList<AuditLogReportRow> Rows { get; set; } = Array.Empty<AuditLogReportRow>();
    public IReadOnlyList<string> Actions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Entities { get; set; } = Array.Empty<string>();
    public int TotalCount { get; set; }
}

public sealed record AuditLogReportRow(string Name, string Email, string Action, DateTime Timestamp, string Entity, string EntityId);
