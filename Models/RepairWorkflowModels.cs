using System.ComponentModel.DataAnnotations;

namespace Backyard_Mech.Models;

public sealed class EstimateSaveForm
{
    public int EstimateId { get; set; }
    public int ExpectedRevisionId { get; set; }
    [Range(1, int.MaxValue)] public int AppointmentId { get; set; }
    public int? TaxRateId { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = "Initial estimate";
    [MinLength(1), MaxLength(100)] public List<EstimateLineForm> Lines { get; set; } = new();
}

public sealed class EstimateLineForm
{
    [Required, RegularExpression("Part|Labour")] public string LineType { get; set; } = "Labour";
    public int? PartId { get; set; }
    public int? ServiceId { get; set; }
    [Required, StringLength(500)] public string Description { get; set; } = "";
    [Range(typeof(decimal), "0.01", "100000")] public decimal Quantity { get; set; } = 1;
    [Range(typeof(decimal), "0", "1000000")] public decimal UnitPrice { get; set; }
}

public sealed class EstimateDecisionForm
{
    public int EstimateId { get; set; }
    public int ExpectedRevisionId { get; set; }
    [Required, RegularExpression("Approved|Declined")] public string Decision { get; set; } = "";
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}

public sealed class WorkflowReasonForm
{
    [Range(1, int.MaxValue)] public int Id { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}

public sealed class WorkOrderSaveForm
{
    public int WorkOrderId { get; set; }
    [Range(1, int.MaxValue)] public int EstimateId { get; set; }
    [Range(1, int.MaxValue)] public int EmployeeProfileId { get; set; }
    [Range(1, int.MaxValue)] public int BayId { get; set; }
    [Required] public DateTime? ScheduledStart { get; set; }
    [Required] public DateTime? ScheduledEnd { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
}

public sealed class StockMovementForm
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired]
    public Guid SubmissionToken { get; set; } = Guid.NewGuid();
    public int PartTransactionId { get; set; }
    [Range(1, int.MaxValue)] public int PartId { get; set; }
    public int? WorkOrderId { get; set; }
    [Required, RegularExpression("Received|Reserved|Installed|Returned|Released|Wasted|Adjustment In|Adjustment Out")]
    public string TransactionType { get; set; } = "Received";
    [Range(1, 100000)] public int Quantity { get; set; } = 1;
    [Required, StringLength(1000)] public string Notes { get; set; } = "";
}

public sealed class InitialInspectionForm
{
    public int AppointmentId { get; set; }
    [Required, StringLength(3000)] public string Findings { get; set; } = "";
    [StringLength(2000)] public string? RecommendedWork { get; set; }
    public List<IFormFile> Photos { get; set; } = new();
}

public sealed class CustomerApprovalForm
{
    public Guid Token { get; set; }
    [Required, RegularExpression("Approved|Partially Approved|Declined|Changes Requested")]
    public string Decision { get; set; } = "";
    public List<int> ApprovedLineIds { get; set; } = new();
    [StringLength(1000)] public string? CustomerNote { get; set; }
}

public sealed class ToolCheckoutForm
{
    public int ToolId { get; set; }
    [Range(1, int.MaxValue)] public int EmployeeProfileId { get; set; }
    public int? WorkOrderId { get; set; }
    [Required, RegularExpression("Excellent|Good|Fair")]
    public string ConditionAtCheckout { get; set; } = "Good";
    [StringLength(1000)] public string? Notes { get; set; }
}

public sealed class ToolReturnForm
{
    public int ToolCheckoutId { get; set; }
    [Required, RegularExpression("Excellent|Good|Fair|Damaged|Missing")]
    public string ConditionAtReturn { get; set; } = "Good";
    [Required, StringLength(1000)] public string Notes { get; set; } = "";
}

public sealed class ToolMaintenanceForm
{
    public int ToolId { get; set; }
    [Required, StringLength(100)] public string MaintenanceType { get; set; } = "Repair";
    [Required, StringLength(1000)] public string Notes { get; set; } = "";
    [Range(typeof(decimal), "0", "10000000")] public decimal Cost { get; set; }
    [Required, RegularExpression("Excellent|Good|Fair|Damaged")] public string ConditionAfter { get; set; } = "Good";
    public DateTime? NextDueUtc { get; set; }
}

public sealed class LabourSaveForm
{
    public int? ApprovedEstimateLineId { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired]
    public Guid SubmissionToken { get; set; } = Guid.NewGuid();
    public int ServiceEntryId { get; set; }
    [Range(1, int.MaxValue)] public int WorkOrderId { get; set; }
    [Range(1, int.MaxValue)] public int EmployeeProfileId { get; set; }
    [Required, StringLength(500)] public string Description { get; set; } = "";
    [Range(1, 60000)] public int Minutes { get; set; } = 60;
    [Range(typeof(decimal), "0", "1000000")] public decimal HourlyRate { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
}

public sealed class TimerTransitionForm
{
    public int ServiceEntryId { get; set; }
    [Required, RegularExpression("Pause|Resume|Complete")] public string Transition { get; set; } = "";
    [StringLength(1000)] public string? Notes { get; set; }
}

public sealed class QcSaveForm
{
    public int WorkOrderId { get; set; }
    [Required, StringLength(2000)] public string Notes { get; set; } = "";
    [MinLength(6), MaxLength(6)] public List<QcAnswer> Answers { get; set; } = new();
}

public sealed class QcAnswer
{
    [Required, RegularExpression("Pass|Fail")] public string Result { get; set; } = "";
    [StringLength(500)] public string? Notes { get; set; }
}

public sealed record QcRecord(string Notes, List<QcAnswer> Answers);

public sealed class GenerateInvoiceForm
{
    [Range(1, int.MaxValue)] public int WorkOrderId { get; set; }
    public int? TaxRateId { get; set; }
}

public sealed class PaymentSaveForm
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired]
    public Guid SubmissionToken { get; set; } = Guid.NewGuid();
    public int PaymentId { get; set; }
    public int InvoiceId { get; set; }
    [Range(typeof(decimal), "0.01", "100000000")] public decimal Amount { get; set; }
    [Required, RegularExpression("Cash|Card|Check|Bank Transfer")] public string PaymentMethod { get; set; } = "Cash";
    [StringLength(160)] public string? ReferenceNumber { get; set; }
    [Required] public DateTime? PaidUtc { get; set; }
    [Required, StringLength(1000)] public string Notes { get; set; } = "";
}

