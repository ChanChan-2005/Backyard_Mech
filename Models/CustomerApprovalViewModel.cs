namespace Backyard_Mech.Models;

public sealed class CustomerApprovalViewModel
{
    public Guid Token { get; set; }
    public string Customer { get; set; } = "";
    public string Vehicle { get; set; } = "";
    public int EstimateNumber { get; set; }
    public int RevisionNumber { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public decimal Total { get; set; }
    public List<EstimateRevisionLineItem> Lines { get; set; } = new();
}
