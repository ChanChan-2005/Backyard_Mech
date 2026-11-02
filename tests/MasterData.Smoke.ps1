param([string]$BaseUrl='http://127.0.0.1:5099', [string]$ConnectionString='Server=.\SQLEXPRESS;Database=Bakyard_Mech;Integrated Security=True;TrustServerCertificate=True', [string]$Login='admin123', [string]$Password='admin123')
$ErrorActionPreference='Stop'
$db=New-Object System.Data.SqlClient.SqlConnection $ConnectionString
$db.Open()
function Sql($query) { $cmd=$db.CreateCommand(); $cmd.CommandText=$query; try {$cmd.ExecuteScalar()} finally {$cmd.Dispose()} }
function Check($ok,$message) { if(!$ok){throw "FAIL: $message"}; Write-Output "PASS: $message" }
$session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
function GetPage($path) { Invoke-WebRequest "$BaseUrl$path" -WebSession $session -UseBasicParsing }
function Token($html) { [regex]::Match($html,'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value }
function Post($action,$body) { $body['__RequestVerificationToken']=Token (GetPage '/Owner/Suppliers').Content; Invoke-WebRequest "$BaseUrl/Owner/$action" -Method Post -Body $body -WebSession $session -UseBasicParsing }
function Stamp($path,$id) { $html=(GetPage $path).Content; [regex]::Match($html,"(?s)id=`"master-edit-$id`".*?name=`"Expected`" value=`"([^`"]+)`"").Groups[1].Value }
$mark='MS-'+[Guid]::NewGuid().ToString('N')
$owner=$reader=0
try {
    $owner=[int](Sql "INSERT Users(Email,UserName,PasswordHash,Role,IsActive,CreatedAt) SELECT N'$mark',N'$mark',PasswordHash,N'Owner',1,GETUTCDATE() FROM Users WHERE Email=N'$Login'; SELECT CAST(SCOPE_IDENTITY() AS int);")
    $page=GetPage '/Account/Login'
    $page=Invoke-WebRequest "$BaseUrl/Account/Login" -Method Post -Body @{Email=$mark;Password=$Password;__RequestVerificationToken=(Token $page.Content)} -WebSession $session -UseBasicParsing
    $key=[regex]::Match($page.Content,'<code>([A-Z2-7]+)</code>').Groups[1].Value
    Check ($key.Length -gt 10) 'Temporary owner uses MFA'
    $code=python -c "import base64,hmac,hashlib,time,struct,sys; d=hmac.new(base64.b32decode(sys.argv[1]),struct.pack('>Q',int(time.time())//30),hashlib.sha1).digest(); o=d[-1]&15; print(str((int.from_bytes(d[o:o+4],'big')&0x7fffffff)%1000000).zfill(6))" $key
    $null=Invoke-WebRequest "$BaseUrl/Account/Mfa" -Method Post -Body @{Code=$code;__RequestVerificationToken=(Token $page.Content)} -WebSession $session -UseBasicParsing
    foreach($path in @('/Owner/Suppliers','/Owner/ServiceCatalog','/Owner/SettingsTax','/Owner/SettingsCategories?kind=services','/Owner/SettingsCategories?kind=parts','/Owner/SettingsCategories?kind=tools')) {
        Check ((GetPage $path).Content.Contains('id="master-new"')) "$path renders modal forms"
    }
    $null=Post 'CreateSupplier' @{Name=$mark;Email='supplier@example.com';ContactFirstName='Test';Phone='555-0100';City='Test City'}
    $supplier=[int](Sql "SELECT SupplierId FROM Supplier WHERE SupplierName=N'$mark'")
    Check ($supplier -gt 0) 'Supplier created'
    $null=Post 'CreateSupplier' @{Name=$mark}
    Check ((Sql "SELECT COUNT(*) FROM Supplier WHERE SupplierName=N'$mark'") -eq 1) 'Duplicate supplier rejected'
    $stamp=Stamp '/Owner/Suppliers' $supplier
    $null=Post 'UpdateSupplier' @{Id=$supplier;Expected=$stamp;Name=$mark;City='Updated City'}
    Check ((Sql "SELECT City FROM Supplier WHERE SupplierId=$supplier") -eq 'Updated City') 'Supplier edit persists'
    $null=Post 'UpdateSupplier' @{Id=$supplier;Expected=$stamp;Name=$mark;City='Stale City'}
    Check ((Sql "SELECT City FROM Supplier WHERE SupplierId=$supplier") -eq 'Updated City') 'Stale edit rejected'
    $null=Post 'SetSupplierState' @{Id=$supplier;Expected=(Stamp '/Owner/Suppliers' $supplier);Active='false';Reason=$mark}
    Check (!(Sql "SELECT IsActive FROM Supplier WHERE SupplierId=$supplier")) 'Supplier deactivated'
    $null=Post 'AddPart' @{Name=$mark;SKU=$mark;SupplierId=$supplier;UnitCost=1;UnitPrice=2;QuantityOnHand=0;ReorderThreshold=0}
    Check ((Sql "SELECT COUNT(*) FROM Part WHERE SKU=N'$mark'") -eq 0) 'Inactive supplier cannot be assigned to a new part'
    $null=Post 'SetSupplierState' @{Id=$supplier;Expected=(Stamp '/Owner/Suppliers' $supplier);Active='true';Reason=$mark}
    Check ([bool](Sql "SELECT IsActive FROM Supplier WHERE SupplierId=$supplier")) 'Supplier reactivated'
    foreach($kind in @('services','parts','tools')) {
        $table=@{services='ServiceCategory';parts='PartCategory';tools='ToolCategory'}[$kind]
        $path="/Owner/SettingsCategories?kind=$kind"
        $null=Post 'CreateCategory' @{Kind=$kind;Name="$mark-parent"}
        $parent=[int](Sql "SELECT ${table}Id FROM $table WHERE CategoryName=N'$mark-parent'")
        $null=Post 'CreateCategory' @{Kind=$kind;Name="$mark-child";ParentId=$parent}
        $child=[int](Sql "SELECT ${table}Id FROM $table WHERE CategoryName=N'$mark-child'")
        Check ($parent -gt 0 -and $child -gt 0) "$kind parent and child create"
        $null=Post 'CreateCategory' @{Kind=$kind;Name="$mark-child";ParentId=$parent}
        Check ((Sql "SELECT COUNT(*) FROM $table WHERE CategoryName=N'$mark-child'") -eq 1) "$kind duplicate sibling rejected"
        $null=Post 'UpdateCategory' @{Id=$parent;Kind=$kind;Expected=(Stamp $path $parent);Name="$mark-parent";ParentId=$child}
        Check ([DBNull]::Value.Equals((Sql "SELECT Parent${table}Id FROM $table WHERE ${table}Id=$parent"))) "$kind cycle rejected"
        $null=Post 'SetCategoryState' @{Id=$parent;Kind=$kind;Expected=(Stamp $path $parent);Active='false';Reason=$mark}
        Check ([bool](Sql "SELECT IsActive FROM $table WHERE ${table}Id=$parent")) "$kind active child protects parent"
        $null=Post 'UpdateCategory' @{Id=$child;Kind=$kind;Expected=(Stamp $path $child);Name="$mark-child";ParentId=$parent;Description='Edited description'}
        Check ((Sql "SELECT Description FROM $table WHERE ${table}Id=$child") -eq 'Edited description') "$kind edit persists"
        $null=Post 'SetCategoryState' @{Id=$child;Kind=$kind;Expected=(Stamp $path $child);Active='false';Reason=$mark}
        if($kind -eq 'parts') {
            $null=Post 'AddPart' @{Name=$mark;SKU=$mark;PartCategoryId=$child;UnitCost=1;UnitPrice=2;QuantityOnHand=0;ReorderThreshold=0}
            Check ((Sql "SELECT COUNT(*) FROM Part WHERE SKU=N'$mark'") -eq 0) 'Inactive part category rejected'
        }
        if($kind -eq 'tools') {
            $null=Post 'AddTool' @{Name=$mark;SerialNumber=$mark;ToolCategoryId=$child;Condition='Good'}
            Check ((Sql "SELECT COUNT(*) FROM Tool WHERE SerialNumber=N'$mark'") -eq 0) 'Inactive tool category rejected'
        }
        $null=Post 'SetCategoryState' @{Id=$parent;Kind=$kind;Expected=(Stamp $path $parent);Active='false';Reason=$mark}
        $null=Post 'SetCategoryState' @{Id=$child;Kind=$kind;Expected=(Stamp $path $child);Active='true';Reason=$mark}
        Check (!(Sql "SELECT IsActive FROM $table WHERE ${table}Id=$child")) "$kind cannot reactivate under inactive parent"
        $null=Post 'SetCategoryState' @{Id=$parent;Kind=$kind;Expected=(Stamp $path $parent);Active='true';Reason=$mark}
        $null=Post 'SetCategoryState' @{Id=$child;Kind=$kind;Expected=(Stamp $path $child);Active='true';Reason=$mark}
        Check ([bool](Sql "SELECT IsActive FROM $table WHERE ${table}Id=$child")) "$kind reactivation persists"
    }
    $category=[int](Sql "SELECT ServiceCategoryId FROM ServiceCategory WHERE CategoryName=N'$mark-child'")
    $null=Post 'CreateService' @{Name=$mark;CategoryId=$category;Price=125;Hours=1.5}
    $service=[int](Sql "SELECT ServiceId FROM Service WHERE ServiceName=N'$mark'")
    Check ($service -gt 0) 'Service created'
    $null=Post 'UpdateService' @{Id=$service;Expected=(Stamp '/Owner/ServiceCatalog' $service);Name=$mark;CategoryId=$category;Price=150;Hours=2}
    Check ((Sql "SELECT DefaultPrice FROM Service WHERE ServiceId=$service") -eq 150) 'Service pricing edit persists'
    $null=Post 'SetServiceState' @{Id=$service;Expected=(Stamp '/Owner/ServiceCatalog' $service);Active='false';Reason=$mark}
    Check (!(Sql "SELECT IsActive FROM Service WHERE ServiceId=$service")) 'Service deactivated'
    $null=Post 'SetServiceState' @{Id=$service;Expected=(Stamp '/Owner/ServiceCatalog' $service);Active='true';Reason=$mark}
    Check ([bool](Sql "SELECT IsActive FROM Service WHERE ServiceId=$service")) 'Service reactivated'
    $null=Post 'CreateTax' @{Name=$mark;Percent=8.25;Start='2020-01-01'}
    $tax=[int](Sql "SELECT TaxRateId FROM TaxRate WHERE Description=N'$mark'")
    Check ((Sql "SELECT Rate FROM TaxRate WHERE TaxRateId=$tax") -eq 0.0825) 'Tax percent stored as exact fraction'
    $null=Post 'CreateTax' @{Name=$mark;Percent=9;Start='2025-01-01'}
    Check ((Sql "SELECT COUNT(*) FROM TaxRate WHERE Description=N'$mark'") -eq 1) 'Overlapping tax version rejected'
    $null=Post 'UpdateTax' @{Id=$tax;Expected=(Stamp '/Owner/SettingsTax' $tax);Name=$mark;Percent=10;Start='2020-01-01'}
    Check ((Sql "SELECT Rate FROM TaxRate WHERE TaxRateId=$tax") -eq 0.1) 'Unused tax edit persists'
    $null=Sql "INSERT Invoice(InvoiceNumber,IssuedUtc,TaxRateId,Subtotal,TaxAmount,Total,InvoiceStatus) VALUES(N'$mark',SYSUTCDATETIME(),$tax,100,10,110,N'Issued');"
    $null=Post 'UpdateTax' @{Id=$tax;Expected=(Stamp '/Owner/SettingsTax' $tax);Name=$mark;Percent=15;Start='2020-01-01'}
    Check ((Sql "SELECT Rate FROM TaxRate WHERE TaxRateId=$tax") -eq 0.1) 'Invoiced tax cannot be edited'
    $null=Post 'SetTaxState' @{Id=$tax;Expected=(Stamp '/Owner/SettingsTax' $tax);Active='false';Reason=$mark}
    Check ((Sql "SELECT COUNT(*) FROM TaxRate WHERE TaxRateId=$tax AND EffectiveTo<=SYSUTCDATETIME()") -eq 1) 'Tax deactivation effective immediately'
    Check ((Sql "SELECT Total FROM Invoice WHERE InvoiceNumber=N'$mark'") -eq 110) 'Invoice snapshot unchanged'
    $null=Post 'CreateTax' @{Name="$mark-invalid";Percent=9;Start='2030-01-02';End='2030-01-01'}
    Check ((Sql "SELECT COUNT(*) FROM TaxRate WHERE Description=N'$mark-invalid'") -eq 0) 'Reversed tax range rejected'
    $status=0
    try { $null=Invoke-WebRequest "$BaseUrl/Owner/CreateSupplier" -Method Post -Body @{Name="$mark-csrf"} -WebSession $session -UseBasicParsing } catch { $status=[int]$_.Exception.Response.StatusCode }
    Check ($status -eq 400) 'Missing CSRF token rejected'
    Check ((Sql "SELECT COUNT(*) FROM AuditLogEntry WHERE UserName=N'$mark'") -gt 20) 'Master changes write audit history'
    $null=Sql "INSERT AppRole(RoleKey,RoleName,DisplayOrder,IsSystemProtected,IsActive) VALUES(N'$mark',N'$mark',99,0,1); INSERT RolePermission(RoleId,PermissionId) SELECT N'$mark',PermissionId FROM Permission WHERE PermissionCode IN ('suppliers.view','services.view');"
    $reader=[int](Sql "INSERT Users(Email,UserName,PasswordHash,Role,IsActive,CreatedAt) SELECT N'$mark-reader',N'$mark-reader',PasswordHash,N'$mark',1,GETUTCDATE() FROM Users WHERE Email=N'$Login'; SELECT CAST(SCOPE_IDENTITY() AS int);")
    $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $page=GetPage '/Account/Login'
    $null=Invoke-WebRequest "$BaseUrl/Account/Login" -Method Post -Body @{Email="$mark-reader";Password=$Password;__RequestVerificationToken=(Token $page.Content)} -WebSession $session -UseBasicParsing
    $page=GetPage '/Owner/Suppliers'
    Check (!$page.Content.Contains('id="master-new"') -and !$page.Content.Contains('id="master-edit-')) 'Read-only role has no mutation forms'
    Check (!$page.Content.Contains('href="/Owner/Settings"')) 'Settings hidden without view permission'
    $token=Token $page.Content
    foreach($action in @('CreateSupplier','UpdateSupplier','SetSupplierState','CreateService','UpdateService','SetServiceState','CreateCategory','UpdateCategory','SetCategoryState','CreateTax','UpdateTax','SetTaxState')) {
        $status=0
        try {$null=Invoke-WebRequest "$BaseUrl/Owner/$action" -Method Post -Body @{__RequestVerificationToken=$token} -WebSession $session -UseBasicParsing} catch {$status=[int]$_.Exception.Response.StatusCode}
        Check ($status -eq 403) "Read-only cannot POST $action"
    }
    Write-Output 'Master-data smoke checks completed.'
} finally {
    $null=Sql "DELETE Invoice WHERE InvoiceNumber=N'$mark'; DELETE TaxRate WHERE Description LIKE N'$mark%'; DELETE Part WHERE SKU=N'$mark'; DELETE Tool WHERE SerialNumber=N'$mark'; DELETE Service WHERE ServiceName=N'$mark'; DELETE Supplier WHERE SupplierName=N'$mark';"
    foreach($table in @('ServiceCategory','PartCategory','ToolCategory')) { $null=Sql "DELETE $table WHERE CategoryName=N'$mark-child'; DELETE $table WHERE CategoryName=N'$mark-parent';" }
    $null=Sql "DELETE AccountSecurity WHERE UserId IN ($owner,$reader); DELETE Users WHERE Id IN ($owner,$reader); DELETE RolePermission WHERE RoleId=N'$mark'; DELETE AppRole WHERE RoleKey=N'$mark';"
    $db.Dispose()
}
