param([string]$BaseUrl='http://127.0.0.1:5099',[string]$ConnectionString='Server=.\SQLEXPRESS;Database=Bakyard_Mech;Integrated Security=True;TrustServerCertificate=True',[string]$Login='admin123',[string]$Password='admin123')
$ErrorActionPreference='Stop'
$db=New-Object System.Data.SqlClient.SqlConnection $ConnectionString
$db.Open()
function Sql($query){$cmd=$db.CreateCommand();$cmd.CommandText=$query;try{$cmd.ExecuteScalar()}finally{$cmd.Dispose()}}
function Check($ok,$message){if(!$ok){throw "FAIL: $message"};Write-Output "PASS: $message"}
function Token($html){[regex]::Match($html,'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value}
$session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
function GetPage($path){Invoke-WebRequest "$BaseUrl$path" -WebSession $session -UseBasicParsing}
function Post($action,$body){$body['__RequestVerificationToken']=Token (GetPage '/Owner/Appointments').Content;Invoke-WebRequest "$BaseUrl/Owner/$action" -Method Post -Body $body -WebSession $session -UseBasicParsing}
function Stamp($path,$dialog){$html=(GetPage $path).Content;[regex]::Match($html,"(?s)id=`"$dialog`".*?name=`"Expected`" value=`"([^`"]+)`"").Groups[1].Value}
function AStamp($id){Stamp '/Owner/Appointments' "appointment-edit-$id"}
function VStamp{Stamp "/Owner/VehicleHistory/$vehicle" 'transfer-owner'}
$mark='IntakeSmoke-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
$owner=$customer=$next=$vehicle=$service=$second=$staff=$reader=0
try {
    $owner=[int](Sql "INSERT Users(Email,UserName,PasswordHash,Role,IsActive,CreatedAt) SELECT N'$mark',N'$mark',PasswordHash,N'Owner',1,GETUTCDATE() FROM Users WHERE Email=N'$Login'; SELECT CAST(SCOPE_IDENTITY() AS int);")
    $page=GetPage '/Account/Login'
    $page=Invoke-WebRequest "$BaseUrl/Account/Login" -Method Post -Body @{Email=$mark;Password=$Password;__RequestVerificationToken=(Token $page.Content)} -WebSession $session -UseBasicParsing
    $key=[regex]::Match($page.Content,'<code>([A-Z2-7]+)</code>').Groups[1].Value
    if($key){
        $code=python -c "import base64,hmac,hashlib,time,struct,sys; d=hmac.new(base64.b32decode(sys.argv[1]),struct.pack('>Q',int(time.time())//30),hashlib.sha1).digest(); o=d[-1]&15; print(str((int.from_bytes(d[o:o+4],'big')&0x7fffffff)%1000000).zfill(6))" $key
        $null=Invoke-WebRequest "$BaseUrl/Account/Mfa" -Method Post -Body @{Code=$code;__RequestVerificationToken=(Token $page.Content)} -WebSession $session -UseBasicParsing
    }
    Check ((GetPage '/Owner/VehicleHistory').Content.Contains('register-vehicle')) 'Vehicle registry and registration modal render'
    Check ((GetPage '/Owner/Appointments').Content.Contains('data-calendar-mode')) 'Appointment calendar and modals render'
    $customer=[int](Sql "INSERT Customer(FirstName,MiddleName,LastName,CreatedUtc,IsActive,IsArchived,IsDeleted) VALUES(N'$mark',N'Middle',N'First',GETUTCDATE(),1,0,0); SELECT CAST(SCOPE_IDENTITY() AS int);")
    $next=[int](Sql "INSERT Customer(FirstName,LastName,CreatedUtc,IsActive,IsArchived,IsDeleted) VALUES(N'$mark',N'Second',GETUTCDATE(),1,0,0); SELECT CAST(SCOPE_IDENTITY() AS int);")
    $service=[int](Sql "INSERT Service(ServiceName,IsActive) VALUES(N'$mark-one',1); SELECT CAST(SCOPE_IDENTITY() AS int);")
    $second=[int](Sql "INSERT Service(ServiceName,IsActive) VALUES(N'$mark-two',1); SELECT CAST(SCOPE_IDENTITY() AS int);")
    $staff=[int](Sql "INSERT EmployeeProfile(FirstName,LastName,IsActive,IsArchived,IsDeleted) VALUES(N'$mark',N'Tech',1,0,0); SELECT CAST(SCOPE_IDENTITY() AS int);")
    $vform=@{CustomerId=$customer;Make='Test';Model='Intake';Year=2024;VIN=$mark;LicensePlate=$mark;Odometer=100}
    $null=Post 'AddVehicle' $vform
    $vehicle=[int](Sql "SELECT VehicleId FROM Vehicle WHERE VIN=N'$mark'")
    Check ($vehicle -gt 0) 'Vehicle registration saved'
    Check ((Sql "SELECT COUNT(*) FROM VehicleOdometerHistory WHERE VehicleId=$vehicle") -eq 1) 'Initial odometer recorded in history'
    Check ((Sql "SELECT OwnerMiddleName FROM VehicleOwnership WHERE VehicleId=$vehicle") -eq 'Middle') 'Owner name snapshot includes middle name'
    $null=Post 'AddVehicle' $vform
    Check ((Sql "SELECT COUNT(*) FROM Vehicle WHERE VIN=N'$mark'") -eq 1) 'Duplicate VIN or plate rejected'
    $stamp=VStamp
    $null=Post 'RecordVehicleOdometer' @{VehicleId=$vehicle;Expected=$stamp;Odometer=99}
    Check ((Sql "SELECT Odometer FROM Vehicle WHERE VehicleId=$vehicle") -eq 100) 'Decreasing mileage rejected'
    $null=Post 'RecordVehicleOdometer' @{VehicleId=$vehicle;Expected=$stamp;Odometer=110}
    $null=Post 'RecordVehicleOdometer' @{VehicleId=$vehicle;Expected=$stamp;Odometer=120}
    Check ((Sql "SELECT Odometer FROM Vehicle WHERE VehicleId=$vehicle") -eq 110) 'Stale odometer write rejected'
    $stamp=VStamp
    $null=Post 'RecordVehicleOdometer' @{VehicleId=$vehicle;Expected=$stamp;Odometer=110}
    $null=Post 'RecordVehicleOdometer' @{VehicleId=$vehicle;Expected=$stamp;Odometer=110}
    Check ((Sql "SELECT COUNT(*) FROM VehicleOdometerHistory WHERE VehicleId=$vehicle") -eq 3) 'Same-reading replay cannot duplicate odometer history'
    $transfer=@{VehicleId=$vehicle;Expected=(VStamp);CustomerId=$next;Reason='Verified sale';AuthorityVerified='false'}
    $null=Post 'TransferVehicleOwnership' $transfer
    Check ((Sql "SELECT COUNT(*) FROM VehicleOwnership WHERE VehicleId=$vehicle") -eq 1) 'Ownership transfer requires authority verification'
    $transfer.AuthorityVerified='true';$null=Post 'TransferVehicleOwnership' $transfer
    Check ((Sql "SELECT COUNT(*) FROM VehicleOwnership WHERE VehicleId=$vehicle") -eq 2) 'Transfer appends ownership history'
    Check ((Sql "SELECT COUNT(*) FROM VehicleOwnership WHERE VehicleId=$vehicle AND OwnershipEndUtc IS NULL AND CustomerId=$next") -eq 1) 'Exactly one current owner after transfer'
    Check ((Sql "SELECT OwnerLastName FROM VehicleOwnership WHERE VehicleId=$vehicle AND CustomerId=$customer") -eq 'First') 'Previous owner snapshot retained'

    $form=@{CustomerId=$customer;VehicleId=$vehicle;ScheduledDate='2036-02-04';StartTime='09:00';EndTime='10:00';'ServiceIds[0]'=$service;'ServiceIds[1]'=$second;PreferredEmployeeProfileId=$staff;Notes=$mark;AppointmentStatus='Completed';AppointmentType='Forged';ViewMode='list'}
    $null=Post 'CreateAppointment' $form
    Check ((Sql "SELECT COUNT(*) FROM Appointment WHERE VehicleId=$vehicle") -eq 0) 'Former owner cannot book transferred vehicle'
    $form.CustomerId=$next;$null=Post 'CreateAppointment' $form
    $appointment=[int](Sql "SELECT AppointmentId FROM Appointment WHERE VehicleId=$vehicle")
    Check ($appointment -gt 0) 'Current owner can create appointment'
    Check ((Sql "SELECT AppointmentStatus FROM Appointment WHERE AppointmentId=$appointment") -eq 'Scheduled') 'Client cannot forge advanced status'
    Check ((Sql "SELECT AppointmentType FROM Appointment WHERE AppointmentId=$appointment") -eq 'Scheduled') 'Appointment type stores entry path rather than service name'
    Check ((Sql "SELECT COUNT(*) FROM AppointmentService WHERE AppointmentId=$appointment AND ServiceId IN ($service,$second)") -eq 2) 'Multiple requested services linked and snapshotted'
    Check ((Sql "SELECT PreferredMechanicName FROM Appointment WHERE AppointmentId=$appointment") -eq "$mark Tech") 'Preferred accountless mechanic snapshot saved'
    Check ([DBNull]::Value.Equals((Sql "SELECT AssignedMechanicName FROM Appointment WHERE AppointmentId=$appointment"))) 'Preference does not assign or reserve mechanic'
    $null=Post 'CreateAppointment' $form
    Check ((Sql "SELECT COUNT(*) FROM Appointment WHERE VehicleId=$vehicle") -eq 1) 'Overlapping vehicle booking rejected'
    $transfer.Expected=VStamp;$transfer.CustomerId=$customer;$null=Post 'TransferVehicleOwnership' $transfer
    Check ((Sql "SELECT COUNT(*) FROM VehicleOwnership WHERE VehicleId=$vehicle") -eq 2) 'Open appointment blocks ownership transfer'
    $form.AppointmentId=$appointment;$form.Expected=AStamp $appointment;$form.AssignedEmployeeProfileId=$staff
    $null=Post 'EditAppointment' $form
    Check ([DBNull]::Value.Equals((Sql "SELECT AssignedMechanicName FROM Appointment WHERE AppointmentId=$appointment"))) 'Assignment outside staff availability rejected'
    $form.Remove('AssignedEmployeeProfileId');$form.StartTime='11:00';$form.EndTime='12:00';$form.Notes='Edited';$null=Post 'EditAppointment' $form
    Check ((Sql "SELECT Notes FROM Appointment WHERE AppointmentId=$appointment") -eq 'Edited') 'Appointment edit persists'
    $form.Notes='Stale';$null=Post 'EditAppointment' $form
    Check ((Sql "SELECT Notes FROM Appointment WHERE AppointmentId=$appointment") -eq 'Edited') 'Stale appointment edit rejected'
    $cancel=@{AppointmentId=$appointment;Expected=(AStamp $appointment);Reason='Customer cancelled'}
    $null=Post 'CancelAppointment' $cancel
    Check ((Sql "SELECT AppointmentStatus FROM Appointment WHERE AppointmentId=$appointment") -eq 'Cancelled') 'Cancellation retains appointment'
    Check ((Sql "SELECT COUNT(*) FROM AppointmentService WHERE AppointmentId=$appointment") -eq 2) 'Cancellation retains requested-service history'
    $form.Remove('AppointmentId');$form.Remove('Expected');$null=Post 'CreateAppointment' $form
    $appointment=[int](Sql "SELECT MAX(AppointmentId) FROM Appointment WHERE VehicleId=$vehicle")
    $checkin=@{AppointmentId=$appointment;Expected=(AStamp $appointment);Odometer=120;IdentityVerified='true'}
    $null=Post 'CheckInAppointment' $checkin
    Check ([DBNull]::Value.Equals((Sql "SELECT CheckedInUtc FROM Appointment WHERE AppointmentId=$appointment"))) 'Future appointment cannot be checked in early'
    # Move only this test fixture to today to exercise the arrival transition.
    $today=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow,'Singapore Standard Time').ToString('yyyy-MM-dd')
    $null=Sql "UPDATE Appointment SET ScheduledStart='$($today)T10:00:00',ScheduledEnd='$($today)T11:00:00' WHERE AppointmentId=$appointment"
    $checkin.Expected=AStamp $appointment;$checkin.IdentityVerified='false';$null=Post 'CheckInAppointment' $checkin
    Check ([DBNull]::Value.Equals((Sql "SELECT CheckedInUtc FROM Appointment WHERE AppointmentId=$appointment"))) 'Identity verification required'
    $checkin.IdentityVerified='true';$null=Post 'CheckInAppointment' $checkin
    Check ((Sql "SELECT AppointmentStatus FROM Appointment WHERE AppointmentId=$appointment") -eq 'Arrived') 'Check-in moves visit into service queue'
    Check ((Sql "SELECT COUNT(*) FROM VehicleOdometerHistory WHERE AppointmentId=$appointment") -eq 1) 'Check-in appends mileage with appointment link'
    $null=Post 'CheckInAppointment' $checkin
    Check ((Sql "SELECT COUNT(*) FROM VehicleOdometerHistory WHERE AppointmentId=$appointment") -eq 1) 'Repeated check-in cannot duplicate history'
    $null=Sql "UPDATE Appointment SET AppointmentStatus='Completed' WHERE AppointmentId=$appointment"
    $walkin=@{CustomerId=$next;VehicleId=$vehicle;IsWalkIn='true';IdentityVerified='true';Odometer=130;'ServiceIds[0]'=$service}
    $walkin.IdentityVerified='false';$null=Post 'CreateAppointment' $walkin
    Check ((Sql "SELECT COUNT(*) FROM Appointment WHERE VehicleId=$vehicle AND AppointmentType='Walk-in'") -eq 0) 'Invalid walk-in rolls back appointment and services'
    $walkin.IdentityVerified='true'
    $null=Post 'CreateAppointment' $walkin
    Check ((Sql "SELECT COUNT(*) FROM Appointment WHERE VehicleId=$vehicle AND AppointmentType='Walk-in' AND CheckedInUtc IS NOT NULL") -eq 1) 'Walk-in atomically creates appointment and check-in'
    Check ((Sql "SELECT COUNT(*) FROM AuditLogEntry WHERE EntityName='Vehicle' AND EntityId='$vehicle'") -ge 4) 'Vehicle changes audited'

    $token=Token (GetPage '/Owner/Appointments').Content
    $status=0;try{$null=Invoke-WebRequest "$BaseUrl/Owner/CheckInAppointment" -Method Post -Body @{AppointmentId=$appointment} -WebSession $session -UseBasicParsing}catch{$status=[int]$_.Exception.Response.StatusCode}
    Check ($status -eq 400) 'Missing anti-forgery token rejected'
    $null=Sql "INSERT AppRole(RoleKey,RoleName,DisplayOrder,IsSystemProtected,IsActive) VALUES(N'$mark',N'$mark',999,0,1); INSERT RolePermission(RoleId,PermissionId) SELECT N'$mark',PermissionId FROM Permission WHERE PermissionCode IN ('vehicles.view','appointments.view'); UPDATE Users SET Role=N'$mark' WHERE Id=$owner;"
    Check ((GetPage '/Owner/VehicleHistory').Content.Contains('name="Password"')) 'Role change invalidates the existing session'
    $reader=[int](Sql "INSERT Users(Email,UserName,PasswordHash,Role,IsActive,CreatedAt) SELECT N'$mark-reader',N'$mark-reader',PasswordHash,N'$mark',1,GETUTCDATE() FROM Users WHERE Email=N'$Login'; SELECT CAST(SCOPE_IDENTITY() AS int);")
    $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $token=Token (GetPage '/Account/Login').Content
    try { $null=Invoke-WebRequest "$BaseUrl/Account/Login" -Method Post -Body @{Email="$mark-reader";Password=$Password;__RequestVerificationToken=$token} -WebSession $session -UseBasicParsing }
    catch { if([int]$_.Exception.Response.StatusCode -ne 403){throw} } # Landing dashboard is intentionally not granted.
    $html=(GetPage '/Owner/VehicleHistory').Content
    Check ($html.Contains('Vehicle Records') -and !$html.Contains('id="register-vehicle"')) 'Read-only permission hides vehicle creation'
    $html=(GetPage '/Owner/Appointments').Content
    Check ($html.Contains('data-calendar-mode') -and !$html.Contains('id="appointment-new"')) 'Read-only permission hides appointment creation'
    $status=0;try{$null=Invoke-WebRequest "$BaseUrl/Owner/RecordVehicleOdometer" -Method Post -Body @{VehicleId=$vehicle;Odometer=999;Expected='x';__RequestVerificationToken=$token} -WebSession $session -UseBasicParsing}catch{$status=[int]$_.Exception.Response.StatusCode}
    Check ($status -eq 403) 'Read-only user cannot bypass permission through direct POST'
    Write-Output 'Vehicle intake smoke tests completed.'
} finally {
    if($vehicle -gt 0){$null=Sql "DELETE VehicleOdometerHistory WHERE VehicleId=$vehicle; DELETE QueueStatusHistory WHERE AppointmentId IN (SELECT AppointmentId FROM Appointment WHERE VehicleId=$vehicle); DELETE AppointmentService WHERE AppointmentId IN (SELECT AppointmentId FROM Appointment WHERE VehicleId=$vehicle); DELETE Appointment WHERE VehicleId=$vehicle; DELETE VehicleOwnership WHERE VehicleId=$vehicle; DELETE Vehicle WHERE VehicleId=$vehicle;"}
    $null=Sql "DELETE EmployeeProfile WHERE EmployeeProfileId=$staff; DELETE Service WHERE ServiceId IN ($service,$second); DELETE Customer WHERE CustomerId IN ($customer,$next); DELETE AccountSecurity WHERE UserId IN ($owner,$reader); DELETE Users WHERE Id IN ($owner,$reader); DELETE RolePermission WHERE RoleId=N'$mark'; DELETE AppRole WHERE RoleKey=N'$mark';"
    $db.Dispose()
}
