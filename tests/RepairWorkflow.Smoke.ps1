param(
    [string]$BaseUrl = 'http://127.0.0.1:5099',
    [string]$ConnectionString = 'Server=.\SQLEXPRESS;Database=Bakyard_Mech;Integrated Security=True;TrustServerCertificate=True',
    [string]$Login = 'admin123',
    [string]$Password = 'admin123'
)
$ErrorActionPreference = 'Stop'
$db = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
$db.Open()
function Sql([string]$query) {
    $cmd = $db.CreateCommand()
    $cmd.CommandText = $query
    try { $cmd.ExecuteScalar() } finally { $cmd.Dispose() }
}
function Check([bool]$condition, [string]$message) {
    if (!$condition) { throw "FAILED: $message" }
    Write-Output "PASS: $message"
}
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
function GetPage([string]$path) {
    Invoke-WebRequest "$BaseUrl$path" -WebSession $session -UseBasicParsing
}
function Post([string]$action, [hashtable]$body) {
    if($action -in @('AddStockMovement','EditStockMovement','AddLabour','EditLabour','StartLabourTimer')) { $body['SubmissionToken'] = [Guid]::NewGuid().ToString() }
    $page = GetPage '/Owner/EstimateApprovals'
    $body['__RequestVerificationToken'] = [regex]::Match($page.Content, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
    if (!$body['__RequestVerificationToken']) { throw 'No antiforgery token found.' }
    try { Invoke-WebRequest "$BaseUrl/Owner/$action" -Method Post -Body $body -WebSession $session -UseBasicParsing }
    catch { Write-Host "Request failed: $action"; if ($_.ErrorDetails.Message) { Write-Host ($_.ErrorDetails.Message.Substring(0, [Math]::Min(3500, $_.ErrorDetails.Message.Length))) }; throw }
}
function PostCustomerApproval([string]$token, [hashtable]$body) {
    $page = GetPage "/CustomerEstimateApproval/Review?token=$token"
    $body['__RequestVerificationToken'] = [regex]::Match($page.Content, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
    if (!$body['__RequestVerificationToken']) { throw 'No customer approval antiforgery token found.' }
    Invoke-WebRequest "$BaseUrl/CustomerEstimateApproval/Submit" -Method Post -Body $body -WebSession $session -UseBasicParsing
}
function ConcurrentPost([string]$action, [hashtable]$body) {
    Add-Type -AssemblyName System.Net.Http
    $page=GetPage '/Owner/EstimateApprovals'
    $body['__RequestVerificationToken']=[regex]::Match($page.Content,'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
    $values=New-Object 'System.Collections.Generic.Dictionary[string,string]'
    foreach($key in $body.Keys) { $values.Add($key,[string]$body[$key]) }
    $handler=New-Object System.Net.Http.HttpClientHandler
    $handler.CookieContainer=$session.Cookies
    $client=New-Object System.Net.Http.HttpClient $handler
    try {
        $a=New-Object System.Net.Http.FormUrlEncodedContent (,$values)
        $b=New-Object System.Net.Http.FormUrlEncodedContent (,$values)
        $first=$client.PostAsync("$BaseUrl/Owner/$action",$a)
        $second=$client.PostAsync("$BaseUrl/Owner/$action",$b)
        [System.Threading.Tasks.Task]::WaitAll(@($first,$second))
        Check ($first.Result.IsSuccessStatusCode -and $second.Result.IsSuccessStatusCode) 'Concurrent requests handled without server failure'
    } finally { $client.Dispose(); $handler.Dispose() }
}
$marker = 'WorkflowSmoke-' + [Guid]::NewGuid().ToString('N')
$appointment = $employee = $bay = $part = $estimate = $order = $invoice = $testUser = $tax = $ownerUser = $tool = $customer = $vehicle = 0
$role = 'Smoke-' + [Guid]::NewGuid().ToString('N')
try {
    $ownerUser = [int](Sql "INSERT Users(Email,UserName,PasswordHash,Role,IsActive,CreatedAt) SELECT N'$marker',N'$marker',PasswordHash,N'Owner',1,GETUTCDATE() FROM Users WHERE Email=N'$Login'; SELECT CAST(SCOPE_IDENTITY() AS int);")
    $page = GetPage '/Account/Login'
    $token = [regex]::Match($page.Content, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
    $mfaPage = Invoke-WebRequest "$BaseUrl/Account/Login" -Method Post -Body @{ Email=$marker; Password=$Password; __RequestVerificationToken=$token } -WebSession $session -UseBasicParsing
    $key = [regex]::Match($mfaPage.Content, '<code>([A-Z2-7]+)</code>').Groups[1].Value
    Check ($key.Length -gt 10) 'Privileged login requires authenticator enrollment'
    $code = python -c "import base64,hmac,hashlib,time,struct,sys; d=hmac.new(base64.b32decode(sys.argv[1]),struct.pack('>Q',int(time.time())//30),hashlib.sha1).digest(); o=d[-1]&15; print(str((int.from_bytes(d[o:o+4],'big')&0x7fffffff)%1000000).zfill(6))" $key
    $token = [regex]::Match($mfaPage.Content, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
    $recoveryPage = Invoke-WebRequest "$BaseUrl/Account/Mfa" -Method Post -Body @{Code=$code;__RequestVerificationToken=$token} -WebSession $session -UseBasicParsing
    Check ($recoveryPage.Content.Contains('Keep Your Recovery Code')) 'MFA validates authenticator and issues recovery code'
    $recoveryCode=[regex]::Match($recoveryPage.Content,'<code>([A-F0-9]+)</code>').Groups[1].Value
    $verifiedSession=$session
    $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $page=GetPage '/Account/Login'
    $token=[regex]::Match($page.Content,'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
    $mfaPage=Invoke-WebRequest "$BaseUrl/Account/Login" -Method Post -Body @{Email=$marker;Password=$Password;__RequestVerificationToken=$token} -WebSession $session -UseBasicParsing
    Check (!$mfaPage.Content.Contains('<code>')) 'Enrolled authenticator secret is never displayed at subsequent login'
    $token=[regex]::Match($mfaPage.Content,'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
    $replay=Invoke-WebRequest "$BaseUrl/Account/Mfa" -Method Post -Body @{Code=$code;__RequestVerificationToken=$token} -WebSession $session -UseBasicParsing
    Check ($replay.Content.Contains('already-used')) 'Authenticator code replay is rejected'
    Check ((GetPage '/Owner/EstimateApprovals').Content.Contains('Welcome Back')) 'Password-only pending session cannot access protected pages'
    $recovered=Invoke-WebRequest "$BaseUrl/Account/Mfa" -Method Post -Body @{Code=$recoveryCode;__RequestVerificationToken=$token} -WebSession $session -UseBasicParsing
    Check ($recovered.Content.Contains('Keep Your Recovery Code') -and !$recovered.Content.Contains($recoveryCode)) 'Recovery code consumed and replaced after successful recovery'
    $session=$verifiedSession
    $customer = [int](Sql "INSERT Customer(FirstName,LastName) VALUES(N'$marker',N'Customer'); SELECT CAST(SCOPE_IDENTITY() AS int);")
    $vehicle = [int](Sql "INSERT Vehicle(VIN,Make,Model,Year,LicensePlate,OwnerFullName,CreatedUtc) VALUES(N'$($marker.Substring(0,17))',N'Test',N'Inspection',2024,N'$($marker.Substring(0,30))',N'$marker Customer',SYSUTCDATETIME()); SELECT CAST(SCOPE_IDENTITY() AS int);")
    $appointment = [int](Sql "INSERT Appointment(CustomerId,CustomerName,VehicleId,AppointmentStatus,CheckedInUtc,CreatedUtc) VALUES($customer,N'$marker Customer',$vehicle,N'Arrived',SYSUTCDATETIME(),SYSUTCDATETIME()); SELECT CAST(SCOPE_IDENTITY() AS int);")
    $employee = [int](Sql "INSERT EmployeeProfile(FirstName,LastName,IsActive,IsArchived,IsDeleted) VALUES(N'$marker',N'Mechanic',1,0,0); SELECT CAST(SCOPE_IDENTITY() AS int);")
    $tool = [int](Sql "INSERT Tool(Name,SerialNumber,Condition,IsActive,IsArchived,IsDeleted) VALUES(N'$marker tool',N'$marker-serial',N'Good',1,0,0); SELECT CAST(SCOPE_IDENTITY() AS int);")
    $toolPage = GetPage '/Owner/ToolTracking'
    Check ($toolPage.Content.Contains('Check Out Tool')) 'Tool inventory exposes checkout modal'
    $null = Post 'CheckoutTool' @{ToolId=$tool;EmployeeProfileId=$employee;ConditionAtCheckout='Good';Notes=$marker}
    $checkout = [int](Sql "SELECT MAX(ToolCheckoutId) FROM ToolCheckout WHERE ToolId=$tool")
    Check ($checkout -gt 0) 'Tool checkout is recorded against a mechanic'
    $null = Post 'ReturnEquipment' @{ToolCheckoutId=$checkout;ConditionAtReturn='Damaged';Notes='Damage recorded during smoke test'}
    Check ((Sql "SELECT COUNT(*) FROM ToolMaintenance WHERE ToolId=$tool AND PerformedUtc IS NULL") -eq 1) 'Damaged return opens a maintenance record'
    $null = Post 'CompleteToolMaintenance' @{ToolId=$tool;MaintenanceType='Repair';Cost='25';ConditionAfter='Good';Notes='Smoke test repair'}
    Check ((Sql "SELECT COUNT(*) FROM ToolMaintenance WHERE ToolId=$tool AND PerformedUtc IS NOT NULL") -eq 1) 'Maintenance completion is saved to history'
    Check ((Sql "SELECT IsActive FROM Tool WHERE ToolId=$tool AND Condition='Good'") -eq 1) 'Completed maintenance returns a safe tool to inventory'
    $null = Sql "INSERT EmployeeAvailability(EmployeeProfileId,DayOfWeek,StartTime,EndTime,IsAvailable) VALUES($employee,2,'08:00','18:00',1);"
    $bay = [int](Sql "INSERT Bay(Name,IsActive) VALUES(N'$marker',1); SELECT CAST(SCOPE_IDENTITY() AS int);")
    $part = [int](Sql "INSERT Part(SKU,Name,QuantityOnHand,ReorderThreshold,UnitCost,UnitPrice,IsActive,IsArchived,IsDeleted) VALUES(N'$marker',N'$marker',10,2,20,30,1,0,0); SELECT CAST(SCOPE_IDENTITY() AS int);")
    $tax = [int](Sql "INSERT TaxRate(Description,Rate,EffectiveFrom) VALUES(N'$marker',0.1,'2000-01-01'); SELECT CAST(SCOPE_IDENTITY() AS int);")
    $body = @{ AppointmentId=$appointment; Reason=$marker; 'Lines[0].LineType'='Part'; 'Lines[0].PartId'=$part; 'Lines[0].Description'='Test part'; 'Lines[0].Quantity'=2; 'Lines[0].UnitPrice'=30;
        'Lines[1].LineType'='Labour'; 'Lines[1].Description'='Test repair'; 'Lines[1].Quantity'=1; 'Lines[1].UnitPrice'=100 }
    $null = Post 'CreateEstimate' $body
    Check ((Sql "SELECT COUNT(*) FROM Estimate WHERE AppointmentId=$appointment") -eq 0) 'Estimate creation requires completed initial inspection'
    $null = Post 'SaveInitialInspection' @{AppointmentId=$appointment;Findings='Front brake pads are worn and need replacement.';RecommendedWork='Replace front brake pads.'}
    Check ((Sql "SELECT COUNT(*) FROM Inspection WHERE AppointmentId=$appointment AND CompletedUtc IS NOT NULL") -eq 1) 'Initial inspection findings are saved to appointment history'
    $page = Post 'CreateEstimate' $body
    $estimate = [int](Sql "SELECT EstimateId FROM Estimate WHERE AppointmentId=$appointment")
    Check ($estimate -gt 0 -and $page.Content.Contains('Test repair')) 'Estimate creates and detail renders'
    $revision = [int](Sql "SELECT MAX(EstimateRevisionId) FROM EstimateRevision WHERE EstimateId=$estimate")
    $body.EstimateId=$estimate; $body.ExpectedRevisionId=$revision; $body.Reason="$marker revised"; $body['Lines[1].UnitPrice']=120
    $body.TaxRateId=$tax
    $null = Post 'UpdateEstimate' $body
    Check ((Sql "SELECT COUNT(*) FROM EstimateRevision WHERE EstimateId=$estimate") -eq 2) 'Estimate edit preserves old revision'
    Check ((Sql "SELECT TOP 1 Total FROM EstimateRevision WHERE EstimateId=$estimate ORDER BY RevisionNumber DESC") -eq 198) 'Estimate tax computed on the server'
    $null = Post 'UpdateEstimate' $body
    Check ((Sql "SELECT COUNT(*) FROM EstimateRevision WHERE EstimateId=$estimate") -eq 2) 'Stale estimate update rejected'
    $revision = [int](Sql "SELECT MAX(EstimateRevisionId) FROM EstimateRevision WHERE EstimateId=$estimate")
    $orderForm = @{ EstimateId=$estimate; EmployeeProfileId=$employee; BayId=$bay; ScheduledStart='2035-01-02T09:00'; ScheduledEnd='2035-01-02T11:00'; Notes=$marker }
    $null = Post 'GenerateWorkOrder' $orderForm
    Check ((Sql "SELECT COUNT(*) FROM WorkOrder WHERE EstimateId=$estimate") -eq 0) 'Unapproved estimate cannot create a work order'
    $null = Post 'CreateCustomerApprovalLink' @{Id=$estimate;Reason=$marker}
    $approvalToken = [string](Sql "SELECT ApprovalToken FROM EstimateRevision WHERE EstimateRevisionId=$revision")
    $approvalPage = GetPage "/CustomerEstimateApproval/Review?token=$approvalToken"
    Check ($approvalPage.Content.Contains('Approve all work')) 'Customer gets a public estimate review link'
    $lineIds = [regex]::Matches($approvalPage.Content, 'name="ApprovedLineIds" value="(\d+)"') | ForEach-Object { $_.Groups[1].Value }
    $approvalBody = @{Token=$approvalToken;Decision='Approved'}
    for($i=0;$i -lt $lineIds.Count;$i++) { $approvalBody["ApprovedLineIds[$i]"]=$lineIds[$i] }
    $approval = PostCustomerApproval $approvalToken $approvalBody
    if (!$approval.Content.Contains('Your response was recorded')) { throw 'Customer approval post did not complete.' }
    Check ($approval.Content.Contains('Your response was recorded')) 'Customer approval is recorded from the public link'
    Check ((Sql "SELECT EstimateStatus FROM EstimateRevision WHERE EstimateRevisionId=$revision") -eq 'Approved') 'Customer approval activates approved estimate scope'
    Check ((GetPage "/CustomerEstimateApproval/Review?token=$approvalToken").Content.Contains('no longer available')) 'Customer approval link cannot be replayed'
    $null = Sql "UPDATE EmployeeAvailability SET IsAvailable=0 WHERE EmployeeProfileId=$employee;"
    $null = Post 'GenerateWorkOrder' $orderForm
    Check ((Sql "SELECT COUNT(*) FROM WorkOrder WHERE EstimateId=$estimate") -eq 0) 'Off-shift mechanic cannot be scheduled'
    $null = Sql "UPDATE EmployeeAvailability SET IsAvailable=1 WHERE EmployeeProfileId=$employee; INSERT EmployeeAvailabilityException(EmployeeProfileId,StartUtc,EndUtc,IsAvailable) VALUES($employee,'2035-01-02T01:00:00','2035-01-02T03:00:00',0);"
    $null = Post 'GenerateWorkOrder' $orderForm
    Check ((Sql "SELECT COUNT(*) FROM WorkOrder WHERE EstimateId=$estimate") -eq 0) 'UTC absence blocks local-time booking'
    $null = Sql "DELETE EmployeeAvailabilityException WHERE EmployeeProfileId=$employee;"
    $collision=[int](Sql "INSERT Appointment(CustomerName,AppointmentStatus,CreatedUtc,BayId,ScheduledStart,ScheduledEnd) VALUES(N'$marker',N'Scheduled',SYSUTCDATETIME(),$bay,'2035-01-02T09:00:00','2035-01-02T11:00:00'); SELECT CAST(SCOPE_IDENTITY() AS int);")
    try {
        $null=Post 'GenerateWorkOrder' $orderForm
        Check ((Sql "SELECT COUNT(*) FROM WorkOrder WHERE EstimateId=$estimate") -eq 0) 'Work-order booking respects existing appointments'
    } finally { $null=Sql "DELETE Appointment WHERE AppointmentId=$collision;" }
    ConcurrentPost 'GenerateWorkOrder' $orderForm
    $order = [int](Sql "SELECT WorkOrderId FROM WorkOrder WHERE EstimateId=$estimate")
    Check ($order -gt 0) 'Approved estimate creates work order'
    $null = Post 'GenerateWorkOrder' $orderForm
    Check ((Sql "SELECT COUNT(*) FROM WorkOrder WHERE EstimateId=$estimate") -eq 1) 'Duplicate work order blocked'
    $orderForm.WorkOrderId=$order; $orderForm.ScheduledEnd='2035-01-02T12:00'
    $null = Post 'EditWorkOrder' $orderForm
    Check ((Sql "SELECT DATEPART(hour,ScheduledEnd) FROM WorkOrder WHERE WorkOrderId=$order") -eq 12) 'Work order schedule edit persists'
    $null = Post 'SetEmployeeShift' @{EmployeeProfileId=$employee;DayOfWeek=2;StartTime='08:00';EndTime='09:00';IsAvailable='true'}
    Check ((Sql "SELECT DATEPART(hour,EndTime) FROM EmployeeAvailability WHERE EmployeeProfileId=$employee") -eq 18) 'Shift edits cannot invalidate a work-order booking'
    $null = Post 'CancelEstimate' @{Id=$estimate; Reason=$marker}
    Check ((Sql "SELECT EstimateStatus FROM EstimateRevision WHERE EstimateRevisionId=$revision") -eq 'Approved') 'Linked estimate cancellation blocked'
    $stock = @{PartId=$part; WorkOrderId=$order; TransactionType='Installed'; Quantity=2; Notes=$marker}
    $null = Post 'AddStockMovement' $stock
    Check ((Sql "SELECT QuantityOnHand FROM Part WHERE PartId=$part") -eq 10) 'Unreserved installation blocked'
    $stock.TransactionType='Reserved'; $stock.Quantity=20
    $null = Post 'AddStockMovement' $stock
    Check ((Sql "SELECT COUNT(*) FROM PartTransaction WHERE PartId=$part") -eq 0) 'Over-reservation rolled back'
    $stock.Quantity=2
    $null = Post 'AddStockMovement' $stock
    $stockPage = GetPage "/Owner/SparesAllocation?partId=$part"
    Check ($stockPage.Content.Contains('data-dialog-open="stock-')) 'Stock editor available from transaction history'
    $null = Post 'ArchivePart' @{PartId=$part;Reason=$marker}
    Check ((Sql "SELECT IsArchived FROM Part WHERE PartId=$part") -eq 0) 'Reserved inventory cannot be archived'
    $reserve = [int](Sql "SELECT MAX(PartTransactionId) FROM PartTransaction WHERE PartId=$part")
    $stock.PartTransactionId=$reserve; $stock.Quantity=3
    $null = Post 'EditStockMovement' $stock
    Check ((Sql "SELECT Quantity FROM PartTransaction WHERE PartTransactionId=$reserve") -eq 2) 'Quantity above approved scope rejected even when stock is available'
    $stock.Quantity=2
    $null = Post 'EditStockMovement' $stock
    Check ((Sql "SELECT Quantity FROM PartTransaction WHERE PartTransactionId=$reserve") -eq 2) 'Stock reservation edit persists'
    $stock.Remove('PartTransactionId'); $stock.TransactionType='Installed'
    $null = Post 'AddStockMovement' $stock
    Check ((Sql "SELECT QuantityOnHand FROM Part WHERE PartId=$part") -eq 8) 'Installation deducts stock exactly once'
    $null = Post 'ReverseStockMovement' @{Id=$reserve; Reason=$marker}
    Check ((Sql "SELECT TransactionType FROM PartTransaction WHERE PartTransactionId=$reserve") -eq 'Reserved') 'Dependent reservation reversal blocked'
    $stock.TransactionType='Returned'; $stock.Quantity=1
    $null = Post 'AddStockMovement' $stock
    Check ((Sql "SELECT QuantityOnHand FROM Part WHERE PartId=$part") -eq 9) 'Returned part restores stock'
    $stock.TransactionType='Reserved'; $stock.Quantity=1
    $null = Post 'AddStockMovement' $stock
    $stock.TransactionType='Wasted'
    $null = Post 'AddStockMovement' $stock
    Check ((Sql "SELECT QuantityOnHand FROM Part WHERE PartId=$part") -eq 8) 'Wasted reserved part is removed from on-hand stock'
    Check ((Sql "SELECT COUNT(*) FROM PartTransaction WHERE PartId=$part AND WorkOrderId=$order AND TransactionType='Wasted'") -eq 1) 'Waste is recorded against its work order'
    $approvedLine = [int](Sql "SELECT EstimateRevisionLineItemId FROM EstimateRevisionLineItem WHERE EstimateRevisionId=$revision AND LineType='Labour'")
    $labour = @{WorkOrderId=$order; EmployeeProfileId=$employee; Description='Smoke repair'; Minutes=50; HourlyRate=999; Notes=$marker}
    $null = Post 'AddLabour' $labour
    Check ((Sql "SELECT COUNT(*) FROM ServiceEntry WHERE WorkOrderId=$order") -eq 0) 'Unapproved labour rejected'
    $labour.ApprovedEstimateLineId=$approvedLine
    $null = Post 'AddLabour' $labour
    $entry = [int](Sql "SELECT MAX(ServiceEntryId) FROM ServiceEntry WHERE WorkOrderId=$order")
    $labour.ServiceEntryId=$entry; $labour.Minutes=90
    $null = Post 'EditLabour' $labour
    Check ((Sql "SELECT HourlyRate FROM ServiceEntry WHERE ServiceEntryId=$entry") -eq 120) 'Client hourly rate cannot override approval'
    Check ((Sql "SELECT DurationMinutes FROM ServiceEntry WHERE ServiceEntryId=$entry") -eq 50) 'Labour above approved task amount rejected'
    $labour.Minutes=30
    $null = Post 'EditLabour' $labour
    Check ((Sql "SELECT ServiceCost FROM ServiceEntry WHERE ServiceEntryId=$entry") -eq 60) 'Approved labour edit recalculates cost'
    $body.ExpectedRevisionId=$revision; $body.Reason="$marker additional approval"
    $body['Lines[1].Quantity']=2
    $null = Post 'UpdateEstimate' $body
    $newRevision = [int](Sql "SELECT MAX(EstimateRevisionId) FROM EstimateRevision WHERE EstimateId=$estimate")
    Check ($newRevision -ne $revision) 'Active unbilled repair allows additional-scope revision'
    Check ((Sql "SELECT ApprovedEstimateRevisionId FROM WorkOrder WHERE WorkOrderId=$order") -eq $revision) 'Draft amendment cannot change authorized scope'
    $null = Post 'RecordEstimateDecision' @{EstimateId=$estimate;ExpectedRevisionId=$newRevision;Decision='Approved';Reason=$marker}
    Check ((Sql "SELECT ApprovedEstimateRevisionId FROM WorkOrder WHERE WorkOrderId=$order") -eq $newRevision) 'Approved amendment updates exact work-order revision'
    $revision=$newRevision
    $approvedLine = [int](Sql "SELECT EstimateRevisionLineItemId FROM EstimateRevisionLineItem WHERE EstimateRevisionId=$revision AND LineType='Labour'")
    $labour.ApprovedEstimateLineId=$approvedLine
    Check ((Sql "SELECT ApprovedEstimateLineId FROM ServiceEntry WHERE ServiceEntryId=$entry") -eq $approvedLine) 'Existing labour maps to retained approved task'
    $null = Post 'DeleteLabour' @{Id=$entry; Reason=$marker}
    Check ((Sql "SELECT COUNT(*) FROM ServiceEntry WHERE ServiceEntryId=$entry") -eq 0) 'Labour deletion persists'
    $labour.Remove('ServiceEntryId'); $labour.Minutes=50
    $null = Post 'AddLabour' $labour
    $null = Post 'StartLabourTimer' $labour
    $timer = [int](Sql "SELECT MAX(ServiceEntryId) FROM ServiceEntry WHERE WorkOrderId=$order")
    $null = Post 'StartLabourTimer' $labour
    Check ((Sql "SELECT COUNT(*) FROM ServiceEntry WHERE WorkOrderId=$order") -eq 2) 'Duplicate mechanic timer blocked'
    $null = Post 'TransitionWorkOrderStatus' @{WorkOrderId=$order;Transition='qc'}
    Check ((Sql "SELECT WorkOrderStatus FROM WorkOrder WHERE WorkOrderId=$order") -eq 'In Progress') 'Running timer blocks QC submission'
    $null = Post 'ChangeLabourTimer' @{ServiceEntryId=$timer;Transition='Pause'}
    Check ((Sql "SELECT SegmentType FROM ServiceTimeSegment WHERE ServiceEntryId=$timer AND SegmentEnd IS NULL") -eq 'Pause') 'Timer pause persisted'
    $null = Post 'ChangeLabourTimer' @{ServiceEntryId=$timer;Transition='Resume'}
    $null = Post 'ChangeLabourTimer' @{ServiceEntryId=$timer;Transition='Complete'}
    Check ((Sql "SELECT COUNT(*) FROM ServiceTimeSegment WHERE ServiceEntryId=$timer AND SegmentEnd IS NULL") -eq 0) 'Timer resume and completion persisted'
    $null = Post 'DeleteLabour' @{Id=$timer;Reason=$marker}
    $null = Post 'TransitionWorkOrderStatus' @{WorkOrderId=$order;Transition='complete'}
    Check ((Sql "SELECT WorkOrderStatus FROM WorkOrder WHERE WorkOrderId=$order") -eq 'In Progress') 'Legacy complete action cannot bypass QC'
    $null = Post 'TransitionWorkOrderStatus' @{WorkOrderId=$order;Transition='qc'}
    $qc = @{WorkOrderId=$order;Notes=$marker}
    for($i=0;$i -lt 6;$i++) { $qc["Answers[$i].Result"]='Pass'; $qc["Answers[$i].Notes"]=$marker }
    $null = Sql "UPDATE EmployeeProfile SET UserId=N'$ownerUser' WHERE EmployeeProfileId=$employee;"
    $null = Post 'SaveQualityInspection' $qc
    Check ((Sql "SELECT COUNT(*) FROM QualityCheck WHERE WorkOrderId=$order") -eq 0) 'Repair mechanic cannot sign off own QC'
    $null = Sql "UPDATE EmployeeProfile SET UserId=NULL WHERE EmployeeProfileId=$employee;"
    $qc['Answers[0].Result']='Fail'
    $null = Post 'SaveQualityInspection' $qc
    Check ((Sql "SELECT WorkOrderStatus FROM WorkOrder WHERE WorkOrderId=$order") -eq 'In Repair') 'Failed QC returns work to repair'
    $null = Post 'IssueInvoice' @{WorkOrderId=$order}
    Check ((Sql "SELECT COUNT(*) FROM Invoice WHERE WorkOrderId=$order") -eq 0) 'Failed QC blocks invoicing'
    $null = Post 'TransitionWorkOrderStatus' @{WorkOrderId=$order;Transition='qc'}
    $qc['Answers[0].Result']='Pass'
    $page = Post 'SaveQualityInspection' $qc
    Check ((Sql "SELECT WorkOrderStatus FROM WorkOrder WHERE WorkOrderId=$order") -eq 'Ready for Pickup') 'Passed QC moves the vehicle to Ready for Pickup'
    $null = Post 'DispatchWorkOrderBay' @{WorkOrderId=$order;BayId=$bay}
    Check ((Sql "SELECT WorkOrderStatus FROM WorkOrder WHERE WorkOrderId=$order") -eq 'Ready for Pickup') 'Dispatch cannot reopen QC-passed work'
    Check ($page.Content.Contains('Smoke') -or $page.Content.Contains($marker)) 'QC result renders stored checklist'
    $null = Post 'IssueInvoice' @{WorkOrderId=$order;TaxRateId=$tax}
    $invoice = [int](Sql "SELECT InvoiceId FROM Invoice WHERE WorkOrderId=$order")
    Check ($invoice -gt 0 -and (Sql "SELECT Total FROM Invoice WHERE InvoiceId=$invoice") -eq 142.56) 'Invoice uses approved pricing, net installed parts, completed labour and tax'
    $null = Post 'IssueInvoice' @{WorkOrderId=$order}
    Check ((Sql "SELECT COUNT(*) FROM Invoice WHERE WorkOrderId=$order") -eq 1) 'Duplicate invoice blocked'
    $pay = @{InvoiceId=$invoice;SubmissionToken=[Guid]::NewGuid().ToString(); Amount=200; PaymentMethod='Cash'; PaidUtc=[DateTime]::UtcNow.ToString('yyyy-MM-dd');Notes=$marker}
    $null = Post 'AddInvoicePayment' $pay
    Check ((Sql "SELECT COUNT(*) FROM Payment WHERE InvoiceId=$invoice") -eq 0) 'Overpayment rolled back'
    $pay.Amount=50
    $null = Post 'AddInvoicePayment' $pay
    $null = Post 'AddInvoicePayment' $pay
    Check ((Sql "SELECT COUNT(*) FROM Payment WHERE InvoiceId=$invoice") -eq 1) 'Payment replay blocked'
    $payment = [int](Sql "SELECT PaymentId FROM Payment WHERE InvoiceId=$invoice")
    $pay.PaymentId=$payment; $pay.Amount=60; $pay.SubmissionToken=[Guid]::NewGuid().ToString()
    $null = Post 'EditInvoicePayment' $pay
    Check ((Sql "SELECT Amount FROM Payment WHERE PaymentId=$payment") -eq 60) 'Payment edit persists'
    $adjust = @{InvoiceId=$invoice; SubmissionToken=[Guid]::NewGuid().ToString();AdjustmentType='Credit';Amount=20;Reason=$marker}
    $null = Post 'AddInvoiceAdjustment' $adjust
    $adjustment = [int](Sql "SELECT InvoiceAdjustmentId FROM InvoiceAdjustment WHERE InvoiceId=$invoice")
    Check ((Sql "SELECT Total FROM Invoice WHERE InvoiceId=$invoice") -eq 122.56) 'Credit adjustment updates invoice balance'
    $adjust.InvoiceAdjustmentId=$adjustment; $adjust.Amount=30; $adjust.SubmissionToken=[Guid]::NewGuid().ToString()
    $null = Post 'EditInvoiceAdjustment' $adjust
    Check ((Sql "SELECT Total FROM Invoice WHERE InvoiceId=$invoice") -eq 112.56) 'Adjustment edit recalculates total'
    $null = Post 'VoidInvoiceAdjustment' @{Id=$adjustment;Reason=$marker}
    Check ((Sql "SELECT Total FROM Invoice WHERE InvoiceId=$invoice") -eq 142.56) 'Adjustment reversal restores invoice total'
    $null = Post 'VoidInvoice' @{Id=$invoice;Reason=$marker}
    Check ((Sql "SELECT InvoiceStatus FROM Invoice WHERE InvoiceId=$invoice") -eq 'Partially Paid') 'Paid invoice cannot be voided'
    $null = Post 'VoidInvoicePayment' @{Id=$payment;Reason=$marker}
    $null = Post 'VoidInvoice' @{Id=$invoice;Reason=$marker}
    Check ((Sql "SELECT InvoiceStatus FROM Invoice WHERE InvoiceId=$invoice") -eq 'Voided') 'Payment and invoice void preserve records'
    $null = Post 'ReopenWorkOrder' @{Id=$order;Reason=$marker}
    Check ((Sql "SELECT WorkOrderStatus FROM WorkOrder WHERE WorkOrderId=$order") -eq 'In Progress') 'Unbilled work order can be reopened'
    foreach($path in @("/Owner/EstimateDetails/$estimate", "/Owner/WorkOrderDetails/$order", "/Owner/LabourTimeTracking?workOrderId=$order", "/Owner/SparesAllocation?workOrderId=$order", '/Owner/QualityControl', "/Owner/InvoiceDetails/$invoice")) {
        Check ((GetPage $path).StatusCode -eq 200) "Page renders: $path"
    }
    $checkId = [int](Sql "SELECT MAX(QualityCheckId) FROM QualityCheck WHERE WorkOrderId=$order")
    $null = Post 'VoidQualityInspection' @{Id=$checkId;Reason=$marker}
    Check ([DBNull]::Value.Equals((Sql "SELECT Passed FROM QualityCheck WHERE QualityCheckId=$checkId"))) 'Inspection void preserves history and requests reinspection'
    $null = Post 'ReopenWorkOrder' @{Id=$order;Reason=$marker}
    $stock.TransactionType='Returned'; $stock.Quantity=1
    $null = Post 'AddStockMovement' $stock
    $entries = $db.CreateCommand(); $entries.CommandText="SELECT ServiceEntryId FROM ServiceEntry WHERE WorkOrderId=$order"
    $reader = $entries.ExecuteReader(); $entryIds = @(); while($reader.Read()) { $entryIds += $reader.GetInt32(0) }; $reader.Close(); $entries.Dispose()
    foreach($entryId in $entryIds) { $null = Post 'DeleteLabour' @{Id=$entryId;Reason=$marker} }
    $null = Post 'CancelWorkOrder' @{Id=$order;Reason=$marker}
    Check ((Sql "SELECT WorkOrderStatus FROM WorkOrder WHERE WorkOrderId=$order") -eq 'Cancelled') 'Work order cancellation persists after resolving repair records'

    $body.EstimateId=0; $body.ExpectedRevisionId=0; $body.Reason="$marker pickup-completion"
    $null = Post 'CreateEstimate' $body
    $pickupEstimate = [int](Sql "SELECT MAX(EstimateId) FROM Estimate WHERE AppointmentId=$appointment AND EstimateId<>$estimate")
    $pickupRevision = [int](Sql "SELECT MAX(EstimateRevisionId) FROM EstimateRevision WHERE EstimateId=$pickupEstimate")
    $null = Post 'RecordEstimateDecision' @{EstimateId=$pickupEstimate;ExpectedRevisionId=$pickupRevision;Decision='Approved';Reason=$marker}
    $orderForm.EstimateId=$pickupEstimate
    $null = Post 'GenerateWorkOrder' $orderForm
    $pickupOrder = [int](Sql "SELECT WorkOrderId FROM WorkOrder WHERE EstimateId=$pickupEstimate")
    $pickupLine = [int](Sql "SELECT EstimateRevisionLineItemId FROM EstimateRevisionLineItem WHERE EstimateRevisionId=$pickupRevision AND LineType='Labour'")
    $pickupLabour = @{WorkOrderId=$pickupOrder;EmployeeProfileId=$employee;ApprovedEstimateLineId=$pickupLine;Description='Approved service';Minutes=30;HourlyRate=0;Notes=$marker}
    $null = Post 'AddLabour' $pickupLabour
    $null = Post 'TransitionWorkOrderStatus' @{WorkOrderId=$pickupOrder;Transition='qc'}
    $pickupQc = @{WorkOrderId=$pickupOrder;Notes=$marker}
    for($i=0;$i -lt 6;$i++) { $pickupQc["Answers[$i].Result"]='Pass'; $pickupQc["Answers[$i].Notes"]=$marker }
    $null = Post 'SaveQualityInspection' $pickupQc
    Check ((Sql "SELECT WorkOrderStatus FROM WorkOrder WHERE WorkOrderId=$pickupOrder") -eq 'Ready for Pickup') 'QC pass makes the pickup fixture ready'
    $null = Post 'ReleaseWorkOrder' @{WorkOrderId=$pickupOrder;ConfirmHandover='true'}
    Check ((Sql "SELECT WorkOrderStatus FROM WorkOrder WHERE WorkOrderId=$pickupOrder") -eq 'Ready for Pickup') 'Vehicle release is blocked before an invoice is paid'
    $null = Post 'IssueInvoice' @{WorkOrderId=$pickupOrder;TaxRateId=$tax}
    $pickupInvoice = [int](Sql "SELECT InvoiceId FROM Invoice WHERE WorkOrderId=$pickupOrder")
    $pickupTotal = [decimal](Sql "SELECT Total FROM Invoice WHERE InvoiceId=$pickupInvoice")
    $pickupPay = @{InvoiceId=$pickupInvoice;SubmissionToken=[Guid]::NewGuid().ToString();Amount=($pickupTotal/2);PaymentMethod='Cash';PaidUtc=[DateTime]::UtcNow.ToString('yyyy-MM-dd');Notes=$marker}
    $null = Post 'AddInvoicePayment' $pickupPay
    Check ((Sql "SELECT InvoiceStatus FROM Invoice WHERE InvoiceId=$pickupInvoice") -eq 'Partially Paid') 'First payment leaves invoice partially paid'
    $null = Post 'ReleaseWorkOrder' @{WorkOrderId=$pickupOrder;ConfirmHandover='true'}
    Check ((Sql "SELECT WorkOrderStatus FROM WorkOrder WHERE WorkOrderId=$pickupOrder") -eq 'Ready for Pickup') 'Vehicle release is blocked while an invoice balance remains'
    $pickupRemaining = [decimal](Sql "SELECT Total - (SELECT SUM(Amount) FROM Payment WHERE InvoiceId=$pickupInvoice) FROM Invoice WHERE InvoiceId=$pickupInvoice")
    $pickupPay.SubmissionToken=[Guid]::NewGuid().ToString(); $pickupPay.Amount=$pickupRemaining
    $null = Post 'AddInvoicePayment' $pickupPay
    Check ((Sql "SELECT InvoiceStatus FROM Invoice WHERE InvoiceId=$pickupInvoice") -eq 'Paid') 'Final payment settles invoice in full'
    Check ([decimal](Sql "SELECT Total - (SELECT SUM(Amount) FROM Payment WHERE InvoiceId=$pickupInvoice) FROM Invoice WHERE InvoiceId=$pickupInvoice") -eq 0) 'Fully paid invoice has zero balance'
    $null = Post 'ReleaseWorkOrder' @{WorkOrderId=$pickupOrder;ConfirmHandover='true'}
    Check ((Sql "SELECT WorkOrderStatus FROM WorkOrder WHERE WorkOrderId=$pickupOrder") -eq 'Picked Up') 'Paid vehicle can be released and pickup recorded'
    Check ((Sql "SELECT AppointmentStatus FROM Appointment WHERE AppointmentId=$appointment") -eq 'Completed') 'Vehicle release closes the appointment'
    Check (![DBNull]::Value.Equals((Sql "SELECT ReleasedUtc FROM WorkOrder WHERE WorkOrderId=$pickupOrder"))) 'Vehicle release timestamp is persisted'
    Check (![string]::IsNullOrWhiteSpace([string](Sql "SELECT ReleasedByName FROM WorkOrder WHERE WorkOrderId=$pickupOrder"))) 'Vehicle release records the staff member'
    Check ((Sql "SELECT COUNT(*) FROM AuditLogEntry WHERE EntityName='WorkOrder' AND EntityId=N'$pickupOrder' AND Action='Vehicle released'") -eq 1) 'Vehicle release is audited'
    $releaseTime = Sql "SELECT ReleasedUtc FROM WorkOrder WHERE WorkOrderId=$pickupOrder"
    $null = Post 'ReleaseWorkOrder' @{WorkOrderId=$pickupOrder;ConfirmHandover='true'}
    Check ((Sql "SELECT ReleasedUtc FROM WorkOrder WHERE WorkOrderId=$pickupOrder") -eq $releaseTime) 'Vehicle cannot be released twice'

    $null = Post 'CancelEstimate' @{Id=$estimate;Reason=$marker}
    Check ((Sql "SELECT EstimateStatus FROM EstimateRevision WHERE EstimateRevisionId=$revision") -eq 'Cancelled') 'Estimate cancellation persists'
    $receipt = @{PartId=$part;TransactionType='Received';Quantity=3;Notes=$marker}
    $null = Post 'AddStockMovement' $receipt
    $receiptId = [int](Sql "SELECT MAX(PartTransactionId) FROM PartTransaction WHERE PartId=$part")
    $null = Post 'ReverseStockMovement' @{Id=$receiptId;Reason=$marker}
    Check ((Sql "SELECT QuantityOnHand FROM Part WHERE PartId=$part") -eq 9) 'Receipt reversal preserves separately recorded waste loss'

    $null = Sql "INSERT AppRole(RoleKey,RoleName,DisplayOrder,IsSystemProtected,IsActive) VALUES(N'$role',N'$role',99,0,1);
        INSERT RolePermission(RoleId,PermissionId) SELECT N'$role',PermissionId FROM Permission WHERE PermissionCode IN ('estimates.view','inventory.view','inventory.create');"
    $testUser = [int](Sql "INSERT Users(Email,UserName,PasswordHash,Role,IsActive,CreatedAt) SELECT N'$role',N'$role',PasswordHash,N'$role',1,GETUTCDATE() FROM Users WHERE Email=N'admin123'; SELECT CAST(SCOPE_IDENTITY() AS int);")
    $ownerSession = $session
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $loginPage = GetPage '/Account/Login'
    $token = [regex]::Match($loginPage.Content, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
    $null = Invoke-WebRequest "$BaseUrl/Account/Login" -Method Post -Body @{Email=$role;Password=$Password;__RequestVerificationToken=$token} -WebSession $session -UseBasicParsing
    $restricted = GetPage '/Owner/EstimateApprovals'
    Check (!$restricted.Content.Contains('data-dialog-open="estimate-editor"')) 'Create estimate button hidden without permission'
    Check (!$restricted.Content.Contains('href="/Owner/WorkOrders"')) 'Work order module hidden without view permission'
    $tokenPage = GetPage '/Owner/SparesAllocation'
    $token = [regex]::Match($tokenPage.Content, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
    foreach($action in @('CreateEstimate','UpdateEstimate','CancelEstimate','RecordEstimateDecision','GenerateWorkOrder','AddLabour','SaveQualityInspection','IssueInvoice','AddInvoicePayment','ReleaseWorkOrder','VoidInvoice')) {
        $code = 0
        try { $null = Invoke-WebRequest "$BaseUrl/Owner/$action" -Method Post -Body @{__RequestVerificationToken=$token} -WebSession $session -UseBasicParsing } catch { $code = [int]$_.Exception.Response.StatusCode }
        Check ($code -eq 403) "Permission enforcement: $action returns 403"
    }
    $null = Sql "UPDATE Users SET IsActive=0 WHERE Id=$testUser;"
    Check ((GetPage '/Owner/EstimateApprovals').Content.Contains('Welcome Back')) 'Disabled account loses an existing session'
    $null = Sql "UPDATE Users SET IsActive=1 WHERE Id=$testUser;"
    function RestrictedLogin([string]$pass) {
        $p=GetPage '/Account/Login'
        $t=[regex]::Match($p.Content,'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
        Invoke-WebRequest "$BaseUrl/Account/Login" -Method Post -Body @{Email=$role;Password=$pass;__RequestVerificationToken=$t} -WebSession $session -UseBasicParsing
    }
    $null = RestrictedLogin $Password
    $null = Sql "UPDATE Users SET Role='Mechanic' WHERE Id=$testUser;"
    Check ((GetPage '/Owner/EstimateApprovals').Content.Contains('Welcome Back')) 'Role change invalidates the old session'
    $null = Sql "UPDATE Users SET Role=N'$role' WHERE Id=$testUser;"
    for($i=0;$i -lt 5;$i++) { $null = RestrictedLogin 'incorrect-test-password' }
    Check ((Sql "SELECT COUNT(*) FROM AccountSecurity WHERE UserId=$testUser AND LockedUntilUtc>SYSUTCDATETIME()") -eq 1) 'Five failures cause persistent temporary lockout'
    Check ((RestrictedLogin $Password).Content.Contains('Sign-in failed')) 'Correct password cannot bypass an active lockout'
    Check ((Sql "SELECT COUNT(*) FROM AuditLogEntry WHERE EntityName='Account' AND EntityId=N'$testUser' AND Action='Login failed'") -ge 5) 'Failed logins are audited'
    $session=$ownerSession
    $permissionId=[int](Sql "SELECT TOP 1 PermissionId FROM Permission WHERE PermissionCode='estimates.view'")
    $null = Post 'SaveRolePermissions' @{RoleId=$role;PermissionIds=$permissionId}
    Check ((Sql "SELECT COUNT(*) FROM AuditLogEntry WHERE EntityName='AppRole' AND EntityId=N'$role' AND Action='Update permissions'") -eq 1) 'Permission updates preserve actor and before/after audit'
    $null = GetPage '/Owner/ExportReport?reportType=sales&format=csv'
    Check ((Sql "SELECT COUNT(*) FROM AuditLogEntry WHERE UserId=N'$ownerUser' AND Action='Export report'") -eq 1) 'Sensitive exports are audited'
    $code=0
    try { $null = Invoke-WebRequest "$BaseUrl/Owner/CreateEstimate" -Method Post -Body @{AppointmentId=$appointment} -WebSession $session -UseBasicParsing } catch { $code=[int]$_.Exception.Response.StatusCode }
    Check ($code -eq 400) 'Mutation without antiforgery token rejected'
    $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $page=GetPage '/Account/Login'
    $token=[regex]::Match($page.Content,'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
    $limited=$false
    for($i=0;$i -lt 25;$i++) {
        try { $null=Invoke-WebRequest "$BaseUrl/Account/Login" -Method Post -Body @{Email=$role;Password='invalid';__RequestVerificationToken=$token} -WebSession $session -UseBasicParsing }
        catch { if([int]$_.Exception.Response.StatusCode -eq 429) { $limited=$true; break }; throw }
    }
    Check $limited 'Login request rate limit returns 429'
}
finally {
    if($testUser -gt 0) { $null = Sql "DELETE AccountSecurity WHERE UserId=$testUser; DELETE Users WHERE Id=$testUser;" }
    if($ownerUser -gt 0) { $null = Sql "DELETE AccountSecurity WHERE UserId=$ownerUser; DELETE Users WHERE Id=$ownerUser;" }
    $null = Sql "DELETE RolePermission WHERE RoleId=N'$role'; DELETE AppRole WHERE RoleKey=N'$role';"
    # Only fixtures created by this invocation are removed. Existing workshop records are untouched.
    if($appointment -gt 0) {
        $null = Sql "DECLARE @orders TABLE(Id int); INSERT @orders SELECT WorkOrderId FROM WorkOrder WHERE AppointmentId=$appointment;
        DECLARE @invoices TABLE(Id int); INSERT @invoices SELECT InvoiceId FROM Invoice WHERE WorkOrderId IN (SELECT Id FROM @orders);
        DELETE Payment WHERE InvoiceId IN (SELECT Id FROM @invoices); DELETE InvoiceAdjustment WHERE InvoiceId IN (SELECT Id FROM @invoices); DELETE InvoiceLineItem WHERE InvoiceId IN (SELECT Id FROM @invoices); DELETE Invoice WHERE InvoiceId IN (SELECT Id FROM @invoices);
        DELETE ServiceTimeSegment WHERE ServiceEntryId IN (SELECT ServiceEntryId FROM ServiceEntry WHERE WorkOrderId IN (SELECT Id FROM @orders));
        DELETE ServiceEntry WHERE WorkOrderId IN (SELECT Id FROM @orders); DELETE QualityCheck WHERE WorkOrderId IN (SELECT Id FROM @orders); DELETE PartTransaction WHERE PartId=$part; DELETE WorkOrderAssignment WHERE WorkOrderId IN (SELECT Id FROM @orders); DELETE WorkOrder WHERE WorkOrderId IN (SELECT Id FROM @orders);
        DELETE EstimateRevisionLineItem WHERE EstimateRevisionId IN (SELECT EstimateRevisionId FROM EstimateRevision WHERE EstimateId IN (SELECT EstimateId FROM Estimate WHERE AppointmentId=$appointment));
        DELETE QueueStatusHistory WHERE AppointmentId=$appointment; DELETE InspectionMedia WHERE InspectionId IN (SELECT InspectionId FROM Inspection WHERE AppointmentId=$appointment); DELETE Inspection WHERE AppointmentId=$appointment;
        DELETE EstimateRevision WHERE EstimateId IN (SELECT EstimateId FROM Estimate WHERE AppointmentId=$appointment); DELETE Estimate WHERE AppointmentId=$appointment;
        DELETE Appointment WHERE AppointmentId=$appointment; DELETE Vehicle WHERE VehicleId=$vehicle; DELETE Customer WHERE CustomerId=$customer; DELETE Part WHERE PartId=$part; DELETE Bay WHERE BayId=$bay; DELETE EmployeeAvailability WHERE EmployeeProfileId=$employee; DELETE EmployeeAvailabilityException WHERE EmployeeProfileId=$employee; DELETE EmployeeProfile WHERE EmployeeProfileId=$employee; DELETE TaxRate WHERE TaxRateId=$tax;"
    }
    else {
        if($vehicle -gt 0) { $null = Sql "DELETE Vehicle WHERE VehicleId=$vehicle;" }
        if($customer -gt 0) { $null = Sql "DELETE Customer WHERE CustomerId=$customer;" }
    }
    if($tool -gt 0) { $null = Sql "DELETE ToolMaintenance WHERE ToolId=$tool; DELETE ToolCheckout WHERE ToolId=$tool; DELETE Tool WHERE ToolId=$tool;" }
    $db.Close(); $db.Dispose()
}
