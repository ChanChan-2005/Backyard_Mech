using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Backyard_Mech.Services
{
    public sealed class OwnerPermissionFilter : IAsyncActionFilter
    {
        private readonly PermissionService _permissionService;
        private readonly AccountSecurityService _security;

        public OwnerPermissionFilter(PermissionService permissionService, AccountSecurityService security)
        {
            _permissionService = permissionService;
            _security = security;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = await _security.ValidateSession(context.HttpContext);
            var role = user?.Role;
            if (user is null)
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            var actionName = context.ActionDescriptor.RouteValues["action"] ?? "";
            var requirement = OwnerPermissionCatalog.Resolve(actionName);
            var codes = await _permissionService.GetPermissionCodesAsync(role);
            var reportType = context.ActionArguments.TryGetValue("reportType", out var requestedType)
                ? Convert.ToString(requestedType)
                : null;
            if (!OwnerPermissionCatalog.CanAccessRequest(role, codes, actionName, reportType))
            {
                if (HttpMethods.IsPost(context.HttpContext.Request.Method))
                {
                    context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
                }
                else
                {
                    context.Result = new RedirectToActionResult("AccessDenied", "Account", new { module = requirement?.Module ?? "this page" });
                }
                return;
            }

            await next();
        }
    }

    public static class OwnerPermissionCatalog
    {
        // Module View opens its navigation pages; other operations retain explicit grants.
        private static readonly HashSet<string> ModulePages = new(StringComparer.OrdinalIgnoreCase)
        {
            "Dashboard", "ServiceQueue", "CustomerVehicle", "VehicleHistory", "Appointments", "WorkOrderEvidencePhoto",
            "EstimateApprovals", "WorkOrders", "BayManagement", "PartsManagement", "PartsPos", "Suppliers",
            "ServiceCatalog", "LabourTimeTracking", "QualityControl", "ToolTracking", "InvoiceRegistry",
            "ArchiveRecords", "RecordsDisposition", "Reports", "Employees", "StaffAccounts", "Settings",
            "SupplierPostalCodeSuggestions"
        };

        public static bool CanAccessAction(ISet<string> codes, string actionName)
        {
            var requirement = Resolve(actionName);
            if (requirement is null) return false;
            if (codes.Contains("*")) return true;
            return codes.Contains(PermissionService.BuildPermissionCode(requirement.Value.Module, "View"))
                && codes.Contains(PermissionService.BuildPermissionCode(requirement.Value.Module, requirement.Value.Action))
                && (ModulePages.Contains(actionName) || codes.Contains(ActionPermissionCode(actionName)));
        }

        public static bool CanAccessRequest(string? roleKey, ISet<string> codes, string actionName, string? reportType = null)
        {
            var isAdministrator = string.Equals(roleKey, "Administrator", StringComparison.OrdinalIgnoreCase);
            if (isAdministrator &&
                (actionName.Equals("ProfitabilityReport", StringComparison.OrdinalIgnoreCase)
                 || actionName.Equals("ExportReport", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(reportType, "profitability", StringComparison.OrdinalIgnoreCase)))
                return false;

            if (isAdministrator && DashboardPermissions.Sections.Contains(actionName))
                return codes.Contains(PermissionService.BuildPermissionCode("Dashboard", "View"));

            return CanAccessAction(codes, actionName);
        }

        public static string ActionPermissionCode(string actionName) => $"owneraction.{actionName.ToLowerInvariant()}";

        public static IReadOnlyList<(string Name, string Module, string Action)> DetailedActions { get; } =
            typeof(Backyard_Mech.Controllers.OwnerController)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                .Where(method => typeof(IActionResult).IsAssignableFrom(method.ReturnType)
                    || method.ReturnType == typeof(Task<IActionResult>))
                .Select(method => (method.Name, Requirement: Resolve(method.Name)))
                .Where(item => item.Requirement.HasValue && !ModulePages.Contains(item.Name))
                .Select(item => (item.Name, item.Requirement!.Value.Module, item.Requirement.Value.Action))
                .Concat(DashboardPermissions.Sections.Select(name => (Name: name, Module: "Dashboard", Action: "View")))
                .Concat(new[] { (Name: "ManageStaffRolePermissions", Module: "User Access", Action: "Edit"), (Name: "ManageStockTransactions", Module: "Inventory", Action: "Create") })
                .Distinct()
                .OrderBy(item => item.Module)
                .ThenBy(item => item.Name)
                .ToArray();

        private static readonly HashSet<string> SingleActionBaseCodes = DetailedActions
            .Where(item => item.Action != "View")
            .GroupBy(item => PermissionService.BuildPermissionCode(item.Module, item.Action), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        public static bool HasSingleActionBase(string module, string action) =>
            SingleActionBaseCodes.Contains(PermissionService.BuildPermissionCode(module, action));

        public static string ActionLabel(string actionName) =>
            System.Text.RegularExpressions.Regex.Replace(
                System.Text.RegularExpressions.Regex.Replace(actionName, "([A-Z])([A-Z][a-z])", "$1 $2"),
                "([a-z0-9])([A-Z])", "$1 $2");

        public static (string Module, string Action)? Resolve(string actionName)
        {
            if (DashboardPermissions.Sections.Contains(actionName)) return ("Dashboard", "View");
            return actionName switch
            {
                "Dashboard" => ("Dashboard", "View"),
                "StaffAccounts" => ("User Access", "View"),
                "ManageStaffRolePermissions" => ("User Access", "Edit"),
                "ManageStockTransactions" => ("Inventory", "Create"),
                "CreateStaffAccount" => ("User Access", "Create"),
                "SaveUserAccess" or "ResetStaffPassword" => ("User Access", "Edit"),
                "ActivateStaffAccount" or "DeactivateStaffAccount" or "BlockStaffAccount" or "UnblockStaffAccount" => ("User Access", "Edit"),
                "StaffAvailability" => ("Employees", "View"),
                "CreateAvailabilityException" or "UpdateAvailabilityException" or "DeleteAvailabilityException" => ("Employees", "Edit"),
                "Suppliers" => ("Suppliers", "View"),
                "SupplierPostalCodeSuggestions" => ("Suppliers", "View"),
                "CreateSupplier" => ("Suppliers", "Create"),
                "UpdateSupplier" or "SetSupplierState" => ("Suppliers", "Edit"),
                "ServiceCatalog" => ("Services", "View"),
                "CreateService" => ("Services", "Create"),
                "UpdateService" or "SetServiceState" => ("Services", "Edit"),
                "SettingsCategories" => ("Settings", "View"),
                "CreateCategory" => ("Settings", "Create"),
                "UpdateCategory" or "SetCategoryState" or "UpdateTax" => ("Settings", "Edit"),

                "CustomerVehicle" or "CustomerDetails" or "CustomerVehicles" or "CustomerServiceHistory" => ("Customers", "View"),
                "AddCustomer" => ("Customers", "Create"),
                "EditCustomer" => ("Customers", "Edit"),
                "ArchiveCustomer" => ("Customers", "Delete"),
                "SoftDeleteCustomer" => ("Customers", "Delete"),
                "AddVehicle" => ("Vehicles", "Create"),
                "UpdateVehicle" => ("Vehicles", "Edit"),
                "VehicleHistory" => ("Vehicles", "View"),
                "TransferVehicleOwnership" or "RecordVehicleOdometer" => ("Vehicles", "Edit"),
                "ArchiveVehicle" => ("Vehicles", "Delete"),
                "SoftDeleteVehicle" => ("Vehicles", "Delete"),

                "ServiceQueue" or "ServiceQueueDetails" => ("Service Queue", "View"),
                "AssignQueueBay" or "AssignQueueMechanic" or "UpdateQueueStatus" => ("Service Queue", "Edit"),

                "Appointments" => ("Appointments", "View"),
                "CreateAppointment" => ("Appointments", "Create"),
                "EditAppointment" or "CheckInAppointment" => ("Appointments", "Edit"),
                "CancelAppointment" => ("Appointments", "Edit"),

                "EstimateApprovals" or "EstimateDetails" => ("Estimates", "View"),
                "EditEstimate" or "UpdateEstimate" or "CreateCustomerApprovalLink" => ("Estimates", "Edit"),
                "CreateEstimate" => ("Estimates", "Create"),
                "RecordEstimateDecision" => ("Estimates", "Approve"),
                "CancelEstimate" => ("Estimates", "Delete"),

                "WorkOrders" or "WorkOrderDetails" or "WorkOrderStatus" or "BayDispatchMatrix" or "StaffDispatchMatrix" => ("Work Orders", "View"),
                "WorkOrderEvidencePhoto" or "UploadWorkOrderEvidence" => ("Work Orders", "View"),
                "CreateWorkOrder" or "GenerateWorkOrder" => ("Work Orders", "Create"),
                "EditWorkOrder" or "ReopenWorkOrder" => ("Work Orders", "Edit"),
                "CancelWorkOrder" => ("Work Orders", "Delete"),
                "DispatchWorkOrderBay" or "DispatchWorkOrderStaff" => ("Work Orders", "Approve"),
                "ReleaseWorkOrder" => ("Work Orders", "Approve"),
                "TransitionWorkOrderStatus" => ("Work Orders", "Edit"),
                "AddWorkOrderLaborEntry" => ("Labour", "Create"),

                "BayManagement" or "BayDetails" => ("Bays", "View"),
                "AddBay" => ("Bays", "Create"),
                "EditBay" or "SetBayMaintenance" => ("Bays", "Edit"),
                "ArchiveBay" or "DeleteBay" => ("Bays", "Delete"),

                "PartsManagement" or "PartDetails" or "PartAuditLog" or "SparesAllocation" => ("Inventory", "View"),
                "PartsPos" or "PartsPosReceipt" => ("Inventory", "View"),
                "CheckoutParts" => ("Inventory", "Create"),
                "PartEntry" or "AddPart" or "AddProductListing" => ("Inventory", "Create"),
                "EditPart" or "EditProductListing" => ("Inventory", "Edit"),
                "ArchivePart" or "SoftDeletePart" or "ArchiveProductListing" or "SoftDeleteProductListing" => ("Inventory", "Delete"),
                "AddStockMovement" => ("Inventory", "Create"),
                "EditStockMovement" => ("Inventory", "Edit"),
                "ReverseStockMovement" => ("Inventory", "Delete"),

                "LabourTimeTracking" or "ServiceTimer" or "LabourHistory" or "WorkOrderLaborTracking" => ("Labour", "View"),
                "AddLabour" or "StartLabourTimer" => ("Labour", "Create"),
                "EditLabour" or "ChangeLabourTimer" => ("Labour", "Edit"),
                "DeleteLabour" => ("Labour", "Delete"),

                "QualityControl" or "InspectionResult" or "QCFailed" or "QCPassed" => ("Inspections", "View"),
                "ExecuteInspection" or "SaveInitialInspection" => ("Inspections", "Create"),
                "InspectionPhoto" => ("Inspections", "View"),
                "SaveQualityInspection" => ("Inspections", "Approve"),
                "VoidQualityInspection" => ("Inspections", "Delete"),

                "ToolTracking" or "ToolProfile" or "ToolAuditRecords" => ("Tools", "View"),
                "AddTool" or "CheckoutTool" => ("Tools", "Create"),
                "EditTool" or "ReturnEquipment" or "StartToolMaintenance" or "CompleteToolMaintenance" => ("Tools", "Edit"),
                "ArchiveTool" or "SoftDeleteTool" => ("Tools", "Delete"),

                "InvoiceRegistry" or "InvoiceDetails" => ("Invoices", "View"),
                "GenerateInvoice" or "RecordPayment" => ("Invoices", "Create"),
                "ApplyInvoiceAdjustment" or "IssueCreditAdjustment" => ("Invoices", "Edit"),
                "IssueInvoice" or "AddInvoicePayment" => ("Invoices", "Create"),
                "EditInvoicePayment" or "AddInvoiceAdjustment" or "EditInvoiceAdjustment" => ("Invoices", "Edit"),
                "VoidInvoice" or "VoidInvoicePayment" or "VoidInvoiceAdjustment" => ("Invoices", "Delete"),

                "ArchiveRecords" or "RecordsDisposition" or "ViewGovernanceRecord" => ("Records", "View"),
                "SaveRecordsDispositionPolicy" or "RestoreArchivedRecord" or "RestoreDispositionRecord" => ("Records", "Edit"),
                "DeleteDispositionRecordPermanently" or "DeleteArchivedRecord" => ("Records", "Delete"),

                "Employees" or "ArchivedEmployees" or "SoftDeletedEmployees" or "EmployeeDetails" or "EmployeeAccountDetails" or "EmployeeSecurityActivity" or "EmployeeSchedule" or "EmployeeAvailability" or "EmployeePerformance" => ("Employees", "View"),
                "AddEmployee" => ("Employees", "Create"),
                "EditEmployee" or "RestoreEmployee" or "SetEmployeeShift" or "SaveEmployeeAvailability" => ("Employees", "Edit"),
                "ArchiveEmployee" or "SoftDeleteEmployee" => ("Employees", "Delete"),

                "Reports" or "AuditLogReport" or "SalesRevenueReport" or "LabourProductivityReport" or "BayUtilizationReport" or "InventoryValueReport" or "LowStockPartsReport" or "ToolLossesReport" or "ProfitabilityReport" or "ExportReport" => ("Reports", "View"),

                "Settings" or "SettingsLogo" or "SettingsTax" or "SettingsRoles" or "SettingsSystemPreferences" => ("Settings", "View"),
                "CreateRole" => ("Settings", "Create"),
                "EditRole" or "SaveRolePermissions" or "SaveGeneralSettings" or "SaveEmailNotifications" => ("Settings", "Edit"),
                "DeleteRole" => ("Settings", "Delete"),

                _ => null
            };
        }

        private static readonly HashSet<string> SupportedPermissionCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "dashboard.view",
            "useraccess.view", "useraccess.create", "useraccess.edit",
            "suppliers.view", "suppliers.create", "suppliers.edit",
            "services.view", "services.create", "services.edit",
            "customers.view", "customers.create", "customers.edit", "customers.delete",
            "vehicles.view", "vehicles.create", "vehicles.edit", "vehicles.delete",
            "appointments.view", "appointments.create", "appointments.edit", "appointments.delete",
            "servicequeue.view", "servicequeue.edit",
            "estimates.view", "estimates.create", "estimates.edit", "estimates.delete", "estimates.approve",
            "workorders.view", "workorders.create", "workorders.edit", "workorders.delete", "workorders.approve",
            "bays.view", "bays.create", "bays.edit", "bays.delete",
            "inventory.view", "inventory.create", "inventory.edit", "inventory.delete",
            "labour.view", "labour.create", "labour.edit", "labour.delete",
            "inspections.view", "inspections.create", "inspections.approve", "inspections.delete",
            "tools.view", "tools.create", "tools.edit", "tools.delete",
            "invoices.view", "invoices.create", "invoices.edit", "invoices.delete",
            "records.view", "records.edit", "records.delete",
            "employees.view", "employees.create", "employees.edit", "employees.delete",
            "reports.view",
            "settings.view", "settings.create", "settings.edit", "settings.delete"
        };

        public static bool IsSupportedPermission(string module, string action)
        {
            return SupportedPermissionCodes.Contains(PermissionService.BuildPermissionCode(module, action))
                || DetailedActions.Any(item => item.Module.Equals(module, StringComparison.OrdinalIgnoreCase)
                    && item.Name.Equals(action, StringComparison.OrdinalIgnoreCase));
        }
    }
}
