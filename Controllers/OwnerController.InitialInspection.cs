using System.Text.Json;
using Backyard_Mech.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> SaveInitialInspection(InitialInspectionForm form) => SaveWorkflow(async () =>
    {
        var appointment = await _dbContext.Appointments.FindAsync(form.AppointmentId);
        Require(appointment is not null && appointment.CheckedInUtc.HasValue && appointment.AppointmentStatus is "Arrived" or "Inspection" or "Waiting Approval",
            "Check in the customer before recording an initial inspection.");
        Require(appointment!.VehicleId.HasValue && appointment.CustomerId.HasValue, "This appointment is missing its customer or vehicle.");
        var findings = form.Findings.Trim();
        Require(findings.Length >= 10, "Describe the vehicle's condition and inspection findings.");
        Require(form.Photos.Count <= 5 && form.Photos.All(x => x.Length is > 0 and <= 5 * 1024 * 1024), "Upload up to five photos, each no larger than 5 MB.");
        var allowed = new Dictionary<string, (string Extension, byte[] Signature)>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = (".jpg", new byte[] { 0xFF, 0xD8, 0xFF }),
            ["image/png"] = (".png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ["image/webp"] = (".webp", new byte[] { 0x52, 0x49, 0x46, 0x46 })
        };
        foreach (var photo in form.Photos)
        {
            Require(allowed.TryGetValue(photo.ContentType, out var fileType), "Only JPG, PNG, and WebP photos are accepted.");
            await using var check = photo.OpenReadStream();
            var signature = new byte[fileType.Signature.Length];
            Require(await check.ReadAsync(signature) == signature.Length && signature.SequenceEqual(fileType.Signature), "A photo's content does not match its file type.");
            if (photo.ContentType == "image/webp")
            {
                check.Position = 8;
                var webp = new byte[4];
                Require(await check.ReadAsync(webp) == 4 && webp.SequenceEqual("WEBP"u8.ToArray()), "A photo's content does not match its file type.");
            }
        }
        var userId = HttpContext.Session.GetString("UserId");
        var userName = HttpContext.Session.GetString("UserName") ?? "Staff";
        var data = JsonSerializer.Serialize(new { Findings = findings, RecommendedWork = form.RecommendedWork?.Trim(), Photos = Array.Empty<string>() });
        var inspection = new Inspection { AppointmentId = form.AppointmentId, MechanicUserId = userId, MechanicName = userName,
            StartedUtc = DateTime.UtcNow, CompletedUtc = DateTime.UtcNow, Notes = data };
        _dbContext.Inspections.Add(inspection);
        appointment.AppointmentStatus = "Inspection";
        await _dbContext.SaveChangesAsync();
        if (form.Photos.Count > 0)
        {
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "InspectionMedia");
            Directory.CreateDirectory(folder);
            foreach (var photo in form.Photos)
            {
                var type = allowed[photo.ContentType];
                var fileName = $"{Guid.NewGuid():N}{type.Extension}";
                await using (var target = System.IO.File.Create(Path.Combine(folder, fileName))) await photo.CopyToAsync(target);
                _dbContext.InspectionMedia.Add(new InspectionMedia { InspectionId = inspection.InspectionId, FileType = photo.ContentType,
                    BlobUrl = fileName, UploadedUtc = DateTime.UtcNow, UploadedByUserId = userId, UploadedByName = userName });
            }
        }
        WorkflowAudit("Inspection", inspection.InspectionId, "Initial vehicle inspection", new { form.AppointmentId, Findings = findings, form.RecommendedWork });
        return RedirectToAction(nameof(Appointments), new { view = "list" });
    }, nameof(Appointments), new { view = "list" });

    public async Task<IActionResult> InspectionPhoto(int id)
    {
        var media = await _dbContext.InspectionMedia.AsNoTracking().FirstOrDefaultAsync(x => x.InspectionMediaId == id);
        if (media?.BlobUrl is null || Path.GetFileName(media.BlobUrl) != media.BlobUrl) return NotFound();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "InspectionMedia", media.BlobUrl);
        return System.IO.File.Exists(path) ? PhysicalFile(path, media.FileType ?? "application/octet-stream") : NotFound();
    }
}
