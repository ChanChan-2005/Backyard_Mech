using System.Text.Json;
using System.Text.RegularExpressions;
using Backyard_Mech.Models;
using Backyard_Mech.Services;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    private async Task ValidateSupplierAddressAsync(MasterDataForm form, Supplier existing)
    {
        Require(string.IsNullOrWhiteSpace(form.StreetType) || SupplierAddressOptions.StreetTypes.Contains(form.StreetType), "Select a valid street type.");
        var hasStreet = new[] { form.UnitNumber, form.StreetNumber, form.StreetName, form.StreetType }.Any(value => !string.IsNullOrWhiteSpace(value));
        Require(!hasStreet || !string.IsNullOrWhiteSpace(form.StreetName), "Enter the street name for this address.");
        if (!string.IsNullOrWhiteSpace(form.PostalCode))
        {
            var postal = form.PostalCode.Replace(" ", "").ToUpperInvariant();
            Require(Regex.IsMatch(postal, "^[ABCEGHJ-NPRSTVXY][0-9][ABCEGHJ-NPRSTV-Z][0-9][ABCEGHJ-NPRSTV-Z][0-9]$"), "Enter a valid Canadian postal code, such as M5V 2T6.");
            form.PostalCode = postal.Insert(3, " ");
        }
        // Existing locations remain editable even when not listed in the bundled census dataset.
        if (form.Id != 0 && Clean(form.Province) == Clean(existing.Province) && Clean(form.City) == Clean(existing.City)) return;
        if (string.IsNullOrWhiteSpace(form.Province))
        {
            Require(string.IsNullOrWhiteSpace(form.City), "Select a province before choosing a city.");
            return;
        }
        var environment = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var path = Path.Combine(environment.WebRootPath, "data", "canada-sgc-2021.json");
        using var catalog = JsonDocument.Parse(await System.IO.File.ReadAllTextAsync(path));
        var province = catalog.RootElement.GetProperty("provinces").EnumerateArray()
            .FirstOrDefault(item => string.Equals(item.GetProperty("name").GetString(), form.Province, StringComparison.OrdinalIgnoreCase));
        Require(province.ValueKind != JsonValueKind.Undefined, "Select a province from the list.");
        form.Province = province.GetProperty("name").GetString();
        if (string.IsNullOrWhiteSpace(form.City)) return;
        var city = catalog.RootElement.GetProperty("municipalities").EnumerateArray()
            .FirstOrDefault(item => item.GetProperty("provinceCode").GetString() == province.GetProperty("code").GetString()
                && string.Equals(item.GetProperty("name").GetString(), form.City, StringComparison.OrdinalIgnoreCase));
        Require(city.ValueKind != JsonValueKind.Undefined, "Select a municipality belonging to the chosen province.");
        form.City = city.GetProperty("name").GetString();
    }
}
