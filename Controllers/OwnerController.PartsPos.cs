using Backyard_Mech.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    [HttpGet]
    public async Task<IActionResult> PartsPos()
    {
        var now = DateTime.UtcNow;
        var parts = await _dbContext.Parts.AsNoTracking()
            .Where(part => part.IsActive && !part.IsArchived && !part.IsDeleted && part.QuantityOnHand > 0 && part.UnitPrice > 0)
            .OrderBy(part => part.Name).ToListAsync();
        var reservations = await _dbContext.PartTransactions.AsNoTracking()
            .Where(row => row.PartId != null && row.WorkOrderId != null)
            .ToListAsync();
        var reservedByPart = reservations.GroupBy(row => row.PartId!.Value)
            .ToDictionary(group => group.Key, group => group.GroupBy(row => row.WorkOrderId).Sum(ReservedUnits));
        var taxes = await _dbContext.TaxRates.AsNoTracking()
            .Where(rate => (!rate.EffectiveFrom.HasValue || rate.EffectiveFrom <= now) &&
                (!rate.EffectiveTo.HasValue || rate.EffectiveTo >= now) && rate.Rate >= 0 && rate.Rate <= 1)
            .OrderBy(rate => rate.Description).ToListAsync();
        return View(new PartsPosViewModel
        {
            Parts = parts.Select(part => new PartsPosItem(part.PartId, part.SKU ?? "", part.Name ?? "Part",
                part.UnitPrice!.Value, Math.Max(0, part.QuantityOnHand - reservedByPart.GetValueOrDefault(part.PartId)))).Where(part => part.Available > 0).ToList(),
            Taxes = taxes.Select(rate => new PartsPosTax(rate.TaxRateId, rate.Description ?? "Tax", rate.Rate ?? 0)).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> PartsPosReceipt(int id)
    {
        var invoice = await _dbContext.Invoices.AsNoTracking().FirstOrDefaultAsync(row => row.InvoiceId == id && row.WorkOrderId == null);
        if (invoice is null) return NotFound();
        var lines = await _dbContext.InvoiceLineItems.AsNoTracking().Where(row => row.InvoiceId == id).ToListAsync();
        if (lines.Count == 0 || lines.Any(row => row.SourceType != "POS Part")) return NotFound();
        var payment = await _dbContext.Payments.AsNoTracking().Where(row => row.InvoiceId == id && row.Amount > 0)
            .OrderByDescending(row => row.PaymentId).FirstOrDefaultAsync();
        return View(new PartsPosReceiptViewModel { Invoice = invoice, Lines = lines, Payment = payment });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> CheckoutParts(PartsPosCheckoutForm form) => SaveWorkflow(async () =>
    {
        await UseSubmission(form.SubmissionToken);
        Require(form.Lines is { Count: > 0 } && form.Lines.Count <= 100, "Add at least one part to the sale.");
        Require(form.Lines.All(line => line.PartId > 0 && line.Quantity > 0 && line.Quantity <= 10000), "Enter a valid quantity for every part.");
        Require(form.Lines.Select(line => line.PartId).Distinct().Count() == form.Lines.Count, "Each part may appear only once.");
        Require(form.PaymentMethod is "Cash" or "Card" or "Bank Transfer", "Choose a valid payment method.");
        Require(form.AmountTendered >= 0, "Enter a valid amount tendered.");

        var now = DateTime.UtcNow;
        var ids = form.Lines.Select(line => line.PartId).ToArray();
        var parts = await _dbContext.Parts.Where(part => ids.Contains(part.PartId)).ToDictionaryAsync(part => part.PartId);
        var movements = await _dbContext.PartTransactions.Where(row => row.PartId != null && ids.Contains(row.PartId.Value) && row.WorkOrderId != null).ToListAsync();
        var reservedByPart = movements.GroupBy(row => row.PartId!.Value)
            .ToDictionary(group => group.Key, group => group.GroupBy(row => row.WorkOrderId).Sum(ReservedUnits));
        var lines = new List<InvoiceLineItem>();
        foreach (var entry in form.Lines)
        {
            Require(parts.TryGetValue(entry.PartId, out var part) && part.IsActive && !part.IsArchived && !part.IsDeleted && part.UnitPrice > 0,
                "A selected part is no longer available. Refresh the POS.");
            Require(entry.Quantity <= part!.QuantityOnHand - reservedByPart.GetValueOrDefault(entry.PartId),
                $"Insufficient unreserved stock for {part.Name}.");
            var price = part.UnitPrice!.Value;
            lines.Add(new InvoiceLineItem { SourceType = "POS Part", SourceId = part.PartId, Description = part.Name,
                Quantity = entry.Quantity, UnitPrice = price, LineTotal = checked(entry.Quantity * price) });
        }
        var subtotal = lines.Sum(line => line.LineTotal ?? 0);
        var tax = Math.Round(subtotal * await TaxFraction(form.TaxRateId, now), 2, MidpointRounding.AwayFromZero);
        var total = checked(subtotal + tax);
        Require(total > 0 && total <= 999999999, "The sale total is invalid.");
        Require(form.PaymentMethod != "Cash" || form.AmountTendered >= total, "Cash tendered must cover the total.");
        Require(form.PaymentMethod == "Cash" || form.AmountTendered == 0, "Tendered amount applies to cash payments only.");
        Require(form.PaymentMethod == "Cash" || !string.IsNullOrWhiteSpace(form.ReferenceNumber), "Enter a payment reference.");

        var number = await NextInvoiceNumberAsync(now);

        var invoice = new Invoice { InvoiceNumber = number, CustomerFirstName = "Walk-in customer",
            IssuedUtc = now, TaxRateId = form.TaxRateId,
            Subtotal = subtotal, TaxAmount = tax, Total = total, InvoiceStatus = "Paid", PaidUtc = now };
        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync();
        foreach (var line in lines) line.InvoiceId = invoice.InvoiceId;
        _dbContext.InvoiceLineItems.AddRange(lines);
        _dbContext.Payments.Add(new Payment { InvoiceId = invoice.InvoiceId, Amount = total,
            PaymentMethod = form.PaymentMethod, ReferenceNumber = form.ReferenceNumber?.Trim(), PaidUtc = now,
            ReceivedByUserId = HttpContext.Session.GetString("UserId"), ReceivedByName = HttpContext.Session.GetString("UserName"),
            Notes = form.PaymentMethod == "Cash" ? $"Cash tendered: {CurrencyDisplay.Cad(form.AmountTendered)}; change: {CurrencyDisplay.Cad(form.AmountTendered - total)}" : "Parts POS sale" });
        foreach (var entry in form.Lines)
        {
            var part = parts[entry.PartId];
            part.QuantityOnHand -= entry.Quantity;
            _dbContext.PartTransactions.Add(new PartTransaction { PartId = part.PartId, TransactionType = "Sold",
                Quantity = entry.Quantity, UnitCostAtTransaction = part.UnitCost, PerformedUtc = now,
                PerformedByUserId = HttpContext.Session.GetString("UserId"), PerformedByName = HttpContext.Session.GetString("UserName"),
                Notes = $"Parts POS invoice {number}" });
        }
        WorkflowAudit("Invoice", invoice.InvoiceId, "Parts POS sale", new { invoice.InvoiceNumber, invoice.Total, form.PaymentMethod, form.Lines });
        TempData["OwnerMessage"] = $"Sale {number} completed. Change: {CurrencyDisplay.Cad(form.PaymentMethod == "Cash" ? form.AmountTendered - total : 0)}.";
        return RedirectToAction(nameof(PartsPosReceipt), new { id = invoice.InvoiceId });
    }, nameof(PartsPos));
}
