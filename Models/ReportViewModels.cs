namespace Backyard_Mech.Models
{
    public class ReportMetricCard
    {
        public string Title { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Badge { get; set; } = string.Empty;
        public string BadgeTone { get; set; } = "gray";
        public string Detail { get; set; } = string.Empty;
    }

    public class ReportChartData
    {
        public IReadOnlyList<string> Labels { get; set; } = Array.Empty<string>();
        public IReadOnlyList<decimal> Data { get; set; } = Array.Empty<decimal>();
        public IReadOnlyList<decimal> Billable { get; set; } = Array.Empty<decimal>();
        public IReadOnlyList<decimal> NonBillable { get; set; } = Array.Empty<decimal>();
        public IReadOnlyList<decimal> Active { get; set; } = Array.Empty<decimal>();
        public IReadOnlyList<decimal> Setup { get; set; } = Array.Empty<decimal>();
        public IReadOnlyList<decimal> Idle { get; set; } = Array.Empty<decimal>();
    }

    public class ReportsOverviewViewModel
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public string PageDateLabel { get; set; } = string.Empty;
        public IReadOnlyList<ReportMetricCard> Metrics { get; set; } = Array.Empty<ReportMetricCard>();
    }

    public class SalesRevenueReportViewModel
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public IReadOnlyList<ReportMetricCard> Metrics { get; set; } = Array.Empty<ReportMetricCard>();
        public IReadOnlyList<SalesRevenueReportRow> Rows { get; set; } = Array.Empty<SalesRevenueReportRow>();
        public ReportChartData RevenueChart { get; set; } = new();
    }

    public class SalesRevenueReportRow
    {
        public string Date { get; set; } = string.Empty;
        public int InvoiceId { get; set; }
        public string Invoice { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string Vehicle { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public string Mechanic { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Tone { get; set; } = "gray";
    }

    public class LabourProductivityReportViewModel
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public IReadOnlyList<ReportMetricCard> Metrics { get; set; } = Array.Empty<ReportMetricCard>();
        public IReadOnlyList<LabourProductivityReportRow> Rows { get; set; } = Array.Empty<LabourProductivityReportRow>();
        public ReportChartData LabourChart { get; set; } = new();
    }

    public class LabourProductivityReportRow
    {
        public string EmployeeName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string TotalHours { get; set; } = string.Empty;
        public string BillableHours { get; set; } = string.Empty;
        public string NonBillableHours { get; set; } = string.Empty;
        public string Utilization { get; set; } = string.Empty;
        public int CompletedJobs { get; set; }
        public string AverageTimePerJob { get; set; } = string.Empty;
    }

    public class BayUtilizationReportViewModel
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public IReadOnlyList<ReportMetricCard> Metrics { get; set; } = Array.Empty<ReportMetricCard>();
        public IReadOnlyList<BayUtilizationReportRow> Rows { get; set; } = Array.Empty<BayUtilizationReportRow>();
        public ReportChartData BayChart { get; set; } = new();
    }

    public class BayUtilizationReportRow
    {
        public string BayIdentifier { get; set; } = string.Empty;
        public string HoursAvailable { get; set; } = string.Empty;
        public string HoursOccupied { get; set; } = string.Empty;
        public string UtilizationRate { get; set; } = string.Empty;
        public int JobsCompleted { get; set; }
        public string AverageJobDuration { get; set; } = string.Empty;
    }

    public class InventoryValueReportViewModel
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public IReadOnlyList<ReportMetricCard> Metrics { get; set; } = Array.Empty<ReportMetricCard>();
        public IReadOnlyList<InventoryValueReportRow> Rows { get; set; } = Array.Empty<InventoryValueReportRow>();
        public ReportChartData InventoryChart { get; set; } = new();
    }

    public class InventoryValueReportRow
    {
        public int PartId { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string PartName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public string QuantityInStock { get; set; } = string.Empty;
        public string UnitCost { get; set; } = string.Empty;
        public string TotalValue { get; set; } = string.Empty;
        public string ReorderLevel { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusTone { get; set; } = "profit";
    }

    public class LowStockPartsReportViewModel
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public IReadOnlyList<ReportMetricCard> Metrics { get; set; } = Array.Empty<ReportMetricCard>();
        public IReadOnlyList<LowStockPartsReportRow> Rows { get; set; } = Array.Empty<LowStockPartsReportRow>();
    }

    public class LowStockPartsReportRow
    {
        public int PartId { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string PartName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string CurrentStock { get; set; } = string.Empty;
        public string ReorderLevel { get; set; } = string.Empty;
        public string ShortageQuantity { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public string LastOrdered { get; set; } = string.Empty;
        public string UrgencyStatus { get; set; } = string.Empty;
        public string UrgencyTone { get; set; } = "yellow";
    }

    public class ProfitabilityReportViewModel
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public IReadOnlyList<ReportMetricCard> Metrics { get; set; } = Array.Empty<ReportMetricCard>();
        public IReadOnlyList<ProfitabilityReportRow> Rows { get; set; } = Array.Empty<ProfitabilityReportRow>();
        public ReportChartData ProfitChart { get; set; } = new();
    }

    public class ProfitabilityReportRow
    {
        public string ServiceType { get; set; } = string.Empty;
        public string JobsCount { get; set; } = string.Empty;
        public string Revenue { get; set; } = string.Empty;
        public string LabourCost { get; set; } = string.Empty;
        public string PartsCost { get; set; } = string.Empty;
        public string TotalCost { get; set; } = string.Empty;
        public string Profit { get; set; } = string.Empty;
        public string Margin { get; set; } = string.Empty;
        public string MarginTone { get; set; } = "green";
    }

    public class ToolLossesReportViewModel
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public IReadOnlyList<ReportMetricCard> Metrics { get; set; } = Array.Empty<ReportMetricCard>();
        public IReadOnlyList<ToolLossReportRow> Rows { get; set; } = Array.Empty<ToolLossReportRow>();
    }

    public class ToolLossReportRow
    {
        public int ToolId { get; set; }
        public string ToolCode { get; set; } = string.Empty;
        public string ToolName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string LastAssignedTo { get; set; } = string.Empty;
        public string CheckoutDate { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusTone { get; set; } = "gray";
        public string ReplacementCost { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}
