namespace Backyard_Mech.Models
{
    public class DashboardViewModel
    {
        public string PageDateLabel { get; set; } = string.Empty;
        public IReadOnlyList<DashboardMetricCard> Metrics { get; set; } = Array.Empty<DashboardMetricCard>();
        public string QueueFilter { get; set; } = "all";
        public IReadOnlyList<DashboardServiceQueueRow> ServiceQueue { get; set; } = Array.Empty<DashboardServiceQueueRow>();
        public IReadOnlyList<DashboardMechanicCard> Mechanics { get; set; } = Array.Empty<DashboardMechanicCard>();
        public IReadOnlyList<DashboardApprovalItem> PendingApprovals { get; set; } = Array.Empty<DashboardApprovalItem>();
        public IReadOnlyList<DashboardPartAlertItem> PartAlerts { get; set; } = Array.Empty<DashboardPartAlertItem>();
        public IReadOnlyList<DashboardInspectionItem> Inspections { get; set; } = Array.Empty<DashboardInspectionItem>();
        public IReadOnlyList<DashboardInvoiceRow> RecentInvoices { get; set; } = Array.Empty<DashboardInvoiceRow>();
        public IReadOnlyList<DashboardActivityItem> RecentActivity { get; set; } = Array.Empty<DashboardActivityItem>();
        public string ActiveMechanicsLabel { get; set; } = string.Empty;
        public string PendingInspectionLabel { get; set; } = string.Empty;
        public string TodayCollected { get; set; } = string.Empty;
    }

    public class DashboardMetricCard
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string Tone { get; set; } = string.Empty;
    }

    public class DashboardServiceQueueRow
    {
        public string Id { get; set; } = string.Empty;
        public string Vehicle { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public string Mechanic { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusTone { get; set; } = "gray";
        public string Priority { get; set; } = string.Empty;
        public string PriorityTone { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
    }

    public class DashboardMechanicCard
    {
        public string Name { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public string Work { get; set; } = string.Empty;
        public string Current { get; set; } = string.Empty;
        public string Productivity { get; set; } = string.Empty;
        public int Bar { get; set; }
    }

    public class DashboardApprovalItem
    {
        public int EstimateId { get; set; }
        public string Customer { get; set; } = string.Empty;
        public string Vehicle { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string Sent { get; set; } = string.Empty;
    }

    public class DashboardPartAlertItem
    {
        public string Name { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Badge { get; set; } = string.Empty;
        public string BadgeTone { get; set; } = "amber";
        public string IconTone { get; set; } = "warn";
    }

    public class DashboardInspectionItem
    {
        public string Vehicle { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Tone { get; set; } = "amber";
    }

    public class DashboardInvoiceRow
    {
        public string Invoice { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Tone { get; set; } = "gray";
        public string Date { get; set; } = string.Empty;
    }

    public class DashboardActivityItem
    {
        public string Title { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; }
    }
}
