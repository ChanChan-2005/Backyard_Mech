using System.Data;
using System.Text.Json;
using Backyard_Mech.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    private sealed class WorkflowValidationException(string message) : Exception(message);
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new WorkflowValidationException(message);
    }

    // Stock, revisions and billing share a transaction lock so competing requests cannot
    // both spend the same balance, consume the same stock, or convert the same estimate.
    private async Task<IActionResult> SaveWorkflow(Func<Task<IActionResult>> save, string fallback, object? route = null)
    {
        if (!ModelState.IsValid)
        {
            TempData["OwnerMessage"] = string.Join(" ", ModelState.Values.SelectMany(x => x.Errors)
                .Select(x => string.IsNullOrWhiteSpace(x.ErrorMessage) ? "Check the entered values." : x.ErrorMessage).Distinct());
            TempData["OwnerMessageTone"] = "error";
            return RedirectToAction(fallback, route);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource=N'BackyardMech.RepairWorkflow', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51000, 'Workflow busy. Please retry.', 1;");
            var result = await save();
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            if (TempData.Peek("OwnerMessage") is null)
            {
                TempData["OwnerMessage"] = "Changes saved successfully.";
                TempData["OwnerMessageTone"] = "success";
            }
            return result;
        }
        catch (WorkflowValidationException error)
        {
            await transaction.RollbackAsync();
            TempData["OwnerMessage"] = error.Message;
            TempData["OwnerMessageTone"] = "error";
            return RedirectToAction(fallback, route);
        }
        catch (DbUpdateException error)
        {
            await transaction.RollbackAsync();
            HttpContext.RequestServices.GetRequiredService<ILogger<OwnerController>>().LogError(error, "Repair workflow could not be saved");
            TempData["OwnerMessage"] = "The record could not be saved. Refresh and try again; no changes were committed.";
            TempData["OwnerMessageTone"] = "error";
            return RedirectToAction(fallback, route);
        }
        catch (Microsoft.Data.SqlClient.SqlException error)
        {
            await transaction.RollbackAsync();
            HttpContext.RequestServices.GetRequiredService<ILogger<OwnerController>>().LogError(error, "Repair workflow transaction failed");
            TempData["OwnerMessage"] = "The database could not complete this request. Refresh and try again; no changes were committed.";
            TempData["OwnerMessageTone"] = "error";
            return RedirectToAction(fallback, route);
        }
    }

    private void WorkflowAudit(string entity, int id, string action, object? detail)
    {
        _dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            EntityName = entity, EntityId = id.ToString(), Action = action,
            UserId = HttpContext.Session.GetString("UserId"), UserName = HttpContext.Session.GetString("UserName"),
            TimestampUtc = DateTime.UtcNow, Details = JsonSerializer.Serialize(detail)
        });
    }

    private async Task<EstimateRevision?> LatestRevision(int id) => await _dbContext.EstimateRevisions
        .Where(x => x.EstimateId == id).OrderByDescending(x => x.RevisionNumber).ThenByDescending(x => x.EstimateRevisionId).FirstOrDefaultAsync();

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> CreateCustomerApprovalLink(WorkflowReasonForm form) => SaveWorkflow(async () =>
    {
        var revision = await LatestRevision(form.Id);
        Require(revision is not null && revision.EstimateStatus is "Draft" or "Pending", "Only a draft or pending estimate can be sent for approval.");
        var lines = await _dbContext.EstimateRevisionLineItems.Where(x => x.EstimateRevisionId == revision!.EstimateRevisionId).ToListAsync();
        Require(lines.Count > 0, "Add estimate items before sending it to the customer.");
        revision!.ApprovalToken = Guid.NewGuid();
        revision.ApprovalTokenExpiresUtc = DateTime.UtcNow.AddDays(7);
        revision.SentToCustomerUtc = DateTime.UtcNow;
        revision.EstimateStatus = "Pending";
        foreach (var line in lines) line.ApprovalStatus = "Pending";
        WorkflowAudit("Estimate", form.Id, "Create customer approval link", new { revision.EstimateRevisionId, ExpiresUtc = revision.ApprovalTokenExpiresUtc, form.Reason });
        return RedirectToAction(nameof(EstimateDetails), new { id = form.Id });
    }, nameof(EstimateDetails), new { id = form.Id });

    private async Task<decimal> TaxFraction(int? id, DateTime date)
    {
        if (id is null) return 0;
        var rate = await _dbContext.TaxRates.FindAsync(id.Value);
        Require(rate is not null && rate.Rate is >= 0 and <= 1 &&
            (!rate.EffectiveFrom.HasValue || rate.EffectiveFrom.Value <= date) &&
            (!rate.EffectiveTo.HasValue || rate.EffectiveTo.Value >= date), "Choose a tax rate that is effective on this date.");
        return rate!.Rate ?? 0;
    }

    private async Task<EstimateEditorViewModel> EstimateEditor(EstimateSaveForm? form = null)
    {
        var appointments = await _dbContext.Appointments.AsNoTracking().Where(x => x.AppointmentStatus != "Cancelled" && x.AppointmentStatus != "No Show" &&
            _dbContext.Inspections.Any(i => i.AppointmentId == x.AppointmentId && i.CompletedUtc != null)).ToListAsync();
        var vehicles = await _dbContext.Vehicles.AsNoTracking().ToDictionaryAsync(x => x.VehicleId);
        return new EstimateEditorViewModel
        {
            Form = form ?? new EstimateSaveForm { Lines = new() { new() } },
            Appointments = appointments.Select(x => new ProductListingOption { Id = x.AppointmentId,
                Label = $"{x.CustomerName} - {BuildVehicleName(vehicles.GetValueOrDefault(x.VehicleId ?? 0))} - {x.ScheduledStart:g}" }).ToList(),
            Parts = await _dbContext.Parts.AsNoTracking().Where(x => x.IsActive && !x.IsArchived && !x.IsDeleted).OrderBy(x => x.Name).ToListAsync(),
            Services = await _dbContext.Services.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.ServiceName).ToListAsync(),
            Taxes = await _dbContext.TaxRates.AsNoTracking().Where(x => (!x.EffectiveFrom.HasValue || x.EffectiveFrom <= DateTime.UtcNow) && (!x.EffectiveTo.HasValue || x.EffectiveTo >= DateTime.UtcNow)).ToListAsync()
        };
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> CreateEstimate(EstimateSaveForm form)
    {
        form.EstimateId = 0;
        return SaveEstimateRevision(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateEstimate(EstimateSaveForm form)
    {
        if (form.EstimateId <= 0) ModelState.AddModelError("EstimateId", "Select an estimate.");
        return SaveEstimateRevision(form);
    }

    private Task<IActionResult> SaveEstimateRevision(EstimateSaveForm form) => SaveWorkflow(async () =>
    {
        Require(form.Lines is { Count: > 0 and <= 100 }, "Add at least one estimate line.");
        var appointment = await _dbContext.Appointments.FindAsync(form.AppointmentId);
        Require(appointment is not null && appointment.AppointmentStatus != "Cancelled" && appointment.AppointmentStatus != "No Show", "Select an active appointment.");
        Require(await _dbContext.Inspections.AnyAsync(x => x.AppointmentId == form.AppointmentId && x.CompletedUtc.HasValue), "Complete the initial vehicle inspection before preparing an estimate.");
        var estimate = form.EstimateId == 0 ? new Estimate { AppointmentId = form.AppointmentId, CreatedUtc = DateTime.UtcNow,
            CreatedByUserId = HttpContext.Session.GetString("UserId"), CreatedByName = HttpContext.Session.GetString("UserName") }
            : await _dbContext.Estimates.FindAsync(form.EstimateId);
        Require(estimate is not null, "Estimate not found.");
        var latest = form.EstimateId == 0 ? null : await LatestRevision(form.EstimateId);
        if (form.EstimateId != 0)
        {
            Require(latest?.EstimateRevisionId == form.ExpectedRevisionId, "This estimate changed. Refresh before editing.");
            Require(latest!.EstimateStatus is not ("Cancelled" or "Archived"), "This estimate is closed.");
            var linkedOrders = await _dbContext.WorkOrders.Where(x => x.EstimateId == form.EstimateId && x.WorkOrderStatus != "Cancelled").ToListAsync();
            foreach (var linkedOrder in linkedOrders) await EditableOrder(linkedOrder.WorkOrderId);
            Require(estimate!.AppointmentId == form.AppointmentId, "The estimate's appointment cannot be changed.");
        }
        var lines = new List<EstimateRevisionLineItem>();
        foreach (var line in form.Lines!)
        {
            Require(!string.IsNullOrWhiteSpace(line.Description) && line.Quantity > 0 && line.UnitPrice >= 0, "Complete all estimate lines.");
            if (line.LineType == "Part")
            {
                Require(line.Quantity == decimal.Truncate(line.Quantity), "Part quantities must be whole units.");
                Require(await _dbContext.Parts.AnyAsync(x => x.PartId == line.PartId && x.IsActive && !x.IsArchived && !x.IsDeleted), "Select an available part for each part line.");
            }
            else if (line.ServiceId.HasValue)
                Require(await _dbContext.Services.AnyAsync(x => x.ServiceId == line.ServiceId && x.IsActive), "The selected service is inactive.");
            lines.Add(new EstimateRevisionLineItem { PartId = line.LineType == "Part" ? line.PartId : null,
                ServiceId = line.LineType == "Labour" ? line.ServiceId : null, LineType = line.LineType,
                Description = line.Description.Trim(), Quantity = line.Quantity, UnitPrice = line.UnitPrice,
                LineTotal = Math.Round(line.Quantity * line.UnitPrice, 2, MidpointRounding.AwayFromZero), ApprovalStatus = "Pending" });
        }
        var subtotal = lines.Sum(x => x.LineTotal ?? 0);
        Require(!lines.Where(x => x.LineType == "Part").GroupBy(x => x.PartId).Any(x => x.Count() > 1), "Use one line per part; update its quantity instead.");
        Require(!lines.Where(x => x.LineType == "Labour").GroupBy(x => (x.ServiceId, x.Description?.ToUpperInvariant())).Any(x => x.Count() > 1), "Use one line per labour task.");
        var tax = Math.Round(subtotal * await TaxFraction(form.TaxRateId, DateTime.UtcNow), 2, MidpointRounding.AwayFromZero);
        if (form.EstimateId == 0) { _dbContext.Estimates.Add(estimate!); await _dbContext.SaveChangesAsync(); }
        if (latest is not null) { latest.ApprovalToken = null; latest.ApprovalTokenExpiresUtc = null; }
        var revision = new EstimateRevision { EstimateId = estimate!.EstimateId, RevisionNumber = (latest?.RevisionNumber ?? 0) + 1,
            EstimateStatus = "Draft", Subtotal = subtotal, TaxAmount = tax, Total = subtotal + tax,
            RevisionReason = form.Reason.Trim(), CreatedByUserId = HttpContext.Session.GetString("UserId"),
            CreatedByName = HttpContext.Session.GetString("UserName"), CreatedUtc = DateTime.UtcNow };
        _dbContext.EstimateRevisions.Add(revision);
        await _dbContext.SaveChangesAsync();
        foreach (var line in lines) line.EstimateRevisionId = revision.EstimateRevisionId;
        _dbContext.EstimateRevisionLineItems.AddRange(lines);
        WorkflowAudit("Estimate", estimate.EstimateId, form.EstimateId == 0 ? "Create" : "Revise", new { revision.RevisionNumber, revision.Total, form.Reason });
        return RedirectToAction(nameof(EstimateDetails), new { id = estimate.EstimateId });
    }, form.EstimateId == 0 ? nameof(EstimateApprovals) : nameof(EstimateDetails), new { id = form.EstimateId });

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> RecordEstimateDecision(EstimateDecisionForm form) => SaveWorkflow(async () =>
    {
        var revision = await LatestRevision(form.EstimateId);
        Require(revision is not null && revision.EstimateRevisionId == form.ExpectedRevisionId, "This estimate changed. Refresh before recording approval.");
        Require(revision!.EstimateStatus is "Draft" or "Pending", "This revision already has a decision or is closed. Create a new revision to change it.");
        revision.EstimateStatus = form.Decision;
        revision.RespondedUtc = DateTime.UtcNow;
        var lines = await _dbContext.EstimateRevisionLineItems.Where(x => x.EstimateRevisionId == revision.EstimateRevisionId).ToListAsync();
        Require(lines.Count > 0, "An empty estimate cannot be approved.");
        foreach (var line in lines) line.ApprovalStatus = form.Decision;
        if (form.Decision == "Approved")
        {
            await _dbContext.SaveChangesAsync();
            var orders = await _dbContext.WorkOrders.Where(x => x.EstimateId == form.EstimateId && x.WorkOrderStatus != "Cancelled").ToListAsync();
            foreach (var order in orders)
            {
                await EditableOrder(order.WorkOrderId);
                var previousRevision = order.ApprovedEstimateRevisionId;
                var entries = await _dbContext.ServiceEntries.Where(x => x.WorkOrderId == order.WorkOrderId).ToListAsync();
                foreach (var entry in entries)
                {
                    var matches = lines.Where(x => x.LineType == "Labour" && x.ServiceId == entry.ServiceId &&
                        string.Equals(x.Description, entry.ServiceDescription, StringComparison.OrdinalIgnoreCase) && x.UnitPrice == entry.HourlyRate).ToList();
                    Require(matches.Count == 1, "The revised estimate must retain each recorded labour task and its rate. Add additional scope on separate lines.");
                    entry.ApprovedEstimateLineId = matches[0].EstimateRevisionLineItemId;
                }
                order.ApprovedEstimateRevisionId = revision.EstimateRevisionId;
                await ValidateApprovedParts(order, await _dbContext.PartTransactions.Where(x => x.WorkOrderId == order.WorkOrderId).ToListAsync());
                await ValidateApprovedLabour(order, entries);
                WorkflowAudit("WorkOrder", order.WorkOrderId, "Apply approved revision", new { previousRevision, revision.EstimateRevisionId, form.Reason });
            }
        }
        WorkflowAudit("Estimate", form.EstimateId, "Record customer decision", new { form.Decision, form.Reason, revision.EstimateRevisionId });
        return RedirectToAction(nameof(EstimateDetails), new { id = form.EstimateId });
    }, nameof(EstimateDetails), new { id = form.EstimateId });

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> CancelEstimate(WorkflowReasonForm form) => SaveWorkflow(async () =>
    {
        var revision = await LatestRevision(form.Id);
        Require(revision is not null && revision.EstimateStatus != "Cancelled", "Estimate is missing or already cancelled.");
        Require(!await _dbContext.WorkOrders.AnyAsync(x => x.EstimateId == form.Id && x.WorkOrderStatus != "Cancelled"), "Cancel the active work order first.");
        revision!.EstimateStatus = "Cancelled";
        revision.ApprovalToken = null;
        WorkflowAudit("Estimate", form.Id, "Cancel", form.Reason);
        return RedirectToAction(nameof(EstimateDetails), new { id = form.Id });
    }, nameof(EstimateDetails), new { id = form.Id });

    private async Task<WorkOrder> EditableOrder(int id)
    {
        var order = await _dbContext.WorkOrders.FindAsync(id);
        Require(order is not null, "Work order not found.");
        Require(order!.WorkOrderStatus is not ("Cancelled" or "Completed" or "Ready for Pickup" or "Picked Up" or "Pending QC"), "This work order is closed or awaiting inspection. Reopen it before changing repair records.");
        Require(!await _dbContext.Invoices.AnyAsync(x => x.WorkOrderId == id && x.InvoiceStatus != "Voided"), "An issued invoice locks this work order.");
        return order;
    }

    private async Task<RepairOperationsViewModel> RepairOperations(int? workOrderId = null, int? partId = null)
    {
        var orders = await _dbContext.WorkOrders.AsNoTracking().OrderByDescending(x => x.CreatedUtc).ToListAsync();
        var appointments = await _dbContext.Appointments.AsNoTracking().ToDictionaryAsync(x => x.AppointmentId);
        var labour = await _dbContext.ServiceEntries.AsNoTracking().Where(x => !workOrderId.HasValue || x.WorkOrderId == workOrderId).ToListAsync();
        var entryIds = labour.Select(x => x.ServiceEntryId).ToArray();
        return new RepairOperationsViewModel { WorkOrderId = workOrderId, PartId = partId, Orders = orders,
            ApprovedLines = await _dbContext.EstimateRevisionLineItems.AsNoTracking().Where(x => x.ApprovalStatus == "Approved").ToListAsync(),
            Customers = orders.ToDictionary(x => x.WorkOrderId, x => appointments.GetValueOrDefault(x.AppointmentId ?? 0)?.CustomerName ?? "Customer unavailable"),
            Parts = await _dbContext.Parts.AsNoTracking().OrderBy(x => x.Name).ToListAsync(),
            Employees = await _dbContext.EmployeeProfiles.AsNoTracking().Where(x => x.IsActive && !x.IsArchived && !x.IsDeleted).ToListAsync(),
            Movements = await _dbContext.PartTransactions.AsNoTracking().Where(x => (!workOrderId.HasValue || x.WorkOrderId == workOrderId) && (!partId.HasValue || x.PartId == partId)).OrderByDescending(x => x.PerformedUtc).ToListAsync(),
            Labour = labour, Segments = await _dbContext.ServiceTimeSegments.AsNoTracking().Where(x => entryIds.Contains(x.ServiceEntryId ?? 0)).ToListAsync(),
            Checks = await _dbContext.QualityChecks.AsNoTracking().Where(x => !workOrderId.HasValue || x.WorkOrderId == workOrderId).OrderByDescending(x => x.PerformedUtc).ThenByDescending(x => x.QualityCheckId).ToListAsync(),
            Taxes = await _dbContext.TaxRates.AsNoTracking().Where(x => (!x.EffectiveFrom.HasValue || x.EffectiveFrom <= DateTime.UtcNow) && (!x.EffectiveTo.HasValue || x.EffectiveTo >= DateTime.UtcNow)).ToListAsync() };
    }
}
