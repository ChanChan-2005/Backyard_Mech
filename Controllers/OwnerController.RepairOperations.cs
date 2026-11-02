using Backyard_Mech.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    private async Task EnsurePartCanBeRetired(int partId)
    {
        var movements = await _dbContext.PartTransactions.Where(x => x.PartId == partId).ToListAsync();
        Require(ReservedUnits(movements) <= 0, "Release reserved stock before archiving or deleting this part.");
        var installedOrders = movements.Where(x => x.TransactionType == "Installed" && x.WorkOrderId.HasValue).Select(x => x.WorkOrderId!.Value).ToArray();
        Require(!await _dbContext.WorkOrders.AnyAsync(x => installedOrders.Contains(x.WorkOrderId) && x.WorkOrderStatus != "Cancelled" &&
            !_dbContext.Invoices.Any(i => i.WorkOrderId == x.WorkOrderId && i.InvoiceStatus != "Voided")), "This part is used by an unbilled repair. Finish billing before archiving or deleting it.");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> GenerateWorkOrder(WorkOrderSaveForm form)
    {
        form.WorkOrderId = 0;
        return SaveOrder(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> EditWorkOrder(WorkOrderSaveForm form)
    {
        if (form.WorkOrderId <= 0) ModelState.AddModelError("WorkOrderId", "Select a work order.");
        return SaveOrder(form);
    }

    private Task<IActionResult> SaveOrder(WorkOrderSaveForm form) => SaveWorkflow(async () =>
    {
        Require(form.ScheduledStart.HasValue && form.ScheduledEnd > form.ScheduledStart, "Completion must be after the start time.");
        var employee = await _dbContext.EmployeeProfiles.FindAsync(form.EmployeeProfileId);
        Require(employee is { IsActive: true, IsArchived: false, IsDeleted: false }, "Select an active mechanic.");
        var bay = await _dbContext.Bays.FindAsync(form.BayId);
        Require(bay is { IsActive: true }, "Select an active bay.");
        var linkedAppointment = form.WorkOrderId == 0
            ? (await _dbContext.Estimates.FindAsync(form.EstimateId))?.AppointmentId
            : (await _dbContext.WorkOrders.FindAsync(form.WorkOrderId))?.AppointmentId;
        var team = new List<EmployeeProfile> { employee! };
        if (form.WorkOrderId > 0)
        {
            var helpers = await _dbContext.WorkOrderAssignments.Where(x => x.WorkOrderId == form.WorkOrderId && x.AssignmentRole == "Helper").ToListAsync();
            var profiles = await _dbContext.EmployeeProfiles.Where(x => x.IsActive && !x.IsArchived && !x.IsDeleted).ToListAsync();
            team.AddRange(profiles.Where(x => helpers.Any(h => h.MechanicName == BuildEmployeeName(x) || (x.UserId != null && h.MechanicUserId == x.UserId))));
        }
        var scheduleError = await ScheduleError(form.ScheduledStart!.Value, form.ScheduledEnd!.Value, form.BayId, team, linkedAppointment, form.WorkOrderId);
        Require(scheduleError is null, scheduleError ?? "Schedule is unavailable.");
        Require(!await _dbContext.BayMaintenanceSchedules.AnyAsync(x => x.BayId == form.BayId && !x.IsCompleted && x.StartDate < form.ScheduledEnd && x.EstimatedReturnDate >= form.ScheduledStart), "The bay is under maintenance during this schedule.");
        var conflicts = await _dbContext.WorkOrders.Where(x => x.WorkOrderId != form.WorkOrderId && x.WorkOrderStatus != "Cancelled" && x.WorkOrderStatus != "Completed" && x.WorkOrderStatus != "Ready for Pickup" && x.WorkOrderStatus != "Picked Up" &&
            x.ScheduledStart < form.ScheduledEnd && x.ScheduledEnd > form.ScheduledStart).ToListAsync();
        Require(!conflicts.Any(x => x.BayId == form.BayId), "This bay already has a work order in that time slot.");
        var conflictIds = conflicts.Select(x => x.WorkOrderId).ToArray();
        var name = BuildEmployeeName(employee!);
        Require(!await _dbContext.WorkOrderAssignments.AnyAsync(x => conflictIds.Contains(x.WorkOrderId ?? 0) &&
            (x.MechanicName == name || (employee!.UserId != null && x.MechanicUserId == employee.UserId))), "This mechanic is already assigned during that time.");
        WorkOrder order;
        if (form.WorkOrderId == 0)
        {
            var estimate = await _dbContext.Estimates.FindAsync(form.EstimateId);
            var revision = await LatestRevision(form.EstimateId);
            Require(estimate is not null && (revision?.EstimateStatus is "Approved" or "Partially Approved"), "Choose an estimate with approved work.");
            Require(await _dbContext.EstimateRevisionLineItems.AnyAsync(x => x.EstimateRevisionId == revision!.EstimateRevisionId && x.ApprovalStatus == "Approved"), "The approved revision has no approved work items.");
            Require(!await _dbContext.WorkOrders.AnyAsync(x => x.EstimateId == form.EstimateId && x.WorkOrderStatus != "Cancelled"), "This estimate already has an active work order.");
            var appointment = await _dbContext.Appointments.FindAsync(estimate!.AppointmentId);
            Require(appointment is not null && appointment.AppointmentStatus is not ("Cancelled" or "No Show" or "Completed"), "The originating appointment is unavailable.");
            Require(!await _dbContext.WorkOrders.AnyAsync(x => x.AppointmentId == estimate.AppointmentId && x.WorkOrderStatus != "Cancelled"), "This appointment already has a work order.");
            order = new WorkOrder { AppointmentId = estimate.AppointmentId, EstimateId = estimate.EstimateId,
                ApprovedEstimateRevisionId = revision!.EstimateRevisionId, WorkOrderStatus = "Pending", CreatedUtc = DateTime.UtcNow };
            _dbContext.WorkOrders.Add(order);
        }
        else
        {
            order = await EditableOrder(form.WorkOrderId);
            Require(order.EstimateId == form.EstimateId, "The originating estimate cannot be changed.");
            WorkflowAudit("WorkOrder", order.WorkOrderId, "Before edit", order);
            _dbContext.WorkOrderAssignments.RemoveRange(await _dbContext.WorkOrderAssignments
                .Where(x => x.WorkOrderId == order.WorkOrderId && (x.AssignmentRole != "Helper" || x.MechanicName == name)).ToListAsync());
        }
        order.BayId = form.BayId;
        order.ScheduledStart = form.ScheduledStart;
        order.ScheduledEnd = form.ScheduledEnd;
        var intake = await _dbContext.Appointments.FindAsync(order.AppointmentId);
        if (intake is not null)
        {
            intake.BayId = form.BayId;
            intake.AssignedMechanicUserId = employee!.UserId;
            intake.AssignedMechanicName = name;
        }
        await _dbContext.SaveChangesAsync();
        _dbContext.WorkOrderAssignments.Add(new WorkOrderAssignment { WorkOrderId = order.WorkOrderId, MechanicUserId = employee!.UserId,
            MechanicName = name, AssignmentRole = "Lead", AssignedUtc = DateTime.UtcNow });
        WorkflowAudit("WorkOrder", order.WorkOrderId, form.WorkOrderId == 0 ? "Create" : "Edit", new { form.Notes, form.ScheduledStart, form.ScheduledEnd, Mechanic = name, Bay = bay!.Name });
        return RedirectToAction(nameof(WorkOrderDetails), new { id = order.WorkOrderId });
    }, nameof(WorkOrders));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> CancelWorkOrder(WorkflowReasonForm form) => SaveWorkflow(async () =>
    {
        var order = await EditableOrder(form.Id);
        Require(!await _dbContext.ServiceEntries.AnyAsync(x => x.WorkOrderId == form.Id), "Remove or complete the labour records before cancellation.");
        var transactions = await _dbContext.PartTransactions.Where(x => x.WorkOrderId == form.Id).ToListAsync();
        Require(transactions.Where(x => x.TransactionType == "Installed").Sum(x => x.Quantity ?? 0) == transactions.Where(x => x.TransactionType == "Returned").Sum(x => x.Quantity ?? 0), "Return installed parts before cancellation.");
        foreach (var group in transactions.GroupBy(x => x.PartId))
        {
            var reserved = ReservedUnits(group);
            if (reserved > 0) _dbContext.PartTransactions.Add(new PartTransaction { PartId = group.Key, WorkOrderId = form.Id,
                TransactionType = "Released", Quantity = reserved, PerformedUtc = DateTime.UtcNow,
                PerformedByUserId = HttpContext.Session.GetString("UserId"), PerformedByName = HttpContext.Session.GetString("UserName"), Notes = form.Reason });
        }
        order.WorkOrderStatus = "Cancelled";
        order.ActualEnd = DateTime.UtcNow;
        var intake = await _dbContext.Appointments.FindAsync(order.AppointmentId);
        if (intake is not null)
        {
            intake.AppointmentStatus = "Arrived";
            intake.BayId = null;
            intake.AssignedMechanicUserId = null;
            intake.AssignedMechanicName = null;
        }
        WorkflowAudit("WorkOrder", form.Id, "Cancel", form.Reason);
        return RedirectToAction(nameof(WorkOrderDetails), new { id = form.Id });
    }, nameof(WorkOrderDetails), new { id = form.Id });

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> ReleaseWorkOrder(ReleaseWorkOrderFormModel form) => SaveWorkflow(async () =>
    {
        Require(form.ConfirmHandover, "Confirm that the vehicle was handed back to the customer.");
        var order = await _dbContext.WorkOrders.FindAsync(form.WorkOrderId);
        Require(order?.WorkOrderStatus == "Ready for Pickup" && order.ReleasedUtc is null, "Only a vehicle marked Ready for Pickup can be released, and it can be released once.");
        var invoice = await _dbContext.Invoices.Where(x => x.WorkOrderId == form.WorkOrderId && x.InvoiceStatus != "Voided")
            .OrderByDescending(x => x.InvoiceId).FirstOrDefaultAsync();
        Require(invoice is not null && invoice.InvoiceStatus == "Paid" && invoice.Total > 0, "Settle the invoice before releasing the vehicle.");
        var paid = await _dbContext.Payments.Where(x => x.InvoiceId == invoice!.InvoiceId).SumAsync(x => x.Amount ?? 0);
        Require(paid == invoice!.Total, "The invoice balance must be zero before vehicle release.");
        var appointment = await _dbContext.Appointments.FindAsync(order!.AppointmentId);
        Require(appointment is not null, "The work order's appointment could not be found.");

        order.WorkOrderStatus = "Picked Up";
        order.ReleasedUtc = DateTime.UtcNow;
        order.ReleasedByUserId = HttpContext.Session.GetString("UserId");
        order.ReleasedByName = HttpContext.Session.GetString("UserName") ?? "Staff";
        appointment!.AppointmentStatus = "Completed";
        IntakeHistory(appointment, "Vehicle released to customer after full payment");
        WorkflowAudit("WorkOrder", order.WorkOrderId, "Vehicle released", new { order.ReleasedUtc, order.ReleasedByUserId, order.ReleasedByName, invoice.InvoiceId, invoice.InvoiceNumber, invoice.Total });
        return RedirectToAction(nameof(WorkOrderStatus), new { id = $"WO-{order.WorkOrderId:0000}" });
    }, nameof(WorkOrderStatus), new { id = $"WO-{form.WorkOrderId:0000}" });

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> ReopenWorkOrder(WorkflowReasonForm form) => SaveWorkflow(async () =>
    {
        var order = await _dbContext.WorkOrders.FindAsync(form.Id);
        Require(order is not null && order.WorkOrderStatus is "Completed" or "Ready for Pickup" or "Pending QC", "Only completed or pending-inspection work can be reopened.");
        Require(!await _dbContext.Invoices.AnyAsync(x => x.WorkOrderId == form.Id && x.InvoiceStatus != "Voided"), "Void the unpaid invoice before reopening this work order.");
        Require(order!.ScheduledStart.HasValue && order.ScheduledEnd.HasValue, "Set a schedule before reopening work.");
        var assignments = await _dbContext.WorkOrderAssignments.Where(x => x.WorkOrderId == form.Id).ToListAsync();
        var employees = await _dbContext.EmployeeProfiles.Where(x => x.IsActive && !x.IsArchived && !x.IsDeleted).ToListAsync();
        var team = employees.Where(x => assignments.Any(a => a.MechanicName == BuildEmployeeName(x) || (x.UserId != null && a.MechanicUserId == x.UserId)));
        var scheduleError = await ScheduleError(order.ScheduledStart!.Value, order.ScheduledEnd!.Value, order.BayId, team, order.AppointmentId, order.WorkOrderId);
        Require(scheduleError is null, scheduleError ?? "Schedule unavailable.");
        order!.WorkOrderStatus = "In Progress";
        order.ActualEnd = null;
        var intake = await _dbContext.Appointments.FindAsync(order.AppointmentId);
        if (intake is not null) intake.AppointmentStatus = "In Bay";
        WorkflowAudit("WorkOrder", form.Id, "Reopen", form.Reason);
        return RedirectToAction(nameof(WorkOrderDetails), new { id = form.Id });
    }, nameof(WorkOrderDetails), new { id = form.Id });

    private static int ReservedUnits(IEnumerable<PartTransaction> rows) => rows.Sum(x => x.TransactionType switch
    {
        "Reserved" => x.Quantity ?? 0,
        "Released" or "Installed" or "Wasted" => -(x.Quantity ?? 0),
        _ => 0
    });

    private static int PhysicalDelta(string? type, int quantity) => type switch
    {
        "Received" or "Returned" or "Adjustment In" => quantity,
        "Installed" or "Wasted" or "Adjustment Out" => -quantity,
        _ => 0
    };

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> AddStockMovement(StockMovementForm form)
    {
        form.PartTransactionId = 0;
        return SaveMovement(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> EditStockMovement(StockMovementForm form)
    {
        if (form.PartTransactionId <= 0) ModelState.AddModelError("PartTransactionId", "Select a transaction.");
        return SaveMovement(form);
    }

    private Task<IActionResult> SaveMovement(StockMovementForm form) => SaveWorkflow(async () =>
    {
        await UseSubmission(form.SubmissionToken);
        var part = await _dbContext.Parts.FindAsync(form.PartId);
        Require(part is { IsActive: true, IsArchived: false, IsDeleted: false }, "Select an active part.");
        var jobMovement = form.TransactionType is "Reserved" or "Installed" or "Returned" or "Released" or "Wasted";
        Require(jobMovement == form.WorkOrderId.HasValue, "Work-order movements need a work order; receipts and adjustments must not be assigned to one.");
        if (jobMovement) await EditableOrder(form.WorkOrderId!.Value);
        var ledger = await _dbContext.PartTransactions.Where(x => x.PartId == form.PartId).ToListAsync();
        PartTransaction movement;
        var oldDelta = 0;
        if (form.PartTransactionId > 0)
        {
            movement = ledger.FirstOrDefault(x => x.PartTransactionId == form.PartTransactionId)!;
            Require(movement is not null && movement.TransactionType == form.TransactionType && movement.WorkOrderId == form.WorkOrderId, "Only quantity and notes can be edited; reverse an incorrect movement type instead.");
            WorkflowAudit("PartTransaction", movement!.PartTransactionId, "Before edit", movement);
            oldDelta = PhysicalDelta(movement.TransactionType, movement.Quantity ?? 0);
        }
        else
        {
            movement = new PartTransaction { PartId = part!.PartId, WorkOrderId = form.WorkOrderId, TransactionType = form.TransactionType,
                UnitCostAtTransaction = part.UnitCost, PerformedUtc = DateTime.UtcNow,
                PerformedByUserId = HttpContext.Session.GetString("UserId"), PerformedByName = HttpContext.Session.GetString("UserName") };
            ledger.Add(movement);
            _dbContext.PartTransactions.Add(movement);
        }
        movement.Quantity = form.Quantity;
        movement.Notes = form.Notes.Trim();
        part!.QuantityOnHand = checked(part.QuantityOnHand - oldDelta + PhysicalDelta(form.TransactionType, form.Quantity));
        ValidateLedger(part, ledger);
        if (jobMovement && form.TransactionType is "Reserved" or "Installed" or "Wasted")
            await ValidateApprovedParts(await EditableOrder(form.WorkOrderId!.Value), ledger);
        await _dbContext.SaveChangesAsync();
        WorkflowAudit("PartTransaction", movement.PartTransactionId, form.PartTransactionId == 0 ? "Create" : "Edit", form);
        return RedirectToAction(nameof(SparesAllocation), new { workOrderId = form.WorkOrderId, partId = form.PartId });
    }, nameof(SparesAllocation), new { workOrderId = form.WorkOrderId, partId = form.PartId });

    private static void ValidateLedger(Part part, List<PartTransaction> ledger)
    {
        Require(part.QuantityOnHand >= 0, "Not enough stock for this movement.");
        foreach (var job in ledger.Where(x => x.WorkOrderId.HasValue).GroupBy(x => x.WorkOrderId))
        {
            Require(ReservedUnits(job) >= 0, "Reserve parts before installing them. This change would leave more installed/released units than reserved.");
            Require(job.Where(x => x.TransactionType == "Installed").Sum(x => x.Quantity ?? 0) >= job.Where(x => x.TransactionType == "Returned").Sum(x => x.Quantity ?? 0), "Returned quantity cannot exceed installed quantity.");
        }
        Require(ledger.GroupBy(x => x.WorkOrderId).Sum(ReservedUnits) <= part.QuantityOnHand, "This stock is reserved for other work orders.");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> ReverseStockMovement(WorkflowReasonForm form) => SaveWorkflow(async () =>
    {
        var movement = await _dbContext.PartTransactions.FindAsync(form.Id);
        Require(movement is not null && movement.TransactionType != "Voided", "Transaction not found or already reversed.");
        if (movement!.WorkOrderId.HasValue) await EditableOrder(movement.WorkOrderId.Value);
        var part = await _dbContext.Parts.FindAsync(movement.PartId);
        Require(part is not null, "Part not found.");
        WorkflowAudit("PartTransaction", form.Id, "Reverse", new { Original = movement, form.Reason });
        part!.QuantityOnHand -= PhysicalDelta(movement.TransactionType, movement.Quantity ?? 0);
        movement.Notes = $"Reversed {movement.TransactionType}: {form.Reason}. Original notes: {movement.Notes}";
        movement.TransactionType = "Voided";
        ValidateLedger(part, await _dbContext.PartTransactions.Where(x => x.PartId == part.PartId).ToListAsync());
        return RedirectToAction(nameof(SparesAllocation), new { workOrderId = movement.WorkOrderId, partId = movement.PartId });
    }, nameof(SparesAllocation));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> AddLabour(LabourSaveForm form)
    {
        form.ServiceEntryId = 0;
        return SaveLabour(form, false);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> StartLabourTimer(LabourSaveForm form)
    {
        form.ServiceEntryId = 0;
        return SaveLabour(form, true);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> EditLabour(LabourSaveForm form)
    {
        if (form.ServiceEntryId <= 0) ModelState.AddModelError("ServiceEntryId", "Select a labour entry.");
        return SaveLabour(form, false);
    }

    private Task<IActionResult> SaveLabour(LabourSaveForm form, bool timer) => SaveWorkflow(async () =>
    {
        await UseSubmission(form.SubmissionToken);
        var order = await EditableOrder(form.WorkOrderId);
        var employee = await _dbContext.EmployeeProfiles.FindAsync(form.EmployeeProfileId);
        Require(employee is { IsActive: true, IsArchived: false, IsDeleted: false }, "Select an active mechanic.");
        var name = BuildEmployeeName(employee!);
        var approved = (await ApprovedScope(order)).SingleOrDefault(x => x.LineType == "Labour" && x.EstimateRevisionLineItemId == form.ApprovedEstimateLineId);
        Require(approved is not null, "Select a task from this work order's approved estimate.");
        if (timer)
            Require(await _dbContext.ServiceEntries.Where(x => x.WorkOrderId == order.WorkOrderId && x.ApprovedEstimateLineId == form.ApprovedEstimateLineId).SumAsync(x => x.DurationMinutes ?? 0) < (approved!.Quantity ?? 0) * 60,
                "This task has used its approved hours. Obtain additional approval before starting another timer.");
        ServiceEntry entry;
        if (form.ServiceEntryId > 0)
        {
            entry = (await _dbContext.ServiceEntries.FindAsync(form.ServiceEntryId))!;
            Require(entry is not null && entry.WorkOrderId == form.WorkOrderId, "Labour entry not found for this work order.");
            Require(!await _dbContext.ServiceTimeSegments.AnyAsync(x => x.ServiceEntryId == form.ServiceEntryId && x.SegmentEnd == null), "Complete the timer before editing.");
            WorkflowAudit("ServiceEntry", form.ServiceEntryId, "Before edit", entry);
        }
        else
        {
            entry = new ServiceEntry { WorkOrderId = order.WorkOrderId };
            _dbContext.ServiceEntries.Add(entry);
        }
        if (timer)
        {
            var employeeEntries = await _dbContext.ServiceEntries.Where(x => x.MechanicName == name || (employee!.UserId != null && x.MechanicUserId == employee.UserId)).Select(x => x.ServiceEntryId).ToListAsync();
            Require(!await _dbContext.ServiceTimeSegments.AnyAsync(x => employeeEntries.Contains(x.ServiceEntryId ?? 0) && x.SegmentEnd == null), "This mechanic already has an active or paused timer.");
        }
        entry.MechanicUserId = employee!.UserId;
        entry.ApprovedEstimateLineId = approved!.EstimateRevisionLineItemId;
        entry.ServiceId = approved.ServiceId;
        entry.MechanicName = name;
        entry.ServiceDescription = approved.Description;
        entry.HourlyRate = approved.UnitPrice;
        entry.DurationMinutes = timer ? 0 : form.Minutes;
        entry.EstimatedHours = Math.Round((entry.DurationMinutes ?? 0) / 60m, 2);
        entry.ServiceCost = Math.Round((entry.EstimatedHours ?? 0) * (entry.HourlyRate ?? 0), 2, MidpointRounding.AwayFromZero);
        var allEntries = await _dbContext.ServiceEntries.Where(x => x.WorkOrderId == order.WorkOrderId).ToListAsync();
        if (form.ServiceEntryId == 0) allEntries.Add(entry);
        await ValidateApprovedLabour(order, allEntries);
        entry.Notes = form.Notes;
        order.WorkOrderStatus = "In Progress";
        order.ActualStart ??= DateTime.UtcNow;
        var intake = await _dbContext.Appointments.FindAsync(order.AppointmentId);
        if (intake is not null) intake.AppointmentStatus = "In Bay";
        await _dbContext.SaveChangesAsync();
        if (timer) _dbContext.ServiceTimeSegments.Add(new ServiceTimeSegment { ServiceEntryId = entry.ServiceEntryId, SegmentType = "Work", SegmentStart = DateTime.UtcNow });
        WorkflowAudit("ServiceEntry", entry.ServiceEntryId, timer ? "Start timer" : "Save labour", form);
        return RedirectToAction(nameof(LabourTimeTracking), new { workOrderId = order.WorkOrderId });
    }, nameof(LabourTimeTracking), new { workOrderId = form.WorkOrderId });

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> ChangeLabourTimer(TimerTransitionForm form) => SaveWorkflow(async () =>
    {
        var entry = await _dbContext.ServiceEntries.FindAsync(form.ServiceEntryId);
        Require(entry?.WorkOrderId is not null, "Labour entry not found.");
        await EditableOrder(entry!.WorkOrderId!.Value);
        var segments = await _dbContext.ServiceTimeSegments.Where(x => x.ServiceEntryId == entry.ServiceEntryId).ToListAsync();
        var active = segments.SingleOrDefault(x => !x.SegmentEnd.HasValue);
        Require(active is not null, "This timer is already complete or has not been started.");
        Require((form.Transition == "Pause" && active!.SegmentType == "Work") || (form.Transition == "Resume" && active!.SegmentType == "Pause") || form.Transition == "Complete", "This timer transition is no longer available. Refresh the page.");
        if (form.Transition == "Resume")
        {
            var order = await EditableOrder(entry.WorkOrderId.Value);
            var approved = (await ApprovedScope(order)).SingleOrDefault(x => x.EstimateRevisionLineItemId == entry.ApprovedEstimateLineId);
            Require(approved is not null && await _dbContext.ServiceEntries.Where(x => x.WorkOrderId == order.WorkOrderId && x.ApprovedEstimateLineId == entry.ApprovedEstimateLineId).SumAsync(x => x.DurationMinutes ?? 0) < (approved.Quantity ?? 0) * 60,
                "Obtain additional approved hours before resuming this task.");
        }
        var now = DateTime.UtcNow;
        active!.SegmentEnd = now;
        if (form.Transition != "Complete") _dbContext.ServiceTimeSegments.Add(new ServiceTimeSegment { ServiceEntryId = entry.ServiceEntryId, SegmentStart = now,
            SegmentType = form.Transition == "Pause" ? "Pause" : "Work" });
        entry.DurationMinutes = (int)Math.Ceiling(segments.Where(x => x.SegmentType == "Work" && x.SegmentStart.HasValue).Sum(x => (x.SegmentEnd!.Value - x.SegmentStart!.Value).TotalMinutes));
        entry.EstimatedHours = Math.Round(entry.DurationMinutes.Value / 60m, 2);
        entry.ServiceCost = Math.Round((entry.EstimatedHours ?? 0) * (entry.HourlyRate ?? 0), 2, MidpointRounding.AwayFromZero);
        WorkflowAudit("ServiceEntry", entry.ServiceEntryId, form.Transition + " timer", form.Notes);
        return RedirectToAction(nameof(LabourTimeTracking), new { workOrderId = entry.WorkOrderId });
    }, nameof(LabourTimeTracking));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteLabour(WorkflowReasonForm form) => SaveWorkflow(async () =>
    {
        var entry = await _dbContext.ServiceEntries.FindAsync(form.Id);
        Require(entry?.WorkOrderId is not null, "Labour entry not found.");
        await EditableOrder(entry!.WorkOrderId!.Value);
        var segments = await _dbContext.ServiceTimeSegments.Where(x => x.ServiceEntryId == form.Id).ToListAsync();
        Require(segments.All(x => x.SegmentEnd.HasValue), "Complete the timer before deleting.");
        WorkflowAudit("ServiceEntry", form.Id, "Delete", new { Original = entry, Segments = segments, form.Reason });
        _dbContext.ServiceTimeSegments.RemoveRange(segments);
        await _dbContext.SaveChangesAsync();
        _dbContext.ServiceEntries.Remove(entry);
        return RedirectToAction(nameof(LabourTimeTracking), new { workOrderId = entry.WorkOrderId });
    }, nameof(LabourTimeTracking));
}
