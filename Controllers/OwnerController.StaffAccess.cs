using Backyard_Mech.Data;
using Backyard_Mech.Models;
using Backyard_Mech.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    public async Task<IActionResult> EmployeeAvailability(int id)
    {
        if (!IsOwnerSession()) return LoginRedirect();
        var employee = await _dbContext.EmployeeProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmployeeProfileId == id && !x.IsArchived && !x.IsDeleted);
        if (employee is null) return RedirectToAction(nameof(Employees));
        var saved = await _dbContext.EmployeeAvailabilities.AsNoTracking()
            .Where(x => x.EmployeeProfileId == id).ToListAsync();
        var days = Enumerable.Range(0, 7).Select(day =>
        {
            var row = saved.FirstOrDefault(x => x.DayOfWeek == day);
            return new WeeklyAvailabilityDayForm
            {
                DayOfWeek = day,
                DayName = ((DayOfWeek)day).ToString(),
                IsAvailable = row?.IsAvailable == true,
                StartTime = row?.StartTime?.ToString("HH:mm") ?? "08:00",
                EndTime = row?.EndTime?.ToString("HH:mm") ?? "17:00",
                BreakStart = (row?.BreakStart ?? row?.LunchStartTime)?.ToString("HH:mm") ?? "",
                BreakEnd = (row?.BreakEnd ?? row?.LunchEndTime)?.ToString("HH:mm") ?? ""
            };
        }).ToList();
        return View(new EmployeeAvailabilityPage
        {
            EmployeeProfileId = id,
            EmployeeName = BuildEmployeeName(employee),
            Days = days,
            CanEdit = await _permissionService.HasPermissionAsync(HttpContext.Session.GetString("UserRole"), "Employees", "Edit")
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> SaveEmployeeAvailability(EmployeeAvailabilityPage form) => SaveWorkflow(async () =>
    {
        Require(IsOwnerSession(), "Sign in to manage employee availability.");
        Require(form.Days.Count == 7 && form.Days.Select(x => x.DayOfWeek).Distinct().Count() == 7 && form.Days.All(x => x.DayOfWeek is >= 0 and <= 6),
            "Availability must include each day of the week exactly once.");
        var employee = await _dbContext.EmployeeProfiles.FirstOrDefaultAsync(x => x.EmployeeProfileId == form.EmployeeProfileId && x.IsActive && !x.IsArchived && !x.IsDeleted);
        Require(employee is not null, "Active employee profile not found.");
        var activeEmployee = employee!;
        foreach (var day in form.Days)
        {
            if (!day.IsAvailable) continue;
            var hasStart = TimeOnly.TryParse(day.StartTime, out var start);
            var hasEnd = TimeOnly.TryParse(day.EndTime, out var end);
            Require(hasStart && hasEnd && end > start,
                $"Enter valid start and end times for {day.DayName}.");
            var hasBreakStart = TimeOnly.TryParse(day.BreakStart, out var breakStart);
            var hasBreakEnd = TimeOnly.TryParse(day.BreakEnd, out var breakEnd);
            Require(hasBreakStart == hasBreakEnd, $"Enter both break start and end times for {day.DayName}, or leave both empty.");
            Require(!hasBreakStart || breakStart > start && breakEnd < end && breakEnd > breakStart,
                $"Break time for {day.DayName} must be within the work hours.");
        }

        var existing = await _dbContext.EmployeeAvailabilities.Where(x => x.EmployeeProfileId == activeEmployee.EmployeeProfileId).ToListAsync();
        foreach (var day in form.Days)
        {
            var row = existing.FirstOrDefault(x => x.DayOfWeek == day.DayOfWeek);
            if (row is null)
            {
                row = new EmployeeAvailability { EmployeeProfileId = activeEmployee.EmployeeProfileId, DayOfWeek = day.DayOfWeek };
                _dbContext.EmployeeAvailabilities.Add(row);
            }
            row.IsAvailable = day.IsAvailable;
            row.StartTime = day.IsAvailable && TimeOnly.TryParse(day.StartTime, out var start) ? start : null;
            row.EndTime = day.IsAvailable && TimeOnly.TryParse(day.EndTime, out var end) ? end : null;
            row.BreakStart = day.IsAvailable && TimeOnly.TryParse(day.BreakStart, out var breakStart) ? breakStart : null;
            row.BreakEnd = day.IsAvailable && TimeOnly.TryParse(day.BreakEnd, out var breakEnd) ? breakEnd : null;
            row.LunchStartTime = row.BreakStart;
            row.LunchEndTime = row.BreakEnd;
        }
        await _dbContext.SaveChangesAsync();
        await RevalidateStaffBookings(activeEmployee);
        WorkflowAudit("EmployeeAvailability", activeEmployee.EmployeeProfileId, "Update weekly availability", form.Days);
        await _dbContext.SaveChangesAsync();
        TempData["OwnerMessage"] = "Weekly availability saved.";
        TempData["OwnerMessageTone"] = "success";
        return RedirectToAction(nameof(EmployeeAvailability), new { id = activeEmployee.EmployeeProfileId });
    }, nameof(EmployeeAvailability), new { id = form.EmployeeProfileId });

    public async Task<IActionResult> AuditLogReport(DateTime? startDate, DateTime? endDate, string? nameOrEmail, string? action, string? entity, int? employeeProfileId, int page = 1)
    {
        if (!IsOwnerSession()) return LoginRedirect();
        var zone = GarageZone();
        var query = _dbContext.AuditLogEntries.AsNoTracking();
        if (startDate.HasValue)
        {
            var localStart = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Unspecified);
            query = query.Where(x => x.TimestampUtc >= TimeZoneInfo.ConvertTimeToUtc(localStart, zone));
        }
        if (endDate.HasValue)
        {
            var localEnd = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Unspecified);
            query = query.Where(x => x.TimestampUtc < TimeZoneInfo.ConvertTimeToUtc(localEnd, zone));
        }
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.Action == action);
        if (!string.IsNullOrWhiteSpace(entity)) query = query.Where(x => x.EntityName == entity);
        if (employeeProfileId.HasValue)
        {
            var selectedProfile = await _dbContext.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeProfileId == employeeProfileId.Value);
            if (selectedProfile is not null && !string.IsNullOrWhiteSpace(selectedProfile.UserId))
                query = query.Where(x => x.UserId == selectedProfile.UserId || x.EntityName == "Employee" && x.EntityId == employeeProfileId.Value.ToString());
            else
                query = query.Where(x => x.EntityName == "Employee" && x.EntityId == employeeProfileId.Value.ToString());
        }
        if (!string.IsNullOrWhiteSpace(nameOrEmail))
        {
            var term = nameOrEmail.Trim();
            var matchingUserIds = await _dbContext.Users.AsNoTracking()
                .Where(x => x.Email.Contains(term) || x.UserName.Contains(term))
                .Select(x => x.Id.ToString()).ToListAsync();
            var matchingProfileIds = await _dbContext.EmployeeProfiles.AsNoTracking()
                .Where(x => x.UserId != null && ((x.FirstName ?? "").Contains(term) || (x.LastName ?? "").Contains(term) || (x.Email ?? "").Contains(term)))
                .Select(x => x.UserId!).ToListAsync();
            var matchingIds = matchingUserIds.Concat(matchingProfileIds).Distinct().ToList();
            query = query.Where(x => x.UserId != null && matchingIds.Contains(x.UserId) || x.UserName != null && x.UserName.Contains(term));
        }

        var totalCount = await query.CountAsync();
        const int pageSize = 50;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var entries = await query.OrderByDescending(x => x.TimestampUtc).ThenByDescending(x => x.AuditLogEntryId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var entryUserIds = entries.Select(x => x.UserId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        var users = await _dbContext.Users.AsNoTracking().Where(x => entryUserIds.Contains(x.Id.ToString())).ToDictionaryAsync(x => x.Id.ToString(), x => x);
        var profiles = await _dbContext.EmployeeProfiles.AsNoTracking().Where(x => x.UserId != null && entryUserIds.Contains(x.UserId)).ToListAsync();
        var profileByUserId = profiles.GroupBy(x => x.UserId!).ToDictionary(x => x.Key, x => x.First());

        var rows = entries.Select(entry =>
        {
            users.TryGetValue(entry.UserId ?? "", out var user);
            profileByUserId.TryGetValue(entry.UserId ?? "", out var profile);
            var actor = profile is null ? entry.UserName ?? user?.UserName ?? "System" : BuildEmployeeName(profile);
            return new AuditLogReportRow(actor, user?.Email ?? profile?.Email ?? "—", entry.Action ?? "Activity",
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(entry.TimestampUtc, DateTimeKind.Utc), zone),
                entry.EntityName ?? "System", entry.EntityId ?? "—");
        });
        var filteredRows = rows.ToList();
        return View(new AuditLogReportPage
        {
            StartDate = startDate,
            EndDate = endDate,
            NameOrEmail = nameOrEmail,
            Action = action,
            Entity = entity,
            EmployeeProfileId = employeeProfileId,
            TotalCount = totalCount,
            Page = page,
            TotalPages = totalPages,
            Rows = filteredRows,
            Actions = await _dbContext.AuditLogEntries.AsNoTracking().Where(x => x.Action != null).Select(x => x.Action!).Distinct().OrderBy(x => x).ToListAsync(),
            Entities = await _dbContext.AuditLogEntries.AsNoTracking().Where(x => x.EntityName != null).Select(x => x.EntityName!).Distinct().OrderBy(x => x).ToListAsync()
        });
    }

    private static string AccessStamp(AppUser user) => MasterStamp(new {
        user.Id,user.Role,user.IsActive,user.IsBlocked,user.Email,user.UserName,
        Credentials=AccountSecurityService.CredentialStamp(user)
    });

    private static string StaffResetPassword(string lastName) =>
        $"{new string(lastName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant()}@bakyard123";

    private bool CanManageStaffPasswords() => HttpContext.Session.GetString("UserRole") is "RootAdmin" or "Administrator";

    public async Task<IActionResult> StaffAccounts(int? userId)
    {
        var role=HttpContext.Session.GetString("UserRole");
        var actorGrants=await _permissionService.GetPermissionCodesAsync(role);
        var model=new StaffAccountsPage {
            Profiles=await _dbContext.EmployeeProfiles.AsNoTracking().Where(x=>!x.IsArchived && !x.IsDeleted).OrderBy(x=>x.FirstName).ToListAsync(),
            Accounts=await _dbContext.Users.AsNoTracking().Where(x=>!_dbContext.EmployeeProfiles.Any(p=>p.UserId==x.Id.ToString() && (p.IsArchived || p.IsDeleted))).OrderBy(x=>x.UserName).ToListAsync(),
            Roles=await _dbContext.AppRoles.AsNoTracking().Where(x=>x.IsActive && StandardRoleProfiles.BusinessRoleKeys.Contains(x.RoleKey)).OrderBy(x=>x.DisplayOrder).ThenBy(x=>x.RoleName).ToListAsync(),
            CanCreate=await _permissionService.HasPermissionAsync(role,"User Access","Create"),
            CanCreateGarageOwner=string.Equals(role,"Owner",StringComparison.OrdinalIgnoreCase),
            CanEdit=await _permissionService.HasPermissionAsync(role,"User Access","Edit"),
            CanActivateAccounts=await _permissionService.HasPermissionAsync(role,"User Access","Edit") &&
                (await _permissionService.GetPermissionCodesAsync(role)).Contains(OwnerPermissionCatalog.ActionPermissionCode(nameof(ActivateStaffAccount))),
            CanDeactivateAccounts=await _permissionService.HasPermissionAsync(role,"User Access","Edit") &&
                actorGrants.Contains(OwnerPermissionCatalog.ActionPermissionCode(nameof(DeactivateStaffAccount))),
            CanBlockAccounts=OwnerPermissionCatalog.CanAccessAction(actorGrants,nameof(BlockStaffAccount)),
            CanUnblockAccounts=OwnerPermissionCatalog.CanAccessAction(actorGrants,nameof(UnblockStaffAccount))
        };
        model.ExpectedByAccount=model.Accounts.ToDictionary(x=>x.Id,AccessStamp);
        model.Selected=model.Accounts.SingleOrDefault(x=>x.Id==userId);
        if(model.Selected is not null)
        {
            Response.Headers["Cache-Control"]="no-store";
            model.Defaults=await _permissionService.GetRolePermissionCodesAsync(model.Selected.Role);
            model.Expected=AccessStamp(model.Selected);
            if(model.Selected.Id.ToString()==HttpContext.Session.GetString("UserId") || model.Defaults.Contains("*")) model.CanEdit=false;
            var profile=model.Profiles.FirstOrDefault(x=>x.UserId==model.Selected.Id.ToString());
            model.CanResetPassword=model.CanEdit && CanManageStaffPasswords() &&
                profile is not null && !string.IsNullOrWhiteSpace(profile.LastName) &&
                PermissionService.CanDelegateStaffPermissions(actorGrants,model.Defaults);
            if (model.CanResetPassword)
            {
                var candidate=StaffResetPassword(profile!.LastName!);
                model.ResetPasswordPreview=candidate;
                var wasReset=await _dbContext.AuditLogEntries.AsNoTracking().AnyAsync(x=>x.EntityName=="Account" &&
                    x.EntityId==model.Selected.Id.ToString() && x.Action=="Reset staff password");
                if (wasReset && PasswordHasher.Verify(candidate,model.Selected.PasswordHash)) model.ResetPasswordValue=candidate;
            }
        }
        return View(model);
    }

    private async Task ValidateDelegation(AppRole role, HashSet<string> grants)
    {
        var actor=await _permissionService.GetPermissionCodesAsync(HttpContext.Session.GetString("UserRole"));
        Require(!role.IsSystemProtected || actor.Contains("*"),"Only the root administrator may assign protected access.");
        Require(PermissionService.CanDelegateStaffPermissions(actor, grants),"You cannot assign access beyond your own permissions.");
    }

    [HttpPost,ValidateAntiForgeryToken]
    public Task<IActionResult> CreateStaffAccount(StaffAccountForm form) => SaveWorkflow(async () => {
        Require(form.Password.Length>=12,"Use a password with at least 12 characters.");
        Require(form.Password==form.ConfirmPassword,"Passwords do not match.");
        EmployeeProfile? profile = null;
        if (form.EmployeeProfileId.HasValue)
        {
            profile = await _dbContext.EmployeeProfiles.FindAsync(form.EmployeeProfileId.Value);
            Require(profile is not null && profile.IsActive && !profile.IsArchived && !profile.IsDeleted,"Choose an active staff profile.");
            Require(string.IsNullOrWhiteSpace(profile.UserId),"This profile already has an account link.");
        }
        var email=form.Email.Trim(); var name=form.UserName.Trim();
        Require(!await _dbContext.Users.AnyAsync(x=>x.Email==email || x.UserName==name),"Login email or username is already in use.");
        Require(form.Password.Any(char.IsUpper) && form.Password.Any(char.IsLower) && form.Password.Any(char.IsDigit) && form.Password.Any(x=>!char.IsLetterOrDigit(x)),
            "Use at least 12 characters with uppercase, lowercase, a number and a symbol.");
        var role=await _dbContext.AppRoles.SingleOrDefaultAsync(x=>x.RoleKey==form.RoleKey && x.IsActive);
        Require(role is not null && StandardRoleProfiles.IsBusinessRole(role.RoleKey),"Choose one of the seven standard roles.");
        Require(!role.IsSystemProtected,"Choose a staff role.");
        Require(role.RoleKey!="Owner" || HttpContext.Session.GetString("UserRole")=="Owner","Only a Garage Owner can create another Garage Owner account.");
        Require(form.Permissions.Count == 0,"Edit permissions under Roles & Permissions. Account permissions come from the selected role.");
        var effective=await _permissionService.GetRolePermissionCodesAsync(role.RoleKey);
        await ValidateDelegation(role,effective);
        var user=new AppUser {Email=email,UserName=name,Role=role.RoleKey,PasswordHash=PasswordHasher.Hash(form.Password),IsActive=true};
        _dbContext.Users.Add(user); await _dbContext.SaveChangesAsync();
        if (profile is not null) profile.UserId=user.Id.ToString();
        WorkflowAudit("Account",user.Id,"Create staff account",new{ProfileId=profile?.EmployeeProfileId,user.UserName,user.Email,user.Role});
        return RedirectToAction(nameof(StaffAccounts));
    },nameof(StaffAccounts));

    [HttpPost,ValidateAntiForgeryToken]
    public Task<IActionResult> SaveUserAccess(UserAccessForm form) => SaveWorkflow(async () => {
        Require(form.UserId.ToString()!=HttpContext.Session.GetString("UserId"),"You cannot change your own account access.");
        var user=await _dbContext.Users.FindAsync(form.UserId); Require(user is not null,"Account not found.");
        var previousRole=await _dbContext.AppRoles.SingleOrDefaultAsync(x=>x.RoleKey==user.Role);
        Require(previousRole?.IsSystemProtected!=true,"Protected root administrator access cannot be overridden here.");
        Require(AccessStamp(user)==form.Expected,"Account access changed. Refresh before saving.");
        var role=await _dbContext.AppRoles.SingleOrDefaultAsync(x=>x.RoleKey==form.RoleKey && x.IsActive);
        Require(role is not null && StandardRoleProfiles.IsBusinessRole(role.RoleKey),"Choose one of the seven standard roles.");
        Require(!role.IsSystemProtected,"Choose a staff role.");
        Require(form.Permissions.Count == 0,"Edit permissions under Roles & Permissions. Account permissions come from the selected role.");
        await ValidateDelegation(role,await _permissionService.GetRolePermissionCodesAsync(role.RoleKey));
        var before = new { user.Role, user.IsActive, user.Email, user.UserName };
        var changes = new List<string>();
        if (form.UpdateDetails)
        {
            var email = form.Email?.Trim();
            var name = form.UserName?.Trim();
            Require(!string.IsNullOrWhiteSpace(email) && new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email),
                "Enter a valid login email.");
            Require(!string.IsNullOrWhiteSpace(name), "Enter a username.");
            Require(!await _dbContext.Users.AnyAsync(x => x.Id != user.Id && (x.Email == email || x.UserName == name)),
                "Login email or username is already in use.");
            if (user.Email != email) { user.Email = email!; changes.Add("Login email changed"); }
            if (user.UserName != name) { user.UserName = name!; changes.Add("Username changed"); }
        }
        if (user.Role != role.RoleKey) changes.Add($"System access role changed from {user.Role} to {role.RoleKey}");
        if (user.IsActive != form.IsActive) changes.Add(form.IsActive ? "Account enabled" : "Account disabled");
        if (changes.Count == 0) return RedirectToAction(nameof(StaffAccounts));
        user.Role=role.RoleKey;user.IsActive=form.IsActive;
        WorkflowAudit("Account", user.Id, "Edit account details", new {
            Summary = string.Join(". ", changes), AccountUserName = user.UserName,
            Before = before, After = new { user.Role, user.IsActive, user.Email, user.UserName }
        });
        return RedirectToAction(nameof(StaffAccounts));
    },nameof(StaffAccounts),new{userId=form.UserId});

    [HttpPost,ValidateAntiForgeryToken]
    public Task<IActionResult> ResetStaffPassword(StaffPasswordResetForm form) => SaveWorkflow(async () => {
        Require(CanManageStaffPasswords(),"Only an Administrator or Root Admin can reset staff passwords.");
        Require(form.UserId.ToString()!=HttpContext.Session.GetString("UserId"),"You cannot reset your own password here.");
        var user=await _dbContext.Users.FindAsync(form.UserId);
        Require(user is not null,"Account not found.");
        Require(AccessStamp(user)==form.Expected,"Account changed. Refresh before resetting its password.");
        Require(user.Role!="RootAdmin","The Root Admin password cannot be reset here.");
        var targetRole=await _dbContext.AppRoles.SingleOrDefaultAsync(x=>x.RoleKey==user.Role);
        Require(targetRole is not null,"The account role could not be found.");
        await ValidateDelegation(targetRole,await _permissionService.GetRolePermissionCodesAsync(targetRole.RoleKey));
        var profile=await _dbContext.EmployeeProfiles.SingleOrDefaultAsync(x=>x.UserId==user.Id.ToString() &&
            !x.IsArchived && !x.IsDeleted);
        Require(profile is not null && !string.IsNullOrWhiteSpace(profile.LastName),
            "Link this account to a staff profile with a last name before resetting its password.");
        Require(!string.IsNullOrWhiteSpace(user.Email) && new EmailAddressAttribute().IsValid(user.Email) &&
            !user.Email.EndsWith(".local",StringComparison.OrdinalIgnoreCase),
            "Set a real login email address before resetting the password. Local addresses cannot receive email.");
        var password=StaffResetPassword(profile.LastName);
        Require(password.Length>=12,"The staff last name is required to create the reset password.");
        var sender=HttpContext.RequestServices.GetRequiredService<GmailSmtpSender>();
        Require(sender.GetStatus().IsConfigured,"Configure the Gmail app password before resetting staff passwords.");
        try
        {
            await sender.SendAsync(user.Email,"Your Backyard Mech password was reset",
                $"Hello {profile.FirstName},\n\nYour Backyard Mech login password has been reset.\n\nUsername: {user.UserName}\nTemporary password: {password}\n\nSign in and change your password in My Profile.\n\nBackyard Mech");
        }
        catch (Exception error)
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<OwnerController>>().LogError(error,
                "Could not email the reset password for account {AccountId}",user.Id);
            Require(false,"The reset email could not be sent. The password was not changed.");
        }
        user.PasswordHash=PasswordHasher.Hash(password);
        var security=await HttpContext.RequestServices.GetRequiredService<AccountSecurityService>().State(user.Id);
        security.FailedAttempts=0;
        security.LockedUntilUtc=null;
        WorkflowAudit("Account",user.Id,"Reset staff password",new { Recipient=user.Email });
        TempData["OwnerMessage"]="Password reset email sent. The new password is shown in the account details below.";
        TempData["OwnerMessageTone"]="success";
        return RedirectToAction(nameof(StaffAccounts),new{userId=form.UserId});
    },nameof(StaffAccounts),new{userId=form.UserId});

    [HttpPost,ValidateAntiForgeryToken]
    public Task<IActionResult> ActivateStaffAccount(AccountStatusForm form) => SetStaffAccountState(form, true);

    [HttpPost,ValidateAntiForgeryToken]
    public Task<IActionResult> DeactivateStaffAccount(AccountStatusForm form) => SetStaffAccountState(form, false);

    [HttpPost,ValidateAntiForgeryToken]
    public Task<IActionResult> BlockStaffAccount(AccountStatusForm form) => SetStaffBlockedState(form, true);

    [HttpPost,ValidateAntiForgeryToken]
    public Task<IActionResult> UnblockStaffAccount(AccountStatusForm form) => SetStaffBlockedState(form, false);

    private Task<IActionResult> SetStaffBlockedState(AccountStatusForm form, bool block) => SaveWorkflow(async () => {
        Require(form.UserId.ToString()!=HttpContext.Session.GetString("UserId"),"You cannot block your own account.");
        var user=await _dbContext.Users.FindAsync(form.UserId);
        Require(user is not null,"Account not found.");
        var currentRole=await _dbContext.AppRoles.SingleOrDefaultAsync(x=>x.RoleKey==user!.Role);
        Require(currentRole?.IsSystemProtected!=true,"Protected administrator accounts cannot be blocked.");
        Require(currentRole is not null,"Account role not found.");
        await ValidateDelegation(currentRole!,await _permissionService.GetRolePermissionCodesAsync(currentRole!.RoleKey));
        Require(AccessStamp(user!)==form.Expected,"Account changed. Refresh before trying again.");
        Require(user!.IsBlocked!=block,block?"This account is already blocked.":"This account is not blocked.");
        user.IsBlocked=block;
        WorkflowAudit("Account",user.Id,block?"Block account":"Unblock account",new{user.UserName,user.Email,user.IsActive,user.IsBlocked});
        TempData["OwnerMessage"]=block?"Account blocked. The user can no longer sign in.":"Account unblocked.";
        TempData["OwnerMessageTone"]="success";
        return RedirectToAction(nameof(StaffAccounts));
    },nameof(StaffAccounts));

    private Task<IActionResult> SetStaffAccountState(AccountStatusForm form, bool activate) => SaveWorkflow(async () => {
        Require(form.UserId.ToString()!=HttpContext.Session.GetString("UserId"),"You cannot change your own account status.");
        var user=await _dbContext.Users.FindAsync(form.UserId);Require(user is not null,"Account not found.");
        var currentRole=await _dbContext.AppRoles.SingleOrDefaultAsync(x=>x.RoleKey==user!.Role);
        Require(currentRole?.IsSystemProtected!=true,"Protected administrator accounts cannot be changed here.");
        Require(AccessStamp(user!)==form.Expected,"Account changed. Refresh before trying again.");
        Require(user!.IsActive!=activate,activate?"This account is already active.":"This account is already inactive.");
        user.IsActive=activate;
        WorkflowAudit("Account",user.Id,activate?"Activate account":"Deactivate account",new{user.UserName,user.Email,IsActive=user.IsActive});
        return RedirectToAction(nameof(StaffAccounts),new{userId=form.UserId});
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
        var zone=GarageZone();
        var row=form.Id==0?new EmployeeAvailabilityException():await _dbContext.EmployeeAvailabilityExceptions.FindAsync(form.Id);
        Require(row is not null,"Exception not found.");
        if(form.Id!=0)
        {
            CheckMaster(row!,form.Expected);
            WorkflowAudit("EmployeeAvailabilityException",row!.EmployeeAvailabilityExceptionId,"Before edit",row);
            Require(row.StartUtc.HasValue,"This exception has no valid start time and cannot be edited.");
            form.Start=TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(row.StartUtc.Value,DateTimeKind.Utc),zone);
        }
        Require(form.Start.HasValue && form.End.HasValue && form.End.Value>form.Start.Value,"Choose an end date and time after the start date and time.");
        var start=DateTime.SpecifyKind(form.Start!.Value,DateTimeKind.Unspecified);var end=DateTime.SpecifyKind(form.End!.Value,DateTimeKind.Unspecified);
        Require(!zone.IsInvalidTime(start) && !zone.IsInvalidTime(end) && !zone.IsAmbiguousTime(start) && !zone.IsAmbiguousTime(end),"Choose unambiguous local times.");
        var utcStart=TimeZoneInfo.ConvertTimeToUtc(start,zone);var utcEnd=TimeZoneInfo.ConvertTimeToUtc(end,zone);
        var employee=await _dbContext.EmployeeProfiles.FindAsync(form.EmployeeProfileId);
        Require(employee is not null && employee.IsActive && !employee.IsArchived && !employee.IsDeleted,"Choose an active staff profile.");
        var oldEmployee=row!.EmployeeProfileId;
        Require(!await _dbContext.EmployeeAvailabilityExceptions.AnyAsync(x=>x.EmployeeProfileId==form.EmployeeProfileId && x.EmployeeAvailabilityExceptionId!=form.Id && x.StartUtc<utcEnd && x.EndUtc>utcStart),"This exception overlaps another exception for this staff member.");
        row.EmployeeProfileId=form.EmployeeProfileId;if(form.Id==0)row.StartUtc=utcStart;row.EndUtc=utcEnd;row.IsAvailable=form.IsAvailable;row.Reason=form.Reason.Trim();
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
