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
        await EnsureSidebarPermissionsAsync(db);
        var definitions=new Dictionary<string,string[]> {
            ["Administrator"]=new[]{"Dashboard","Customers","Vehicles","Appointments","Service Queue","Estimates","Work Orders","Bays","Parts Management","Parts POS","Suppliers","Services","Labour","Inspections","Tools","Invoices","Reports","Employees","Settings","User Access"},
            ["Mechanic/Technician"]=new[]{"Work Orders","Labour"},
            ["Supervisor/QC Inspector"]=new[]{"Inspections"},
            ["Accounting/Cashier"]=new[]{"Invoices","Reports"},
            ["Service Advisor"]=new[]{"Dashboard","Customers","Vehicles","Appointments","Service Queue","Estimates","Work Orders"},
            ["Parts Clerk"]=new[]{"Parts Management","Parts POS","Suppliers"},
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
                "Mechanic/Technician"=>new[]{"workorders.view","labour.view","labour.create","labour.edit"},
                "Supervisor/QC Inspector"=>new[]{"inspections.view","inspections.create","inspections.approve"},
                "Accounting/Cashier"=>new[]{"invoices.view","invoices.create","invoices.edit","reports.view"},
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
        await EnsureSubTabPermissionsAsync(db);
        var assignedRoleKeys = (await db.Users.AsNoTracking().Select(user => user.Role).Distinct().ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var obsoleteRoles = await db.AppRoles
            .Where(role => role.IsActive && role.RoleKey != "RootAdmin")
            .ToListAsync();
        foreach (var role in obsoleteRoles.Where(role =>
            !StandardRoleProfiles.IsBusinessRole(role.RoleKey) && !assignedRoleKeys.Contains(role.RoleKey)))
            role.IsActive = false;
        await db.SaveChangesAsync();
    }

    private static async Task EnsureSubTabPermissionsAsync(ApplicationDbContext db)
    {
        var knownCodes = (await db.Permissions.AsNoTracking()
            .Where(permission => permission.PermissionCode != null)
            .Select(permission => permission.PermissionCode!)
            .ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var additions = SubTabPermissionCatalog.Tabs
            .Where(tab => !knownCodes.Contains(tab.Code))
            .Select(tab => new { Tab = tab, Permission = new Permission
            {
                ModuleName = tab.Module,
                ActionName = tab.ActionName,
                PermissionCode = tab.Code,
                Description = $"View {tab.Label}."
            }}).ToList();
        if (additions.Count == 0) return;

        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Permissions.AddRange(additions.Select(item => item.Permission));
        await db.SaveChangesAsync();
        var grants = await (from grant in db.RolePermissions.AsNoTracking()
            join permission in db.Permissions.AsNoTracking() on grant.PermissionId equals permission.PermissionId
            where permission.PermissionCode != null
            select new { grant.RoleId, permission.PermissionCode }).ToListAsync();
        var codesByRole = grants.GroupBy(grant => grant.RoleId)
            .ToDictionary(group => group.Key,
                group => group.Select(item => item.PermissionCode!).ToHashSet(StringComparer.OrdinalIgnoreCase));
        foreach (var (roleKey, codes) in codesByRole)
        foreach (var item in additions.Where(item => OwnerPermissionCatalog.CanAccessAction(codes, item.Tab.Page)))
            db.RolePermissions.Add(new RolePermission { RoleId = roleKey, PermissionId = item.Permission.PermissionId });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static async Task EnsureSidebarPermissionsAsync(ApplicationDbContext db)
    {
        var replacements = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["inventory.view"] = new[] { "partsmanagement.view", "partspos.view" },
            ["inventory.create"] = new[] { "partsmanagement.create", "partspos.create" },
            ["inventory.edit"] = new[] { "partsmanagement.edit" },
            ["inventory.delete"] = new[] { "partsmanagement.delete" },
            ["records.view"] = new[] { "archive.view", "recordsdisposition.view" },
            ["records.edit"] = new[] { "archive.edit", "recordsdisposition.edit" },
            ["records.delete"] = new[] { "archive.delete", "recordsdisposition.delete" }
        };
        var moduleActions = new (string Module, string[] Actions)[]
        {
            ("Parts Management", new[] { "View", "Create", "Edit", "Delete" }),
            ("Parts POS", new[] { "View", "Create" }),
            ("Archive", new[] { "View", "Edit", "Delete" }),
            ("Records Disposition", new[] { "View", "Edit", "Delete" })
        };
        var permissions = await db.Permissions.ToListAsync();
        var byCode = permissions.Where(permission => permission.PermissionCode is not null)
            .ToDictionary(permission => permission.PermissionCode!, StringComparer.OrdinalIgnoreCase);
        foreach (var (module, actions) in moduleActions)
        foreach (var action in actions)
        {
            var code = PermissionService.BuildPermissionCode(module, action);
            if (byCode.ContainsKey(code)) continue;
            var permission = new Permission
            {
                ModuleName = module, ActionName = action, PermissionCode = code,
                Description = $"{action} {module.ToLowerInvariant()}."
            };
            db.Permissions.Add(permission);
            byCode.Add(code, permission);
        }
        await db.SaveChangesAsync();

        var grants = await db.RolePermissions.ToListAsync();
        var existing = grants.Where(grant => grant.PermissionId.HasValue)
            .Select(grant => (grant.RoleId, grant.PermissionId!.Value)).ToHashSet();
        foreach (var (oldCode, newCodes) in replacements)
        {
            if (!byCode.TryGetValue(oldCode, out var oldPermission)) continue;
            foreach (var grant in grants.Where(grant => grant.PermissionId == oldPermission.PermissionId))
            {
                foreach (var newCode in newCodes)
                {
                    var key = (grant.RoleId, byCode[newCode].PermissionId);
                    if (existing.Add(key))
                        db.RolePermissions.Add(new RolePermission { RoleId = grant.RoleId, PermissionId = key.PermissionId });
                }
                db.RolePermissions.Remove(grant);
            }
            oldPermission.IsActive = false;
        }
        foreach (var permission in permissions.Where(permission => permission.PermissionCode?.StartsWith("owneraction.", StringComparison.OrdinalIgnoreCase) == true))
        {
            var actionName = permission.PermissionCode!["owneraction.".Length..];
            var match = OwnerPermissionCatalog.DetailedActions.FirstOrDefault(action =>
                action.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase));
            if (match.Name is not null) permission.ModuleName = match.Module;
        }
        foreach (var permission in permissions.Where(permission =>
            permission.ModuleName is "Inventory" or "Records"))
            permission.IsActive = false;
        await db.SaveChangesAsync();
    }

    private static async Task EnsureDetailedActionsAsync(ApplicationDbContext db)
    {
        var retiredTestPermission = await db.Permissions
            .Where(permission => (permission.PermissionCode == "owneraction.sendtestemail" ||
                permission.PermissionCode == "owneraction.savesmtpsender") && permission.IsActive)
            .ToListAsync();
        foreach (var permission in retiredTestPermission) permission.IsActive = false;
        if (retiredTestPermission.Count > 0) await db.SaveChangesAsync();

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
