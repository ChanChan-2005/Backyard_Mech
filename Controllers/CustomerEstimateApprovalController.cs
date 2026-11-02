using System.Data;
using Backyard_Mech.Data;
using Backyard_Mech.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Controllers;

public sealed class CustomerEstimateApprovalController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Review(Guid token)
    {
        var model = await Load(token);
        return model is null ? View("Unavailable") : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(CustomerApprovalForm form)
    {
        if (!ModelState.IsValid || form.Token == Guid.Empty)
        {
            ViewBag.Error = string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
            if (string.IsNullOrWhiteSpace((string?)ViewBag.Error)) ViewBag.Error = "Choose an option and check the estimate items you approve.";
            var invalidModel = await Load(form.Token);
            return invalidModel is null ? View("Unavailable") : View("Review", invalidModel);
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var revision = await db.EstimateRevisions.SingleOrDefaultAsync(x => x.ApprovalToken == form.Token);
        if (revision is null || revision.EstimateStatus != "Pending" || revision.ApprovalTokenExpiresUtc <= DateTime.UtcNow ||
            await db.EstimateRevisions.AnyAsync(x => x.EstimateId == revision.EstimateId && x.RevisionNumber > revision.RevisionNumber))
        {
            await tx.RollbackAsync();
            return View("Unavailable");
        }

        var lines = await db.EstimateRevisionLineItems.Where(x => x.EstimateRevisionId == revision.EstimateRevisionId).ToListAsync();
        var selected = form.ApprovedLineIds.Distinct().ToHashSet();
        if (selected.Count != form.ApprovedLineIds.Count || selected.Any(id => lines.All(x => x.EstimateRevisionLineItemId != id)) ||
            (form.Decision == "Approved" && selected.Count != lines.Count) ||
            (form.Decision == "Partially Approved" && (selected.Count == 0 || selected.Count == lines.Count)) ||
            ((form.Decision is "Declined" or "Changes Requested") && selected.Count != 0) ||
            (form.Decision == "Changes Requested" && string.IsNullOrWhiteSpace(form.CustomerNote)))
        {
            await tx.RollbackAsync();
            ViewBag.Error = "The selected items do not match the chosen response. Review the form and try again.";
            var model = await Load(form.Token);
            return model is null ? View("Unavailable") : View("Review", model);
        }

        foreach (var line in lines) line.ApprovalStatus = selected.Contains(line.EstimateRevisionLineItemId) ? "Approved" :
            form.Decision == "Changes Requested" ? "Pending" : "Declined";
        revision.EstimateStatus = form.Decision;
        revision.RespondedUtc = DateTime.UtcNow;
        revision.ApprovalToken = null;
        revision.ApprovalTokenExpiresUtc = null;
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            EntityName = "Estimate", EntityId = revision.EstimateId.ToString(), Action = "Customer decision",
            UserName = "Customer approval link", TimestampUtc = DateTime.UtcNow,
            Details = System.Text.Json.JsonSerializer.Serialize(new { form.Decision, SelectedLineCount = selected.Count, CustomerNote = form.CustomerNote?.Trim(), revision.EstimateRevisionId })
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return View("Submitted", form.Decision);
    }

    private async Task<CustomerApprovalViewModel?> Load(Guid token)
    {
        var revision = await db.EstimateRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.ApprovalToken == token &&
            x.EstimateStatus == "Pending" && x.ApprovalTokenExpiresUtc > DateTime.UtcNow);
        if (revision?.EstimateId is not int estimateId || await db.EstimateRevisions.AnyAsync(x => x.EstimateId == estimateId && x.RevisionNumber > revision.RevisionNumber)) return null;
        var estimate = await db.Estimates.AsNoTracking().SingleOrDefaultAsync(x => x.EstimateId == estimateId);
        var appointment = estimate?.AppointmentId is int appointmentId ? await db.Appointments.AsNoTracking().SingleOrDefaultAsync(x => x.AppointmentId == appointmentId) : null;
        var vehicle = appointment?.VehicleId is int vehicleId ? await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x => x.VehicleId == vehicleId) : null;
        if (estimate is null || appointment is null) return null;
        return new CustomerApprovalViewModel { Token = token, EstimateNumber = estimate.EstimateId, RevisionNumber = revision.RevisionNumber ?? 1,
            Customer = appointment.CustomerName ?? "Customer", Vehicle = vehicle is null ? "Vehicle" : $"{vehicle.Year} {vehicle.Make} {vehicle.Model} ({vehicle.LicensePlate})",
            ExpiresUtc = revision.ApprovalTokenExpiresUtc!.Value, Total = revision.Total ?? 0,
            Lines = await db.EstimateRevisionLineItems.AsNoTracking().Where(x => x.EstimateRevisionId == revision.EstimateRevisionId).OrderBy(x => x.EstimateRevisionLineItemId).ToListAsync() };
    }
}
