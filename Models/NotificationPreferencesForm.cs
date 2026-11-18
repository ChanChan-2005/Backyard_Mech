namespace Backyard_Mech.Models;

public sealed class NotificationPreferencesForm
{
    public bool EmailAppointmentReminders { get; set; }
    public bool LowStockAlerts { get; set; }
    public bool OverdueInvoiceReminders { get; set; }
}
