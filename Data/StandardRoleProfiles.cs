using Backyard_Mech.Services;

namespace Backyard_Mech.Data;

// Explicit profiles: adding a new application action does not silently grant it to these roles.
public static class StandardRoleProfiles
{
    public static readonly string[] BusinessRoleKeys =
    {
        "Owner", "Administrator", "Service Advisor", "Mechanic/Technician",
        "Parts Clerk", "Supervisor/QC Inspector", "Accounting/Cashier"
    };

    public static bool IsBusinessRole(string? roleKey) =>
        roleKey is not null && BusinessRoleKeys.Contains(roleKey, StringComparer.OrdinalIgnoreCase);

    public sealed record Profile(string Key, string Name, string Description, string[] Actions, string[] DashboardSections)
    {
        public HashSet<string> PermissionCodes()
        {
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (DashboardSections.Length > 0) codes.Add("dashboard.view");
            foreach (var action in Actions.Concat(DashboardSections.Select(section => "Dashboard" + section)))
            {
                var requirement = OwnerPermissionCatalog.Resolve(action) ?? throw new InvalidOperationException($"Unknown action: {action}");
                codes.Add(PermissionService.BuildPermissionCode(requirement.Module, "View"));
                if (!OwnerPermissionCatalog.HasSingleActionBase(requirement.Module, requirement.Action))
                    codes.Add(PermissionService.BuildPermissionCode(requirement.Module, requirement.Action));
                // Main pages need only module View. Detail pages and operations require explicit grants.
                if (OwnerPermissionCatalog.DetailedActions.Any(item => item.Name == action))
                    codes.Add(OwnerPermissionCatalog.ActionPermissionCode(action));
            }
            return codes;
        }
    }

    public static readonly Profile[] Staff = {
        new("Administrator", "Administrator", "Manages employee accounts/roles; day-to-day system administration.",
            new[] { "StaffAccounts", "CreateStaffAccount", "SaveUserAccess", "ManageStaffRolePermissions",
                "Employees", "EmployeeDetails", "EmployeeAccountDetails", "EmployeeSecurityActivity", "ArchivedEmployees", "SoftDeletedEmployees",
                "AddEmployee", "EditEmployee", "ArchiveEmployee", "SoftDeleteEmployee", "RestoreEmployee", "EmployeeSchedule", "StaffAvailability", "SetEmployeeShift",
                "CreateAvailabilityException", "UpdateAvailabilityException", "DeleteAvailabilityException",
                "Settings", "SettingsLogo", "SaveGeneralSettings", "SettingsRoles", "CreateRole", "EditRole", "DeleteRole", "SaveRolePermissions", "SettingsSystemPreferences" },
            Array.Empty<string>()),
        new("Service Advisor", "Service Advisor", "Handles check-in, scheduling, estimates, and customer communication.",
            new[] { "CustomerVehicle", "CustomerDetails", "CustomerVehicles", "CustomerServiceHistory", "AddCustomer", "EditCustomer",
                "ArchiveCustomer", "SoftDeleteCustomer", "VehicleHistory", "AddVehicle", "RecordVehicleOdometer", "ArchiveVehicle", "SoftDeleteVehicle",
                "Appointments", "CreateAppointment", "EditAppointment", "CheckInAppointment", "CancelAppointment",
                "ServiceQueue", "ServiceQueueDetails", "AssignQueueBay", "EstimateApprovals", "EstimateDetails", "CreateEstimate", "EditEstimate", "UpdateEstimate",
                "CreateCustomerApprovalLink", "RecordEstimateDecision", "CancelEstimate", "WorkOrders", "WorkOrderDetails", "WorkOrderStatus", "CreateWorkOrder", "GenerateWorkOrder",
                "BayManagement", "BayDetails", "ServiceCatalog" },
            new[] { "ActiveWorkOrders", "VehiclesServiced", "PendingApprovals", "VehiclesInGarage", "ServiceQueue" }),
        new("Mechanic/Technician", "Mechanic / Technician", "Performs inspections and repairs; records labour time and parts used.",
            new[] { "WorkOrders", "WorkOrderDetails", "WorkOrderStatus", "TransitionWorkOrderStatus", "VehicleHistory", "ServiceCatalog",
                "LabourTimeTracking", "ServiceTimer", "LabourHistory", "WorkOrderLaborTracking", "AddWorkOrderLaborEntry", "AddLabour", "EditLabour", "StartLabourTimer", "ChangeLabourTimer",
                "QualityControl", "InspectionResult", "InspectionPhoto", "ExecuteInspection", "SaveInitialInspection",
                "PartsManagement", "PartDetails", "SparesAllocation", "AddStockMovement" },
            new[] { "ActiveWorkOrders", "VehiclesInGarage", "ServiceQueue", "LabourProductivity", "Inspections" }),
        new("Parts Clerk", "Parts Clerk", "Manages parts inventory: receiving, reservation, issue, returns.",
            new[] { "PartsManagement", "PartDetails", "PartAuditLog", "SparesAllocation", "PartEntry", "AddPart", "EditPart", "AddProductListing", "EditProductListing",
                "AddStockMovement", "EditStockMovement", "ReverseStockMovement", "ManageStockTransactions", "PartsPos", "CheckoutParts",
                "Suppliers", "CreateSupplier", "UpdateSupplier", "SetSupplierState", "WorkOrders", "WorkOrderDetails", "WorkOrderStatus" },
            new[] { "InventoryAlerts" }),
        new("Supervisor/QC Inspector", "Supervisor / QC Inspector", "Signs off completed repairs before invoicing.",
            new[] { "WorkOrders", "WorkOrderDetails", "WorkOrderStatus", "TransitionWorkOrderStatus", "ReleaseWorkOrder",
                "QualityControl", "InspectionResult", "InspectionPhoto", "QCFailed", "QCPassed", "ExecuteInspection", "SaveInitialInspection", "SaveQualityInspection",
                "VehicleHistory", "LabourTimeTracking", "LabourHistory", "WorkOrderLaborTracking" },
            new[] { "ActiveWorkOrders", "VehiclesServiced", "VehiclesInGarage", "ServiceQueue", "LabourProductivity", "Inspections" }),
        new("Accounting/Cashier", "Accounting / Cashier", "Handles invoicing, payments, and financial reporting.",
            new[] { "InvoiceRegistry", "InvoiceDetails", "GenerateInvoice", "IssueInvoice", "RecordPayment", "AddInvoicePayment", "EditInvoicePayment", "PartsPos", "CheckoutParts",
                "ApplyInvoiceAdjustment", "IssueCreditAdjustment", "AddInvoiceAdjustment", "EditInvoiceAdjustment",
                "CustomerVehicle", "CustomerDetails", "CustomerVehicles", "WorkOrders", "WorkOrderDetails", "WorkOrderStatus", "EstimateApprovals", "EstimateDetails",
                "Reports", "SalesRevenueReport", "ExportReport" },
            new[] { "Revenue", "VehiclesServiced", "UnpaidInvoices", "RecentInvoices" })
    };
}
