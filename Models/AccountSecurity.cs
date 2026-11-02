using System.ComponentModel.DataAnnotations;

namespace Backyard_Mech.Models;

public sealed class AccountSecurity
{
    [Key] public int UserId { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    [MaxLength(2048)] public string? AuthenticatorSecret { get; set; }
    public long LastOtpStep { get; set; } = -1;
    public string? RecoveryHashes { get; set; }
}

public sealed class MfaForm
{
    [Required, StringLength(64)] public string Code { get; set; } = "";
    public string? SetupKey { get; set; }
    public string? RecoveryCode { get; set; }
}
