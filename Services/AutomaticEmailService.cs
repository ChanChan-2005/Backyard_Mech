using Backyard_Mech.Data;
using Backyard_Mech.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;

namespace Backyard_Mech.Services;

public sealed class AutomaticEmailService
{
    private readonly ApplicationDbContext _db;
    private readonly GmailSmtpSender _sender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AutomaticEmailService> _logger;

    public AutomaticEmailService(ApplicationDbContext db, GmailSmtpSender sender,
        IConfiguration configuration, ILogger<AutomaticEmailService> logger)
    {
        _db = db;
        _sender = sender;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var status = _sender.GetStatus();
        if (!status.IsConfigured) return;

        var valuesJson = await _db.GarageSettings.AsNoTracking()
            .Where(row => row.Id == 1).Select(row => row.ValuesJson)
            .SingleAsync(cancellationToken);
        var preferences = JsonSerializer.Deserialize<GeneralSettingsValues>(valuesJson) ?? new();
        var nowUtc = DateTime.UtcNow;
        var garageZone = TimeZoneInfo.FindSystemTimeZoneById(_configuration["GarageTimeZone"] ?? "Asia/Manila");
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, garageZone);

        if (preferences.EmailAppointmentReminders)
            await SendAppointmentRemindersAsync(nowLocal, cancellationToken);
        if (preferences.LowStockAlerts)
            await SendLowStockSummaryAsync(nowLocal, status.SenderAddress, cancellationToken);
        if (preferences.OverdueInvoiceReminders)
            await SendOverdueInvoiceRemindersAsync(nowUtc, cancellationToken);
    }

    private async Task SendAppointmentRemindersAsync(DateTime nowLocal, CancellationToken cancellationToken)
    {
        // Appointment times are stored as garage-local wall times by the intake workflow.
        var appointments = await _db.Appointments.AsNoTracking()
            .Where(a => a.AppointmentStatus == "Scheduled" && a.CheckedInUtc == null &&
                a.ScheduledStart > nowLocal && a.ScheduledStart <= nowLocal.AddHours(24))
            .ToListAsync(cancellationToken);
        if (appointments.Count == 0) return;
        var customerIds = appointments.Where(a => a.CustomerId.HasValue)
            .Select(a => a.CustomerId!.Value).Distinct().ToArray();
        var customers = await _db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.CustomerId) && c.IsActive && !c.IsArchived && !c.IsDeleted)
            .ToDictionaryAsync(c => c.CustomerId, cancellationToken);

        foreach (var appointment in appointments)
        {
            if (!appointment.CustomerId.HasValue || !customers.TryGetValue(appointment.CustomerId.Value, out var customer) ||
                !ValidEmail(customer.Email) || !appointment.ScheduledStart.HasValue) continue;
            var name = CustomerName(customer);
            var date = appointment.ScheduledStart.Value.ToString("dddd, MMMM d, yyyy 'at' h:mm tt", CultureInfo.InvariantCulture);
            var body = $"Hello {name},\n\nThis is a reminder of your Backyard Mech appointment on {date}.\n\nIf you need to change your appointment, please contact the garage.\n\nBackyard Mech";
            await SendOnceAsync($"appointment:{appointment.AppointmentId}:{appointment.ScheduledStart.Value.Ticks}",
                "Appointment reminder", customer.Email!, "Your Backyard Mech appointment reminder", body, cancellationToken);
        }
    }

    private async Task SendLowStockSummaryAsync(DateTime nowLocal, string garageEmail, CancellationToken cancellationToken)
    {
        var parts = await _db.Parts.AsNoTracking()
            .Where(p => p.IsActive && !p.IsArchived && !p.IsDeleted && p.ReorderThreshold > 0 &&
                p.QuantityOnHand <= p.ReorderThreshold)
            .OrderBy(p => p.Name).ToListAsync(cancellationToken);
        if (parts.Count == 0) return;
        var lines = parts.Take(50).Select(p =>
            $"- {p.Name ?? p.SKU ?? $"Part #{p.PartId}"} ({p.SKU ?? "no SKU"}): {p.QuantityOnHand} on hand; reorder level {p.ReorderThreshold}");
        var more = parts.Count > 50 ? $"\n...and {parts.Count - 50} more low-stock parts." : "";
        var body = $"Low-stock parts as of {nowLocal:MMMM d, yyyy}:\n\n{string.Join("\n", lines)}{more}\n\nBackyard Mech";
        await SendOnceAsync($"low-stock:{nowLocal:yyyyMMdd}", "Low stock summary", garageEmail,
            $"Backyard Mech low-stock summary ({parts.Count} parts)", body, cancellationToken);
    }

    private async Task SendOverdueInvoiceRemindersAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        // Send on the day the invoice reaches 15 days. Older invoices are not mailed on rollout.
        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => (i.InvoiceStatus == "Issued" || i.InvoiceStatus == "Partially Paid") &&
                i.IssuedUtc > nowUtc.AddDays(-16) && i.IssuedUtc <= nowUtc.AddDays(-15) &&
                i.Total > 0 && i.CustomerId != null)
            .ToListAsync(cancellationToken);
        if (invoices.Count == 0) return;
        var customerIds = invoices.Select(i => i.CustomerId!.Value).Distinct().ToArray();
        var customers = await _db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.CustomerId) && c.IsActive && !c.IsArchived && !c.IsDeleted)
            .ToDictionaryAsync(c => c.CustomerId, cancellationToken);
        var invoiceIds = invoices.Select(i => i.InvoiceId).ToArray();
        var paidByInvoice = await _db.Payments.AsNoTracking()
            .Where(p => p.InvoiceId.HasValue && invoiceIds.Contains(p.InvoiceId.Value))
            .GroupBy(p => p.InvoiceId!.Value)
            .Select(g => new { InvoiceId = g.Key, Paid = g.Sum(p => p.Amount ?? 0) })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Paid, cancellationToken);

        foreach (var invoice in invoices)
        {
            if (!invoice.CustomerId.HasValue || !customers.TryGetValue(invoice.CustomerId.Value, out var customer) ||
                !ValidEmail(customer.Email)) continue;
            var balance = Math.Max(0, (invoice.Total ?? 0) - paidByInvoice.GetValueOrDefault(invoice.InvoiceId));
            if (balance <= 0) continue;
            var number = invoice.InvoiceNumber ?? $"INV-{invoice.InvoiceId:0000}";
            var body = $"Hello {CustomerName(customer)},\n\nThis is a reminder that invoice {number}, issued 15 days ago, has an outstanding balance of CA${balance.ToString("N2", CultureInfo.InvariantCulture)}.\n\nIf you have already paid, please disregard this message. Contact the garage if you have any questions.\n\nBackyard Mech";
            await SendOnceAsync($"invoice:{invoice.InvoiceId}:{invoice.IssuedUtc.Ticks}",
                "Overdue invoice", customer.Email!, $"Backyard Mech invoice {number} reminder", body, cancellationToken);
        }
    }

    private async Task SendOnceAsync(string key, string kind, string recipient, string subject,
        string body, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (!await _db.EmailDeliveries.AsNoTracking().AnyAsync(row => row.DeliveryKey == key, cancellationToken))
        {
            var delivery = new EmailDelivery
            {
                DeliveryKey = key, Kind = kind, Recipient = recipient,
                CreatedUtc = now, NextAttemptUtc = now
            };
            _db.EmailDeliveries.Add(delivery);
            try { await _db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is 2601 or 2627)
            {
                _db.Entry(delivery).State = EntityState.Detached;
            }
        }

        var claimed = await _db.EmailDeliveries.Where(row => row.DeliveryKey == key && row.SentUtc == null &&
                (row.LeaseUntilUtc == null || row.LeaseUntilUtc <= now) &&
                (row.NextAttemptUtc == null || row.NextAttemptUtc <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.Status, "Sending")
                .SetProperty(row => row.AttemptCount, row => row.AttemptCount + 1)
                .SetProperty(row => row.LeaseUntilUtc, now.AddMinutes(3)), cancellationToken);
        if (claimed == 0) return;

        try
        {
            await _sender.SendAsync(recipient, subject, body, cancellationToken);
            var sentAt = DateTime.UtcNow;
            await _db.EmailDeliveries.Where(row => row.DeliveryKey == key)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.Status, "Sent")
                    .SetProperty(row => row.SentUtc, sentAt)
                    .SetProperty(row => row.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(row => row.NextAttemptUtc, (DateTime?)null)
                    .SetProperty(row => row.LastError, (string?)null), cancellationToken);
            _logger.LogInformation("Automatic email sent: {Kind}, {Key}", kind, key);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Automatic email failed: {Kind}, {Key}", kind, key);
            await _db.EmailDeliveries.Where(row => row.DeliveryKey == key)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.Status, "Failed")
                    .SetProperty(row => row.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(row => row.NextAttemptUtc, DateTime.UtcNow.AddMinutes(15))
                    .SetProperty(row => row.LastError, exception.GetType().Name), cancellationToken);
        }
    }

    private static bool ValidEmail(string? address) =>
        !string.IsNullOrWhiteSpace(address) && new EmailAddressAttribute().IsValid(address);

    private static string CustomerName(Customer customer) =>
        string.Join(" ", new[] { customer.FirstName, customer.LastName }
            .Where(name => !string.IsNullOrWhiteSpace(name))) is { Length: > 0 } name ? name : "customer";
}

public sealed class AutomaticEmailWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutomaticEmailWorker> _logger;

    public AutomaticEmailWorker(IServiceScopeFactory scopeFactory, ILogger<AutomaticEmailWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunOnceAsync(stoppingToken);
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AutomaticEmailService>().RunAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Automatic email cycle failed.");
        }
    }
}
