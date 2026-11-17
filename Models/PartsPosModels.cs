using System.ComponentModel.DataAnnotations;

namespace Backyard_Mech.Models;

public sealed class PartsPosViewModel
{
    public IReadOnlyList<PartsPosItem> Parts { get; set; } = Array.Empty<PartsPosItem>();
    public IReadOnlyList<PartsPosTax> Taxes { get; set; } = Array.Empty<PartsPosTax>();
}

public sealed class PartsPosReceiptViewModel
{
    public Invoice Invoice { get; set; } = new();
    public IReadOnlyList<InvoiceLineItem> Lines { get; set; } = Array.Empty<InvoiceLineItem>();
    public Payment? Payment { get; set; }
}

public sealed record PartsPosItem(int PartId, string Sku, string Name, decimal Price, int Available);
public sealed record PartsPosTax(int TaxRateId, string Name, decimal Rate);
public sealed class PartsPosCheckoutForm
{
    public Guid SubmissionToken { get; set; }
    public int? TaxRateId { get; set; }
    [Required, StringLength(30)] public string PaymentMethod { get; set; } = "Cash";
    [StringLength(100)] public string? ReferenceNumber { get; set; }
    [Range(0, 999999999)] public decimal AmountTendered { get; set; }
    public List<PartsPosCheckoutLine> Lines { get; set; } = new();
}

public sealed class PartsPosCheckoutLine
{
    public int PartId { get; set; }
    public int Quantity { get; set; }
}
