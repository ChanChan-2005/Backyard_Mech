using Backyard_Mech.Models;
using Backyard_Mech.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    private async Task<List<Permission>> AssignablePermissions() => (await _dbContext.Permissions.AsNoTracking()
        .Where(x=>x.IsActive).OrderBy(x=>x.ModuleName).ThenBy(x=>x.ActionName).ToListAsync())
        .Where(x=>OwnerPermissionCatalog.IsSupportedPermission(x.ModuleName??"",x.ActionName??"")).ToList();

    private static string AccessStamp(AppUser user, IEnumerable<UserPermissionOverride> overrides) => MasterStamp(new {
        user.Id,user.Role,user.IsActive,user.Email,user.UserName,
        Credentials=AccountSecurityService.CredentialStamp(user),
        Permissions=overrides.OrderBy(x=>x.PermissionId).Select(x=>new{x.PermissionId,x.IsAllowed}).ToArray()
    });

    public async Task<IActionResult> StaffAccounts(int? userId)
    {
        var role=HttpContext.Session.GetString("UserRole");
        var model=new StaffAccountsPage {
            Profiles=await _dbContext.EmployeeProfiles.AsNoTracking().Where(x=>!x.IsArchived && !x.IsDeleted).OrderBy(x=>x.FirstName).ToListAsync(),
            Accounts=await _dbContext.Users.AsNoTracking().OrderBy(x=>x.UserName).ToListAsync(),
            Roles=await _dbContext.AppRoles.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.DisplayOrder).ThenBy(x=>x.RoleName).ToListAsync(),
            Permissions=await AssignablePermissions(),
            CanCreate=await _permissionService.HasPermissionAsync(role,"User Access","Create"),
            CanEdit=await _permissionService.HasPermissionAsync(role,"User Access","Edit")
        };
        model.Selected=model.Accounts.SingleOrDefault(x=>x.Id==userId);
        foreach(var availableRole in model.Roles)
            model.RoleDefaults[availableRole.RoleKey]=(await _permissionService.GetRolePermissionCodesAsync(availableRole.RoleKey)).ToArray();
        if(model.Selected is not null)
        {
            model.Overrides=await _dbContext.UserPermissionOverrides.AsNoTracking().Where(x=>x.UserId==model.Selected.Id).ToListAsync();
            model.Defaults=await _permissionService.GetRolePermissionCodesAsync(model.Selected.Role);
            model.Expected=AccessStamp(model.Selected,model.Overrides);
            if(model.Selected.Id.ToString()==HttpContext.Session.GetString("UserId") || model.Defaults.Contains("*")) model.CanEdit=false;
        }
        return View(model);
    }

    private async Task ValidateDelegation(AppRole role, HashSet<string> grants)
    {
        var actor=await _permissionService.GetPermissionCodesAsync(HttpContext.Session.GetString("UserRole"));
        Require(!role.IsSystemProtected || actor.Contains("*"),"Only a protected owner may assign the owner role.");
        Require(actor.Contains("*") || (!grants.Contains("*") && grants.All(actor.Contains)),"You cannot assign access beyond your own permissions.");
    }

    [HttpPost,ValidateAntiForgeryToken]
    public Task<IActionResult> CreateStaffAccount(StaffAccountForm form) => SaveWorkflow(async () => {
        Require(form.Password.Length>=12,"Use a password with at least 12 characters.");
        Require(form.Password==form.ConfirmPassword,"Passwords do not match.");
        var profile=await _dbContext.EmployeeProfiles.FindAsync(form.EmployeeProfileId);
        Require(profile is not null && profile.IsActive && !profile.IsArchived && !profile.IsDeleted,"Choose an active staff profile.");
        Require(string.IsNullOrWhiteSpace(profile.UserId),"This profile already has an account link.");
        var email=form.Email.Trim(); var name=form.UserName.Trim();
        Require(!await _dbContext.Users.AnyAsync(x=>x.Email==email || x.UserName==name),"Login email or username is already in use.");
        Require(form.Password.Any(char.IsUpper) && form.Password.Any(char.IsLower) && form.Password.Any(char.IsDigit) && form.Password.Any(x=>!char.IsLetterOrDigit(x)),
            "Use at least 12 characters with uppercase, lowercase, a number and a symbol.");
        var role=await _dbContext.AppRoles.SingleOrDefaultAsync(x=>x.RoleKey==form.RoleKey && x.IsActive);
        Require(role is not null,"Choose an active role.");
        Require(!role.IsSystemProtected,"Choose a staff role.");
        var available=await AssignablePermissions();
        Require(form.Permissions.Count==available.Count && form.Permissions.Select(x=>x.PermissionId).Distinct().Count()==available.Count &&
            form.Permissions.All(x=>available.Any(p=>p.PermissionId==x.PermissionId)),"Permission list changed. Refresh and try again.");
        var effective=new HashSet<string>(await _permissionService.GetRolePermissionCodesAsync(role.RoleKey),StringComparer.OrdinalIgnoreCase);
        foreach(var choice in form.Permissions)
        {
            var code=available.Single(x=>x.PermissionId==choice.PermissionId).PermissionCode!;
            if(choice.Mode=="Allow") effective.Add(code); else if(choice.Mode=="Deny") effective.Remove(code);
        }
        await ValidateDelegation(role,effective);
        var user=new AppUser {Email=email,UserName=name,Role=role.RoleKey,PasswordHash=PasswordHasher.Hash(form.Password),IsActive=true};
        _dbContext.Users.Add(user); await _dbContext.SaveChangesAsync();
        profile.UserId=user.Id.ToString();
        foreach(var choice in form.Permissions.Where(x=>x.Mode!="Inherit"))
            _dbContext.UserPermissionOverrides.Add(new UserPermissionOverride {UserId=user.Id,PermissionId=choice.PermissionId,IsAllowed=choice.Mode=="Allow"});
        WorkflowAudit("Account",user.Id,"Create staff account",new{profile.EmployeeProfileId,user.UserName,user.Email,user.Role,form.Permissions});
        return RedirectToAction(nameof(StaffAccounts));
    },nameof(StaffAccounts));

    [HttpPost,ValidateAntiForgeryToken]
    public Task<IActionResult> SaveUserAccess(UserAccessForm form) => SaveWorkflow(async () => {
        Require(form.UserId.ToString()!=HttpContext.Session.GetString("UserId"),"You cannot change your own account access.");
        var user=await _dbContext.Users.FindAsync(form.UserId); Require(user is not null,"Account not found.");
        var previousRole=await _dbContext.AppRoles.SingleOrDefaultAsync(x=>x.RoleKey==user.Role);
        Require(previousRole?.IsSystemProtected!=true,"Protected owner access cannot be overridden here.");
        var old=await _dbContext.UserPermissionOverrides.Where(x=>x.UserId==user.Id).ToListAsync();
        Require(AccessStamp(user,old)==form.Expected,"Account access changed. Refresh before saving.");
        var role=await _dbContext.AppRoles.SingleOrDefaultAsync(x=>x.RoleKey==form.RoleKey && x.IsActive);
        Require(role is not null,"Choose an active role.");
        Require(!role.IsSystemProtected,"Use a non-protected staff role for individual permissions.");
        var available=await AssignablePermissions();
        Require(form.Permissions.Count==available.Count && form.Permissions.Select(x=>x.PermissionId).Distinct().Count()==available.Count &&
            form.Permissions.All(x=>available.Any(p=>p.PermissionId==x.PermissionId)),"Permission list changed. Refresh and try again.");
        var effective=new HashSet<string>(await _permissionService.GetRolePermissionCodesAsync(role.RoleKey),StringComparer.OrdinalIgnoreCase);
        foreach(var choice in form.Permissions)
        {
            var code=available.Single(x=>x.PermissionId==choice.PermissionId).PermissionCode!;
            if(choice.Mode=="Allow") effective.Add(code); else if(choice.Mode=="Deny") effective.Remove(code);
        }
        await ValidateDelegation(role,effective);
        WorkflowAudit("Account",user.Id,"Before access change",new {user.Role,user.IsActive,Overrides=old.Select(x=>new{x.PermissionId,x.IsAllowed}).ToArray()});
        user.Role=role.RoleKey;user.IsActive=form.IsActive;
        _dbContext.UserPermissionOverrides.RemoveRange(old);
        await _dbContext.SaveChangesAsync();
        foreach(var choice in form.Permissions.Where(x=>x.Mode!="Inherit"))
            _dbContext.UserPermissionOverrides.Add(new UserPermissionOverride {UserId=user.Id,PermissionId=choice.PermissionId,IsAllowed=choice.Mode=="Allow"});
        WorkflowAudit("Account",user.Id,"Change access",new{user.Role,user.IsActive,form.Permissions,form.Reason});
        return RedirectToAction(nameof(StaffAccounts),new{userId=user.Id});
    },nameof(StaffAccounts),new{userId=form.UserId});

    private TimeZoneInfo GarageZone() => TimeZoneInfo.FindSystemTimeZoneById(HttpContext.RequestServices.GetRequiredService<IConfiguration>()["GarageTimeZone"]??"Asia/Manila");

    public async Task<IActionResult> StaffAvailability()
    {
        var zone=GarageZone();
        var profiles=await _dbContext.EmployeeProfiles.AsNoTracking().ToListAsync();
        var rows=await _dbContext.EmployeeAvailabilityExceptions.AsNoTracking().OrderByDescending(x=>x.StartUtc).ToListAsync();
        return View(new StaffAvailabilityPage {TimeZone=zone.Id,Profiles=profiles.Where(x=>x.IsActive && !x.IsArchived && !x.IsDeleted).ToList(),
            CanEdit=await _permissionService.HasPermissionAsync(HttpContext.Session.GetString("UserRole"),"Employees","Edit"),
            Rows=rows.Where(x=>x.StartUtc.HasValue && x.EndUtc.HasValue).Select(x=>new StaffExceptionRow(x,
                profiles.Where(p=>p.EmployeeProfileId==x.EmployeeProfileId).Select(BuildEmployeeName).FirstOrDefault()??"Former staff",
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(x.StartUtc!.Value,DateTimeKind.Utc),zone),
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(x.EndUtc!.Value,DateTimeKind.Utc),zone),MasterStamp(x))).ToList()});
    }
    [HttpPost,ValidateAntiForgeryToken]
    public Task<IActionResult> CreateAvailabilityException(AvailabilityExceptionForm form) {form.Id=0;return SaveAvailabilityException(form);}
    [HttpPost,ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateAvailabilityException(AvailabilityExceptionForm form) {if(form.Id<=0)ModelState.AddModelError("Id","Select an exception.");return SaveAvailabilityException(form);}
    private Task<IActionResult> SaveAvailabilityException(AvailabilityExceptionForm form) => SaveWorkflow(async () => {
        Require(form.Start.HasValue && form.End.HasValue && form.End>form.Start,"Choose an end after the start.");
        var zone=GarageZone();var start=DateTime.SpecifyKind(form.Start.Value,DateTimeKind.Unspecified);var end=DateTime.SpecifyKind(form.End.Value,DateTimeKind.Unspecified);
        Require(!zone.IsInvalidTime(start) && !zone.IsInvalidTime(end) && !zone.IsAmbiguousTime(start) && !zone.IsAmbiguousTime(end),"Choose unambiguous local times.");
        var utcStart=TimeZoneInfo.ConvertTimeToUtc(start,zone);var utcEnd=TimeZoneInfo.ConvertTimeToUtc(end,zone);
        var employee=await _dbContext.EmployeeProfiles.FindAsync(form.EmployeeProfileId);
        Require(employee is not null && employee.IsActive && !employee.IsArchived && !employee.IsDeleted,"Choose an active staff profile.");
        var row=form.Id==0?new EmployeeAvailabilityException():await _dbContext.EmployeeAvailabilityExceptions.FindAsync(form.Id);
        Require(row is not null,"Exception not found.");
        var oldEmployee=row.EmployeeProfileId;
        if(form.Id!=0){CheckMaster(row,form.Expected);WorkflowAudit("EmployeeAvailabilityException",row.EmployeeAvailabilityExceptionId,"Before edit",row);}
        Require(!await _dbContext.EmployeeAvailabilityExceptions.AnyAsync(x=>x.EmployeeProfileId==form.EmployeeProfileId && x.EmployeeAvailabilityExceptionId!=form.Id && x.StartUtc<utcEnd && x.EndUtc>utcStart),"This exception overlaps another exception for this staff member.");
        row.EmployeeProfileId=form.EmployeeProfileId;row.StartUtc=utcStart;row.EndUtc=utcEnd;row.IsAvailable=form.IsAvailable;row.Reason=form.Reason.Trim();
        if(form.Id==0)_dbContext.EmployeeAvailabilityExceptions.Add(row);
        await _dbContext.SaveChangesAsync();
        await RevalidateStaffBookings(employee);
        if(oldEmployee.HasValue && oldEmployee!=employee.EmployeeProfileId)
        {var previous=await _dbContext.EmployeeProfiles.FindAsync(oldEmployee);if(previous is not null)await RevalidateStaffBookings(previous);}
        WorkflowAudit("EmployeeAvailabilityException",row.EmployeeAvailabilityExceptionId,form.Id==0?"Create":"Edit",row);
        return RedirectToAction(nameof(StaffAvailability));
    },nameof(StaffAvailability));

    [HttpPost,ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteAvailabilityException(MasterStateForm form) => SaveWorkflow(async () => {
        var row=await _dbContext.EmployeeAvailabilityExceptions.FindAsync(form.Id);Require(row is not null,"Exception not found.");CheckMaster(row,form.Expected);
        var employee=await _dbContext.EmployeeProfiles.FindAsync(row.EmployeeProfileId);
        WorkflowAudit("EmployeeAvailabilityException",form.Id,"Delete",new{Record=row,form.Reason});
        _dbContext.EmployeeAvailabilityExceptions.Remove(row);await _dbContext.SaveChangesAsync();
        if(employee is not null)await RevalidateStaffBookings(employee);
        return RedirectToAction(nameof(StaffAvailability));
    },nameof(StaffAvailability));

    private async Task RevalidateStaffBookings(EmployeeProfile employee)
    {
        var name=BuildEmployeeName(employee);var now=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,GarageZone());
        var appointments=await _dbContext.Appointments.Where(x=>x.ScheduledEnd>now && x.AppointmentStatus!="Cancelled" && x.AppointmentStatus!="Completed" && x.AppointmentStatus!="No Show" &&
            (x.AssignedMechanicName==name || (employee.UserId!=null && x.AssignedMechanicUserId==employee.UserId))).ToListAsync();
        foreach(var a in appointments.Where(x=>x.ScheduledStart.HasValue && x.ScheduledEnd.HasValue))
        {
            var linked=await _dbContext.WorkOrders.FirstOrDefaultAsync(x=>x.AppointmentId==a.AppointmentId && x.WorkOrderStatus!="Cancelled");
            var error=await ScheduleError(a.ScheduledStart!.Value,a.ScheduledEnd!.Value,null,new[]{employee},a.AppointmentId,linked?.WorkOrderId);
            Require(error is null,error??"Existing appointment conflict.");
        }
        var ids=await _dbContext.WorkOrderAssignments.Where(x=>x.MechanicName==name || (employee.UserId!=null && x.MechanicUserId==employee.UserId)).Select(x=>x.WorkOrderId).ToListAsync();
        var orders=await _dbContext.WorkOrders.Where(x=>ids.Contains(x.WorkOrderId) && x.ScheduledEnd>now && x.WorkOrderStatus!="Cancelled" && x.WorkOrderStatus!="Completed" && x.WorkOrderStatus!="Ready for Pickup" && x.WorkOrderStatus!="Picked Up").ToListAsync();
        foreach(var o in orders.Where(x=>x.ScheduledStart.HasValue && x.ScheduledEnd.HasValue))
        {
            var error=await ScheduleError(o.ScheduledStart!.Value,o.ScheduledEnd!.Value,null,new[]{employee},o.AppointmentId,o.WorkOrderId);
            Require(error is null,error??"Existing work order conflict.");
        }
    }
}
