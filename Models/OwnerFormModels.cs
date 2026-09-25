using System.ComponentModel.DataAnnotations;

namespace Backyard_Mech.Models
{
    public class CustomerFormModel
    {
        public int CustomerId { get; set; }

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string StreetAddress { get; set; } = string.Empty;

        public string? Notes { get; set; }
    }

    public class VehicleFormModel
    {
        public int CustomerId { get; set; }

        [Required]
        public string Make { get; set; } = string.Empty;

        [Required]
        public string Model { get; set; } = string.Empty;

        [Required]
        public int? Year { get; set; }

        public string? Color { get; set; }

        [Required]
        public string LicensePlate { get; set; } = string.Empty;

        public int? Odometer { get; set; }

        [Required]
        public string VIN { get; set; } = string.Empty;
    }

    public class ProductListingFormModel
    {
        public int ProductListingId { get; set; }

        [Required]
        [StringLength(40)]
        public string ListingType { get; set; } = "Part";

        public int? PartId { get; set; }

        public int? ServiceId { get; set; }

        [Required]
        [StringLength(200)]
        public string ListingTitle { get; set; } = string.Empty;

        public string? ListingDescription { get; set; }

        [StringLength(1000)]
        [Url]
        public string? ImageUrl { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999.99")]
        public decimal? DisplayPrice { get; set; }

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; }

        public bool IsFeatured { get; set; }

        public bool IsPublished { get; set; }
    }

    public class PartFormModel
    {
        public int PartId { get; set; }

        public int? SupplierId { get; set; }

        public int? PartCategoryId { get; set; }

        [Required]
        public string SKU { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public decimal? UnitCost { get; set; }

        public decimal? UnitPrice { get; set; }

        [Range(0, int.MaxValue)]
        public int QuantityOnHand { get; set; }

        [Range(0, int.MaxValue)]
        public int ReorderThreshold { get; set; }
    }

    public class PartStateFormModel
    {
        public int PartId { get; set; }
        public string? Reason { get; set; }
    }

    public class ToolFormModel
    {
        public int ToolId { get; set; }

        public int? ToolCategoryId { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string SerialNumber { get; set; } = string.Empty;

        public string? Location { get; set; }

        public string? Condition { get; set; }

        public DateTime? LastCalibratedUtc { get; set; }

        public DateTime? NextCalibrationDueUtc { get; set; }
    }

    public class ToolStateFormModel
    {
        public int ToolId { get; set; }
        public string? Reason { get; set; }
    }

    public class EmployeeFormModel
    {
        public int EmployeeProfileId { get; set; }

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        public string? MiddleInitial { get; set; }

        [Required]
        public string StreetAddress { get; set; } = string.Empty;

        [Required]
        public string City { get; set; } = string.Empty;

        public string? Province { get; set; }

        [Required]
        public string PostalCode { get; set; } = string.Empty;

        [Required]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string JobTitle { get; set; } = string.Empty;

        [Required]
        public decimal? HourlyRate { get; set; }

        public string? RoleId { get; set; }
    }

    public class EmployeeStateFormModel
    {
        public int EmployeeProfileId { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
    }

    public class EmployeeShiftFormModel
    {
        [Range(1, int.MaxValue)]
        public int EmployeeProfileId { get; set; }

        [Range(0, 6)]
        public int DayOfWeek { get; set; }

        public bool IsAvailable { get; set; } = true;

        [Required]
        public string StartTime { get; set; } = "08:00";

        [Required]
        public string EndTime { get; set; } = "17:00";
    }

    public class BayEditFormModel
    {
        public int BayId { get; set; }

        [Required]
        public string BayNumber { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string Status { get; set; } = "Available";
    }

    public class BayMaintenanceFormModel
    {
        public int BayId { get; set; }

        [Required]
        public string MaintenanceType { get; set; } = string.Empty;

        [Required]
        public DateTime? StartDate { get; set; }

        [Required]
        public DateTime? EstimatedReturnDate { get; set; }

        public string? AssignedTechnician { get; set; }

        [Required]
        public string Notes { get; set; } = string.Empty;
    }

    public class QueueBayAssignmentFormModel
    {
        public int AppointmentId { get; set; }

