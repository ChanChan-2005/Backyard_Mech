using System.ComponentModel.DataAnnotations;

namespace Backyard_Mech.Models;

public sealed class AccountSecurity
{
    [Key] public int UserId { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
}
