namespace Backyard_Mech.Services;

public static class SidebarPermissionCatalog
{
    public static readonly (string Module, string Label)[] Items =
    {
        ("Dashboard", "Dashboard"),
        ("Service Queue", "Service Queue"),
        ("Customers", "Customer and Vehicle"),
        ("Vehicles", "Vehicle Records"),
        ("Appointments", "Appointments and Check-In"),
        ("Estimates", "Estimates / Customers Approval"),
        ("Work Orders", "Work Order and Assignment"),
        ("Bays", "Bay Management"),
        ("Parts Management", "Parts Management"),
        ("Parts POS", "Parts POS"),
        ("Suppliers", "Suppliers"),
        ("Services", "Service Catalog"),
        ("Labour", "Labour Time Tracking"),
        ("Inspections", "Quality Control"),
        ("Tools", "Tools Tracking"),
        ("Invoices", "Invoicing and Payments"),
        ("Archive", "Archive"),
        ("Records Disposition", "Records Disposition"),
        ("Reports", "Reports"),
        ("Employees", "User Management"),
        ("User Access", "Account Access"),
        ("Settings", "Settings")
    };

    public static string LabelForModule(string module) =>
        Items.FirstOrDefault(item => item.Module.Equals(module, StringComparison.OrdinalIgnoreCase)).Label ?? module;

    public static int OrderForModule(string module)
    {
        for (var index = 0; index < Items.Length; index++)
            if (Items[index].Module.Equals(module, StringComparison.OrdinalIgnoreCase)) return index;
        return Items.Length;
    }
}