        [Range(1, int.MaxValue)]
        public int BayId { get; set; }
    }

    public class RecordActionFormModel
    {
        [Required]
        public string RecordType { get; set; } = string.Empty;

        public int RecordId { get; set; }

        public string? Reason { get; set; }
    }

    public class RecordsDispositionPolicyFormModel
    {
        [Range(1, 10000)]
        public int RetentionValue { get; set; } = 30;

        [Required]
        public string RetentionUnit { get; set; } = "Days";
    }

    public class RecordGovernanceViewModel
    {
        public IReadOnlyList<RecordGovernanceItem> Records { get; set; } = Array.Empty<RecordGovernanceItem>();
        public int RetentionValue { get; set; } = 30;
        public string RetentionUnit { get; set; } = "Days";
        public int RetentionDays { get; set; } = 30;
        public string ActiveTab { get; set; } = "all";
        public int AllCount { get; set; }
        public int PartsCount { get; set; }
        public int ProductListingsCount { get; set; }
        public int ToolsCount { get; set; }
    }

    public class RecordGovernanceItem
    {
        public string RecordType { get; set; } = string.Empty;
        public int RecordId { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public string RecordName { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime? MovedUtc { get; set; }
        public string MovedDate => MovedUtc?.ToString("MMM dd, yyyy") ?? "Not recorded";
        public DateTime? EligibleUtc { get; set; }
        public string EligibleDate => EligibleUtc?.ToString("MMM dd, yyyy") ?? "Pending date";
        public string Remaining { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Tone { get; set; } = "gray";
    }

    public class CustomerListItem
    {
        public int CustomerId { get; set; }
        public string Initials { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string StreetAddress { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string Vehicles { get; set; } = "0 Vehicles";
        public string LastVisit { get; set; } = "No visits";
        public string TotalSpent { get; set; } = "$0.00";
        public string Status { get; set; } = "Active";
    }

    public class EmployeeListItem
    {
        public int EmployeeProfileId { get; set; }
        public string Initials { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public string Tone { get; set; } = "active";
        public string Util { get; set; } = "0%";
        public string Progress { get; set; } = "progress-0";
        public string? StateDate { get; set; }
        public string? Reason { get; set; }
    }

    public class EmployeeWorkOrderRow
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string Vehicle { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public string Hours { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Tone { get; set; } = "green";
    }

    public class EmployeeShiftDay
    {
        public string Day { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public bool IsOff { get; set; }
    }

    public class EmployeeDetailViewModel
    {
        public int EmployeeProfileId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string HireDate { get; set; } = string.Empty;
        public string EmployeeNumber { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public string JobsCompleted { get; set; } = "0";
        public string AvgJobTime { get; set; } = "0 hrs";
        public string TargetJobTime { get; set; } = "Target: 2.5h";
        public string Rating { get; set; } = "4.8/5.0";
        public string RatingDetail { get; set; } = "reviews";
        public string Utilization { get; set; } = "0%";
        public string UtilizationDetail { get; set; } = "Current";
        public IReadOnlyList<string> Certifications { get; set; } = Array.Empty<string>();
        public IReadOnlyList<EmployeeWorkOrderRow> RecentWorkOrders { get; set; } = Array.Empty<EmployeeWorkOrderRow>();
        public IReadOnlyList<EmployeeShiftDay> WeekSchedule { get; set; } = Array.Empty<EmployeeShiftDay>();
    }

    public class EmployeePerformancePersonRow
    {
        public int Rank { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int Jobs { get; set; }
        public string Rating { get; set; } = string.Empty;
        public string Revenue { get; set; } = string.Empty;
        public int Progress { get; set; }
    }

    public class EmployeePerformanceMonthRow
    {
        public string Month { get; set; } = string.Empty;
        public int Value { get; set; }
        public bool IsGoal { get; set; }
    }

    public class EmployeePerformanceViewModel
    {
        public int TotalJobsCompleted { get; set; }
        public string TotalJobsTrend { get; set; } = "+12% vs last month";
        public string AverageRating { get; set; } = "4.8 / 5";
        public string RatingTarget { get; set; } = "Target: 4.5";
        public string ShopUtilization { get; set; } = "0%";
        public string UtilizationTrend { get; set; } = "+2% vs last week";
        public string RevenuePerTech { get; set; } = "$0.00";
        public string HighestTech { get; set; } = "Highest: -";
        public IReadOnlyList<EmployeePerformancePersonRow> Employees { get; set; } = Array.Empty<EmployeePerformancePersonRow>();
        public IReadOnlyList<EmployeePerformanceMonthRow> MonthlyEfficiency { get; set; } = Array.Empty<EmployeePerformanceMonthRow>();
    }

    public class EmployeeScheduleCell
    {
        public string Label { get; set; } = "OFF";
        public bool IsOff { get; set; } = true;
    }

    public class EmployeeScheduleRow
    {
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public IReadOnlyList<EmployeeScheduleCell> Days { get; set; } = Array.Empty<EmployeeScheduleCell>();
    }

    public class EmployeeTimeOffRow
    {
        public string Name { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Status { get; set; } = "Approved";
        public string Tone { get; set; } = "yellow";
    }

    public class EmployeeScheduleViewModel
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public IReadOnlyList<string> DayLabels { get; set; } = Array.Empty<string>();
        public IReadOnlyList<ProductListingOption> EmployeeOptions { get; set; } = Array.Empty<ProductListingOption>();
        public IReadOnlyList<EmployeeScheduleRow> Employees { get; set; } = Array.Empty<EmployeeScheduleRow>();
        public int MechanicsActive { get; set; }
        public int AdvisorsActive { get; set; }
        public string CapacityLabel { get; set; } = "Optimal (85%)";
        public IReadOnlyList<EmployeeTimeOffRow> UpcomingTimeOff { get; set; } = Array.Empty<EmployeeTimeOffRow>();
    }

    public class EmployeeSecurityActivityRow
    {
        public string Event { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Actor { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string Tone { get; set; } = "blue";
    }

    public class EmployeeSecurityActivityViewModel
    {
        public int EmployeeProfileId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string HireDate { get; set; } = string.Empty;
        public string EmployeeNumber { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public string LastActivity { get; set; } = "No activity recorded";
        public int SecurityEvents { get; set; }
        public int ProfileChanges { get; set; }
        public int AccessChanges { get; set; }
        public IReadOnlyList<EmployeeSecurityActivityRow> Activity { get; set; } = Array.Empty<EmployeeSecurityActivityRow>();
    }

    public class SettingsRoleOption
    {
        public int AppRoleId { get; set; }
        public string RoleId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int UserCount { get; set; }
        public bool IsSelected { get; set; }
        public bool IsProtected { get; set; }
    }

    public class SettingsPermissionCell
    {
        public int? PermissionId { get; set; }
        public bool IsChecked { get; set; }
        public bool Exists { get; set; }
    }

    public class SettingsPermissionRow
    {
        public string ModuleName { get; set; } = string.Empty;
        public Dictionary<string, SettingsPermissionCell> Actions { get; set; } = new();
    }

    public class SettingsRolesViewModel
    {
        public string SelectedRoleId { get; set; } = "Owner";
        public string SelectedRoleLabel { get; set; } = "Garage Owner";
        public bool IsSystemProtected { get; set; }
        public IReadOnlyList<string> ActionColumns { get; set; } = Array.Empty<string>();
        public IReadOnlyList<SettingsRoleOption> Roles { get; set; } = Array.Empty<SettingsRoleOption>();
        public IReadOnlyList<SettingsPermissionRow> PermissionRows { get; set; } = Array.Empty<SettingsPermissionRow>();
        public int TotalPermissionCount { get; set; }
        public bool CanCreateRole { get; set; }
        public bool CanEditRole { get; set; }
        public bool CanDeleteRole { get; set; }
    }

    public class SettingsRolePermissionFormModel
    {
        [Required]
        public string RoleId { get; set; } = string.Empty;

        public List<int> PermissionIds { get; set; } = new();
    }

    public class SettingsRoleFormModel
    {
        public int AppRoleId { get; set; }

        [Required]
        [StringLength(120, MinimumLength = 2)]
        public string RoleName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }
    }

    public class DeleteSettingsRoleFormModel
    {
        [Required]
        public string RoleId { get; set; } = string.Empty;
    }

    public class AppointmentListItem
    {
        public int AppointmentId { get; set; }
        public int? CustomerId { get; set; }
        public int? VehicleId { get; set; }
        public int? BayId { get; set; }
        public int? AssignedEmployeeProfileId { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string Vehicle { get; set; } = string.Empty;
        public string Vin { get; set; } = string.Empty;
        public string Plate { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public int BayNumber { get; set; }
        public string Bay { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Tone { get; set; } = "blue";
        public string Advisor { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string Priority { get; set; } = "Normal";
        public string PriorityTone { get; set; } = "blue";
        public string DateInput { get; set; } = string.Empty;
        public string StartInput { get; set; } = string.Empty;
        public string EndInput { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class AppointmentCustomerOption
    {
        public int CustomerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
    }

    public class AppointmentVehicleOption
    {
        public int VehicleId { get; set; }
        public int CustomerId { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class AppointmentFormModel
    {
        public int AppointmentId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Please select a customer.")]
        public int CustomerId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Please select a vehicle.")]
        public int VehicleId { get; set; }

        public int? BayId { get; set; }
        public int? AssignedEmployeeProfileId { get; set; }

        public DateTime ScheduledDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        [Required]
        [StringLength(100)]
        public string AppointmentType { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string AppointmentStatus { get; set; } = "Scheduled";

        [StringLength(50)]
        public string Priority { get; set; } = "Normal";

        [StringLength(2000)]
        public string? Notes { get; set; }

        public bool IsWalkIn { get; set; }
        public string ViewMode { get; set; } = "calendar";
    }

    public class CancelAppointmentFormModel
    {
        [Range(1, int.MaxValue)]
        public int AppointmentId { get; set; }

        [StringLength(500)]
        public string? Reason { get; set; }
        public string ViewMode { get; set; } = "calendar";
    }

    public class ServiceQueueColumnItem
    {
        public string Title { get; set; } = string.Empty;
        public string Dot { get; set; } = "gray";
        public List<AppointmentListItem> Cards { get; set; } = new();
        public int Count => Cards.Count;
    }

    public class WorkOrderListItem
    {
        public int WorkOrderId { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string Vehicle { get; set; } = string.Empty;
        public string Bay { get; set; } = string.Empty;
        public string Mechanic { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusTone { get; set; } = "pending";
        public string Priority { get; set; } = "Normal";
        public string PriorityTone { get; set; } = "blue";
        public string Expected { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    public class WorkOrderActivityItem
    {
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
    }

    public class WorkOrderLaborRow
    {
        public string Mechanic { get; set; } = string.Empty;
        public string Task { get; set; } = string.Empty;
        public string Hours { get; set; } = string.Empty;
        public string Cost { get; set; } = string.Empty;
        public decimal HoursValue { get; set; }
        public decimal CostValue { get; set; }
    }

    public class WorkOrderPartRow
    {
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Quantity { get; set; } = string.Empty;
        public string UnitPrice { get; set; } = string.Empty;
        public string TotalCost { get; set; } = string.Empty;
    }

    public class WorkOrderDetailsViewModel
    {
        public int WorkOrderId { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string Vehicle { get; set; } = string.Empty;
        public string Vin { get; set; } = string.Empty;
        public string Plate { get; set; } = string.Empty;
        public string Bay { get; set; } = string.Empty;
        public string Mechanic { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusTone { get; set; } = "pending";
        public string Priority { get; set; } = "Normal";
        public string PriorityTone { get; set; } = "blue";
        public string ScheduledStart { get; set; } = string.Empty;
        public string ScheduledEnd { get; set; } = string.Empty;
        public string ActualStart { get; set; } = string.Empty;
        public string ActualEnd { get; set; } = string.Empty;
        public string Hours { get; set; } = "0 Hrs";
        public string ServiceSummary { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string EstimateId { get; set; } = string.Empty;
        public string Initials { get; set; } = "WO";
        public string Odometer { get; set; } = string.Empty;
        public string LaborTotal { get; set; } = "$0.00";
        public string PartsTotal { get; set; } = "$0.00";
        public string ShopSuppliesFee { get; set; } = "$0.00";
        public string EstimatedTotal { get; set; } = "$0.00";
        public string LoggedHours { get; set; } = "0 hrs";
        public string PlannedHours { get; set; } = "0 hrs";
        public IReadOnlyList<string> Services { get; set; } = Array.Empty<string>();
        public IReadOnlyList<WorkOrderLaborRow> LaborRows { get; set; } = Array.Empty<WorkOrderLaborRow>();
        public IReadOnlyList<WorkOrderPartRow> PartRows { get; set; } = Array.Empty<WorkOrderPartRow>();
        public IReadOnlyList<WorkOrderActivityItem> Activity { get; set; } = Array.Empty<WorkOrderActivityItem>();
    }

    public class WorkOrderBayDispatchRow
    {
        public int BayId { get; set; }
        public string BayIdentifier { get; set; } = string.Empty;
        public string HardwareType { get; set; } = string.Empty;
        public string BayStatus { get; set; } = string.Empty;
        public string StatusTone { get; set; } = "available";
        public string CurrentOccupant { get; set; } = string.Empty;
        public string EstimatedRelease { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public bool CanDispatch { get; set; } = true;
    }

    public class WorkOrderBayDispatchViewModel
    {
        public WorkOrderDetailsViewModel WorkOrder { get; set; } = new();
        public IReadOnlyList<WorkOrderBayDispatchRow> Bays { get; set; } = Array.Empty<WorkOrderBayDispatchRow>();
    }

    public class WorkOrderStaffDispatchRow
    {
        public int EmployeeProfileId { get; set; }
        public string Mechanic { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public string ActiveJobs { get; set; } = string.Empty;
        public string Availability { get; set; } = string.Empty;
        public string AvailabilityTone { get; set; } = "available";
        public string Certifications { get; set; } = string.Empty;
        public bool IsLead { get; set; }
        public bool IsHelper { get; set; }
    }

    public class WorkOrderStaffDispatchViewModel
    {
        public WorkOrderDetailsViewModel WorkOrder { get; set; } = new();
        public IReadOnlyList<WorkOrderStaffDispatchRow> Mechanics { get; set; } = Array.Empty<WorkOrderStaffDispatchRow>();
        public string Warning { get; set; } = string.Empty;
    }

    public class WorkOrderLaborTrackingViewModel
    {
        public WorkOrderDetailsViewModel WorkOrder { get; set; } = new();
        public IReadOnlyList<WorkOrderLaborRow> Entries { get; set; } = Array.Empty<WorkOrderLaborRow>();
        public IReadOnlyList<WorkOrderLaborMechanicSummary> MechanicSummaries { get; set; } = Array.Empty<WorkOrderLaborMechanicSummary>();
        public string ActiveTimerText { get; set; } = "00:00:00";
        public bool HasActiveTimer { get; set; }
    }

    public class WorkOrderLaborMechanicSummary
    {
        public string Mechanic { get; set; } = string.Empty;
        public string Hours { get; set; } = string.Empty;
        public int Percent { get; set; }
    }

    public class WorkOrderBayDispatchFormModel
    {
        public int WorkOrderId { get; set; }
        public int BayId { get; set; }
    }

    public class WorkOrderStaffDispatchFormModel
    {
        public int WorkOrderId { get; set; }
        public int LeadEmployeeProfileId { get; set; }
        public int? HelperEmployeeProfileId { get; set; }
    }

    public class WorkOrderStatusTransitionFormModel
    {
        public int WorkOrderId { get; set; }
        public string Transition { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    public class WorkOrderLaborEntryFormModel
    {
        public int WorkOrderId { get; set; }

        [Required]
        public string MechanicName { get; set; } = string.Empty;

        [Required]
        public string TaskDescription { get; set; } = string.Empty;

        [Range(0.1, 99)]
        public decimal Hours { get; set; }

        [Range(0, 10000)]
        public decimal HourlyRate { get; set; }
    }

    public class BayListItem
    {
        public int BayId { get; set; }
        public string No { get; set; } = string.Empty;
        public string BayNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string Status { get; set; } = string.Empty;
        public string Tone { get; set; } = "available";
        public string Vehicle { get; set; } = "-";
        public string Tech { get; set; } = "-";
        public string Tools { get; set; } = "0";
        public string MaintenanceWindow { get; set; } = string.Empty;
    }

    public class PartListItem
    {
        public int PartId { get; set; }
        public int? SupplierId { get; set; }
        public int? PartCategoryId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RawDescription { get; set; } = string.Empty;
        public decimal? UnitCost { get; set; }
        public decimal? UnitPrice { get; set; }
        public int QuantityOnHand { get; set; }
        public int ReorderThreshold { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Cost { get; set; } = string.Empty;
        public string Selling { get; set; } = string.Empty;
        public string Available { get; set; } = string.Empty;
        public string Reserved { get; set; } = "0 units";
        public string Threshold { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Tone { get; set; } = "stock";
        public string QuantityClass { get; set; } = string.Empty;
    }

    public class PartsManagementViewModel
    {
        public string ActiveTab { get; set; } = "parts";
        public IReadOnlyList<PartListItem> Parts { get; set; } = Array.Empty<PartListItem>();
        public IReadOnlyList<ProductListingListItem> ProductListings { get; set; } = Array.Empty<ProductListingListItem>();
        public IReadOnlyList<ProductListingOption> PartOptions { get; set; } = Array.Empty<ProductListingOption>();
        public IReadOnlyList<ProductListingOption> ServiceOptions { get; set; } = Array.Empty<ProductListingOption>();
        public IReadOnlyList<ProductListingOption> PartCategoryOptions { get; set; } = Array.Empty<ProductListingOption>();
        public IReadOnlyList<ProductListingOption> SupplierOptions { get; set; } = Array.Empty<ProductListingOption>();
    }

    public class ProductListingListItem
    {
        public int ProductListingId { get; set; }
        public string ListingType { get; set; } = string.Empty;
        public string LinkedItem { get; set; } = string.Empty;
        public string ListingTitle { get; set; } = string.Empty;
        public string ListingDescription { get; set; } = string.Empty;
        public int? PartId { get; set; }
        public int? ServiceId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public decimal? DisplayPriceValue { get; set; }
        public string DisplayPrice { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsPublished { get; set; }
        public string Status => IsPublished ? "Published" : "Draft";
        public string StatusTone => IsPublished ? "stock" : "gray";
        public string FeaturedLabel => IsFeatured ? "Featured" : "Standard";
        public string Created { get; set; } = string.Empty;
    }

    public class ProductListingOption
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class QueueEmployeeOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int ActiveJobs { get; set; }
        public bool IsSelected { get; set; }
    }

    public class QueueBayOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusClass { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }

    public class ToolListItem
    {
        public int ToolId { get; set; }
        public int? ToolCategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Serial { get; set; } = string.Empty;
        public string RawLocation { get; set; } = string.Empty;
        public string RawCondition { get; set; } = string.Empty;
        public DateTime? LastCalibratedUtc { get; set; }
        public DateTime? NextCalibrationDueUtc { get; set; }
        public string LastCalibratedDate => LastCalibratedUtc?.ToString("yyyy-MM-dd") ?? "";
        public string NextCalibrationDueDate => NextCalibrationDueUtc?.ToString("yyyy-MM-dd") ?? "";
        public string Category { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public string Calibration { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Tone { get; set; } = "available";
        public string Action { get; set; } = "View";
    }

    public class ToolTrackingViewModel
    {
        public IReadOnlyList<ToolListItem> Tools { get; set; } = Array.Empty<ToolListItem>();
        public IReadOnlyList<ProductListingOption> ToolCategoryOptions { get; set; } = Array.Empty<ProductListingOption>();
    }

    public class InvoiceListItem
    {
        public int InvoiceId { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string Vehicle { get; set; } = string.Empty;
        public string WorkOrder { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string Paid { get; set; } = string.Empty;
        public string Balance { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Tone { get; set; } = "draft";
        public string Date { get; set; } = string.Empty;
    }

    public class EstimateListItem
    {
        public int EstimateId { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string Vehicle { get; set; } = string.Empty;
        public string Services { get; set; } = string.Empty;
        public string Total { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Tone { get; set; } = "draft";
        public string Date { get; set; } = string.Empty;
    }
}
