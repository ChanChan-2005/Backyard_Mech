using Backyard_Mech.Models;
using Backyard_Mech.Services;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Data;

public static class RootAdministratorSeed
{
    public static async Task EnsureAsync(ApplicationDbContext db, IConfiguration configuration)
    {
        // One-time separation of the legacy protected Owner role from the root account.
        // Later changes to Garage Owner grants are never overwritten.
        var owner = await db.AppRoles.SingleAsync(x => x.RoleKey == "Owner");
        const string marker = "Separate root administrator from Garage Owner";
        if (await db.AuditLogEntries.AnyAsync(x => x.Action == marker)) return;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var login = configuration["SeedAdminUser:Email"] ?? "admin123";
        var account = await db.Users.SingleOrDefaultAsync(x => x.Email == login);
        if (await db.Users.AnyAsync() && account is null)
            throw new InvalidOperationException("Configured root administrator account was not found.");
        var permissions = (await db.Permissions.Where(x => x.IsActive).ToListAsync())
            .Where(x => OwnerPermissionCatalog.IsSupportedPermission(x.ModuleName ?? "", x.ActionName ?? "")).ToList();
        var existing = await db.RolePermissions.Where(x => x.RoleId == "Owner").Select(x => x.PermissionId).ToListAsync();
        foreach (var permission in permissions.Where(x => !existing.Contains(x.PermissionId)))
            db.RolePermissions.Add(new RolePermission { RoleId = "Owner", PermissionId = permission.PermissionId });
        owner.IsSystemProtected = false;
        if (account is not null) { account.Role = "RootAdmin"; account.IsActive = true; }
        db.AuditLogEntries.Add(new AuditLogEntry {
            Action = marker, EntityName = "AppRole", EntityId = "RootAdmin", TimestampUtc = DateTime.UtcNow,
            Details = System.Text.Json.JsonSerializer.Serialize(new { RootAccountId = account?.Id, GarageOwnerProtected = false, GarageOwnerPermissions = permissions.Select(x => x.PermissionCode) })
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
