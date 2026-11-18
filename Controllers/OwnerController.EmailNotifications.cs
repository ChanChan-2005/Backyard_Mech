using Backyard_Mech.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEmailNotifications(NotificationPreferencesForm form)
    {
        if (!IsOwnerSession()) return LoginRedirect();

        var row = await _dbContext.GarageSettings.SingleAsync(settings => settings.Id == 1);
        var values = System.Text.Json.JsonSerializer.Deserialize<GeneralSettingsValues>(row.ValuesJson) ?? new();
        values.EmailAppointmentReminders = form.EmailAppointmentReminders;
        values.LowStockAlerts = form.LowStockAlerts;
        values.OverdueInvoiceReminders = form.OverdueInvoiceReminders;
        row.ValuesJson = System.Text.Json.JsonSerializer.Serialize(values);
        row.Revision = Guid.NewGuid();
        WorkflowAudit("GarageSettings", 1, "Update email notifications", new
        {
            values.EmailAppointmentReminders, values.LowStockAlerts, values.OverdueInvoiceReminders
        });
        await _dbContext.SaveChangesAsync();
        TempData["SmtpMessage"] = "Email notification settings saved. Enabled messages are checked every 15 minutes.";
        return RedirectToAction(nameof(SettingsSystemPreferences));
    }
}
