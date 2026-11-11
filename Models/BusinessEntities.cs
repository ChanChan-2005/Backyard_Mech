namespace Backyard_Mech.Models
{
    public class AppRole
    {
        public int AppRoleId { get; set; }
        public string RoleKey { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsSystemProtected { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class EmployeeProfile
    {
        public int EmployeeProfileId { get; set; }
        public string? UserId { get; set; }
        public string? EmployeeNumber { get; set; }
        public string? JobTitle { get; set; }
        public string? EmploymentType { get; set; }
        public string? SkillsAndQualifications { get; set; }
        public decimal? HourlyRate { get; set; }
        public DateOnly? HireDate { get; set; }
        public string? Phone { get; set; }
        public bool IsActive { get; set; } = true;
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? StreetAddress { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public string? PostalCode { get; set; }
        public bool IsArchived { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? ArchivedUtc { get; set; }
        public DateTime? DeletedUtc { get; set; }
        public string? ArchiveReason { get; set; }
        public string? DeleteReason { get; set; }
    }

    public class EmployeeRateHistory
    {
        public int EmployeeRateHistoryId { get; set; }
        public int? EmployeeProfileId { get; set; }
        public decimal? HourlyRate { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string? ChangedByUserId { get; set; }
        public string? ChangedByName { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class EmployeeAvailability
    {
        public int EmployeeAvailabilityId { get; set; }
        public int? EmployeeProfileId { get; set; }
        public int? DayOfWeek { get; set; }
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public bool IsAvailable { get; set; } = true;
    }

    public class EmployeeAvailabilityException
    {
        public int EmployeeAvailabilityExceptionId { get; set; }
        public int? EmployeeProfileId { get; set; }
        public DateTime? StartUtc { get; set; }
        public DateTime? EndUtc { get; set; }
        public string? Reason { get; set; }
        public bool IsAvailable { get; set; }
    }

    public class Permission
    {
        public int PermissionId { get; set; }
        public string? ModuleName { get; set; }
        public string? ActionName { get; set; }
        public string? PermissionCode { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class RolePermission
    {
        public int RolePermissionId { get; set; }
        public string? RoleId { get; set; }
        public int? PermissionId { get; set; }
    }

    public class AuditLogEntry
    {
        public long AuditLogEntryId { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? Action { get; set; }
        public string? EntityName { get; set; }
        public string? EntityId { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string? Details { get; set; }
    }

    public class RecordsDispositionPolicy
    {
        public int RecordsDispositionPolicyId { get; set; }
        public int RetentionValue { get; set; } = 30;
        public string RetentionUnit { get; set; } = "Days";
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
        public string? UpdatedByName { get; set; }
    }

    public class Customer
    {
        public int CustomerId { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? StreetAddress { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public string? PostalCode { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedUtc { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsArchived { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? ArchivedUtc { get; set; }
        public DateTime? DeletedUtc { get; set; }
        public string? ArchiveReason { get; set; }
        public string? DeleteReason { get; set; }
    }

    public class Vehicle
    {
        public int VehicleId { get; set; }
        public string? VIN { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
        public int? Year { get; set; }
        public string? LicensePlate { get; set; }
        public string? Color { get; set; }
        public string? OwnerFullName { get; set; }
        public int? Odometer { get; set; }
        public DateTime CreatedUtc { get; set; }
        public bool IsArchived { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? ArchivedUtc { get; set; }
        public DateTime? DeletedUtc { get; set; }
        public string? ArchiveReason { get; set; }
        public string? DeleteReason { get; set; }
    }

    public class VehicleOwnership
    {
        public int VehicleOwnershipId { get; set; }
        public int? VehicleId { get; set; }
        public int? CustomerId { get; set; }
        public string? OwnerFirstName { get; set; }
        public string? OwnerMiddleName { get; set; }
        public string? OwnerLastName { get; set; }
        public DateTime? OwnershipStartUtc { get; set; }
        public DateTime? OwnershipEndUtc { get; set; }
    }

    public class VehicleOdometerHistory
    {
        public int VehicleOdometerHistoryId { get; set; }
        public int? VehicleId { get; set; }
        public int? AppointmentId { get; set; }
        public int? Odometer { get; set; }
        public DateTime RecordedUtc { get; set; }
        public string? RecordedByUserId { get; set; }
        public string? RecordedByName { get; set; }
    }

    public class Bay
    {
        public int BayId { get; set; }
        public string? BayNumber { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class BayMaintenanceSchedule
    {
        public int BayMaintenanceScheduleId { get; set; }
        public int BayId { get; set; }
        public string? MaintenanceType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EstimatedReturnDate { get; set; }
        public string? AssignedTechnician { get; set; }
        public string? Notes { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    public class Appointment
    {
        public int AppointmentId { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int? VehicleId { get; set; }
        public int? BayId { get; set; }
        public string? PreferredMechanicUserId { get; set; }
        public string? PreferredMechanicName { get; set; }
        public string? AssignedMechanicUserId { get; set; }
        public string? AssignedMechanicName { get; set; }
        public DateTime? ScheduledStart { get; set; }
        public DateTime? ScheduledEnd { get; set; }
        public string? AppointmentType { get; set; }
        public string? AppointmentStatus { get; set; }
        public string? CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? CheckedInUtc { get; set; }
        public string? CheckedInByUserId { get; set; }
        public string? CheckedInByName { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class AppointmentService
    {
        public int AppointmentServiceId { get; set; }
        public int? AppointmentId { get; set; }
        public int? ServiceId { get; set; }
        public string? ServiceName { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class QueueStatusHistory
    {
        public int QueueStatusHistoryId { get; set; }
        public int? AppointmentId { get; set; }
        public string? QueueStatus { get; set; }
        public DateTime ChangedUtc { get; set; }
        public string? ChangedByUserId { get; set; }
        public string? ChangedByName { get; set; }
        public string? Notes { get; set; }
    }

    public class Inspection
    {
        public int InspectionId { get; set; }
        public int? AppointmentId { get; set; }
        public string? MechanicUserId { get; set; }
        public string? MechanicName { get; set; }
        public DateTime? StartedUtc { get; set; }
        public DateTime? CompletedUtc { get; set; }
        public string? Notes { get; set; }
    }

    public class InspectionMedia
    {
        public int InspectionMediaId { get; set; }
        public int? InspectionId { get; set; }
        public string? FileType { get; set; }
        public string? BlobUrl { get; set; }
        public DateTime UploadedUtc { get; set; }
        public string? UploadedByUserId { get; set; }
        public string? UploadedByName { get; set; }
    }

    public class Supplier
    {
        public int SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public string? ContactFirstName { get; set; }
        public string? ContactMiddleName { get; set; }
        public string? ContactLastName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? StreetAddress { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public string? PostalCode { get; set; }
        public string? AccountNumber { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class PartCategory
    {
        public int PartCategoryId { get; set; }
        public int? ParentPartCategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class Part
    {
        public int PartId { get; set; }
        public int? SupplierId { get; set; }
        public int? PartCategoryId { get; set; }
        public string? SKU { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal? UnitPrice { get; set; }
        public int QuantityOnHand { get; set; }
        public int ReorderThreshold { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsArchived { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? ArchivedUtc { get; set; }
        public DateTime? DeletedUtc { get; set; }
        public string? ArchiveReason { get; set; }
        public string? DeleteReason { get; set; }
    }

    public class PartTransaction
    {
        public int PartTransactionId { get; set; }
        public int? PartId { get; set; }
        public int? WorkOrderId { get; set; }
        public string? TransactionType { get; set; }
        public int? Quantity { get; set; }
        public decimal? UnitCostAtTransaction { get; set; }
        public string? PerformedByUserId { get; set; }
        public string? PerformedByName { get; set; }
        public DateTime PerformedUtc { get; set; }
        public string? Notes { get; set; }
    }

    public class ServiceCategory
    {
        public int ServiceCategoryId { get; set; }
        public int? ParentServiceCategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class Service
    {
        public int ServiceId { get; set; }
        public int? ServiceCategoryId { get; set; }
        public string? ServiceName { get; set; }
        public string? Description { get; set; }
        public decimal? DefaultPrice { get; set; }
        public decimal? DefaultEstimatedHours { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class Estimate
    {
        public int EstimateId { get; set; }
        public int? AppointmentId { get; set; }
        public string? CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class EstimateRevision
    {
        public int EstimateRevisionId { get; set; }
        public int? EstimateId { get; set; }
        public int? RevisionNumber { get; set; }
        public string? EstimateStatus { get; set; }
        public Guid? ApprovalToken { get; set; }
        public DateTime? ApprovalTokenExpiresUtc { get; set; }
        public DateTime? SentToCustomerUtc { get; set; }
        public DateTime? RespondedUtc { get; set; }
        public decimal? Subtotal { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? Total { get; set; }
        public string? RevisionReason { get; set; }
        public string? CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class EstimateRevisionLineItem
    {
        public int EstimateRevisionLineItemId { get; set; }
        public int? EstimateRevisionId { get; set; }
        public int? PartId { get; set; }
        public int? ServiceId { get; set; }
        public string? Description { get; set; }
        public string? LineType { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? LineTotal { get; set; }
        public string? ApprovalStatus { get; set; }
    }

    public class WorkOrder
    {
        public int WorkOrderId { get; set; }
        public int? AppointmentId { get; set; }
        public int? EstimateId { get; set; }
        public int? ApprovedEstimateRevisionId { get; set; }
        public int? BayId { get; set; }
        public string? WorkOrderStatus { get; set; }
        public DateTime? ScheduledStart { get; set; }
        public DateTime? ScheduledEnd { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualEnd { get; set; }
        public DateTime? ReleasedUtc { get; set; }
        public string? ReleasedByUserId { get; set; }
        public string? ReleasedByName { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class WorkOrderAssignment
    {
        public int WorkOrderAssignmentId { get; set; }
        public int? WorkOrderId { get; set; }
        public string? MechanicUserId { get; set; }
        public string? MechanicName { get; set; }
        public string? AssignmentRole { get; set; }
        public DateTime AssignedUtc { get; set; }
    }

    public class ServiceEntry
    {
        public int? ApprovedEstimateLineId { get; set; }
        public int ServiceEntryId { get; set; }
        public int? WorkOrderId { get; set; }
        public int? ServiceId { get; set; }
        public string? MechanicUserId { get; set; }
        public string? MechanicName { get; set; }
        public string? ServiceDescription { get; set; }
        public decimal? EstimatedHours { get; set; }
        public int? DurationMinutes { get; set; }
        public decimal? HourlyRate { get; set; }
        public decimal? ServiceCost { get; set; }
        public string? Notes { get; set; }
    }

    public class ServiceTimeSegment
    {
        public int ServiceTimeSegmentId { get; set; }
        public int? ServiceEntryId { get; set; }
        public DateTime? SegmentStart { get; set; }
        public DateTime? SegmentEnd { get; set; }
        public string? SegmentType { get; set; }
    }

    public class ToolCategory
    {
        public int ToolCategoryId { get; set; }
        public int? ParentToolCategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class Tool
    {
        public int ToolId { get; set; }
        public int? ToolCategoryId { get; set; }
        public string? Name { get; set; }
        public string? SerialNumber { get; set; }
        public string? Location { get; set; }
        public string? Condition { get; set; }
        public DateTime? LastCalibratedUtc { get; set; }
        public DateTime? NextCalibrationDueUtc { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsArchived { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? ArchivedUtc { get; set; }
        public DateTime? DeletedUtc { get; set; }
        public string? ArchiveReason { get; set; }
        public string? DeleteReason { get; set; }
    }

    public class ToolCheckout
    {
        public int ToolCheckoutId { get; set; }
        public int? ToolId { get; set; }
        public int? WorkOrderId { get; set; }
        public string? MechanicUserId { get; set; }
        public string? MechanicName { get; set; }
        public DateTime CheckedOutUtc { get; set; }
        public DateTime? ReturnedUtc { get; set; }
        public string? ConditionAtCheckout { get; set; }
        public string? ConditionAtReturn { get; set; }
        public string? ToolIssueStatus { get; set; }
        public string? Notes { get; set; }
    }

    public class ToolMaintenance
    {
        public int ToolMaintenanceId { get; set; }
        public int? ToolId { get; set; }
        public string? MaintenanceType { get; set; }
        public DateTime? PerformedUtc { get; set; }
        public DateTime? NextDueUtc { get; set; }
        public string? PerformedByUserId { get; set; }
        public string? PerformedByName { get; set; }
        public decimal? Cost { get; set; }
        public string? Notes { get; set; }
    }

    public class QualityCheck
    {
        public int QualityCheckId { get; set; }
        public int? WorkOrderId { get; set; }
        public string? InspectorUserId { get; set; }
        public string? InspectorName { get; set; }
        public bool? Passed { get; set; }
        public DateTime PerformedUtc { get; set; }
        public string? Notes { get; set; }
    }

    public class TaxRate
    {
        public int TaxRateId { get; set; }
        public string? Description { get; set; }
        public decimal? Rate { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }

    public class Invoice
    {
        public int InvoiceId { get; set; }
        public int? WorkOrderId { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerFirstName { get; set; }
        public string? CustomerMiddleName { get; set; }
        public string? CustomerLastName { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime IssuedUtc { get; set; }
        public int? TaxRateId { get; set; }
        public decimal? Subtotal { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? Total { get; set; }
        public string? InvoiceStatus { get; set; }
        public DateTime? PaidUtc { get; set; }
    }

    public class InvoiceLineItem
    {
        public int InvoiceLineItemId { get; set; }
        public int? InvoiceId { get; set; }
        public string? Description { get; set; }
        public string? SourceType { get; set; }
        public int? SourceId { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? LineTotal { get; set; }
    }

    public class InvoiceAdjustment
    {
        public int InvoiceAdjustmentId { get; set; }
        public int? InvoiceId { get; set; }
        public string? AdjustmentType { get; set; }
        public decimal? Amount { get; set; }
        public string? Reason { get; set; }
        public string? PerformedByUserId { get; set; }
        public string? PerformedByName { get; set; }
        public DateTime PerformedUtc { get; set; }
    }

    public class Payment
    {
        public int PaymentId { get; set; }
        public int? InvoiceId { get; set; }
        public decimal? Amount { get; set; }
        public string? PaymentMethod { get; set; }
        public string? ReferenceNumber { get; set; }
        public DateTime? PaidUtc { get; set; }
        public string? ReceivedByUserId { get; set; }
        public string? ReceivedByName { get; set; }
        public string? Notes { get; set; }
    }

    public class InvoiceNumberCounter
    {
        public int InvoiceNumberCounterId { get; set; }
        public string Prefix { get; set; } = "INV-";
        public int LastNumber { get; set; }
    }

    public class ProductListing
    {
        public int ProductListingId { get; set; }
        public string? ListingType { get; set; }
        public int? PartId { get; set; }
        public int? ServiceId { get; set; }
        public string? ListingTitle { get; set; }
        public string? ListingDescription { get; set; }
        public string? ImageUrl { get; set; }
        public decimal? DisplayPrice { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsPublished { get; set; }
        public DateTime CreatedUtc { get; set; }
        public bool IsArchived { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? ArchivedUtc { get; set; }
        public DateTime? DeletedUtc { get; set; }
        public string? ArchiveReason { get; set; }
        public string? DeleteReason { get; set; }
    }
}
