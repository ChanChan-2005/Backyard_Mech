using Backyard_Mech.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> UploadWorkOrderEvidence(WorkOrderEvidenceForm form) => SaveWorkflow(async () =>
    {
        var userId = HttpContext.Session.GetString("UserId");
        Require(!string.IsNullOrWhiteSpace(userId), "Sign in with your assigned mechanic account to upload evidence.");
        var workOrder = await _dbContext.WorkOrders.FindAsync(form.WorkOrderId);
        Require(workOrder is not null && workOrder.WorkOrderStatus is not ("Cancelled" or "Picked Up"),
            "Evidence cannot be uploaded for a cancelled or released work order.");
        var isAssignedMechanic = await _dbContext.WorkOrderAssignments
            .AnyAsync(x => x.WorkOrderId == form.WorkOrderId && x.MechanicUserId == userId);
        var canManageWorkOrders = await _permissionService.HasPermissionAsync(
            HttpContext.Session.GetString("UserRole"), "Work Orders", "Edit");
        Require(isAssignedMechanic || canManageWorkOrders,
            "Only an assigned mechanic/helper or a user with Work Orders edit access can upload evidence.");
        Require(form.Photos.Count is > 0 and <= 5 && form.Photos.All(x => x.Length is > 0 and <= 5 * 1024 * 1024),
            "Upload one to five photos, each no larger than 5 MB.");
        var allowed = new Dictionary<string, (string Extension, byte[] Signature)>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = (".jpg", new byte[] { 0xFF, 0xD8, 0xFF }),
            ["image/png"] = (".png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ["image/webp"] = (".webp", new byte[] { 0x52, 0x49, 0x46, 0x46 })
        };
        foreach (var photo in form.Photos)
        {
            Require(allowed.TryGetValue(photo.ContentType, out var fileType), "Only JPG, PNG, and WebP photos are accepted.");
            await using var stream = photo.OpenReadStream();
            var signature = new byte[fileType.Signature.Length];
            Require(await stream.ReadAsync(signature) == signature.Length && signature.SequenceEqual(fileType.Signature),
                "A photo's content does not match its file type.");
            if (photo.ContentType == "image/webp")
            {
                stream.Position = 8;
                var webp = new byte[4];
                Require(await stream.ReadAsync(webp) == 4 && webp.SequenceEqual("WEBP"u8.ToArray()),
                    "A photo's content does not match its file type.");
            }
        }

        var userName = HttpContext.Session.GetString("UserName") ?? "Mechanic";
        var folder = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "WorkOrderEvidence");
        Directory.CreateDirectory(folder);
        foreach (var photo in form.Photos)
        {
            var fileType = allowed[photo.ContentType];
            var fileName = $"{Guid.NewGuid():N}{fileType.Extension}";
            await using (var target = System.IO.File.Create(Path.Combine(folder, fileName)))
                await photo.CopyToAsync(target);
            _dbContext.WorkOrderEvidence.Add(new WorkOrderEvidence
            {
                WorkOrderId = form.WorkOrderId,
                Stage = form.Stage,
                Description = form.Description.Trim(),
                FileType = photo.ContentType,
                BlobUrl = fileName,
                UploadedUtc = DateTime.UtcNow,
                UploadedByUserId = userId,
                UploadedByName = userName
            });
        }
        WorkflowAudit("WorkOrder", form.WorkOrderId, $"{form.Stage} repair evidence uploaded", new { form.Stage, Description = form.Description.Trim(), PhotoCount = form.Photos.Count, userName });
        return RedirectToAction(nameof(WorkOrderDetails), new { id = form.WorkOrderId });
    }, nameof(WorkOrderDetails), new { id = form.WorkOrderId });

    public async Task<IActionResult> WorkOrderEvidencePhoto(int id)
    {
        var evidence = await _dbContext.WorkOrderEvidence.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkOrderEvidenceId == id);
        if (evidence is null || Path.GetFileName(evidence.BlobUrl) != evidence.BlobUrl) return NotFound();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "WorkOrderEvidence", evidence.BlobUrl);
        return System.IO.File.Exists(path) ? PhysicalFile(path, evidence.FileType) : NotFound();
    }
}
