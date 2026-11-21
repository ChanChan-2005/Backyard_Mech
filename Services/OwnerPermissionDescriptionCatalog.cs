namespace Backyard_Mech.Services;

public static class OwnerPermissionDescriptionCatalog
{
    private static readonly Dictionary<string, string> ModuleSubjects = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Appointments"] = "customer bookings, check-in, and scheduled visits",
        ["Archive"] = "archived parts, product listings, and tools",
        ["Bays"] = "service bays, assignments, and maintenance status",
        ["Customers"] = "customer profiles and their service history",
        ["Dashboard"] = "garage activity and summary cards",
        ["Employees"] = "staff profiles, schedules, and availability",
        ["Estimates"] = "repair estimates, revisions, and customer decisions",
        ["Inspections"] = "inspection results and quality-control decisions",
        ["Invoices"] = "invoices, payments, credits, and adjustments",
        ["Labour"] = "technician time entries and work timers",
        ["Parts Management"] = "inventory parts, product listings, and stock movements",
        ["Parts POS"] = "over-the-counter parts sales and receipts",
        ["Records Disposition"] = "records awaiting restoration or permanent disposal",
        ["Reports"] = "operational and financial reports",
        ["Service Queue"] = "vehicles waiting for service and their queue assignments",
        ["Services"] = "the garage's service catalog",
        ["Settings"] = "garage settings, categories, taxes, and role configuration",
        ["Suppliers"] = "supplier contact and status records",
        ["Tools"] = "tools, checkouts, returns, and maintenance",
        ["User Access"] = "staff login accounts and their assigned roles",
        ["Vehicles"] = "vehicle records, ownership, and odometer history",
        ["Work Orders"] = "repair work orders, dispatch, and completion"
    };

    public static string ModuleAction(string module, string action)
    {
        if (module.Equals("Estimates", StringComparison.OrdinalIgnoreCase))
            return action switch
            {
                "View" => "View estimates and approval status.",
                "Create" => "Start new repair estimates.",
                "Edit" => "Revise estimates and approval links.",
                "Approve" => "Record customer approval decisions.",
                _ => "Manage repair estimates."
            };
        var subject = ModuleSubjects.GetValueOrDefault(module, $"{module.ToLowerInvariant()} records");
        return action switch
        {
            "View" => $"View {subject}.",
            "Create" => $"Add new {subject}.",
            "Edit" => $"Change existing {subject}.",
            "Delete" => $"Archive, cancel, void, or remove {subject}.",
            "Approve" => $"Approve, release, or dispatch {subject}.",
            _ => $"Use {action.ToLowerInvariant()} actions for {subject}."
        };
    }

    public static string ForPermission(string module, string actionName)
    {
        if (actionName is "View" or "Create" or "Edit" or "Delete" or "Approve")
            return ModuleAction(module, actionName);
        var label = SubTabPermissionCatalog.ByActionName(actionName)?.Label ?? OwnerPermissionCatalog.ActionLabel(actionName);
        return DetailedAction(module, actionName, label);
    }

    public static string DetailedAction(string module, string actionName, string label)
    {
        if (SubTabPermissionCatalog.ByActionName(actionName) is { } tab)
            return SubTab(tab);

        var description = actionName switch
        {
            "CreateStaffAccount" => "Create a staff login with email, username, and role.",
            "SaveUserAccess" => "Change a login's email, username, role, or status.",
            "BlockStaffAccount" => "Stop this account from signing in without changing its Active or Inactive status.",
            "UnblockStaffAccount" => "Restore sign-in access to a blocked account if it is active.",
            "ResetStaffPassword" => "Reset a staff password and email it to the login address.",
            "ManageStaffRolePermissions" or "SaveRolePermissions" => "Change the permissions granted to every account assigned a selected role.",
            "StaffAccounts" => "Open the staff account list to see linked profiles, login addresses, roles, and account status.",
            "EmployeeDetails" => "Open an employee's profile to review contact, job, and employment information.",
            "AddEmployee" => "Create an employee profile with identity, contact, position, and employment details.",
            "EditEmployee" or "EmployeeAccountDetails" => "Update an employee's profile, contact details, position, and employment information.",
            "ArchiveEmployee" or "SoftDeleteEmployee" => "Remove a staff profile from active lists and block its linked login while retaining its records.",
            "RestoreEmployee" => "Restore an archived or soft-deleted employee profile to the staff directory.",
            "EmployeeSchedule" or "SetEmployeeShift" => "Review or change an employee's regular working shifts and schedule.",
            "StaffAvailability" or "CreateAvailabilityException" or "UpdateAvailabilityException" or "DeleteAvailabilityException" => "Review or maintain exceptions to an employee's regular availability, including time off and special shifts.",
            "EmployeeSecurityActivity" => "Review login and account security activity for a staff member.",
            "EmployeePerformance" => "Open performance information connected to the employee's work.",
            "ArchivedEmployees" or "SoftDeletedEmployees" => "View staff profiles that have been archived or soft deleted.",
            "AddCustomer" => "Create a customer record with contact information for visits, estimates, and billing.",
            "EditCustomer" => "Update an existing customer's contact and identifying information.",
            "CustomerServiceHistory" => "Read the customer's previous appointments, work, and service records.",
            "CustomerDetails" => "Open a customer profile to review contact information and linked service records.",
            "CustomerVehicles" => "View vehicles registered to a customer and open their records.",
            "AddVehicle" => "Register a vehicle and connect it to its customer or owner.",
            "RecordVehicleOdometer" => "Add a dated mileage reading to a vehicle's history.",
            "TransferVehicleOwnership" => "Record a change of owner while preserving the vehicle's ownership history.",
            "CreateAppointment" => "Schedule a customer visit with its vehicle, requested service, and appointment time.",
            "EditAppointment" => "Change the scheduled visit's details, timing, or assigned staff.",
            "CheckInAppointment" => "Mark an arriving customer and vehicle as checked in for service.",
            "CancelAppointment" => "Cancel a scheduled visit while retaining its record.",
            "AssignQueueBay" => "Assign a service-queue vehicle to an available work bay.",
            "ServiceQueueDetails" => "Open a queued job to review its vehicle, customer, priority, and current service status.",
            "AssignQueueMechanic" => "Assign a mechanic to a queued service job.",
            "UpdateQueueStatus" => "Move a vehicle to another stage of the service queue.",
            "CreateEstimate" => "Start a repair estimate for a customer and vehicle.",
            "EstimateDetails" => "Open an estimate to review its parts, labour, totals, revisions, and approval status.",
            "EditEstimate" => "Open an estimate's editor from the list.",
            "UpdateEstimate" => "Save a revised estimate with updated parts, labour, or prices.",
            "CreateCustomerApprovalLink" => "Create a customer-facing link for reviewing and deciding on an estimate.",
            "RecordEstimateDecision" => "Record the customer's approval or rejection of an estimate.",
            "CancelEstimate" => "Cancel an estimate and stop its pending approval flow.",
            "CreateWorkOrder" or "GenerateWorkOrder" => "Create a repair work order from approved work and assign its initial details.",
            "WorkOrderDetails" => "Open a repair order to review assigned work, parts, labour, and completion status.",
            "WorkOrderStatus" => "Review the current repair stage and progress history of a work order.",
            "EditWorkOrder" => "Change work-order details before or during repair.",
            "TransitionWorkOrderStatus" => "Move a work order through its repair status workflow.",
            "DispatchWorkOrderBay" => "Send a work order to a selected service bay.",
            "DispatchWorkOrderStaff" => "Assign a technician to work on a work order.",
            "ReleaseWorkOrder" => "Release a completed work order after required checks are satisfied.",
            "ReopenWorkOrder" => "Reopen a work order that needs more work or correction.",
            "CancelWorkOrder" => "Cancel a work order while preserving its history.",
            "BayDispatchMatrix" or "StaffDispatchMatrix" => "Open the dispatch overview to compare work orders with available bays or staff.",
            "AddBay" => "Add a service bay with its capacity and operating details.",
            "BayDetails" => "Open a bay to review its current assignment, availability, and maintenance status.",
            "EditBay" => "Change a bay's name, description, or operating details.",
            "SetBayMaintenance" => "Mark a bay in or out of maintenance so it is handled correctly during assignments.",
            "ExecuteInspection" or "SaveInitialInspection" => "Record the vehicle inspection findings used to guide repair work.",
            "SaveQualityInspection" => "Record the final quality-control result for completed repairs.",
            "VoidQualityInspection" => "Void an incorrect quality-control result while retaining its audit history.",
            "QCPassed" or "QCFailed" => "View inspections grouped by their quality-control outcome.",
            "InspectionPhoto" => "Open photos attached to an inspection result.",
            "InspectionResult" => "Review the recorded inspection findings and quality-control result for a vehicle.",
            "AddLabour" or "AddWorkOrderLaborEntry" => "Record technician labour time against approved repair work.",
            "EditLabour" => "Correct an existing technician time entry.",
            "DeleteLabour" => "Remove a labour entry while keeping the related work-order history.",
            "StartLabourTimer" or "ChangeLabourTimer" => "Start, pause, resume, or stop a technician's work timer.",
            "ServiceTimer" => "Open the timer view to follow technician time on an active repair task.",
            "LabourHistory" or "WorkOrderLaborTracking" => "Review recorded labour time and timer history for repair work.",
            "AddPart" or "PartEntry" => "Add a part to inventory with its identifying and stock details.",
            "EditPart" => "Update an inventory part's details, prices, or stock settings.",
            "AddProductListing" => "Create a product listing for parts offered by the garage.",
            "EditProductListing" => "Change a product listing's description, price, or availability.",
            "AddStockMovement" => "Record stock received, issued, reserved, returned, or adjusted.",
            "EditStockMovement" => "Correct a previously recorded stock movement.",
            "ReverseStockMovement" => "Reverse a stock movement and restore the resulting inventory balance.",
            "ManageStockTransactions" => "Open stock transaction controls for receiving, issuing, returning, and adjusting inventory.",
            "SparesAllocation" => "Review and allocate parts needed by a repair job.",
            "PartAuditLog" => "Read the change and stock history for an inventory part.",
            "PartDetails" => "Open a part to review its identification, stock level, pricing, and transaction history.",
            "ArchivePart" or "ArchiveProductListing" => "Move a part or listing out of active inventory while retaining its history.",
            "SoftDeletePart" or "SoftDeleteProductListing" => "Soft delete a part or listing without erasing its historical use.",
            "CheckoutParts" => "Complete a parts counter sale and record its receipt and stock change.",
            "PartsPosReceipt" => "Open or print the receipt for a completed parts sale.",
            "CreateSupplier" => "Add a supplier and its contact details.",
            "UpdateSupplier" => "Change a supplier's contact or business details.",
            "SetSupplierState" => "Activate or deactivate a supplier record.",
            "CreateService" => "Add a service to the catalog with its description and pricing.",
            "UpdateService" => "Change a catalog service's description, pricing, or category.",
            "SetServiceState" => "Activate or deactivate a service in the catalog.",
            "DeleteService" => "Remove a service from the catalog while keeping its repair history.",
            "GenerateInvoice" => "Open invoice creation for completed, quality-approved repair work.",
            "InvoiceDetails" => "Open an invoice to review its line items, tax, payments, adjustments, and balance.",
            "IssueInvoice" => "Create and issue an invoice from approved parts and labour on a completed work order.",
            "RecordPayment" or "AddInvoicePayment" => "Record money received against an invoice and update its balance.",
            "EditInvoicePayment" => "Correct a recorded invoice payment.",
            "VoidInvoicePayment" => "Void an incorrect payment and restore the invoice balance.",
            "AddInvoiceAdjustment" or "ApplyInvoiceAdjustment" => "Add a documented charge or credit adjustment to an invoice.",
            "EditInvoiceAdjustment" => "Correct an invoice adjustment before finalization.",
            "IssueCreditAdjustment" => "Issue a credit that reduces the customer's invoice balance.",
            "VoidInvoiceAdjustment" => "Void an adjustment while preserving its audit history.",
            "VoidInvoice" => "Void an issued invoice and preserve the reason and payment history.",
            "CreateCategory" or "UpdateCategory" or "SetCategoryState" => "Create, edit, activate, or deactivate a service, part, or tool category.",
            "CreateTax" or "UpdateTax" or "SetTaxState" => "Create, edit, activate, or deactivate a tax rate used in billing.",
            "SaveGeneralSettings" => "Save garage identity and general operating preferences.",
            "SettingsCategories" => "Open category settings for services, parts, and tools.",
            "SettingsRoles" => "Open roles and permissions to review access granted to each staff role.",
            "SettingsTax" => "Open tax settings to review the rates used when invoices are issued.",
            "SaveEmailNotifications" => "Change which automatic email notifications the garage sends.",
            "SettingsLogo" => "Open settings for the garage logo shown in the application.",
            "SettingsSystemPreferences" => "Open system-wide garage preferences.",
            "AddTool" => "Register a tool and its identifying details for tracking.",
            "EditTool" => "Change a tool's details, condition, or tracking information.",
            "CheckoutTool" => "Assign a tool to a staff member or job and record its checkout.",
            "ReturnEquipment" => "Record a checked-out tool's return and condition.",
            "StartToolMaintenance" or "CompleteToolMaintenance" => "Record when a tool enters or leaves maintenance.",
            "ArchiveTool" or "SoftDeleteTool" => "Remove a tool from active tracking while retaining its transaction history.",
            "ToolAuditRecords" => "Read a tool's checkout, return, maintenance, and change history.",
            "ToolProfile" => "Open a tool's profile to review identification, condition, assignments, and maintenance history.",
            "SaveRecordsDispositionPolicy" => "Set how long disposed records are retained before permanent deletion.",
            "RestoreArchivedRecord" or "RestoreDispositionRecord" => "Return a retained record to its active area.",
            "DeleteArchivedRecord" => "Move an archived record into records disposition for later review or permanent deletion.",
            "DeleteDispositionRecordPermanently" => "Permanently delete a disposition record when protected history allows it.",
            "ExportReport" => "Download report results in an export format for offline review.",
            "SalesRevenueReport" => "Review sales and revenue totals over the selected period.",
            "ProfitabilityReport" => "Review revenue, cost, and profit information for garage work.",
            "InventoryValueReport" => "Review the value of parts currently held in inventory.",
            "LowStockPartsReport" => "Find parts at or below their reorder level.",
            "LabourProductivityReport" => "Compare technician time and labour productivity.",
            "BayUtilizationReport" => "Review how service bays have been used over time.",
            "ToolLossesReport" => "Review tools reported lost or not returned.",
            _ when actionName.StartsWith("Dashboard", StringComparison.OrdinalIgnoreCase) =>
                $"Show the {label.Replace("Dashboard", "", StringComparison.OrdinalIgnoreCase).Trim()} summary card on the dashboard.",
            _ => $"Open or use {label} to work with {ModuleSubjects.GetValueOrDefault(module, module.ToLowerInvariant())}."
        };
        return description;
    }

    private static string SubTab(SubTabPermissionCatalog.Tab tab) => tab.Page switch
    {
        "PartsManagement" => $"Open the {tab.Label} inventory tab.",
        "ArchiveRecords" => $"Review {tab.Label.ToLowerInvariant()}.",
        "RecordsDisposition" => $"Review {tab.Label.ToLowerInvariant()} awaiting a decision.",
        "SettingsCategories" => $"Manage {tab.Label.ToLowerInvariant()}.",
        "SettingsRoles" => $"Open the {tab.Label} settings tab.",
        "Employees" => $"Show {tab.Label.ToLowerInvariant()} in the directory.",
        _ => $"Open the {tab.Label} tab."
    };
}
