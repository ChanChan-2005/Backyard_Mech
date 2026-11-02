param([string]$BaseUrl='http://127.0.0.1:5099',[string]$ConnectionString='Server=.\SQLEXPRESS;Database=Bakyard_Mech;Integrated Security=True;TrustServerCertificate=True',[string]$Login='admin123',[string]$Password='admin123')
$ErrorActionPreference='Stop'
$db=New-Object System.Data.SqlClient.SqlConnection $ConnectionString
$db.Open()
function Sql($query){$cmd=$db.CreateCommand();$cmd.CommandText=$query;try{$cmd.ExecuteScalar()}finally{$cmd.Dispose()}}
function Check($ok,$message){if(!$ok){throw "FAIL: $message"};Write-Output "PASS: $message"}
function Token($html){[regex]::Match($html,'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value}
$session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
function GetPage($path){Invoke-WebRequest "$BaseUrl$path" -WebSession $session -UseBasicParsing}
function Post($action,$body){$body['__RequestVerificationToken']=Token (GetPage '/Owner/StaffAccounts').Content;Invoke-WebRequest "$BaseUrl/Owner/$action" -Method Post -Body $body -WebSession $session -UseBasicParsing}
function SignIn($email,$password){
    $page=GetPage '/Account/Login'
    $script:loginToken=Token $page.Content
    $page=Invoke-WebRequest "$BaseUrl/Account/Login" -Method Post -Body @{Email=$email;Password=$password;__RequestVerificationToken=(Token $page.Content)} -WebSession $session -UseBasicParsing
    $key=[regex]::Match($page.Content,'<code>([A-Z2-7]+)</code>').Groups[1].Value
    if($key){
        $code=python -c "import base64,hmac,hashlib,time,struct,sys; d=hmac.new(base64.b32decode(sys.argv[1]),struct.pack('>Q',int(time.time())//30),hashlib.sha1).digest(); o=d[-1]&15; print(str((int.from_bytes(d[o:o+4],'big')&0x7fffffff)%1000000).zfill(6))" $key
        $null=Invoke-WebRequest "$BaseUrl/Account/Mfa" -Method Post -Body @{Code=$code;__RequestVerificationToken=(Token $page.Content)} -WebSession $session -UseBasicParsing
    }
}
function AccessForm($id,$role,$choices){
    $html=(GetPage "/Owner/StaffAccounts?userId=$id").Content
    $stamp=[regex]::Match($html,'name="Expected" value="([^"]+)"').Groups[1].Value
    $body=@{UserId=$id;Expected=$stamp;RoleKey=$role;IsActive='true';Reason=$mark}
    $matches=[regex]::Matches($html,'name="Permissions\[(\d+)\].PermissionId" value="(\d+)"')
    foreach($match in $matches){$i=$match.Groups[1].Value;$permission=$match.Groups[2].Value;$code=[string](Sql "SELECT PermissionCode FROM Permission WHERE PermissionId=$permission");$body["Permissions[$i].PermissionId"]=$permission;$body["Permissions[$i].Mode"]=if($choices.ContainsKey($code)){$choices[$code]}else{'Inherit'}}
    return $body
}
function ExceptionStamp($id){[regex]::Match((GetPage '/Owner/StaffAvailability').Content,"(?s)id=`"exception-$id`".*?name=`"Expected`" value=`"([^`"]+)`"").Groups[1].Value}
$mark='StaffSmoke-'+[Guid]::NewGuid().ToString('N').Substring(0,16)
$role=$mark
$owner=$profile=$second=$account=$other=$appointment=$workorder=0
try {
    $owner=[int](Sql "INSERT Users(Email,UserName,PasswordHash,Role,IsActive,CreatedAt) SELECT N'$mark-owner',N'$mark-owner',PasswordHash,N'Owner',1,GETUTCDATE() FROM Users WHERE Email=N'$Login'; SELECT CAST(SCOPE_IDENTITY() AS int);")
    SignIn "$mark-owner" $Password
    $ownerSession=$session
    Check ((GetPage '/Owner/StaffAccounts').Content.Contains('Create Staff Account')) 'Create-account modal renders'
    Check ((GetPage '/Owner/StaffAvailability').Content.Contains('Special Availability')) 'Exceptions page renders'
    Check ((Sql "SELECT COUNT(*) FROM AppRole WHERE RoleName IN ('Shop Manager','Service Advisor','Mechanic','Parts Clerk','Tool Room Attendant','Quality Control Inspector','Accounting Clerk')") -eq 7) 'Seven PDF roles present'
    $body=@{FirstName=$mark;LastName='Staff';StreetAddress='1 Test Street';City='Test';PostalCode='1000';Phone='555-0100';Email="$mark@example.com";JobTitle='Mechanic';HourlyRate=100}
    $null=Post 'AddEmployee' $body
    $profile=[int](Sql "SELECT EmployeeProfileId FROM EmployeeProfile WHERE FirstName=N'$mark'")
    Check ($profile -gt 0 -and [DBNull]::Value.Equals((Sql "SELECT UserId FROM EmployeeProfile WHERE EmployeeProfileId=$profile"))) 'Creating a profile does not create an account'
    Check ((Sql "SELECT COUNT(*) FROM Users WHERE Email=N'$mark@example.com'") -eq 0) 'Profile contact email is not a login'
    $null=Sql "INSERT AppRole(RoleKey,RoleName,DisplayOrder,IsSystemProtected,IsActive) VALUES(N'$role',N'$role',999,0,1); INSERT RolePermission(RoleId,PermissionId) SELECT N'$role',PermissionId FROM Permission WHERE PermissionCode IN ('suppliers.view','suppliers.create');"
    $accountForm=@{EmployeeProfileId=$profile;Email="$mark-login@example.com";UserName=$mark;Password='Test-Only!Password123';RoleKey=$role}
    $null=Post 'CreateStaffAccount' $accountForm
    $account=[int](Sql "SELECT Id FROM Users WHERE UserName=N'$mark'")
    Check ($account -gt 0 -and (Sql "SELECT UserId FROM EmployeeProfile WHERE EmployeeProfileId=$profile") -eq "$account") 'Explicit account creation links selected profile'
    Check ((Sql "SELECT PasswordHash FROM Users WHERE Id=$account") -ne 'Test-Only!Password123') 'Password stored as hash'
    $null=Post 'CreateStaffAccount' $accountForm
    Check ((Sql "SELECT COUNT(*) FROM Users WHERE UserName=N'$mark'") -eq 1) 'Duplicate account for profile rejected'
    $other=[int](Sql "INSERT Users(Email,UserName,PasswordHash,Role,IsActive,CreatedAt) SELECT N'$mark-other@example.com',N'$mark-other',PasswordHash,Role,1,GETUTCDATE() FROM Users WHERE Id=$account; SELECT CAST(SCOPE_IDENTITY() AS int);")
    $defaults=[int](Sql "SELECT COUNT(*) FROM RolePermission WHERE RoleId=N'$role'")
    $form=AccessForm $account $role @{'suppliers.view'='Deny';'services.view'='Allow'}
    $null=Post 'SaveUserAccess' $form
    Check ((Sql "SELECT COUNT(*) FROM UserPermissionOverride WHERE UserId=$account") -eq 2) 'Allow and Deny saved per user'
    Check ((Sql "SELECT COUNT(*) FROM RolePermission WHERE RoleId=N'$role'") -eq $defaults) 'Role defaults unchanged'
    $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    SignIn "$mark-login@example.com" 'Test-Only!Password123'
    $accountSession=$session
    $page=GetPage '/Owner/ServiceCatalog'
    Check ($page.Content.Contains('Service Catalog') -and !$page.Content.Contains('href="/Owner/Suppliers"')) 'Allow exposes module; Deny hides sidebar module'
    $status=0
    try{$null=GetPage '/Owner/Suppliers'}catch{$status=[int]$_.Exception.Response.StatusCode}
    Check ($status -eq 403) 'Denied module cannot be opened directly'
    $status=0
    try{$null=Invoke-WebRequest "$BaseUrl/Owner/CreateSupplier" -Method Post -WebSession $session -Body @{Name=$mark;__RequestVerificationToken=$script:loginToken} -UseBasicParsing}catch{$status=[int]$_.Exception.Response.StatusCode}
    Check ($status -eq 403) 'Deny module View also blocks inherited mutation'
    $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    SignIn "$mark-other@example.com" 'Test-Only!Password123'
    Check ((GetPage '/Owner/Suppliers').Content.Contains('id="master-new"')) 'Second user with same role remains unaffected'
    $session=$ownerSession
    $null=Post 'SaveUserAccess' (AccessForm $account $role @{})
    Check ((Sql "SELECT COUNT(*) FROM UserPermissionOverride WHERE UserId=$account") -eq 0) 'Inherit removes override rows'
    $session=$accountSession
    Check ((GetPage '/Owner/Suppliers').Content.Contains('id="master-new"')) 'Existing session immediately inherits restored role defaults'
    $session=$ownerSession
    $stale=AccessForm $account $role @{'services.view'='Allow'}
    $null=Post 'SaveUserAccess' $stale
    $null=Post 'SaveUserAccess' $stale
    Check ((Sql "SELECT COUNT(*) FROM UserPermissionOverride WHERE UserId=$account") -eq 1) 'Stale access save rejected without duplication'
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
    Write-Output 'Staff accounts, overrides and availability checks completed.'
} finally {
    $null=Sql "DELETE WorkOrderAssignment WHERE WorkOrderId=$workorder; DELETE WorkOrder WHERE WorkOrderId=$workorder; DELETE Appointment WHERE AppointmentId=$appointment; DELETE EmployeeAvailabilityException WHERE EmployeeProfileId IN ($profile,$second); DELETE EmployeeAvailability WHERE EmployeeProfileId IN ($profile,$second); DELETE EmployeeProfile WHERE EmployeeProfileId IN ($profile,$second); DELETE UserPermissionOverride WHERE UserId IN ($owner,$account,$other); DELETE AccountSecurity WHERE UserId IN ($owner,$account,$other); DELETE Users WHERE Id IN ($owner,$account,$other); DELETE RolePermission WHERE RoleId=N'$role'; DELETE AppRole WHERE RoleKey=N'$role';"
    $db.Dispose()
}
