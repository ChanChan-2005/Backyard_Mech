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
            if (requirement is null || !await _permissionService.HasPermissionAsync(role, requirement.Value.Module, requirement.Value.Action))
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
        public static (string Module, string Action)? Resolve(string actionName)
        {
            return actionName switch
            {
                "Dashboard" => ("Dashboard", "View"),
                "StaffAccounts" => ("User Access", "View"),
                "CreateStaffAccount" => ("User Access", "Create"),
                "SaveUserAccess" => ("User Access", "Edit"),
                "StaffAvailability" => ("Employees", "View"),
                "CreateAvailabilityException" or "UpdateAvailabilityException" or "DeleteAvailabilityException" => ("Employees", "Edit"),
                "Suppliers" => ("Suppliers", "View"),
                "CreateSupplier" => ("Suppliers", "Create"),
                "UpdateSupplier" or "SetSupplierState" => ("Suppliers", "Edit"),
                "ServiceCatalog" => ("Services", "View"),
                "CreateService" => ("Services", "Create"),
                "UpdateService" or "SetServiceState" => ("Services", "Edit"),
                "SettingsCategories" => ("Settings", "View"),
                "CreateCategory" or "CreateTax" => ("Settings", "Create"),
                "UpdateCategory" or "SetCategoryState" or "UpdateTax" or "SetTaxState" => ("Settings", "Edit"),

                "CustomerVehicle" or "CustomerDetails" or "CustomerVehicles" or "CustomerServiceHistory" => ("Customers", "View"),
                "AddCustomer" => ("Customers", "Create"),
                "EditCustomer" => ("Customers", "Edit"),
                "AddVehicle" => ("Vehicles", "Create"),
                "VehicleHistory" => ("Vehicles", "View"),
                "TransferVehicleOwnership" or "RecordVehicleOdometer" => ("Vehicles", "Edit"),

                "ServiceQueue" or "ServiceQueueDetails" => ("Service Queue", "View"),
                "AssignQueueBay" => ("Service Queue", "Edit"),

                "Appointments" => ("Appointments", "View"),
                "CreateAppointment" => ("Appointments", "Create"),
                "EditAppointment" or "CheckInAppointment" => ("Appointments", "Edit"),
                "CancelAppointment" => ("Appointments", "Delete"),

                "EstimateApprovals" or "EstimateDetails" => ("Estimates", "View"),
                "EditEstimate" or "UpdateEstimate" or "CreateCustomerApprovalLink" => ("Estimates", "Edit"),
                "CreateEstimate" => ("Estimates", "Create"),
                "RecordEstimateDecision" => ("Estimates", "Approve"),
                "CancelEstimate" => ("Estimates", "Delete"),

                "WorkOrders" or "WorkOrderDetails" or "WorkOrderStatus" or "BayDispatchMatrix" or "StaffDispatchMatrix" => ("Work Orders", "View"),
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

                "PartsManagement" or "PartDetails" or "PartAuditLog" or "SparesAllocation" => ("Inventory", "View"),
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

                "Employees" or "ArchivedEmployees" or "SoftDeletedEmployees" or "EmployeeDetails" or "EmployeeAccountDetails" or "EmployeeSecurityActivity" or "EmployeeSchedule" or "EmployeePerformance" => ("Employees", "View"),
                "AddEmployee" => ("Employees", "Create"),
                "EditEmployee" or "RestoreEmployee" or "SetEmployeeShift" => ("Employees", "Edit"),
                "ArchiveEmployee" or "SoftDeleteEmployee" => ("Employees", "Delete"),

                "Reports" or "SalesRevenueReport" or "LabourProductivityReport" or "BayUtilizationReport" or "InventoryValueReport" or "LowStockPartsReport" or "ToolLossesReport" or "ProfitabilityReport" or "ExportReport" => ("Reports", "View"),

                "Settings" or "SettingsTax" or "SettingsRoles" or "SettingsSystemPreferences" => ("Settings", "View"),
                "CreateRole" => ("Settings", "Create"),
                "EditRole" or "SaveRolePermissions" => ("Settings", "Edit"),
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
            "customers.view", "customers.create", "customers.edit",
            "vehicles.view", "vehicles.create", "vehicles.edit",
            "appointments.view", "appointments.create", "appointments.edit", "appointments.delete",
            "servicequeue.view", "servicequeue.edit",
            "estimates.view", "estimates.create", "estimates.edit", "estimates.delete", "estimates.approve",
            "workorders.view", "workorders.create", "workorders.edit", "workorders.delete", "workorders.approve",
            "bays.view", "bays.create", "bays.edit",
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
            return SupportedPermissionCodes.Contains(PermissionService.BuildPermissionCode(module, action));
        }
    }
}
