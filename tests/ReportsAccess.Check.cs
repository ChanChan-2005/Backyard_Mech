using Backyard_Mech.Data;
using Backyard_Mech.Models;
using Backyard_Mech.Services;

static void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException($"FAIL: {description}");
    Console.WriteLine($"PASS: {description}");
}

var reports = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "reports.view",
    OwnerPermissionCatalog.ActionPermissionCode("SalesRevenueReport")
};
var adminWithAllGrants = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "*" };
var owner = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "*" };

Check(OwnerPermissionCatalog.CanAccessRequest("Administrator", reports, "SalesRevenueReport"),
    "Administrator can open the read-only sales report when granted");
Check(!OwnerPermissionCatalog.CanAccessRequest("Administrator", adminWithAllGrants, "ProfitabilityReport"),
    "Administrator cannot open financial summary, even with broad grants");
Check(!OwnerPermissionCatalog.CanAccessRequest("Administrator", adminWithAllGrants, "ExportReport", "profitability"),
    "Administrator cannot export financial summary, even with broad grants");

var dashboard = new DashboardViewModel();
DashboardPermissions.Apply(dashboard, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "dashboard.view" }, "Administrator");
Check(dashboard.VisibleSections.SetEquals(DashboardPermissions.Sections),
    "Administrator receives all operational dashboard sections");

Check(!OwnerPermissionCatalog.CanAccessRequest("Accounting/Cashier", reports, "ProfitabilityReport"),
    "Accounting/Cashier cannot view financial summary without an explicit report grant");
reports.Add(OwnerPermissionCatalog.ActionPermissionCode("ProfitabilityReport"));
Check(OwnerPermissionCatalog.CanAccessRequest("Accounting/Cashier", reports, "ProfitabilityReport"),
    "Accounting/Cashier can view financial summary when explicitly granted");
Check(OwnerPermissionCatalog.CanAccessRequest("Owner", owner, "ProfitabilityReport"),
    "Garage Owner retains full financial-summary access");

var cashierProfile = StandardRoleProfiles.Staff.Single(profile => profile.Key == "Accounting/Cashier");
Check(!cashierProfile.PermissionCodes().Contains(OwnerPermissionCatalog.ActionPermissionCode("ProfitabilityReport")),
    "Default Accounting/Cashier profile does not grant financial summary");
Check(cashierProfile.PermissionCodes().Contains(OwnerPermissionCatalog.ActionPermissionCode("SalesRevenueReport")),
    "Default Accounting/Cashier profile grants transaction-level sales reporting");
