using Backyard_Mech.Models;

namespace Backyard_Mech.Services;

public static class DashboardPermissions
{
    public static readonly string[] Sections = {
        "DashboardRevenue", "DashboardActiveWorkOrders", "DashboardVehiclesServiced",
        "DashboardPendingApprovals", "DashboardUnpaidInvoices", "DashboardVehiclesInGarage",
        "DashboardServiceQueue", "DashboardLabourProductivity", "DashboardInventoryAlerts",
        "DashboardInspections", "DashboardRecentInvoices", "DashboardRecentActivity"
    };

    // Strip restricted data before the view receives it; hiding markup alone is insufficient.
    public static void Apply(DashboardViewModel model, ISet<string> codes)
    {
        model.VisibleSections = Sections.Where(section => codes.Contains("*") ||
            (codes.Contains("dashboard.view") && codes.Contains(OwnerPermissionCatalog.ActionPermissionCode(section))))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        model.Metrics = model.Metrics.Where(metric => model.VisibleSections.Contains(metric.Permission)).ToArray();
        if (!model.CanSee("DashboardServiceQueue")) model.ServiceQueue = Array.Empty<DashboardServiceQueueRow>();
        if (!model.CanSee("DashboardLabourProductivity")) { model.Mechanics = Array.Empty<DashboardMechanicCard>(); model.ActiveMechanicsLabel = ""; }
        if (!model.CanSee("DashboardPendingApprovals")) model.PendingApprovals = Array.Empty<DashboardApprovalItem>();
        if (!model.CanSee("DashboardInventoryAlerts")) model.PartAlerts = Array.Empty<DashboardPartAlertItem>();
        if (!model.CanSee("DashboardInspections")) { model.Inspections = Array.Empty<DashboardInspectionItem>(); model.PendingInspectionLabel = ""; }
        if (!model.CanSee("DashboardRecentInvoices")) { model.RecentInvoices = Array.Empty<DashboardInvoiceRow>(); model.TodayCollected = ""; }
        if (!model.CanSee("DashboardRecentActivity")) model.RecentActivity = Array.Empty<DashboardActivityItem>();
    }
}
