namespace Backyard_Mech.Services;

public static class SubTabPermissionCatalog
{
    public sealed record Tab(string Page, string QueryName, string Value, string Module, string Label)
    {
        public string ActionName => $"SubTab{Page}{new string(Value.Where(char.IsLetterOrDigit).ToArray())}";
        public string Code => $"subtab.{Page.ToLowerInvariant()}.{new string(Value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant()}";
    }

    public static readonly Tab[] Tabs =
    {
        new("PartsManagement", "tab", "parts", "Parts Management", "Inventory Registry"),
        new("PartsManagement", "tab", "product-listings", "Parts Management", "Product Listing"),
        new("ArchiveRecords", "tab", "all", "Archive", "All Archived Records"),
        new("ArchiveRecords", "tab", "parts", "Archive", "Archived Parts"),
        new("ArchiveRecords", "tab", "product-listings", "Archive", "Archived Product Listings"),
        new("ArchiveRecords", "tab", "tools", "Archive", "Archived Tools"),
        new("RecordsDisposition", "tab", "all", "Records Disposition", "All Disposition Records"),
        new("RecordsDisposition", "tab", "parts", "Records Disposition", "Disposition Parts"),
        new("RecordsDisposition", "tab", "product-listings", "Records Disposition", "Disposition Product Listings"),
        new("RecordsDisposition", "tab", "tools", "Records Disposition", "Disposition Tools"),
        new("SettingsCategories", "kind", "services", "Settings", "Service Categories"),
        new("SettingsCategories", "kind", "parts", "Settings", "Part Categories"),
        new("SettingsCategories", "kind", "tools", "Settings", "Tool Categories"),
        new("SettingsRoles", "tab", "roles", "Settings", "Roles"),
        new("SettingsRoles", "tab", "permissions", "Settings", "Permissions"),
        new("Employees", "view", "all", "Employees", "All Employees"),
        new("Employees", "view", "active", "Employees", "Active Employees"),
        new("Employees", "view", "inactive", "Employees", "Inactive Employees")
    };

    public static Tab? Find(string page, string? value) => Tabs.FirstOrDefault(tab =>
        tab.Page.Equals(page, StringComparison.OrdinalIgnoreCase) &&
        tab.Value.Equals(value, StringComparison.OrdinalIgnoreCase));

    public static Tab? Selected(string page, Microsoft.AspNetCore.Http.IQueryCollection query)
    {
        var first = Tabs.FirstOrDefault(tab => tab.Page.Equals(page, StringComparison.OrdinalIgnoreCase));
        if (first is null) return null;
        var value = query[first.QueryName].ToString();
        return Find(page, string.IsNullOrWhiteSpace(value) ? first.Value : value) ?? first;
    }

    public static bool CanAccess(ISet<string> codes, string page, string value) =>
        codes.Contains("*") || (Find(page, value) is { } tab && codes.Contains(tab.Code));

    public static Tab? ByActionName(string? actionName) => Tabs.FirstOrDefault(tab =>
        tab.ActionName.Equals(actionName, StringComparison.OrdinalIgnoreCase));

    public static Tab? RequiredForAction(string actionName) => actionName switch
    {
        "PartDetails" or "PartAuditLog" or "PartEntry" or "AddPart" or "EditPart" or
        "ArchivePart" or "SoftDeletePart" => Find("PartsManagement", "parts"),
        "AddProductListing" or "EditProductListing" or "ArchiveProductListing" or
        "SoftDeleteProductListing" => Find("PartsManagement", "product-listings"),
        _ => null
    };

    public static bool CanAccessRecordType(ISet<string> codes, string page, string? recordType)
    {
        if (codes.Contains("*")) return true;
        if (CanAccess(codes, page, "all")) return true;
        var tab = recordType?.Trim().ToLowerInvariant() switch
        {
            "part" => "parts",
            "product listing" => "product-listings",
            "tool" => "tools",
            _ => "all"
        };
        return CanAccess(codes, page, tab);
    }
}
