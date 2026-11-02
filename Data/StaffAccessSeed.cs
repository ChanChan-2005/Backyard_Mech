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
            // Never overwrite permissions already customized on an existing role.
            if(await db.AppRoles.AnyAsync(x=>x.RoleName==definition.Key || x.RoleKey==definition.Key)) continue;
            db.AppRoles.Add(new AppRole {RoleKey=definition.Key,RoleName=definition.Key,Description="Master plan role",DisplayOrder=100});
            var codes=definition.Key switch {
                "Mechanic"=>new[]{"workorders.view","labour.view","labour.create","labour.edit"},
                "Tool Room Attendant"=>new[]{"tools.view","tools.create","tools.edit"},
                "Quality Control Inspector"=>new[]{"inspections.view","inspections.create","inspections.approve"},
                "Accounting Clerk"=>new[]{"invoices.view","invoices.create","invoices.edit","reports.view"},
                _=>Array.Empty<string>()
            };
            foreach(var permission in permissions.Where(x=>definition.Value.Contains(x.ModuleName) &&
                OwnerPermissionCatalog.IsSupportedPermission(x.ModuleName??"",x.ActionName??"") && x.ActionName!="Delete" &&
                (codes.Length==0 || codes.Contains(x.PermissionCode))))
                db.RolePermissions.Add(new RolePermission {RoleId=definition.Key,PermissionId=permission.PermissionId});
        }
        await db.SaveChangesAsync();
    }
}
