using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Backyard_Mech.Services
{
    public sealed class OwnerPermissionFilter : IAsyncActionFilter
    {
        private readonly PermissionService _permissionService;

        public OwnerPermissionFilter(PermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var role = context.HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrWhiteSpace(role))
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

                "CustomerVehicle" or "CustomerDetails" or "CustomerVehicles" or "CustomerServiceHistory" => ("Customers", "View"),
                "AddCustomer" => ("Customers", "Create"),
                "EditCustomer" => ("Customers", "Edit"),
                "AddVehicle" => ("Vehicles", "Create"),

                "ServiceQueue" or "ServiceQueueDetails" => ("Service Queue", "View"),
                "AssignQueueBay" => ("Service Queue", "Edit"),

                "Appointments" => ("Appointments", "View"),
                "CreateAppointment" => ("Appointments", "Create"),
                "EditAppointment" => ("Appointments", "Edit"),
                "CancelAppointment" => ("Appointments", "Delete"),

                "EstimateApprovals" or "EstimateDetails" => ("Estimates", "View"),
                "EditEstimate" => ("Estimates", "Edit"),

                "WorkOrders" or "WorkOrderDetails" or "WorkOrderLaborTracking" or "WorkOrderStatus" or "BayDispatchMatrix" or "StaffDispatchMatrix" => ("Work Orders", "View"),
                "CreateWorkOrder" => ("Work Orders", "Create"),
                "DispatchWorkOrderBay" or "DispatchWorkOrderStaff" => ("Work Orders", "Approve"),
                "TransitionWorkOrderStatus" => ("Work Orders", "Edit"),
                "AddWorkOrderLaborEntry" => ("Work Orders", "Create"),

                "BayManagement" or "BayDetails" => ("Bays", "View"),
                "AddBay" => ("Bays", "Create"),
                "EditBay" or "SetBayMaintenance" => ("Bays", "Edit"),

                "PartsManagement" or "PartDetails" or "PartAuditLog" or "SparesAllocation" => ("Inventory", "View"),
                "PartEntry" or "AddPart" or "AddProductListing" => ("Inventory", "Create"),
                "EditPart" or "EditProductListing" => ("Inventory", "Edit"),
                "ArchivePart" or "SoftDeletePart" or "ArchiveProductListing" or "SoftDeleteProductListing" => ("Inventory", "Delete"),

                "LabourTimeTracking" or "ServiceTimer" or "LabourHistory" => ("Labour", "View"),

                "QualityControl" or "InspectionResult" or "QCFailed" or "QCPassed" => ("Inspections", "View"),
                "ExecuteInspection" => ("Inspections", "Create"),

                "ToolTracking" or "ToolProfile" or "ToolAuditRecords" => ("Tools", "View"),
                "AddTool" or "CheckoutTool" => ("Tools", "Create"),
                "EditTool" or "ReturnEquipment" => ("Tools", "Edit"),
                "ArchiveTool" or "SoftDeleteTool" => ("Tools", "Delete"),

                "InvoiceRegistry" or "InvoiceDetails" => ("Invoices", "View"),
                "GenerateInvoice" or "RecordPayment" or "IssueCreditAdjustment" => ("Invoices", "Create"),
                "ApplyInvoiceAdjustment" => ("Invoices", "Edit"),

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
            "customers.view", "customers.create", "customers.edit",
            "vehicles.create",
            "appointments.view", "appointments.create", "appointments.edit", "appointments.delete",
            "servicequeue.view", "servicequeue.edit",
            "estimates.view", "estimates.edit",
            "workorders.view", "workorders.create", "workorders.edit", "workorders.approve",
            "bays.view", "bays.create", "bays.edit",
            "inventory.view", "inventory.create", "inventory.edit", "inventory.delete",
            "labour.view",
            "inspections.view", "inspections.create",
            "tools.view", "tools.create", "tools.edit", "tools.delete",
            "invoices.view", "invoices.create", "invoices.edit",
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
