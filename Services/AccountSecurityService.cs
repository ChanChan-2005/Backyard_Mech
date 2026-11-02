using System.Security.Cryptography;
using System.Text;
using Backyard_Mech.Data;
using Backyard_Mech.Models;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Services;

public sealed class AccountSecurityService(ApplicationDbContext db, PermissionService permissions)
{
    public static string CredentialStamp(AppUser user) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user.PasswordHash)));

    public async Task<bool> RequiresMfa(AppUser user)
    {
        var codes = await permissions.GetPermissionCodesAsync(user.Role, user.Id);
        return codes.Contains("*") || codes.Any(x => x is "useraccess.create" or "useraccess.edit" or "settings.edit" or "settings.create" or "settings.delete" or
            "employees.edit" or "employees.create" or "employees.delete" or "invoices.create" or "invoices.edit" or "invoices.delete" or
            "estimates.approve" or "inspections.approve" or "workorders.approve" or "records.delete");
    }

    public async Task<AppUser?> ValidateSession(HttpContext context)
    {
        var session = context.Session;
        if (!int.TryParse(session.GetString("UserId"), out var id)) return null;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        var state = await db.AccountSecurity.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == id);
        var expired = !long.TryParse(session.GetString("AuthenticatedAt"), out var ticks) || DateTime.UtcNow.Ticks - ticks > TimeSpan.FromHours(8).Ticks;
        if (user is null || !user.IsActive || user.Role != session.GetString("UserRole") ||
            CredentialStamp(user) != session.GetString("CredentialStamp") || expired ||
            state?.LockedUntilUtc > DateTime.UtcNow ||
            ((state?.AuthenticatorSecret != null || await RequiresMfa(user)) && session.GetString("MfaVerified") != "true") ||
            !await db.AppRoles.AnyAsync(x => x.RoleKey == user.Role && x.IsActive))
        {
            session.Clear();
            return null;
        }
        if(await db.EmployeeProfiles.AnyAsync(x=>x.UserId==id.ToString() && (!x.IsActive || x.IsArchived || x.IsDeleted)))
        {
            session.Clear(); return null;
        }
        return user;
    }

    public void Audit(HttpContext context, string action, AppUser? user)
    {
        db.AuditLogEntries.Add(new AuditLogEntry {
            UserId = user?.Id.ToString(), UserName = user?.UserName, EntityName = "Account", EntityId = user?.Id.ToString(),
            Action = action, TimestampUtc = DateTime.UtcNow,
            Details = System.Text.Json.JsonSerializer.Serialize(new { Ip = context.Connection.RemoteIpAddress?.ToString() })
        });
    }

    public async Task<AccountSecurity> State(int id)
    {
        var state = await db.AccountSecurity.FindAsync(id);
        if (state is null) { state = new AccountSecurity { UserId = id }; db.AccountSecurity.Add(state); }
        return state;
    }

    public void Failed(AccountSecurity state)
    {
        state.FailedAttempts++;
        if (state.FailedAttempts >= 5) { state.LockedUntilUtc = DateTime.UtcNow.AddMinutes(15); state.FailedAttempts = 0; }
    }
}
