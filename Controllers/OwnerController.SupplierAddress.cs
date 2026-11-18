using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backyard_Mech.Models;
using Backyard_Mech.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    private sealed record DemoPostalCode(string City, string Province, string Code);

    private static readonly HttpClient PostalCodeDataClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly SemaphoreSlim PostalCodeDataLock = new(1, 1);
    private static IReadOnlyList<DemoPostalCode>? _demoPostalCodes;
    private static readonly Regex CanadianPostalCodePattern = new("^[ABCEGHJ-NPRSTVXY][0-9][ABCEGHJ-NPRSTV-Z][0-9][ABCEGHJ-NPRSTV-Z][0-9]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [HttpGet]
    public async Task<IActionResult> SupplierPostalCodeSuggestions(string? city, string? province)
    {
        if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(province)) return Json(Array.Empty<string>());

        try
        {
            var records = await LoadDemoPostalCodesAsync();
            var cityKey = NormalizeLocation(city);
            var provinceKey = NormalizeLocation(province);
            var codes = records
                .Where(record => NormalizeLocation(record.City) == cityKey && NormalizeLocation(record.Province) == provinceKey)
                .Select(record => record.Code)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase);
            return Json(codes);
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IReadOnlyList<DemoPostalCode>> LoadDemoPostalCodesAsync()
    {
        if (_demoPostalCodes is not null) return _demoPostalCodes;

        await PostalCodeDataLock.WaitAsync();
        try
        {
            if (_demoPostalCodes is not null) return _demoPostalCodes;

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://github.com/djbelieny/geoinfo-dataset/raw/refs/heads/master/unique_zipcodes_csv.zip");
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("BackyardMech", "1.0"));
            using var response = await PostalCodeDataClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var entry = archive.Entries.FirstOrDefault(item => item.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("The demo postal-code archive has no CSV file.");
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8, true);
            await reader.ReadLineAsync();

            var records = new List<DemoPostalCode>();
            while (await reader.ReadLineAsync() is { } line)
            {
                var fields = ParsePostalCsvLine(line);
                if (fields.Count < 6 || !fields[4].Equals("CA", StringComparison.OrdinalIgnoreCase)) continue;

                var code = Regex.Replace(fields[5], "\\s+", "").ToUpperInvariant();
                if (CanadianPostalCodePattern.IsMatch(code)) records.Add(new DemoPostalCode(fields[0], fields[1], code.Insert(3, " ")));
            }

            _demoPostalCodes = records;
            return _demoPostalCodes;
        }
        finally
        {
            PostalCodeDataLock.Release();
        }
    }

    private static List<string> ParsePostalCsvLine(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else quoted = !quoted;
            }
            else if (character == ',' && !quoted)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else field.Append(character);
        }
        fields.Add(field.ToString());
        return fields;
    }

    private static string NormalizeLocation(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character)) normalized.Append(char.ToUpperInvariant(character));
        }
        return normalized.ToString();
    }

    private async Task ValidateSupplierAddressAsync(MasterDataForm form, Supplier existing)
    {
        Require(string.IsNullOrWhiteSpace(form.StreetType) || SupplierAddressOptions.StreetTypes.Contains(form.StreetType), "Select a valid street type.");
        var hasStreet = new[] { form.UnitNumber, form.StreetNumber, form.StreetName, form.StreetType }.Any(value => !string.IsNullOrWhiteSpace(value));
        Require(!hasStreet || !string.IsNullOrWhiteSpace(form.StreetName), "Enter the street name for this address.");
        if (!string.IsNullOrWhiteSpace(form.PostalCode))
        {
            var postal = form.PostalCode.Replace(" ", "").ToUpperInvariant();
            Require(CanadianPostalCodePattern.IsMatch(postal), "Enter a valid Canadian postal code, such as M5V 2T6.");
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
