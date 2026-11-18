namespace Backyard_Mech.Models;

public sealed class EmailDelivery
{
    public string DeliveryKey { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? NextAttemptUtc { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public DateTime? SentUtc { get; set; }
    public string? LastError { get; set; }
}