public sealed class AdjustmentSaveForm
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired]
    public Guid SubmissionToken { get; set; } = Guid.NewGuid();
    public int InvoiceAdjustmentId { get; set; }
    public int InvoiceId { get; set; }
    [Required, RegularExpression("Credit|Debit")] public string AdjustmentType { get; set; } = "Credit";
    [Range(typeof(decimal), "0.01", "100000000")] public decimal Amount { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}

public sealed class EstimateEditorViewModel
{
    public EstimateSaveForm Form { get; set; } = new();
    public List<ProductListingOption> Appointments { get; set; } = new();
    public List<Part> Parts { get; set; } = new();
    public List<Service> Services { get; set; } = new();
    public List<TaxRate> Taxes { get; set; } = new();
}

public sealed class EstimateWorkflowViewModel
{
    public Estimate Estimate { get; set; } = new();
    public EstimateRevision Revision { get; set; } = new();
    public List<EstimateRevision> Revisions { get; set; } = new();
    public List<EstimateRevisionLineItem> Lines { get; set; } = new();
    public string Customer { get; set; } = "";
    public string Vehicle { get; set; } = "";
    public int? WorkOrderId { get; set; }
    public string? ApprovalUrl { get; set; }
    public EstimateEditorViewModel Editor { get; set; } = new();
}

public sealed class RepairOperationsViewModel
{
    public List<EstimateRevisionLineItem> ApprovedLines { get; set; } = new();
    public int? WorkOrderId { get; set; }
    public int? PartId { get; set; }
    public List<WorkOrder> Orders { get; set; } = new();
    public List<Part> Parts { get; set; } = new();
    public List<PartTransaction> Movements { get; set; } = new();
    public List<EmployeeProfile> Employees { get; set; } = new();
    public List<ServiceEntry> Labour { get; set; } = new();
    public List<ServiceTimeSegment> Segments { get; set; } = new();
    public List<QualityCheck> Checks { get; set; } = new();
    public List<TaxRate> Taxes { get; set; } = new();
    public Dictionary<int, string> Customers { get; set; } = new();
}

public sealed class InvoiceWorkflowViewModel
{
    public Invoice Invoice { get; set; } = new();
    public List<InvoiceLineItem> Lines { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public List<InvoiceAdjustment> Adjustments { get; set; } = new();
    public decimal Balance => (Invoice.Total ?? 0) - Payments.Sum(x => x.Amount ?? 0);
    public string? OpenModal { get; set; }
}

public sealed record WorkflowReasonDialog(string DialogId, string Title, string Action, int Id);
public sealed record LabourEditor(LabourSaveForm Form, RepairOperationsViewModel Options);
public sealed record StockMovementEditor(StockMovementForm Form, RepairOperationsViewModel Options);
public sealed record EstimateLineEditor(EstimateLineForm Line, EstimateEditorViewModel Options);
