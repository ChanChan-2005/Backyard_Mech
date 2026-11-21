param([string]$BaseUrl='http://127.0.0.1:5099',[string]$ConnectionString='Server=.\SQLEXPRESS;Database=Bakyard_Mech;Integrated Security=True;TrustServerCertificate=True',[string]$Login='admin123',[string]$Password='admin123')
$ErrorActionPreference='Stop'
$db=New-Object System.Data.SqlClient.SqlConnection $ConnectionString
$db.Open()
function Sql($query){$cmd=$db.CreateCommand();$cmd.CommandText=$query;try{$cmd.ExecuteScalar()}finally{$cmd.Dispose()}}
function Check($ok,$message){if(!$ok){throw "FAIL: $message"};Write-Output "PASS: $message"}
function Token($html){[regex]::Match($html,'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value}
$session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
function GetPage($path){Invoke-WebRequest "$BaseUrl$path" -WebSession $session -UseBasicParsing}
function Post($action,$body){
    $body['__RequestVerificationToken']=Token (GetPage '/Owner/StaffAccounts').Content
    $pairs=foreach($key in $body.Keys){foreach($value in @($body[$key])){[Uri]::EscapeDataString([string]$key)+'='+[Uri]::EscapeDataString([string]$value)}}
    Invoke-WebRequest "$BaseUrl/Owner/$action" -Method Post -Body ($pairs -join '&') -ContentType 'application/x-www-form-urlencoded' -WebSession $session -UseBasicParsing
}
function SignIn($email,$password){
    $page=GetPage '/Account/Login'
    $script:loginToken=Token $page.Content
    $null=Invoke-WebRequest "$BaseUrl/Account/Login" -Method Post -Body @{Email=$email;Password=$password;__RequestVerificationToken=(Token $page.Content)} -WebSession $session -UseBasicParsing
}
function AccessForm($id,$role,$choices){
    $html=(GetPage "/Owner/StaffAccounts?userId=$id").Content
    $stamp=[regex]::Match($html,'name="Expected" value="([^"]+)"').Groups[1].Value
    $body=@{UserId=$id;Expected=$stamp;RoleKey=$role;IsActive='true';Reason=$mark}
    $i=0
    foreach($code in $choices.Keys){$permission=Sql "SELECT PermissionId FROM Permission WHERE PermissionCode=N'$code'";$body["Permissions[$i].PermissionId"]=$permission;$body["Permissions[$i].Mode"]=$choices[$code];$i++}
    return $body
}
function ExceptionStamp($id){[regex]::Match((GetPage '/Owner/StaffAvailability').Content,"(?s)id=`"exception-$id`".*?name=`"Expected`" value=`"([^`"]+)`"").Groups[1].Value}
$mark='StaffSmoke-'+[Guid]::NewGuid().ToString('N').Substring(0,16)
$role='Parts Clerk'
$originalRolePermissions=[string](Sql "SELECT STRING_AGG(CONVERT(nvarchar(20), PermissionId), ',') FROM RolePermission WHERE RoleId=N'$role'")
$owner=$profile=$second=$account=$other=$appointment=$workorder=0
try {
    $owner=[int](Sql "INSERT Users(Email,UserName,PasswordHash,Role,IsActive,CreatedAt) SELECT N'$mark-owner',N'$mark-owner',PasswordHash,N'Owner',1,GETUTCDATE() FROM Users WHERE Email=N'$Login'; SELECT CAST(SCOPE_IDENTITY() AS int);")
    SignIn "$mark-owner" $Password
    $ownerSession=$session
    Check ((GetPage '/Owner/StaffAccounts').Content.Contains('Create Staff Account')) 'Create-account modal renders'
    $accountPage=(GetPage '/Owner/StaffAccounts').Content
    Check ($accountPage -match 'data-staff-open="create-account" type="button"(?! disabled)') 'Create Account remains available when all profiles are linked'
    Check ((GetPage '/Owner/Employees?newProfileForAccount=1').Content.Contains('name="ReturnToStaffAccounts" value="true"')) 'Add-profile flow returns to account creation'
    Check ((GetPage '/Owner/StaffAvailability').Content.Contains('Special Availability')) 'Exceptions page renders'
    Check ((Sql "SELECT COLUMNPROPERTY(OBJECT_ID('AccountSecurity'),'UserId','IsIdentity')") -eq 0) 'Login security uses the existing user key, not an independent identity'
    Check ((Sql "SELECT COUNT(*) FROM RolePermission WHERE RoleId='Mechanic/Technician'") -gt 0) 'Mechanic role has initial permissions'
    Check ((Sql "SELECT COUNT(*) FROM RolePermission WHERE RoleId='Service Advisor'") -gt 0) 'Existing unconfigured service-advisor role receives defaults'
    Check ((Sql "SELECT COUNT(*) FROM AppRole WHERE IsActive=1 AND RoleKey<>'RootAdmin' AND RoleKey IN ('Owner','Administrator','Service Advisor','Mechanic/Technician','Parts Clerk','Supervisor/QC Inspector','Accounting/Cashier')") -eq 7) 'Seven standard roles are active'
    Check ((Sql "SELECT COUNT(*) FROM AppRole WHERE IsActive=1 AND RoleKey NOT IN ('RootAdmin','Owner','Administrator','Service Advisor','Mechanic/Technician','Parts Clerk','Supervisor/QC Inspector','Accounting/Cashier')") -eq 0) 'Extra roles are inactive'
    $permissionPage=(GetPage '/Owner/SettingsRoles?role=Owner&tab=permissions').Content
    $permissionGroups=[regex]::Matches($permissionPage,'<summary>([^<]+)</summary>') | ForEach-Object { $_.Groups[1].Value }
    $sidebarGroups=@('Dashboard','Service Queue','Customer and Vehicle','Vehicle Records','Appointments and Check-In','Estimates / Customers Approval','Work Order and Assignment','Bay Management','Parts Management','Parts POS','Suppliers','Service Catalog','Labour Time Tracking','Quality Control','Tools Tracking','Invoicing and Payments','Archive','Records Disposition','Reports','User Management','Settings','Account Access')
    Check (($permissionGroups -join '|') -eq ($sidebarGroups -join '|')) 'Permission groups match sidebar labels and order'
    $renderedIds=[regex]::Matches($permissionPage,'name="PermissionIds" value="(\d+)"') | ForEach-Object { $_.Groups[1].Value }
    $availableCount=[int]([regex]::Match($permissionPage,'(\d+) available permissions').Groups[1].Value)
    Check ($renderedIds.Count -eq $availableCount -and ($renderedIds | Select-Object -Unique).Count -eq $availableCount) 'Every available permission has one editor control'
    Check ((Sql "SELECT COUNT(*) FROM Permission WHERE IsActive=1") -eq $availableCount) 'No hidden obsolete permissions remain active'
    Check ((Sql "SELECT COUNT(*) FROM Permission WHERE PermissionCode='estimates.delete' AND IsActive=1") -eq 0 -and
        (Sql "SELECT COUNT(*) FROM Permission WHERE PermissionCode='owneraction.cancelestimate' AND IsActive=1") -eq 1) 'Estimate cancellation has one active action permission'
    Check ((Sql "SELECT COUNT(*) FROM Permission WHERE IsActive=1 AND PermissionCode IN ('appointments.create','customers.edit','settings.delete')") -eq 0 -and
        (Sql "SELECT COUNT(*) FROM Permission WHERE IsActive=1 AND PermissionCode IN ('owneraction.createappointment','owneraction.editcustomer')") -eq 2) 'Single-action and unused broad permissions are hidden'
    Check ((Sql "SELECT COUNT(*) FROM Permission WHERE IsActive=1 AND PermissionCode LIKE 'subtab.%'") -eq 18) 'Every navigation sub-tab has a permission'
    foreach($label in @('Inventory Registry','Product Listing','All Archived Records','Archived Parts','Archived Product Listings','Archived Tools','All Disposition Records','Disposition Parts','Disposition Product Listings','Disposition Tools','Service Categories','Part Categories','Tool Categories','Roles','Permissions','All Employees','Active Employees','Inactive Employees')) {
        Check ($permissionPage.Contains(">$label<")) "Sub-tab permission appears in editor: $label"
    }
    Check ((Sql "SELECT COUNT(*) FROM Permission WHERE IsActive=1 AND PermissionCode IN ('partsmanagement.view','partspos.view','archive.view','recordsdisposition.view')") -eq 4) 'Shared legacy groups have separate sidebar access permissions'
    $body=@{FirstName=$mark;LastName='Staff';StreetAddress='1 Test Street';City='St. John''s';Province='Newfoundland and Labrador';PostalCode='A1A 1A1';Phone='7095550100';Email="$mark@example.com";JobTitle='Mechanic / Technician';EmploymentType='Full-time';EmploymentStatus='Active';HourlyRate=100}
    $null=Post 'AddEmployee' $body
    $profile=[int](Sql "SELECT EmployeeProfileId FROM EmployeeProfile WHERE FirstName=N'$mark'")
    Check ($profile -gt 0 -and [DBNull]::Value.Equals((Sql "SELECT UserId FROM EmployeeProfile WHERE EmployeeProfileId=$profile"))) 'Creating a profile does not create an account'
    Check ((Sql "SELECT COUNT(*) FROM Users WHERE Email=N'$mark@example.com'") -eq 0) 'Profile contact email is not a login'
    $roleCreateStatus=0
    try { $null=Post 'CreateRole' @{RoleName=$mark;Description='Account login regression role'} } catch { $roleCreateStatus=[int]$_.Exception.Response.StatusCode }
    Check ($roleCreateStatus -eq 405 -and (Sql "SELECT COUNT(*) FROM AppRole WHERE RoleName=N'$mark'") -eq 0) 'Custom role creation is blocked'
    $viewPermission=Sql "SELECT PermissionId FROM Permission WHERE PermissionCode='suppliers.view'"
    $createPermission=Sql "SELECT PermissionId FROM Permission WHERE PermissionCode='suppliers.create'"
    $null=Post 'SaveRolePermissions' @{RoleId=$role;PermissionIds=@($viewPermission,$createPermission)}
    Check ((Sql "SELECT COUNT(*) FROM RolePermission WHERE RoleId=N'$role'") -eq 2) 'Settings permission checkboxes persist role grants'
    $rolePage=(GetPage "/Owner/SettingsRoles?role=$role").Content
    Check ($rolePage -match "(?s)value=`"$viewPermission`"[^>]*checked" -and $rolePage -match "(?s)value=`"$createPermission`"[^>]*checked") 'Saved role grants render checked after reload'
    $accountForm=@{EmployeeProfileId=$profile;Email="$mark-login@example.com";UserName=$mark;Password='Test-Only!Password123';ConfirmPassword='Test-Only!Password123';RoleKey='Mechanic/Technician'}
    $null=Post 'CreateStaffAccount' $accountForm
    $account=[int](Sql "SELECT Id FROM Users WHERE UserName=N'$mark'")
    Check ($account -gt 0 -and (Sql "SELECT UserId FROM EmployeeProfile WHERE EmployeeProfileId=$profile") -eq "$account") 'Explicit account creation links selected profile'
    Check ((Sql "SELECT PasswordHash FROM Users WHERE Id=$account") -ne 'Test-Only!Password123') 'Password stored as hash'
    $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    SignIn $mark 'Test-Only!Password123'
    Check ((GetPage '/Owner/WorkOrders').Content.Contains('Work Orders')) 'Profile-created account signs in by username with built-in mechanic role'
    $initialSession=$session
    $session=$ownerSession
    $null=Post 'SaveUserAccess' (AccessForm $account $role @{})
    Check ((Sql "SELECT Role FROM Users WHERE Id=$account") -eq $role) 'Changing the account role persists the selected role key'
    $session=$initialSession
    Check ((GetPage '/Owner/WorkOrders').Content.Contains('name="Password"')) 'Old session is required to sign in again after a role change'
    $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    SignIn "$mark-login@example.com" 'Test-Only!Password123'
    Check ((GetPage '/Owner/Suppliers').Content.Contains('id="master-new"')) 'Same profile-created account signs in by email with its new role'
    $session=$ownerSession
    $null=Post 'CreateStaffAccount' $accountForm
    Check ((Sql "SELECT COUNT(*) FROM Users WHERE UserName=N'$mark'") -eq 1) 'Duplicate account for profile rejected'
    $null=Post 'CreateStaffAccount' @{Email="$mark-other@example.com";UserName="$mark-other";Password='Test-Only!Password123';ConfirmPassword='Test-Only!Password123';RoleKey=$role}
    $other=[int](Sql "SELECT Id FROM Users WHERE Email=N'$mark-other@example.com'")
    Check ($other -gt 0 -and (Sql "SELECT COUNT(*) FROM EmployeeProfile WHERE UserId=N'$other'") -eq 0) 'Create Account supports a login without a staff profile'
    $form=AccessForm $account $role @{'suppliers.view'='Deny';'services.view'='Allow'}
    $null=Post 'SaveUserAccess' $form
    Check ((Sql "SELECT COUNT(*) FROM UserPermissionOverride WHERE UserId=$account") -eq 0) 'Individual permission submissions rejected'
    $html=(GetPage "/Owner/StaffAccounts?userId=$account").Content
    Check ($html.Contains('Edit Login Account') -and $html.Contains('name="UserName"') -and $html.Contains('name="Email"') -and !$html.Contains('name="NewPassword"') -and !$html.Contains('data-edit-permission-toggle')) 'Account Edit shows login details without manual password or per-account permission controls'
    $null=Sql "INSERT UserPermissionOverride(UserId,PermissionId,IsAllowed) SELECT $account,PermissionId,CASE WHEN PermissionCode='suppliers.view' THEN 0 ELSE 1 END FROM Permission WHERE PermissionCode IN ('suppliers.view','services.view');"
    $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    SignIn "$mark-login@example.com" 'Test-Only!Password123'
    $accountSession=$session
    Check ((GetPage '/Owner/Suppliers').Content.Contains('id="master-new"')) 'Legacy deny cannot remove role access'
    $status=0
    try{$null=GetPage '/Owner/ServiceCatalog'}catch{$status=[int]$_.Exception.Response.StatusCode}
    Check ($status -eq 403) 'Legacy allow cannot grant access outside role'
    $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    SignIn "$mark-other@example.com" 'Test-Only!Password123'
    $otherSession=$session
    Check ((GetPage '/Owner/Suppliers').Content.Contains('id="master-new"')) 'Accounts with same role have same access'
    $session=$ownerSession
    $servicePermission=Sql "SELECT PermissionId FROM Permission WHERE PermissionCode='services.view'"
    $null=Post 'SaveRolePermissions' @{RoleId=$role;'PermissionIds[0]'=$servicePermission}
    foreach($staffSession in @($accountSession,$otherSession)){
        $session=$staffSession
        Check ((GetPage '/Owner/ServiceCatalog').Content.Contains('Service Catalog')) 'Role permission update reaches existing employee session'
        $status=0
        try{$null=GetPage '/Owner/Suppliers'}catch{$status=[int]$_.Exception.Response.StatusCode}
        Check ($status -eq 403) 'Role permission removal applies to every assigned account'
    }
    $session=$ownerSession
    $stale=AccessForm $account $role @{}
    $stale.IsActive='false'
    $null=Post 'SaveUserAccess' $stale
    $stale.IsActive='true'
    $null=Post 'SaveUserAccess' $stale
    Check (!(Sql "SELECT IsActive FROM Users WHERE Id=$account")) 'Stale account access save rejected'
    $null=Post 'SaveUserAccess' (AccessForm $account $role @{})
    $body.EmployeeProfileId=$profile;$body.RoleId='Owner'
    $null=Post 'EditEmployee' $body
    Check ((Sql "SELECT Role FROM Users WHERE Id=$account") -eq $role) 'Legacy profile form cannot escalate account role'
    $form=AccessForm $owner 'Owner' @{}
    $null=Post 'SaveUserAccess' $form
    Check ((Sql "SELECT Role FROM Users WHERE Id=$owner") -eq 'Owner') 'Self/protected account changes rejected'

    $second=[int](Sql "INSERT EmployeeProfile(FirstName,LastName,IsActive,IsArchived,IsDeleted) VALUES(N'$mark-noaccount',N'Staff',1,0,0); SELECT CAST(SCOPE_IDENTITY() AS int);")
    $exceptionForm=@{EmployeeProfileId=$second;Start='2035-01-02T09:00';End='2035-01-02T12:00';IsAvailable='true';Reason=$mark}
    $null=Post 'CreateAvailabilityException' $exceptionForm
    $exception=[int](Sql "SELECT EmployeeAvailabilityExceptionId FROM EmployeeAvailabilityException WHERE EmployeeProfileId=$second")
    Check ($exception -gt 0) 'Accountless staff can have special availability'
    Check ((Sql "SELECT DATEPART(hour,StartUtc) FROM EmployeeAvailabilityException WHERE EmployeeAvailabilityExceptionId=$exception") -eq 1) 'Local time converted to UTC'
    $null=Post 'CreateAvailabilityException' $exceptionForm
    Check ((Sql "SELECT COUNT(*) FROM EmployeeAvailabilityException WHERE EmployeeProfileId=$second") -eq 1) 'Overlapping exceptions rejected'
    $appointment=[int](Sql "INSERT Appointment(CustomerName,AssignedMechanicName,AppointmentStatus,ScheduledStart,ScheduledEnd,CreatedUtc) VALUES(N'$mark',N'$mark-noaccount Staff',N'Scheduled','2035-01-02T10:00:00','2035-01-02T11:00:00',SYSUTCDATETIME()); SELECT CAST(SCOPE_IDENTITY() AS int);")
    $null=Post 'DeleteAvailabilityException' @{Id=$exception;Expected=(ExceptionStamp $exception);Reason=$mark}
    Check ((Sql "SELECT COUNT(*) FROM EmployeeAvailabilityException WHERE EmployeeAvailabilityExceptionId=$exception") -eq 1) 'Cannot remove availability supporting an appointment'
    $exceptionForm.Id=$exception;$exceptionForm.Expected=ExceptionStamp $exception;$exceptionForm.IsAvailable='false'
    $null=Post 'UpdateAvailabilityException' $exceptionForm
    Check ([bool](Sql "SELECT IsAvailable FROM EmployeeAvailabilityException WHERE EmployeeAvailabilityExceptionId=$exception")) 'Leave cannot invalidate an existing appointment'
    $null=Sql "DELETE Appointment WHERE AppointmentId=$appointment;"
    $workorder=[int](Sql "INSERT WorkOrder(WorkOrderStatus,ScheduledStart,ScheduledEnd,CreatedUtc) VALUES(N'Scheduled','2035-01-02T10:00:00','2035-01-02T11:00:00',SYSUTCDATETIME()); SELECT CAST(SCOPE_IDENTITY() AS int);")
    $null=Sql "INSERT WorkOrderAssignment(WorkOrderId,MechanicName,AssignmentRole,AssignedUtc) VALUES($workorder,N'$mark-noaccount Staff','Lead',SYSUTCDATETIME());"
    $null=Post 'UpdateAvailabilityException' $exceptionForm
    Check ([bool](Sql "SELECT IsAvailable FROM EmployeeAvailabilityException WHERE EmployeeAvailabilityExceptionId=$exception")) 'Leave cannot invalidate a work-order assignment'
    $null=Sql "DELETE WorkOrderAssignment WHERE WorkOrderId=$workorder; DELETE WorkOrder WHERE WorkOrderId=$workorder;"
    $null=Post 'UpdateAvailabilityException' $exceptionForm
    Check (!(Sql "SELECT IsAvailable FROM EmployeeAvailabilityException WHERE EmployeeAvailabilityExceptionId=$exception")) 'Exception edit succeeds once bookings are clear'
    $null=Post 'DeleteAvailabilityException' @{Id=$exception;Expected=(ExceptionStamp $exception);Reason=$mark}
    Check ((Sql "SELECT COUNT(*) FROM EmployeeAvailabilityException WHERE EmployeeAvailabilityExceptionId=$exception") -eq 0) 'Exception delete succeeds and preserves audit'
    Check ((Sql "SELECT COUNT(*) FROM AuditLogEntry WHERE UserName=N'$mark-owner' AND EntityName='EmployeeAvailabilityException'") -ge 3) 'Exception lifecycle audited'
    $status=0
    $accountForm.Remove('__RequestVerificationToken')
    try{$null=Invoke-WebRequest "$BaseUrl/Owner/CreateStaffAccount" -Method Post -WebSession $session -Body $accountForm -UseBasicParsing}catch{$status=[int]$_.Exception.Response.StatusCode}
    Check ($status -eq 400) 'Account creation requires antiforgery token'
    $null=Post 'SaveUserAccess' (AccessForm $account $role @{})
    $null=Post 'SaveRolePermissions' @{RoleId=$role}
    Check ((Sql "SELECT COUNT(*) FROM RolePermission WHERE RoleId=N'$role'") -eq 0) 'An intentionally empty role can be saved'
    $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $deniedPage=''
    try { SignIn "$mark-login@example.com" 'Test-Only!Password123' }
    catch {
        if ([int]$_.Exception.Response.StatusCode -ne 403) { throw }
        $reader=New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        try { $deniedPage=$reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    Check ($deniedPage.Contains('Account signed in') -and $deniedPage.Contains('no module access yet')) 'No-permission login explains missing access instead of suggesting invalid credentials'
    $session=$ownerSession
    $details=AccessForm $other $role @{}
    $details.UpdateDetails='true';$details.Email="$mark-updated@example.com";$details.UserName="$mark-updated"
    $null=Post 'SaveUserAccess' $details
    Check ((Sql "SELECT Email FROM Users WHERE Id=$other") -eq "$mark-updated@example.com" -and
        (Sql "SELECT UserName FROM Users WHERE Id=$other") -eq "$mark-updated") 'Account Edit saves login email and username'
    Write-Output 'Staff accounts, overrides and availability checks completed.'
} finally {
    $null=Sql "DELETE WorkOrderAssignment WHERE WorkOrderId=$workorder; DELETE WorkOrder WHERE WorkOrderId=$workorder; DELETE Appointment WHERE AppointmentId=$appointment; DELETE EmployeeAvailabilityException WHERE EmployeeProfileId IN ($profile,$second); DELETE EmployeeAvailability WHERE EmployeeProfileId IN ($profile,$second); DELETE EmployeeProfile WHERE EmployeeProfileId IN ($profile,$second); DELETE UserPermissionOverride WHERE UserId IN ($owner,$account,$other); DELETE AccountSecurity WHERE UserId IN ($owner,$account,$other); DELETE Users WHERE Id IN ($owner,$account,$other); DELETE RolePermission WHERE RoleId=N'$role';"
    if ($originalRolePermissions) { $null=Sql "INSERT RolePermission(RoleId,PermissionId) SELECT N'$role', PermissionId FROM Permission WHERE PermissionId IN ($originalRolePermissions);" }
    $db.Dispose()
}
