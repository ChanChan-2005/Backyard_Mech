using Backyard_Mech.Models;
using Backyard_Mech.Services;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Data;

public static class StaffAccessSeed
{
    public static async Task EnsureAsync(ApplicationDbContext db)
    {
        foreach(var action in new[]{"View","Create","Edit"})
        {
            var code=PermissionService.BuildPermissionCode("User Access",action);
            if(!await db.Permissions.AnyAsync(x=>x.PermissionCode==code))
                db.Permissions.Add(new Permission {ModuleName="User Access",ActionName=action,PermissionCode=code,Description=$"{action} login accounts and individual access."});
        }
        await db.SaveChangesAsync();
        var definitions=new Dictionary<string,string[]> {
            ["Administrator"]=new[]{"Dashboard","Customers","Vehicles","Appointments","Service Queue","Estimates","Work Orders","Bays","Inventory","Suppliers","Services","Labour","Inspections","Tools","Invoices","Reports","Employees","Settings","User Access"},
            ["Mechanic/Technician"]=new[]{"Work Orders","Labour"},
            ["Supervisor/QC Inspector"]=new[]{"Inspections"},
            ["Accounting/Cashier"]=new[]{"Invoices","Reports"},
            ["Shop Manager"]=new[]{"Dashboard","Customers","Vehicles","Appointments","Service Queue","Estimates","Work Orders","Bays","Inventory","Suppliers","Services","Labour","Inspections","Tools","Invoices","Reports","Employees","Settings","User Access"},
            ["Service Advisor"]=new[]{"Dashboard","Customers","Vehicles","Appointments","Service Queue","Estimates","Work Orders"},
            ["Mechanic"]=new[]{"Work Orders","Labour"},
            ["Parts Clerk"]=new[]{"Inventory","Suppliers"},
            ["Tool Room Attendant"]=new[]{"Tools"},
            ["Quality Control Inspector"]=new[]{"Inspections"},
            ["Accounting Clerk"]=new[]{"Invoices","Reports"}
        };
        var permissions=await db.Permissions.Where(x=>x.IsActive).ToListAsync();
        foreach(var definition in definitions)
        {
            var role = await db.AppRoles.SingleOrDefaultAsync(x=>x.RoleKey==definition.Key);
            if (role is null)
            {
                role = new AppRole {RoleKey=definition.Key,RoleName=definition.Key,Description="Master plan role",DisplayOrder=100};
                db.AppRoles.Add(role);
            }
            // Repair built-in roles created without grants, but preserve saved empty permissions.
            if (!role.IsActive || await db.RolePermissions.AnyAsync(x=>x.RoleId==role.RoleKey) ||
                await db.AuditLogEntries.AnyAsync(x=>x.EntityName=="AppRole" && x.EntityId==role.RoleKey &&
                    (x.Action=="Update permissions" || x.Action=="Initialize role defaults"))) continue;
            var codes=definition.Key switch {
                "Mechanic" or "Mechanic/Technician"=>new[]{"workorders.view","labour.view","labour.create","labour.edit"},
                "Tool Room Attendant"=>new[]{"tools.view","tools.create","tools.edit"},
                "Quality Control Inspector" or "Supervisor/QC Inspector"=>new[]{"inspections.view","inspections.create","inspections.approve"},
                "Accounting Clerk" or "Accounting/Cashier"=>new[]{"invoices.view","invoices.create","invoices.edit","reports.view"},
                _=>Array.Empty<string>()
            };
            foreach(var permission in permissions.Where(x=>definition.Value.Contains(x.ModuleName) &&
                OwnerPermissionCatalog.IsSupportedPermission(x.ModuleName??"",x.ActionName??"") && x.ActionName!="Delete" &&
                (codes.Length==0 || codes.Contains(x.PermissionCode))))
                db.RolePermissions.Add(new RolePermission {RoleId=role.RoleKey,PermissionId=permission.PermissionId});
            db.AuditLogEntries.Add(new AuditLogEntry {
                UserName="System",EntityName="AppRole",EntityId=role.RoleKey,Action="Initialize role defaults",
                TimestampUtc=DateTime.UtcNow,Details="Initialized an unconfigured built-in role. Saved role permissions and user overrides were preserved."
            });
        }
        await db.SaveChangesAsync();
        await EnsureDetailedActionsAsync(db);
    }

    private static async Task EnsureDetailedActionsAsync(ApplicationDbContext db)
    {
        var knownCodes = (await db.Permissions.AsNoTracking()
            .Where(permission => permission.PermissionCode != null)
            .Select(permission => permission.PermissionCode!)
            .ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var additions = OwnerPermissionCatalog.DetailedActions
            .Where(action => !knownCodes.Contains(OwnerPermissionCatalog.ActionPermissionCode(action.Name)))
            .Select(action => new Permission
            {
                ModuleName = action.Module,
                ActionName = action.Name,
                PermissionCode = OwnerPermissionCatalog.ActionPermissionCode(action.Name),
                Description = $"{OwnerPermissionCatalog.ActionLabel(action.Name)} ({action.Action.ToLowerInvariant()})"
            }).ToList();
        if (additions.Count == 0) return;

        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Permissions.AddRange(additions);
        await db.SaveChangesAsync();
        var existingGrants = await (from grant in db.RolePermissions.AsNoTracking()
            join permission in db.Permissions.AsNoTracking() on grant.PermissionId equals permission.PermissionId
            select new { grant.RoleId, permission.PermissionCode }).ToListAsync();
        var grantedByRole = existingGrants.GroupBy(item => item.RoleId)
            .ToDictionary(group => group.Key,
                group => group.Select(item => item.PermissionCode).Where(code => code != null)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase));
        foreach (var role in await db.AppRoles.AsNoTracking().Where(item => item.IsActive && !item.IsSystemProtected).ToListAsync())
        {
            if (!grantedByRole.TryGetValue(role.RoleKey, out var grants)) continue;
            foreach (var permission in additions)
            {
                // Section visibility is explicitly assigned by the administrator, never inferred from page access.
                if (DashboardPermissions.Sections.Contains(permission.ActionName)) continue;
                if (permission.ActionName == "ManageStaffRolePermissions") continue;
                var requirement = OwnerPermissionCatalog.Resolve(permission.ActionName!);
                if (requirement is null || !grants.Contains(PermissionService.BuildPermissionCode(requirement.Value.Module, "View"))
                    || !grants.Contains(PermissionService.BuildPermissionCode(requirement.Value.Module, requirement.Value.Action))) continue;
                db.RolePermissions.Add(new RolePermission { RoleId = role.RoleKey, PermissionId = permission.PermissionId });
            }
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
