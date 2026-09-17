namespace Backyard_Mech.Models
{
    public class PartDetailsViewModel
    {
        public int PartId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CostPrice { get; set; } = string.Empty;
        public string SellingPrice { get; set; } = string.Empty;
        public string Markup { get; set; } = string.Empty;
        public string Available { get; set; } = string.Empty;
        public string Reserved { get; set; } = string.Empty;
        public string TotalOnHand { get; set; } = string.Empty;
        public string ReorderThreshold { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusTone { get; set; } = "stock";
        public string StatusAssessment { get; set; } = string.Empty;
        public string StatusClass { get; set; } = "parts-positive";
        public IReadOnlyList<PartTransactionDetailRow> Transactions { get; set; } = Array.Empty<PartTransactionDetailRow>();
    }

    public class PartTransactionDetailRow
    {
        public string Date { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Tone { get; set; } = "reserved";
        public string WorkOrder { get; set; } = "-";
        public string Quantity { get; set; } = string.Empty;
        public string QuantityClass { get; set; } = string.Empty;
        public string Balance { get; set; } = string.Empty;
        public string By { get; set; } = string.Empty;
    }

    public class ToolProfileViewModel
    {
        public int ToolId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusTone { get; set; } = "checked";
        public string LastCalibration { get; set; } = string.Empty;
        public string NextCalibration { get; set; } = string.Empty;
        public ToolCheckoutDetail? ActiveCheckout { get; set; }
        public IReadOnlyList<ToolCheckoutLogRow> Logs { get; set; } = Array.Empty<ToolCheckoutLogRow>();
    }

    public class ToolCheckoutDetail
    {
        public string Mechanic { get; set; } = string.Empty;
        public string WorkOrder { get; set; } = string.Empty;
        public string CheckoutTime { get; set; } = string.Empty;
    }

    public class ToolCheckoutLogRow
    {
        public string Date { get; set; } = string.Empty;
        public string Mechanic { get; set; } = string.Empty;
        public string WorkOrder { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string ConditionOut { get; set; } = string.Empty;
        public string ConditionReturn { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class ServiceQueueDetailsViewModel
    {
        public int AppointmentId { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string Vehicle { get; set; } = string.Empty;
        public string Vin { get; set; } = string.Empty;
        public string Plate { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusTone { get; set; } = "blue";
        public string Type { get; set; } = string.Empty;
        public string Bay { get; set; } = string.Empty;
        public string Technician { get; set; } = string.Empty;
        public string Advisor { get; set; } = string.Empty;
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public IReadOnlyList<string> Services { get; set; } = Array.Empty<string>();
        public IReadOnlyList<ServiceQueueHistoryRow> History { get; set; } = Array.Empty<ServiceQueueHistoryRow>();
    }

    public class ServiceQueueHistoryRow
    {
        public string Status { get; set; } = string.Empty;
        public string Changed { get; set; } = string.Empty;
        public string ChangedBy { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}
