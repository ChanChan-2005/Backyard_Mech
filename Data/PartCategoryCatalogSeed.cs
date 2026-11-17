using Backyard_Mech.Models;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Data;

public static class PartCategoryCatalogSeed
{
    private const string SeedAction = "Seed complete car parts categories v1";

    private static readonly (string Name, string Examples)[] Catalog =
    {
        ("Engine Parts", "Pistons|Crankshafts|Camshafts|Cylinder Heads|Timing Chains|Engine Mounts"),
        ("Transmission & Drivetrain", "Clutch Kits|Gearboxes|Driveshafts|CV Joints|Differentials"),
        ("Brake System", "Brake Pads|Brake Rotors|Brake Calipers|Brake Shoes|Master Cylinders"),
        ("Suspension System", "Shock Absorbers|Struts|Coil Springs|Control Arms|Ball Joints"),
        ("Steering System", "Steering Racks|Tie Rods|Power Steering Pumps|Steering Columns"),
        ("Electrical & Electronics", "Alternators|Starters|Batteries|Fuses|Relays|Sensors|Wiring"),
        ("Cooling System", "Radiators|Water Pumps|Thermostats|Cooling Fans|Radiator Hoses"),
        ("Fuel System", "Fuel Pumps|Fuel Injectors|Fuel Filters|Fuel Tanks|Fuel Lines"),
        ("Exhaust System", "Mufflers|Catalytic Converters|Exhaust Pipes|Oxygen Sensors"),
        ("Air Intake System", "Air Filters|Throttle Bodies|Intake Manifolds|Turbochargers"),
        ("Body & Exterior", "Bumpers|Hoods|Fenders|Doors|Side Mirrors|Grilles"),
        ("Interior Parts", "Dashboards|Seats|Seat Belts|Door Panels|Center Consoles"),
        ("Lighting System", "Headlights|Taillights|Fog Lights|Turn Signals|LED Bulbs"),
        ("Wheels & Tires", "Tires|Rims|Wheel Bearings|Wheel Hubs|Lug Nuts"),
        ("Air Conditioning & HVAC", "AC Compressors|Condensers|Evaporators|Blower Motors"),
        ("Filters & Maintenance", "Oil Filters|Cabin Filters|Air Filters|Spark Plugs|Belts"),
        ("Fluids & Lubricants", "Engine Oil|Transmission Fluid|Brake Fluid|Coolant|Grease"),
        ("Safety & Security", "Airbags|ABS Sensors|Alarm Systems|Parking Sensors"),
        ("Wipers & Washers", "Wiper Blades|Wiper Motors|Washer Pumps|Washer Reservoirs"),
        ("Accessories & Upgrades", "Dashcams|Car Stereos|Roof Racks|Spoilers|Phone Holders")
    };

    public static IReadOnlyList<string> ParentNames { get; } = Catalog.Select(row => row.Name).ToArray();

    public static async Task EnsureAsync(ApplicationDbContext db)
    {
        if (await db.AuditLogEntries.AnyAsync(row => row.Action == SeedAction)) return;
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (await db.AuditLogEntries.AnyAsync(row => row.Action == SeedAction)) return;

        var categories = await db.PartCategories.ToListAsync();
        var parts = await db.Parts.ToListAsync();
        var oldNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Brakes"] = "Brake System",
            ["Electrical"] = "Electrical & Electronics",
            ["Cooling"] = "Cooling System",
            ["Filters"] = "Filters & Maintenance"
        };
        foreach (var (oldName, newName) in oldNames)
        {
            var old = categories.FirstOrDefault(row => row.ParentPartCategoryId == null &&
                string.Equals(row.CategoryName, oldName, StringComparison.OrdinalIgnoreCase));
            if (old is null) continue;
            var canonical = categories.FirstOrDefault(row => row.ParentPartCategoryId == null &&
                string.Equals(row.CategoryName, newName, StringComparison.OrdinalIgnoreCase));
            if (canonical is null) old.CategoryName = newName;
            else
            {
                foreach (var part in parts.Where(part => part.PartCategoryId == old.PartCategoryId)) part.PartCategoryId = canonical.PartCategoryId;
                foreach (var child in categories.Where(row => row.ParentPartCategoryId == old.PartCategoryId)) child.ParentPartCategoryId = canonical.PartCategoryId;
                old.IsActive = false;
            }
        }

        foreach (var (name, examples) in Catalog)
        {
            var root = categories.FirstOrDefault(row => row.ParentPartCategoryId == null &&
                string.Equals(row.CategoryName, name, StringComparison.OrdinalIgnoreCase));
            if (root is null)
            {
                root = new PartCategory { CategoryName = name, IsActive = true };
                db.PartCategories.Add(root);
                categories.Add(root);
            }
            root.Description = string.Join(", ", examples.Split('|'));
        }
        await db.SaveChangesAsync();

        foreach (var (name, examples) in Catalog)
        {
            var root = categories.Single(row => row.ParentPartCategoryId == null &&
                string.Equals(row.CategoryName, name, StringComparison.OrdinalIgnoreCase));
            foreach (var childName in examples.Split('|'))
            {
                if (categories.Any(row => row.ParentPartCategoryId == root.PartCategoryId &&
                    string.Equals(row.CategoryName, childName, StringComparison.OrdinalIgnoreCase))) continue;
                var child = new PartCategory { ParentPartCategoryId = root.PartCategoryId, CategoryName = childName, IsActive = true };
                db.PartCategories.Add(child);
                categories.Add(child);
            }
        }
        db.AuditLogEntries.Add(new AuditLogEntry { EntityName = "PartCategory", EntityId = "catalog-v1",
            Action = SeedAction, UserName = "System", TimestampUtc = DateTime.UtcNow,
            Details = "20 parent categories and their example subcategories." });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
