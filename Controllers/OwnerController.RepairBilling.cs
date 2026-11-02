using System.Text.Json;
using Backyard_Mech.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    internal static readonly string[] QcChecklist =
    {
        "Brake pedal and power assist", "Fluid leaks and fluid levels", "Road test and warning lights",
        "Parking brake holding test", "Wheel nut torque and repair fasteners", "Repair completion and vehicle cleanliness"
    };

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> SaveQualityInspection(QcSaveForm form) => SaveWorkflow(async () =>
    {
        Require(form.Answers is { Count: 6 }, "Answer every inspection check.");
        Require(form.Answers.All(x => x.Result is "Pass" or "Fail"), "Every item needs a Pass or Fail result.");
        Require(form.Answers.All(x => x.Result != "Fail" || !string.IsNullOrWhiteSpace(x.Notes)), "Explain each failed checklist item.");
        var order = await _dbContext.WorkOrders.FindAsync(form.WorkOrderId);
        Require(order?.WorkOrderStatus == "Pending QC", "Only work orders awaiting QC can be inspected.");
        Require(!await _dbContext.Invoices.AnyAsync(x => x.WorkOrderId == form.WorkOrderId && x.InvoiceStatus != "Voided"), "This work order has already been invoiced.");
        var userId = HttpContext.Session.GetString("UserId");
        var userName = HttpContext.Session.GetString("UserName") ?? "Inspector";
        var user = int.TryParse(userId, out var accountId) ? await _dbContext.Users.FindAsync(accountId) : null;
        var profile = await _dbContext.EmployeeProfiles.FirstOrDefaultAsync(x => x.UserId == userId || (user != null && x.Email == user.Email));
        var inspectorName = profile is null ? userName : BuildEmployeeName(profile);
        var assignments = await _dbContext.WorkOrderAssignments.Where(x => x.WorkOrderId == form.WorkOrderId).ToListAsync();
        var labour = await _dbContext.ServiceEntries.Where(x => x.WorkOrderId == form.WorkOrderId).ToListAsync();
        bool IsInspector(string? id, string? name) => (!string.IsNullOrWhiteSpace(id) && (id == userId || id == profile?.UserId)) ||
            string.Equals(name, inspectorName, StringComparison.OrdinalIgnoreCase) || string.Equals(name, userName, StringComparison.OrdinalIgnoreCase);
        Require(!assignments.Any(x => IsInspector(x.MechanicUserId, x.MechanicName)) && !labour.Any(x => IsInspector(x.MechanicUserId, x.MechanicName)), "The inspector cannot be a mechanic assigned to or working on this repair.");
        var entryIds = labour.Select(x => x.ServiceEntryId).ToList();
        Require(!await _dbContext.ServiceTimeSegments.AnyAsync(x => entryIds.Contains(x.ServiceEntryId ?? 0) && x.SegmentEnd == null), "Complete all labour timers first.");
        var passed = form.Answers.All(x => x.Result == "Pass");
        var check = new QualityCheck { WorkOrderId = form.WorkOrderId, InspectorUserId = userId, InspectorName = inspectorName,
            Passed = passed, PerformedUtc = DateTime.UtcNow, Notes = JsonSerializer.Serialize(new QcRecord(form.Notes.Trim(), form.Answers)) };
        _dbContext.QualityChecks.Add(check);
        order!.WorkOrderStatus = passed ? "Ready for Pickup" : "In Repair";
        order.ActualEnd = passed ? check.PerformedUtc : null;
        var intake = await _dbContext.Appointments.FindAsync(order.AppointmentId);
        if (intake is not null)
        {
            intake.AppointmentStatus = passed ? "Ready for Pickup" : "In Bay";
            if (passed) IntakeHistory(intake, "Quality inspection passed; vehicle ready for pickup");
        }
        await _dbContext.SaveChangesAsync();
        WorkflowAudit("QualityCheck", check.QualityCheckId, passed ? "Pass inspection" : "Fail inspection", form);
        return RedirectToAction(nameof(InspectionResult), new { id = check.QualityCheckId });
    }, nameof(ExecuteInspection), new { id = form.WorkOrderId });

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> VoidQualityInspection(WorkflowReasonForm form) => SaveWorkflow(async () =>
    {
        var check = await _dbContext.QualityChecks.FindAsync(form.Id);
        Require(check?.WorkOrderId is not null && check.Passed.HasValue, "Inspection is missing or already voided.");
        Require(!await _dbContext.Invoices.AnyAsync(x => x.WorkOrderId == check!.WorkOrderId && x.InvoiceStatus != "Voided"), "Void the unpaid invoice before invalidating its inspection.");
        var latest = await _dbContext.QualityChecks.Where(x => x.WorkOrderId == check!.WorkOrderId).OrderByDescending(x => x.PerformedUtc).ThenByDescending(x => x.QualityCheckId).FirstAsync();
        Require(latest.QualityCheckId == form.Id, "Historical inspections cannot be changed. Only the latest inspection can be voided.");
        WorkflowAudit("QualityCheck", form.Id, "Void inspection", new { Original = check, form.Reason });
        check!.Passed = null;
        var order = await _dbContext.WorkOrders.FindAsync(check.WorkOrderId);
        if (order is not null) { order.WorkOrderStatus = "Pending QC"; order.ActualEnd = null; }
        var intake = order?.AppointmentId is int appointmentId ? await _dbContext.Appointments.FindAsync(appointmentId) : null;
        if (intake is not null) intake.AppointmentStatus = "In Bay";
        return RedirectToAction(nameof(QualityControl));
    }, nameof(QualityControl));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> IssueInvoice(GenerateInvoiceForm form) => SaveWorkflow(async () =>
    {
        var order = await _dbContext.WorkOrders.FindAsync(form.WorkOrderId);
        var check = await _dbContext.QualityChecks.Where(x => x.WorkOrderId == form.WorkOrderId).OrderByDescending(x => x.PerformedUtc).ThenByDescending(x => x.QualityCheckId).FirstOrDefaultAsync();
        Require((order?.WorkOrderStatus is "Completed" or "Ready for Pickup") && check?.Passed == true && order.ActualEnd >= check.PerformedUtc, "Only completed, QC-passed work orders can be invoiced.");
        Require(!await _dbContext.Invoices.AnyAsync(x => x.WorkOrderId == form.WorkOrderId && x.InvoiceStatus != "Voided"), "This work order already has an invoice.");
        var appointment = await _dbContext.Appointments.FindAsync(order!.AppointmentId);
        Require(appointment is not null, "The work order has no originating appointment.");
        var customer = appointment!.CustomerId.HasValue ? await _dbContext.Customers.FindAsync(appointment.CustomerId) : null;
        var lines = new List<InvoiceLineItem>();
        var movements = await _dbContext.PartTransactions.Where(x => x.WorkOrderId == order.WorkOrderId).ToListAsync();
        await ValidateApprovedParts(order, movements);
        var approved = await _dbContext.EstimateRevisionLineItems.Where(x => x.EstimateRevisionId == order.ApprovedEstimateRevisionId && x.ApprovalStatus == "Approved").ToListAsync();
        foreach (var group in movements.Where(x => x.PartId.HasValue).GroupBy(x => x.PartId!.Value))
        {
            var quantity = group.Where(x => x.TransactionType == "Installed").Sum(x => x.Quantity ?? 0) - group.Where(x => x.TransactionType == "Returned").Sum(x => x.Quantity ?? 0);
            Require(ReservedUnits(group) == 0, "Install or release all reserved parts before billing.");
            if (quantity <= 0) continue;
            var part = await _dbContext.Parts.FindAsync(group.Key);
            Require(part is not null, "An installed part is missing from inventory.");
            var quoted = approved.FirstOrDefault(x => x.PartId == group.Key);
            Require(quoted is not null, "An installed part has no approved estimate price.");
            Require(approved.Where(x => x.PartId == group.Key).Select(x => x.UnitPrice).Distinct().Count() == 1, "This part has conflicting approved prices. Revise the estimate before billing.");
            var price = quoted!.UnitPrice ?? 0;
            lines.Add(new InvoiceLineItem { SourceType = "Part", SourceId = group.Key, Description = part!.Name,
                Quantity = quantity, UnitPrice = price, LineTotal = Math.Round(quantity * price, 2, MidpointRounding.AwayFromZero) });
        }
        var labour = await _dbContext.ServiceEntries.Where(x => x.WorkOrderId == order.WorkOrderId).ToListAsync();
        await ValidateApprovedLabour(order, labour);
        var ids = labour.Select(x => x.ServiceEntryId).ToArray();
        Require(!await _dbContext.ServiceTimeSegments.AnyAsync(x => ids.Contains(x.ServiceEntryId ?? 0) && x.SegmentEnd == null), "Complete all timers before billing.");
        foreach (var entry in labour)
            lines.Add(new InvoiceLineItem { SourceType = "Labour", SourceId = entry.ServiceEntryId, Description = entry.ServiceDescription,
                Quantity = Math.Round((entry.DurationMinutes ?? 0) / 60m, 2), UnitPrice = entry.HourlyRate,
                LineTotal = entry.ServiceCost ?? 0 });
        Require(lines.Count > 0 && lines.Sum(x => x.LineTotal ?? 0) > 0, "Record installed parts or completed labour before issuing an invoice.");
        var subtotal = lines.Sum(x => x.LineTotal ?? 0);
        var tax = Math.Round(subtotal * await TaxFraction(form.TaxRateId, DateTime.UtcNow), 2, MidpointRounding.AwayFromZero);
        var counter = await _dbContext.InvoiceNumberCounters.FirstOrDefaultAsync(x => x.InvoiceNumberCounterId == 1);
        Require(counter is not null, "The invoice numbering counter is missing. Run the database setup script.");
        string number;
        do { counter!.LastNumber++; number = $"{counter.Prefix}{counter.LastNumber:000000}"; }
        while (await _dbContext.Invoices.AnyAsync(x => x.InvoiceNumber == number));
        var invoice = new Invoice { WorkOrderId = order.WorkOrderId, CustomerId = customer?.CustomerId,
            CustomerFirstName = customer?.FirstName ?? appointment.CustomerName, CustomerMiddleName = customer?.MiddleName, CustomerLastName = customer?.LastName,
            InvoiceNumber = number, IssuedUtc = DateTime.UtcNow, TaxRateId = form.TaxRateId, Subtotal = subtotal, TaxAmount = tax,
            Total = subtotal + tax, InvoiceStatus = "Issued" };
        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync();
        foreach (var line in lines) line.InvoiceId = invoice.InvoiceId;
        _dbContext.InvoiceLineItems.AddRange(lines);
        WorkflowAudit("Invoice", invoice.InvoiceId, "Issue", new { invoice.InvoiceNumber, invoice.WorkOrderId, invoice.Total });
        return RedirectToAction(nameof(InvoiceDetails), new { id = invoice.InvoiceId });
    }, nameof(GenerateInvoice));

    private async Task<Invoice> OpenInvoice(int id)
    {
        var invoice = await _dbContext.Invoices.FindAsync(id);
        Require(invoice is not null && invoice.InvoiceStatus != "Voided", "Invoice is missing or voided.");
        return invoice!;
    }

    private async Task UseSubmission(Guid token)
    {
        Require(token != Guid.Empty, "Reload the form before submitting.");
        var action = "Request " + token.ToString("N");
        Require(!await _dbContext.AuditLogEntries.AnyAsync(x => x.Action == action), "This submission has already been saved. Refresh to see the result.");
        WorkflowAudit("WorkflowRequest", 0, action, "Accepted");
    }

    private async Task RefreshInvoiceStatus(Invoice invoice)
    {
        await _dbContext.SaveChangesAsync();
        var paid = await _dbContext.Payments.Where(x => x.InvoiceId == invoice.InvoiceId).SumAsync(x => x.Amount ?? 0);
        Require(paid <= (invoice.Total ?? 0), "The change would leave payments above the invoice total.");
        invoice.InvoiceStatus = paid == invoice.Total ? "Paid" : paid > 0 ? "Partially Paid" : "Issued";
        invoice.PaidUtc = invoice.InvoiceStatus == "Paid" ? DateTime.UtcNow : null;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> AddInvoicePayment(PaymentSaveForm form)
    {
        form.PaymentId = 0;
        return SavePayment(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> EditInvoicePayment(PaymentSaveForm form)
    {
        if (form.PaymentId <= 0) ModelState.AddModelError("PaymentId", "Select a payment.");
        return SavePayment(form);
    }

    private Task<IActionResult> SavePayment(PaymentSaveForm form) => SaveWorkflow(async () =>
    {
        await UseSubmission(form.SubmissionToken);
        var invoice = await OpenInvoice(form.InvoiceId);
        Require(form.PaidUtc.HasValue && form.PaidUtc.Value.Date >= invoice.IssuedUtc.Date && form.PaidUtc.Value.Date <= DateTime.UtcNow.Date, "Payment date must fall between the invoice date and today.");
        Require(form.PaymentMethod == "Cash" || !string.IsNullOrWhiteSpace(form.ReferenceNumber), "Enter a reference number for a non-cash payment.");
        Payment payment;
        if (form.PaymentId == 0)
        {
            payment = new Payment { InvoiceId = invoice.InvoiceId, ReceivedByUserId = HttpContext.Session.GetString("UserId"), ReceivedByName = HttpContext.Session.GetString("UserName") };
            _dbContext.Payments.Add(payment);
        }
        else
        {
            payment = (await _dbContext.Payments.FindAsync(form.PaymentId))!;
            Require(payment is not null && payment.InvoiceId == invoice.InvoiceId && payment.Amount > 0, "Payment not found or already voided.");
            WorkflowAudit("Payment", form.PaymentId, "Before edit", payment);
        }
        payment.Amount = form.Amount;
        payment.PaymentMethod = form.PaymentMethod;
        payment.ReferenceNumber = form.ReferenceNumber?.Trim();
        payment.PaidUtc = form.PaidUtc;
        payment.Notes = form.Notes.Trim();
        await RefreshInvoiceStatus(invoice);
        WorkflowAudit("Payment", payment.PaymentId, form.PaymentId == 0 ? "Record payment" : "Edit payment", form);
        return RedirectToAction(nameof(InvoiceDetails), new { id = invoice.InvoiceId });
    }, nameof(InvoiceDetails), new { id = form.InvoiceId });

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> VoidInvoicePayment(WorkflowReasonForm form) => SaveWorkflow(async () =>
    {
        var payment = await _dbContext.Payments.FindAsync(form.Id);
        Require(payment?.InvoiceId is not null && payment.Amount > 0, "Payment is missing or already voided.");
        var invoice = await OpenInvoice(payment!.InvoiceId!.Value);
        WorkflowAudit("Payment", form.Id, "Void payment", new { Original = payment, form.Reason });
        payment.Notes = $"Voided payment of {payment.Amount:N2}: {form.Reason}. {payment.Notes}";
        payment.Amount = 0;
        await RefreshInvoiceStatus(invoice);
        return RedirectToAction(nameof(InvoiceDetails), new { id = invoice.InvoiceId });
    }, nameof(InvoiceRegistry));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> AddInvoiceAdjustment(AdjustmentSaveForm form)
    {
        form.InvoiceAdjustmentId = 0;
        return SaveAdjustment(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> EditInvoiceAdjustment(AdjustmentSaveForm form)
    {
        if (form.InvoiceAdjustmentId <= 0) ModelState.AddModelError("InvoiceAdjustmentId", "Select an adjustment.");
        return SaveAdjustment(form);
    }

    private Task<IActionResult> SaveAdjustment(AdjustmentSaveForm form) => SaveWorkflow(async () =>
    {
        await UseSubmission(form.SubmissionToken);
        var invoice = await OpenInvoice(form.InvoiceId);
        InvoiceAdjustment adjustment;
        if (form.InvoiceAdjustmentId == 0)
        {
            adjustment = new InvoiceAdjustment { InvoiceId = invoice.InvoiceId, PerformedUtc = DateTime.UtcNow,
                PerformedByUserId = HttpContext.Session.GetString("UserId"), PerformedByName = HttpContext.Session.GetString("UserName") };
            _dbContext.InvoiceAdjustments.Add(adjustment);
        }
        else
        {
            adjustment = (await _dbContext.InvoiceAdjustments.FindAsync(form.InvoiceAdjustmentId))!;
            Require(adjustment is not null && adjustment.InvoiceId == invoice.InvoiceId && adjustment.AdjustmentType is "Credit" or "Debit", "Adjustment not found or already voided.");
            WorkflowAudit("InvoiceAdjustment", adjustment!.InvoiceAdjustmentId, "Before edit", adjustment);
        }
        adjustment.AdjustmentType = form.AdjustmentType;
        adjustment.Amount = form.Amount;
        adjustment.Reason = form.Reason.Trim();
        await RecalculateAdjustedInvoice(invoice);
        WorkflowAudit("InvoiceAdjustment", adjustment.InvoiceAdjustmentId, "Save adjustment", form);
        return RedirectToAction(nameof(InvoiceDetails), new { id = invoice.InvoiceId });
    }, nameof(InvoiceDetails), new { id = form.InvoiceId });

    private async Task RecalculateAdjustedInvoice(Invoice invoice)
    {
        await _dbContext.SaveChangesAsync();
        var adjustments = await _dbContext.InvoiceAdjustments.Where(x => x.InvoiceId == invoice.InvoiceId).ToListAsync();
        invoice.Total = (invoice.Subtotal ?? 0) + (invoice.TaxAmount ?? 0) + adjustments.Sum(x => x.AdjustmentType == "Credit" ? -(x.Amount ?? 0) : x.AdjustmentType == "Debit" ? x.Amount ?? 0 : 0);
        Require(invoice.Total >= 0, "Credit cannot exceed the invoice total.");
        await RefreshInvoiceStatus(invoice);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> VoidInvoiceAdjustment(WorkflowReasonForm form) => SaveWorkflow(async () =>
    {
        var adjustment = await _dbContext.InvoiceAdjustments.FindAsync(form.Id);
        Require(adjustment?.InvoiceId is not null && adjustment.AdjustmentType is "Credit" or "Debit", "Adjustment is missing or already voided.");
        var invoice = await OpenInvoice(adjustment!.InvoiceId!.Value);
        WorkflowAudit("InvoiceAdjustment", form.Id, "Void adjustment", new { Original = adjustment, form.Reason });
        adjustment.AdjustmentType = "Voided";
        await RecalculateAdjustedInvoice(invoice);
        return RedirectToAction(nameof(InvoiceDetails), new { id = invoice.InvoiceId });
    }, nameof(InvoiceRegistry));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> VoidInvoice(WorkflowReasonForm form) => SaveWorkflow(async () =>
    {
        var invoice = await OpenInvoice(form.Id);
        Require(!await _dbContext.Payments.AnyAsync(x => x.InvoiceId == form.Id && x.Amount > 0), "An invoice with payments cannot be voided. Resolve its payments first.");
        WorkflowAudit("Invoice", form.Id, "Void invoice", new { Original = invoice, form.Reason });
        invoice.InvoiceStatus = "Voided";
        invoice.PaidUtc = null;
        return RedirectToAction(nameof(InvoiceDetails), new { id = invoice.InvoiceId });
    }, nameof(InvoiceDetails), new { id = form.Id });
}
