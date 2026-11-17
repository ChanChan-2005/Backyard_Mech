using Backyard_Mech.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    private async Task<GeneralSettingsForm> LoadGeneralSettings()
    {
        var row = await _dbContext.GarageSettings.AsNoTracking().SingleAsync(x => x.Id == 1);
        var form = JsonSerializer.Deserialize<GeneralSettingsForm>(row.ValuesJson) ?? new();
        form.Expected = row.Revision.ToString();
        form.HasLogo = row.Logo is { Length: > 0 };
        form.CanEdit = await _permissionService.HasPermissionAsync(HttpContext.Session.GetString("UserRole"), "Settings", "Edit");
        return form;
    }

    public async Task<IActionResult> SettingsLogo()
    {
        var row = await _dbContext.GarageSettings.AsNoTracking().SingleAsync(x => x.Id == 1);
        if (row.Logo is not { Length: > 0 } || row.LogoContentType is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, no-cache";
        return File(row.Logo, row.LogoContentType);
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> SaveGeneralSettings(GeneralSettingsForm form)
    {
        var row = await _dbContext.GarageSettings.SingleAsync(x => x.Id == 1);
        form.HasLogo = row.Logo is { Length: > 0 };
        form.CanEdit = true; // The action filter enforces Settings/Edit before entry.
        if (!Guid.TryParse(form.Expected, out var revision) || revision != row.Revision)
            ModelState.AddModelError("", "Settings changed since you opened this page. Reload before saving.");
        byte[]? logo = null;
        string? logoType = null;
        if (form.LogoUpload is { Length: > 0 } upload)
        {
            if (upload.Length > 5 * 1024 * 1024)
                ModelState.AddModelError(nameof(form.LogoUpload), "Choose a PNG or JPG image up to 5 MB.");
            else
            {
                using var buffer = new MemoryStream();
                await upload.CopyToAsync(buffer);
                logo = buffer.ToArray();
                if (logo.Length >= 8 && logo.Take(8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) logoType = "image/png";
                else if (logo.Length >= 3 && logo[0] == 255 && logo[1] == 216 && logo[2] == 255) logoType = "image/jpeg";
                else ModelState.AddModelError(nameof(form.LogoUpload), "Choose a PNG or JPG image.");
            }
        }
        if (!ModelState.IsValid) return View("Settings", form);
        row.ValuesJson = JsonSerializer.Serialize<GeneralSettingsValues>(form);
        if (logo is not null) { row.Logo = logo; row.LogoContentType = logoType; }
        row.Revision = Guid.NewGuid();
        WorkflowAudit("GarageSettings", 1, "Update general settings", new { form.GarageName, LogoUpdated = logo is not null });
        try { await _dbContext.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError("", "Settings changed while saving. Reload before trying again.");
            return View("Settings", form);
        }
        TempData["OwnerMessage"] = "General settings saved.";
        TempData["OwnerMessageTone"] = "success";
        return RedirectToAction(nameof(Settings));
    }
}
