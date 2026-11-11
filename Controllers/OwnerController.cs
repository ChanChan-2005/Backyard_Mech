using Backyard_Mech.Data;
using Backyard_Mech.Models;
using Backyard_Mech.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Controllers
{
    [ServiceFilter(typeof(OwnerPermissionFilter))]
    public partial class OwnerController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly RecordsDispositionService _recordsDispositionService;
        private readonly PermissionService _permissionService;
        private readonly ReportExportService _reportExportService;

        public OwnerController(
            ApplicationDbContext dbContext,
            RecordsDispositionService recordsDispositionService,
            PermissionService permissionService,
            ReportExportService reportExportService)
        {
            _dbContext = dbContext;
            _recordsDispositionService = recordsDispositionService;
            _permissionService = permissionService;
            _reportExportService = reportExportService;
        }

        private bool IsOwnerSession()
        {
            return !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("UserRole"));
        }

        private IActionResult LoginRedirect()
        {
            return RedirectToAction("Login", "Account");
        }

        private static string FormatRelativeTime(DateTime value)
        {
            var elapsed = DateTime.UtcNow - DateTime.SpecifyKind(value, DateTimeKind.Utc);
            if (elapsed.TotalMinutes < 1)
            {
                return "Just now";
            }

            if (elapsed.TotalMinutes < 60)
            {
                return $"{Math.Max(1, (int)elapsed.TotalMinutes)}m ago";
            }

            if (elapsed.TotalHours < 24)
            {
                return $"{Math.Max(1, (int)elapsed.TotalHours)}h ago";
            }

            if (elapsed.TotalDays < 7)
            {
                return $"{Math.Max(1, (int)elapsed.TotalDays)}d ago";
            }

            return value.ToString("MMM dd, yyyy");
        }

        private static string GetDashboardStatusTone(string status)
        {
            return status.ToLowerInvariant() switch
            {
                "in progress" => "blue",
                "in bay" => "blue",
                "inspection" => "blue",
                "waiting approval" => "amber",
                "pending qc" => "amber",
                "scheduled" => "gray",
                "queued" => "gray",
                "pending" => "gray",
                "completed" => "green",
                "paid" => "green",
                "partially paid" => "amber",
                "issued" => "amber",
                "overdue" => "danger",
                "failed" => "danger",
                _ => "gray"
            };
        }

        private async Task<DashboardViewModel> LoadDashboardAsync(string queueFilter)
        {
            var nowUtc = DateTime.UtcNow;
            var today = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, GarageZone()).Date;
            var tomorrow = today.AddDays(1);
            var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(today, GarageZone());
            var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(tomorrow, GarageZone());
            var yesterdayStartUtc = TimeZoneInfo.ConvertTimeToUtc(today.AddDays(-1), GarageZone());

            var invoices = await _dbContext.Invoices.AsNoTracking().OrderByDescending(invoice => invoice.IssuedUtc).ToListAsync();
            var payments = await _dbContext.Payments
                .AsNoTracking()
                .OrderByDescending(payment => payment.PaidUtc ?? DateTime.MinValue)
                .ThenByDescending(payment => payment.PaymentId)
                .ToListAsync();
            var appointments = await _dbContext.Appointments.AsNoTracking().OrderBy(appointment => appointment.ScheduledStart).ToListAsync();
            var workOrders = await _dbContext.WorkOrders.AsNoTracking().OrderBy(order => order.ScheduledStart).ToListAsync();
            var assignments = await _dbContext.WorkOrderAssignments.AsNoTracking().ToListAsync();
            var vehicles = await _dbContext.Vehicles.AsNoTracking().ToDictionaryAsync(vehicle => vehicle.VehicleId);
            var appointmentServices = await _dbContext.AppointmentServices.AsNoTracking().ToListAsync();
            var estimates = await _dbContext.Estimates.AsNoTracking().ToListAsync();
            var estimateRevisions = await _dbContext.EstimateRevisions.AsNoTracking().ToListAsync();
            var parts = await _dbContext.Parts.AsNoTracking().Where(part => part.IsActive && !part.IsArchived && !part.IsDeleted).ToListAsync();
            var inspections = await _dbContext.QualityChecks.AsNoTracking().OrderByDescending(check => check.PerformedUtc).ToListAsync();
            var employees = await _dbContext.EmployeeProfiles.AsNoTracking().Where(employee => employee.IsActive && !employee.IsArchived && !employee.IsDeleted).ToListAsync();
            var labourSegments = await _dbContext.ServiceTimeSegments.AsNoTracking()
                .Where(segment => segment.ServiceEntryId.HasValue &&
                    (segment.SegmentEnd == null ||
                     (segment.SegmentType == "Work" && segment.SegmentStart < dayEndUtc && segment.SegmentEnd > dayStartUtc)))
                .ToListAsync();
            var manualEntryIds = (await _dbContext.AuditLogEntries.AsNoTracking()
                .Where(audit => audit.EntityName == "ServiceEntry" && audit.Action == "Save labour" &&
                    audit.TimestampUtc >= dayStartUtc && audit.TimestampUtc < dayEndUtc)
                .Select(audit => audit.EntityId)
                .ToListAsync())
                .Select(value => int.TryParse(value, out var id) ? id : 0)
                .Where(id => id > 0)
                .ToHashSet();
            var labourEntryIds = labourSegments.Select(segment => segment.ServiceEntryId!.Value)
                .Concat(manualEntryIds).Distinct().ToArray();
            var labourEntries = labourEntryIds.Length == 0
                ? new List<ServiceEntry>()
                : await _dbContext.ServiceEntries.AsNoTracking()
                    .Where(entry => labourEntryIds.Contains(entry.ServiceEntryId)).ToListAsync();

            var workOrderByAppointmentId = workOrders
                .Where(order => order.AppointmentId is not null)
                .GroupBy(order => order.AppointmentId!.Value)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(order => order.WorkOrderId).First());
            var appointmentById = appointments.ToDictionary(appointment => appointment.AppointmentId);
            var workOrderById = workOrders.ToDictionary(order => order.WorkOrderId);

            var todaysRevenue = invoices
                .Where(invoice => invoice.IssuedUtc >= dayStartUtc && invoice.IssuedUtc < dayEndUtc && invoice.InvoiceStatus != "Voided")
                .Sum(invoice => invoice.Total ?? 0);
            var yesterdayRevenue = invoices
                .Where(invoice => invoice.IssuedUtc >= yesterdayStartUtc && invoice.IssuedUtc < dayStartUtc && invoice.InvoiceStatus != "Voided")
                .Sum(invoice => invoice.Total ?? 0);
            var activeWorkOrders = workOrders.Count(order => order.WorkOrderStatus is not ("Completed" or "Cancelled" or "Ready for Pickup" or "Picked Up"));
            var pendingQcWorkOrders = workOrders.Count(order => string.Equals(order.WorkOrderStatus, "Pending QC", StringComparison.OrdinalIgnoreCase));
            var vehiclesServiced = workOrders
                .Where(order => (order.WorkOrderStatus is "Completed" or "Ready for Pickup" or "Picked Up")
                    && order.ActualEnd >= dayStartUtc && order.ActualEnd < dayEndUtc)
                .Select(order => appointmentById.GetValueOrDefault(order.AppointmentId ?? 0)?.VehicleId)
                .Where(vehicleId => vehicleId.HasValue)
                .Distinct()
                .Count();
            if (vehiclesServiced == 0)
            {
                vehiclesServiced = appointments
                    .Where(appointment => string.Equals(appointment.AppointmentStatus, "Completed", StringComparison.OrdinalIgnoreCase)
                        && appointment.ScheduledStart?.Date == today)
                    .Select(appointment => appointment.VehicleId)
                    .Where(vehicleId => vehicleId.HasValue)
                    .Distinct()
                    .Count();
            }

            var pendingApprovals = estimateRevisions
                .Where(revision => revision.EstimateId.HasValue
                    && string.Equals(revision.EstimateStatus, "Pending", StringComparison.OrdinalIgnoreCase)
                    && !estimateRevisions.Any(other => other.EstimateId == revision.EstimateId
                        && (other.RevisionNumber ?? 0) > (revision.RevisionNumber ?? 0)))
                .OrderByDescending(revision => revision.CreatedUtc)
                .ToList();
            var unpaidInvoices = invoices.Count(invoice => new[] { "Issued", "Partially Paid", "Overdue" }
                .Contains(invoice.InvoiceStatus, StringComparer.OrdinalIgnoreCase));
            var vehiclesInGarage = appointments
                .Where(appointment => appointment.AppointmentStatus is not ("Completed" or "Cancelled" or "No Show")
                    && (appointment.CheckedInUtc.HasValue || appointment.AppointmentStatus is "In Bay" or "Inspection"))
                .Select(appointment => appointment.VehicleId)
                .Where(vehicleId => vehicleId is not null)
                .Distinct()
                .Count();

            var revenueDelta = yesterdayRevenue == 0
                ? todaysRevenue > 0 ? "+100%" : "0%"
                : $"{((todaysRevenue - yesterdayRevenue) / yesterdayRevenue * 100):+0;-0;0}%";

            var queue = appointments
                .Where(appointment => appointment.AppointmentStatus is not ("Completed" or "Cancelled" or "No Show"))
                .Where(appointment => queueFilter == "all" || (queueFilter == "walkins") == string.Equals(appointment.AppointmentType, "Walk-in", StringComparison.OrdinalIgnoreCase))
                .OrderBy(appointment => appointment.ScheduledStart ?? DateTime.MaxValue)
                .Take(8)
                .Select(appointment =>
                {
                    vehicles.TryGetValue(appointment.VehicleId ?? 0, out var vehicle);
                    workOrderByAppointmentId.TryGetValue(appointment.AppointmentId, out var order);
                    var service = appointmentServices
                        .Where(item => item.AppointmentId == appointment.AppointmentId)
                        .Select(item => NormalizeText(item.ServiceName, "Service"))
                        .FirstOrDefault() ?? NormalizeText(appointment.AppointmentType, "Appointment");
                    var mechanic = assignments
                        .Where(assignment => assignment.WorkOrderId == order?.WorkOrderId)
                        .Select(assignment => NormalizeText(assignment.MechanicName, "Unassigned"))
                        .FirstOrDefault() ?? NormalizeText(appointment.AssignedMechanicName, "Unassigned");
                    var status = NormalizeStatus(order?.WorkOrderStatus, NormalizeStatus(appointment.AppointmentStatus, "Queued"));
                    var priority = string.Equals(status, "Waiting Approval", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Pending QC", StringComparison.OrdinalIgnoreCase)
                        ? "Urgent"
                        : "Normal";

                    return new DashboardServiceQueueRow
                    {
                        Id = (order is null ? appointment.AppointmentId : order.WorkOrderId).ToString("000"),
                        Vehicle = BuildVehicleName(vehicle),
                        Customer = NormalizeText(appointment.CustomerName, "Unknown customer"),
                        Service = service,
                        Mechanic = mechanic,
                        Status = status,
                        StatusTone = GetDashboardStatusTone(status),
                        Priority = priority,
                        PriorityTone = priority == "Urgent" ? "danger" : "",
                        Time = appointment.ScheduledStart?.ToString("hh:mm tt") ?? "-"
                    };
                })
                .ToList();

            var mechanicEmployees = employees.Where(employee =>
            {
                var name = NormalizeText($"{employee.FirstName} {employee.LastName}".Trim(), employee.EmployeeNumber ?? $"Employee {employee.EmployeeProfileId}");
                return employee.JobTitle?.Contains("mechanic", StringComparison.OrdinalIgnoreCase) == true
                    || employee.JobTitle?.Contains("technician", StringComparison.OrdinalIgnoreCase) == true
                    || assignments.Any(assignment =>
                        (!string.IsNullOrWhiteSpace(employee.UserId) && assignment.MechanicUserId == employee.UserId)
                        || string.Equals(assignment.MechanicName, name, StringComparison.OrdinalIgnoreCase))
                    || labourEntries.Any(entry =>
                        (!string.IsNullOrWhiteSpace(employee.UserId) && entry.MechanicUserId == employee.UserId)
                        || string.Equals(entry.MechanicName, name, StringComparison.OrdinalIgnoreCase));
            });
            var mechanicActivity = mechanicEmployees.Select(employee =>
            {
                var name = NormalizeText($"{employee.FirstName} {employee.LastName}".Trim(), employee.EmployeeNumber ?? $"Employee {employee.EmployeeProfileId}");
                var entryIds = labourEntries
                    .Where(entry => (!string.IsNullOrWhiteSpace(employee.UserId) && entry.MechanicUserId == employee.UserId)
                        || string.Equals(entry.MechanicName, name, StringComparison.OrdinalIgnoreCase))
                    .Select(entry => entry.ServiceEntryId).ToHashSet();
                var employeeSegments = labourSegments.Where(segment => entryIds.Contains(segment.ServiceEntryId ?? 0)).ToList();
                var running = employeeSegments.FirstOrDefault(segment => segment.SegmentEnd == null && segment.SegmentType == "Work");
                var paused = employeeSegments.FirstOrDefault(segment => segment.SegmentEnd == null && segment.SegmentType == "Pause");
                var currentEntry = labourEntries.FirstOrDefault(entry => entry.ServiceEntryId == (running ?? paused)?.ServiceEntryId);
                var workedMinutes = employeeSegments.Where(segment => segment.SegmentType == "Work" && segment.SegmentStart.HasValue)
                    .Sum(segment =>
                    {
                        var start = segment.SegmentStart!.Value > dayStartUtc ? segment.SegmentStart.Value : dayStartUtc;
                        var end = segment.SegmentEnd ?? nowUtc;
                        if (end > dayEndUtc) end = dayEndUtc;
                        return Math.Max(0, (end - start).TotalMinutes);
                    });
                var manualMinutes = labourEntries.Where(entry => entryIds.Contains(entry.ServiceEntryId) && manualEntryIds.Contains(entry.ServiceEntryId)
                    && !employeeSegments.Any(segment => segment.ServiceEntryId == entry.ServiceEntryId))
                    .Sum(entry => Math.Max(0, entry.DurationMinutes ?? 0));
                var hasActivity = workedMinutes > 0 || manualEntryIds.Overlaps(entryIds) || running is not null;
                var productivity = Math.Clamp((int)Math.Round((workedMinutes + manualMinutes) / 480d * 100), 0, 100);

                return (Card: new DashboardMechanicCard
                {
                    Name = name,
                    Avatar = BuildInitials(employee.FirstName, employee.LastName),
                    Work = running is not null ? "Busy" : paused is not null ? "Paused" : hasActivity ? "Worked today" : "Idle",
                    Current = currentEntry?.WorkOrderId is int workOrderId ? $"WO-{workOrderId:0000}" : "N/A",
                    Productivity = $"{productivity}%",
                    Bar = productivity
                }, HasActivity: hasActivity);
            }).ToList();
            var mechanics = mechanicActivity.OrderByDescending(item => item.HasActivity).Take(4).Select(item => item.Card).ToList();

            var approvalItems = pendingApprovals.Take(3).Select(revision =>
            {
                var estimate = estimates.FirstOrDefault(item => item.EstimateId == revision.EstimateId);
                appointmentById.TryGetValue(estimate?.AppointmentId ?? 0, out var appointment);
                vehicles.TryGetValue(appointment?.VehicleId ?? 0, out var vehicle);

                return new DashboardApprovalItem
                {
                    EstimateId = revision.EstimateId!.Value,
                    Customer = NormalizeText(appointment?.CustomerName, "Unknown customer"),
                    Vehicle = BuildVehicleName(vehicle),
                    Amount = FormatMoney(revision.Total),
                    Sent = revision.ApprovalTokenExpiresUtc <= nowUtc ? "Link expired" : FormatRelativeTime(revision.SentToCustomerUtc ?? revision.CreatedUtc)
                };
            }).ToList();

            var partAlerts = parts
                .Where(part => part.ReorderThreshold > 0 && part.QuantityOnHand <= part.ReorderThreshold)
                .OrderBy(part => part.QuantityOnHand)
                .Take(3)
                .Select(part => new DashboardPartAlertItem
                {
                    Name = NormalizeText(part.Name, part.SKU ?? $"Part {part.PartId}"),
                    Detail = $"min: {part.ReorderThreshold:N0}",
                    Badge = $"{part.QuantityOnHand:N0} left",
                    BadgeTone = part.QuantityOnHand == 0 ? "danger" : "amber",
                    IconTone = part.QuantityOnHand == 0 ? "danger" : "warn"
                })
                .ToList();

            var inspectionItems = inspections.Take(3).Select(check =>
            {
                workOrderById.TryGetValue(check.WorkOrderId ?? 0, out var order);
                appointmentById.TryGetValue(order?.AppointmentId ?? 0, out var appointment);
                vehicles.TryGetValue(appointment?.VehicleId ?? 0, out var vehicle);
                var status = check.Passed is null ? "Pending Review" : check.Passed.Value ? "Passed" : "Failed";

                return new DashboardInspectionItem
                {
                    Vehicle = BuildVehicleName(vehicle),
                    Detail = NormalizeText(check.Notes, "Quality inspection"),
                    Status = status,
                    Tone = status == "Passed" ? "green" : status == "Failed" ? "danger" : "amber"
                };
            }).ToList();

            var recentInvoiceRows = invoices.Take(4).Select(invoice =>
            {
                var status = NormalizeStatus(invoice.InvoiceStatus, "Draft");
                var issuedLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(invoice.IssuedUtc, DateTimeKind.Utc), GarageZone());
                return new DashboardInvoiceRow
                {
                    Invoice = NormalizeText(invoice.InvoiceNumber, $"INV-{invoice.InvoiceId:0000}"),
                    Customer = NormalizeText($"{invoice.CustomerFirstName} {invoice.CustomerLastName}".Trim(), "Unknown customer"),
                    Amount = FormatMoney(invoice.Total),
                    Status = status,
                    Tone = GetDashboardStatusTone(status),
                    Date = issuedLocal.Date == today ? $"Today, {issuedLocal:hh:mm tt}" : issuedLocal.ToString("MMM dd, hh:mm tt")
                };
            }).ToList();

            var collectedToday = payments
                .Where(payment => payment.PaidUtc?.Date == today)
                .Sum(payment => payment.Amount ?? 0);

            var activity = new List<DashboardActivityItem>();
            activity.AddRange(payments.Take(6).Select(payment =>
            {
                var invoice = invoices.FirstOrDefault(item => item.InvoiceId == payment.InvoiceId);
                return new DashboardActivityItem
                {
                    Title = $"Invoice {NormalizeText(invoice?.InvoiceNumber, $"#{payment.InvoiceId}")} paid - {FormatMoney(payment.Amount)}",
                    Time = payment.PaidUtc?.ToString("MMM dd, yyyy") ?? "Date unavailable",
                    TimestampUtc = payment.PaidUtc.HasValue
                        ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(payment.PaidUtc.Value, DateTimeKind.Unspecified), GarageZone())
                        : DateTime.MinValue
                };
            }));
            activity.AddRange(appointments.OrderByDescending(appointment => appointment.CreatedUtc).Take(6).Select(appointment => new DashboardActivityItem
            {
                Title = $"{NormalizeText(appointment.AppointmentType, "Appointment")} registered - {appointment.CustomerName}",
                Time = FormatRelativeTime(appointment.CreatedUtc),
                TimestampUtc = appointment.CreatedUtc
            }));
            activity.AddRange(workOrders.OrderByDescending(order => order.ActualEnd ?? order.CreatedUtc).Take(6).Select(order => new DashboardActivityItem
            {
                Title = $"Work Order #{order.WorkOrderId:0000} - {NormalizeStatus(order.WorkOrderStatus, "Pending").ToUpperInvariant()}",
                Time = FormatRelativeTime(order.ActualEnd ?? order.CreatedUtc),
                TimestampUtc = order.ActualEnd ?? order.CreatedUtc
            }));

            return new DashboardViewModel
            {
                PageDateLabel = today.ToString("dddd, MMMM d, yyyy"),
                QueueFilter = queueFilter,
                Metrics = new[]
                {
                    new DashboardMetricCard { Label = "Today's Revenue", Value = FormatMoney(todaysRevenue), Note = revenueDelta, Tone = todaysRevenue >= yesterdayRevenue ? "good" : "danger" },
                    new DashboardMetricCard { Label = "Active Work Orders", Value = activeWorkOrders.ToString("N0"), Note = pendingQcWorkOrders > 0 ? $"{pendingQcWorkOrders} pending QC" : "", Tone = pendingQcWorkOrders > 0 ? "warn" : "" },
                    new DashboardMetricCard { Label = "Vehicles Serviced", Value = vehiclesServiced.ToString("N0") },
                    new DashboardMetricCard { Label = "Pending Approvals", Value = pendingApprovals.Count.ToString("N0"), Note = pendingApprovals.Any() ? "action required" : "", Tone = pendingApprovals.Any() ? "warn" : "" },
                    new DashboardMetricCard { Label = "Unpaid Invoices", Value = unpaidInvoices.ToString("N0") },
                    new DashboardMetricCard { Label = "Vehicles in the Garage", Value = vehiclesInGarage.ToString("N0") }
                },
                ServiceQueue = queue,
                Mechanics = mechanics,
                PendingApprovals = approvalItems,
                PartAlerts = partAlerts,
                Inspections = inspectionItems,
                RecentInvoices = recentInvoiceRows,
                RecentActivity = activity.OrderByDescending(item => item.TimestampUtc).Take(6).ToList(),
                ActiveMechanicsLabel = $"{mechanicActivity.Count(item => item.HasActivity)} Active Today",
                PendingInspectionLabel = $"{inspections.Count(check => check.Passed is null)} pending review",
                TodayCollected = FormatMoney(collectedToday)
            };
        }

        public async Task<IActionResult> Dashboard(string queue = "all")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return View(await LoadDashboardAsync(queue is "appointments" or "walkins" ? queue : "all"));
        }

        public async Task<IActionResult> CustomerVehicle()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var customers = await _dbContext.Customers
                .AsNoTracking()
                .Where(customer => !customer.IsArchived && !customer.IsDeleted)
                .OrderBy(customer => customer.LastName)
                .ThenBy(customer => customer.FirstName)
                .Select(customer => new CustomerListItem
                {
                    CustomerId = customer.CustomerId,
                    FirstName = customer.FirstName ?? "",
                    LastName = customer.LastName ?? "",
                    Name = ((customer.FirstName ?? "") + " " + (customer.LastName ?? "")).Trim(),
                    Phone = customer.Phone ?? "",
                    Email = customer.Email ?? "",
                    StreetAddress = customer.StreetAddress ?? "",
                    Notes = customer.Notes ?? "",
                    Status = customer.IsActive ? "Active" : "Inactive"
                })
                .ToListAsync();

            var vehicleCounts = await _dbContext.VehicleOwnerships
                .AsNoTracking()
                .Where(ownership => ownership.CustomerId != null)
                .GroupBy(ownership => ownership.CustomerId!.Value)
                .Select(group => new { CustomerId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(group => group.CustomerId, group => group.Count);

            foreach (var customer in customers)
            {
                customer.Initials = BuildInitials(customer.FirstName, customer.LastName);
                var vehicleCount = vehicleCounts.GetValueOrDefault(customer.CustomerId);
                customer.Vehicles = vehicleCount == 1 ? "1 Vehicle" : $"{vehicleCount} Vehicles";
            }

            return View(customers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCustomer(CustomerFormModel model)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            if (!ModelState.IsValid)
            {
                TempData["OwnerMessage"] = FormValidationMessage("Customer was not saved. Check the entered values.");
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(CustomerVehicle));
            }

            _dbContext.Customers.Add(new Customer
            {
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Phone = model.Phone.Trim(),
                Email = model.Email.Trim(),
                StreetAddress = model.StreetAddress.Trim(),
                Notes = model.Notes?.Trim(),
                CreatedUtc = DateTime.UtcNow,
                IsActive = true
            });

            await _dbContext.SaveChangesAsync();
            TempData["OwnerMessage"] = "Customer saved.";
            return RedirectToAction(nameof(CustomerVehicle));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCustomer(CustomerFormModel model)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            if (!ModelState.IsValid)
            {
                TempData["OwnerMessage"] = FormValidationMessage("Customer was not updated. Check the entered values.");
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(CustomerVehicle));
            }

            var customer = await _dbContext.Customers.FindAsync(model.CustomerId);
            if (customer is null)
            {
                TempData["OwnerMessage"] = "Customer was not found.";
                return RedirectToAction(nameof(CustomerVehicle));
            }

            customer.FirstName = model.FirstName.Trim();
            customer.LastName = model.LastName.Trim();
            customer.Phone = model.Phone.Trim();
            customer.Email = model.Email.Trim();
            customer.StreetAddress = model.StreetAddress.Trim();
            customer.Notes = model.Notes?.Trim();

            await _dbContext.SaveChangesAsync();
            TempData["OwnerMessage"] = "Customer updated.";
            return RedirectToAction(nameof(CustomerVehicle));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> AddVehicle(VehicleFormModel form) => RegisterVehicle(form);

        private static string BuildInitials(string? firstName, string? lastName)
        {
            var first = string.IsNullOrWhiteSpace(firstName) ? "" : firstName.Trim()[0].ToString();
            var last = string.IsNullOrWhiteSpace(lastName) ? "" : lastName.Trim()[0].ToString();
            var initials = $"{first}{last}";
            return string.IsNullOrWhiteSpace(initials) ? "BM" : initials.ToUpperInvariant();
        }

        private static int ParseEntityId(string? id, string prefix)
        {
            var value = NormalizeText(id, "");
            if (int.TryParse(value, out var numericId))
            {
                return numericId;
            }

            var normalizedPrefix = $"{prefix}-";
            if (value.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(value[normalizedPrefix.Length..], out var prefixedId))
            {
                return prefixedId;
            }

            return 0;
        }

        private async Task<List<AppointmentListItem>> LoadAppointmentRowsAsync()
        {
            var appointments = await _dbContext.Appointments
                .AsNoTracking()
                .OrderBy(appointment => appointment.ScheduledStart ?? appointment.CreatedUtc)
                .ToListAsync();
            var vehicles = await _dbContext.Vehicles.AsNoTracking().ToDictionaryAsync(vehicle => vehicle.VehicleId);
            var bays = await _dbContext.Bays.AsNoTracking().ToDictionaryAsync(bay => bay.BayId);
            var employees = await _dbContext.EmployeeProfiles.AsNoTracking().ToListAsync();

            return appointments.Select(appointment =>
            {
                vehicles.TryGetValue(appointment.VehicleId ?? 0, out var vehicle);
                bays.TryGetValue(appointment.BayId ?? 0, out var bay);
                var assignedEmployee = employees.FirstOrDefault(employee =>
                    (!string.IsNullOrWhiteSpace(appointment.AssignedMechanicUserId) && employee.UserId == appointment.AssignedMechanicUserId) ||
                    string.Equals($"{employee.FirstName} {employee.LastName}".Trim(), appointment.AssignedMechanicName, StringComparison.OrdinalIgnoreCase));

                var start = appointment.ScheduledStart;
                var end = appointment.ScheduledEnd;
                var status = NormalizeStatus(appointment.AppointmentStatus, "Scheduled");
                var vehicleName = BuildVehicleName(vehicle);
                var duration = start.HasValue && end.HasValue
                    ? $"{Math.Max(0.25, (end.Value - start.Value).TotalHours):0.#}h"
                    : "";

                return new AppointmentListItem
                {
                    AppointmentId = appointment.AppointmentId,
                    CustomerId = appointment.CustomerId,
                    VehicleId = appointment.VehicleId,
                    BayId = appointment.BayId,
                    AssignedEmployeeProfileId = assignedEmployee?.EmployeeProfileId,
                    Id = $"APT-{appointment.AppointmentId:0000}",
                    Customer = NormalizeText(appointment.CustomerName, "Unknown customer"),
                    Vehicle = vehicleName,
                    Vin = vehicle?.VIN ?? "",
                    Plate = vehicle?.LicensePlate ?? "",
                    Date = start?.ToString("yyyy-MM-dd") ?? appointment.CreatedUtc.ToString("yyyy-MM-dd"),
                    Start = start?.ToString("hh:mm tt") ?? "",
                    End = end?.ToString("hh:mm tt") ?? "",
                    Time = start?.ToString("hh:mm tt") ?? "",
                    BayNumber = appointment.BayId ?? 0,
                    Bay = bay?.Name ?? (appointment.BayId.HasValue ? $"Bay {appointment.BayId}" : "Unassigned"),
                    Status = status,
                    Tone = GetAppointmentTone(status),
                    Advisor = NormalizeText(appointment.CreatedByName, "System"),
                    Type = NormalizeText(appointment.AppointmentType, "Appointment"),
                    Duration = duration,
                    Priority = "Normal",
                    PriorityTone = "blue",
                    DateInput = start?.ToString("yyyy-MM-dd") ?? "",
                    StartInput = start?.ToString("HH:mm") ?? "",
                    EndInput = end?.ToString("HH:mm") ?? "",
                    Notes = appointment.Notes ?? ""
                };
            }).ToList();
        }

        private async Task<ServiceQueueDetailsViewModel?> LoadServiceQueueDetailsAsync(int appointmentId)
        {
            var appointment = await _dbContext.Appointments
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.AppointmentId == appointmentId);

            if (appointment is null)
            {
                return null;
            }

            var vehicle = appointment.VehicleId.HasValue
                ? await _dbContext.Vehicles.AsNoTracking().FirstOrDefaultAsync(item => item.VehicleId == appointment.VehicleId)
                : null;
            var bay = appointment.BayId.HasValue
                ? await _dbContext.Bays.AsNoTracking().FirstOrDefaultAsync(item => item.BayId == appointment.BayId)
                : null;
            var services = await _dbContext.AppointmentServices
                .AsNoTracking()
                .Where(service => service.AppointmentId == appointment.AppointmentId)
                .OrderBy(service => service.CreatedUtc)
                .Select(service => service.ServiceName ?? $"Service {service.ServiceId}")
                .ToListAsync();
            var history = await _dbContext.QueueStatusHistories
                .AsNoTracking()
                .Where(item => item.AppointmentId == appointment.AppointmentId)
                .OrderByDescending(item => item.ChangedUtc)
                .Select(item => new ServiceQueueHistoryRow
                {
                    Status = item.QueueStatus ?? "Status update",
                    Changed = item.ChangedUtc.ToString("MMM dd, yyyy hh:mm tt"),
                    ChangedBy = item.ChangedByName ?? "System",
                    Notes = item.Notes ?? ""
                })
                .ToListAsync();

            var start = appointment.ScheduledStart;
            var end = appointment.ScheduledEnd;
            var status = NormalizeStatus(appointment.AppointmentStatus, "Scheduled");

            return new ServiceQueueDetailsViewModel
            {
                CheckedIn = appointment.CheckedInUtc.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(appointment.CheckedInUtc.Value, DateTimeKind.Utc), GarageZone()).ToString("MMM dd, yyyy hh:mm tt") : "Not checked in",
                AppointmentId = appointment.AppointmentId,
                Id = $"APT-{appointment.AppointmentId:0000}",
                Customer = NormalizeText(appointment.CustomerName, "Unknown customer"),
                Vehicle = BuildVehicleName(vehicle),
                Vin = vehicle?.VIN ?? "",
                Plate = vehicle?.LicensePlate ?? "",
                Status = status,
                StatusTone = GetAppointmentTone(status),
                Type = NormalizeText(appointment.AppointmentType, "Appointment"),
                Bay = bay?.Name ?? (appointment.BayId.HasValue ? $"Bay {appointment.BayId}" : "Unassigned"),
                Technician = NormalizeText(appointment.AssignedMechanicName, "Unassigned"),
                Advisor = NormalizeText(appointment.CreatedByName, "System"),
                Start = start?.ToString("MMM dd, yyyy hh:mm tt") ?? "Not scheduled",
                End = end?.ToString("MMM dd, yyyy hh:mm tt") ?? "Not scheduled",
                Duration = start.HasValue && end.HasValue
                    ? $"{Math.Max(0.25, (end.Value - start.Value).TotalHours):0.#} hours"
                    : "Not scheduled",
                Notes = NormalizeText(appointment.Notes, "No notes recorded"),
                Services = services,
                History = history
            };
        }

        private async Task SetOperationalOptionViewDataAsync()
        {
            var bays = await _dbContext.Bays
                .AsNoTracking()
                .Where(bay => bay.IsActive)
                .OrderBy(bay => bay.BayNumber)
                .ThenBy(bay => bay.Name)
                .Select(bay => new ProductListingOption
                {
                    Id = bay.BayId,
                    Label = string.IsNullOrWhiteSpace(bay.Name)
                        ? $"Bay {bay.BayNumber ?? bay.BayId.ToString()}"
                        : bay.Name
                })
                .ToListAsync();

            var activeAppointments = await _dbContext.Appointments
                .AsNoTracking()
                .Where(appointment => !new[] { "Completed", "Cancelled", "No Show" }.Contains(appointment.AppointmentStatus ?? ""))
                .ToListAsync();
            var currentAppointmentId = ViewData["CurrentAppointmentId"] as int?;
            var currentTechnician = ViewData["CurrentTechnician"]?.ToString() ?? "";
            var currentBay = ViewData["CurrentBay"]?.ToString() ?? "";

            var employees = await _dbContext.EmployeeProfiles
                .AsNoTracking()
                .Where(employee => employee.IsActive && !employee.IsArchived && !employee.IsDeleted)
                .OrderBy(employee => employee.FirstName)
                .ThenBy(employee => employee.LastName)
                .Select(employee => new ProductListingOption
                {
                    Id = employee.EmployeeProfileId,
                    Label = string.IsNullOrWhiteSpace((employee.FirstName + " " + employee.LastName).Trim())
                        ? employee.Email ?? employee.EmployeeNumber ?? $"Employee {employee.EmployeeProfileId}"
                        : (employee.FirstName + " " + employee.LastName).Trim()
                })
                .ToListAsync();
            var employeeRows = await _dbContext.EmployeeProfiles
                .AsNoTracking()
                .Where(employee => employee.IsActive && !employee.IsArchived && !employee.IsDeleted)
                .OrderBy(employee => employee.FirstName)
                .ThenBy(employee => employee.LastName)
                .ToListAsync();
            var queueEmployeeOptions = employeeRows.Select(employee =>
            {
                var name = NormalizeText($"{employee.FirstName} {employee.LastName}".Trim(), employee.Email ?? employee.EmployeeNumber ?? $"Employee {employee.EmployeeProfileId}");
                var activeJobs = activeAppointments.Count(appointment =>
                    !string.Equals(appointment.AppointmentId.ToString(), currentAppointmentId?.ToString(), StringComparison.Ordinal) &&
                    (string.Equals(appointment.AssignedMechanicName, name, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(appointment.PreferredMechanicName, name, StringComparison.OrdinalIgnoreCase)));

                return new QueueEmployeeOption
                {
                    Id = employee.EmployeeProfileId,
                    Name = name,
                    Role = NormalizeText(employee.JobTitle, "Technician"),
                    ActiveJobs = activeJobs,
                    IsSelected = string.Equals(name, currentTechnician, StringComparison.OrdinalIgnoreCase)
                };
            }).ToList();
            var bayRows = await _dbContext.Bays
                .AsNoTracking()
                .Where(bay => bay.IsActive)
                .OrderBy(bay => bay.BayNumber)
                .ThenBy(bay => bay.Name)
                .ToListAsync();
            var queueBayOptions = bayRows.Select(bay =>
            {
                var label = string.IsNullOrWhiteSpace(bay.Name)
                    ? $"Bay {bay.BayNumber ?? bay.BayId.ToString()}"
                    : bay.Name;
                var isSelected = string.Equals(label, currentBay, StringComparison.OrdinalIgnoreCase);
                var isOccupied = activeAppointments.Any(appointment =>
                    appointment.AppointmentId != currentAppointmentId &&
                    appointment.BayId == bay.BayId);

                return new QueueBayOption
                {
                    Id = bay.BayId,
                    Name = label,
                    Description = NormalizeText(bay.Description, "Service bay"),
                    Status = isSelected ? "Selected" : isOccupied ? "Occupied" : "Available",
                    StatusClass = isSelected ? "selected" : isOccupied ? "occupied" : "available",
                    IsSelected = isSelected
                };
            }).ToList();

            var approvedEstimates = await LoadEstimateRowsAsync();

            ViewData["BayOptions"] = bays;
            ViewData["EmployeeOptions"] = employees;
            var customerOptions = await _dbContext.Customers
                .AsNoTracking()
                .Where(customer => customer.IsActive && !customer.IsArchived && !customer.IsDeleted)
                .OrderBy(customer => customer.FirstName)
                .ThenBy(customer => customer.LastName)
                .Select(customer => new AppointmentCustomerOption
                {
                    CustomerId = customer.CustomerId,
                    Name = ((customer.FirstName ?? "") + " " + (customer.LastName ?? "")).Trim(),
                    Contact = customer.Phone ?? customer.Email ?? ""
                })
                .ToListAsync();
            var activeCustomerIds = customerOptions.Select(customer => customer.CustomerId).ToHashSet();
            var ownerships = await _dbContext.VehicleOwnerships
                .AsNoTracking()
                .Where(ownership => ownership.VehicleId.HasValue && ownership.CustomerId.HasValue && !ownership.OwnershipEndUtc.HasValue)
                .ToListAsync();
            var vehicleRows = await _dbContext.Vehicles
                .AsNoTracking()
                .Where(vehicle => !vehicle.IsArchived && !vehicle.IsDeleted)
                .OrderBy(vehicle => vehicle.Make)
                .ThenBy(vehicle => vehicle.Model)
                .ToListAsync();
            var vehicleOptions = (from ownership in ownerships
                                  where activeCustomerIds.Contains(ownership.CustomerId!.Value)
                                  join vehicle in vehicleRows on ownership.VehicleId equals vehicle.VehicleId
                                  select new AppointmentVehicleOption
                                  {
                                      VehicleId = vehicle.VehicleId,
                                      CustomerId = ownership.CustomerId.GetValueOrDefault(),
                                      Label = $"{vehicle.Year} {vehicle.Make} {vehicle.Model} - {vehicle.LicensePlate ?? vehicle.VIN ?? "No plate"}".Trim()
                                  })
                .GroupBy(option => new { option.VehicleId, option.CustomerId })
                .Select(group => group.First())
                .ToList();
            ViewData["AppointmentCustomerOptions"] = customerOptions;
            ViewData["AppointmentVehicleOptions"] = vehicleOptions;
            ViewData["QueueEmployeeOptions"] = queueEmployeeOptions;
            ViewData["QueueBayOptions"] = queueBayOptions;
            ViewData["ApprovedEstimateOptions"] = approvedEstimates
                .Where(estimate => string.Equals(estimate.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                .Select(estimate => new ProductListingOption
                {
                    Id = estimate.EstimateId,
                    Label = $"{estimate.Id} - {estimate.Customer} - {estimate.Vehicle}"
                })
                .ToList();
        }

        private async Task<List<WorkOrderListItem>> LoadWorkOrderRowsAsync()
        {
            var workOrders = await _dbContext.WorkOrders
                .AsNoTracking()
                .OrderByDescending(order => order.CreatedUtc)
                .ToListAsync();
            var appointments = await _dbContext.Appointments.AsNoTracking().ToDictionaryAsync(appointment => appointment.AppointmentId);
            var vehicles = await _dbContext.Vehicles.AsNoTracking().ToDictionaryAsync(vehicle => vehicle.VehicleId);
            var bays = await _dbContext.Bays.AsNoTracking().ToDictionaryAsync(bay => bay.BayId);
            var assignments = await _dbContext.WorkOrderAssignments.AsNoTracking().ToListAsync();

            return workOrders.Select(order =>
            {
                appointments.TryGetValue(order.AppointmentId ?? 0, out var appointment);
                vehicles.TryGetValue(appointment?.VehicleId ?? 0, out var vehicle);
                bays.TryGetValue(order.BayId ?? appointment?.BayId ?? 0, out var bay);
                var mechanics = assignments
                    .Where(assignment => assignment.WorkOrderId == order.WorkOrderId)
                    .Select(assignment => NormalizeText(assignment.MechanicName, "Unassigned"))
                    .Distinct()
                    .ToArray();
                var status = NormalizeStatus(order.WorkOrderStatus, "Pending");

                return new WorkOrderListItem
                {
                    WorkOrderId = order.WorkOrderId,
                    Id = $"WO-{order.WorkOrderId:0000}",
                    Customer = NormalizeText(appointment?.CustomerName, "Unknown customer"),
                    Vehicle = BuildVehicleName(vehicle),
                    Bay = bay?.Name ?? "Unassigned",
                    Mechanic = mechanics.Length > 0 ? string.Join(", ", mechanics) : "Unassigned",
                    Status = status,
                    StatusTone = GetWorkOrderTone(status),
                    Priority = "Normal",
                    PriorityTone = "blue",
                    Expected = order.ScheduledEnd?.ToString("MMM dd, hh:mm tt") ?? order.ActualEnd?.ToString("MMM dd, hh:mm tt") ?? "Not scheduled",
                    SortOrder = order.ScheduledEnd.HasValue ? 0 : 1
                };
            }).ToList();
        }

        private async Task<WorkOrderDetailsViewModel?> LoadWorkOrderDetailsAsync(int workOrderId)
        {
            var order = await _dbContext.WorkOrders.AsNoTracking().FirstOrDefaultAsync(item => item.WorkOrderId == workOrderId);
            if (order is null)
            {
                return null;
            }

            var appointment = order.AppointmentId.HasValue
                ? await _dbContext.Appointments.AsNoTracking().FirstOrDefaultAsync(item => item.AppointmentId == order.AppointmentId)
                : null;
            var vehicle = appointment?.VehicleId is > 0
                ? await _dbContext.Vehicles.AsNoTracking().FirstOrDefaultAsync(item => item.VehicleId == appointment.VehicleId)
                : null;
            var bay = (order.BayId ?? appointment?.BayId) is int bayId and > 0
                ? await _dbContext.Bays.AsNoTracking().FirstOrDefaultAsync(item => item.BayId == bayId)
                : null;
            var assignments = await _dbContext.WorkOrderAssignments.AsNoTracking()
                .Where(item => item.WorkOrderId == order.WorkOrderId)
                .OrderBy(item => item.AssignedUtc)
                .ToListAsync();
            var services = appointment is null
                ? new List<string>()
                : await _dbContext.AppointmentServices.AsNoTracking()
                    .Where(item => item.AppointmentId == appointment.AppointmentId)
                    .OrderBy(item => item.CreatedUtc)
                    .Select(item => item.ServiceName ?? $"Service {item.ServiceId}")
                    .ToListAsync();
            var serviceEntries = await _dbContext.ServiceEntries.AsNoTracking()
                .Where(item => item.WorkOrderId == order.WorkOrderId)
                .OrderByDescending(item => item.ServiceEntryId)
                .ToListAsync();
            var partTransactions = await _dbContext.PartTransactions.AsNoTracking()
                .Where(item => item.WorkOrderId == order.WorkOrderId)
                .OrderByDescending(item => item.PerformedUtc)
                .ToListAsync();
            var partIds = partTransactions
                .Where(item => item.PartId.HasValue)
                .Select(item => item.PartId!.Value)
                .Distinct()
                .ToList();
            var parts = partIds.Count == 0
                ? new Dictionary<int, Part>()
                : await _dbContext.Parts.AsNoTracking()
                    .Where(part => partIds.Contains(part.PartId))
                    .ToDictionaryAsync(part => part.PartId);
            var estimateRevision = order.ApprovedEstimateRevisionId.HasValue
                ? await _dbContext.EstimateRevisions.AsNoTracking().FirstOrDefaultAsync(item => item.EstimateRevisionId == order.ApprovedEstimateRevisionId)
                : order.EstimateId.HasValue
                    ? await _dbContext.EstimateRevisions.AsNoTracking()
                        .Where(item => item.EstimateId == order.EstimateId)
                        .OrderByDescending(item => item.RevisionNumber)
                        .FirstOrDefaultAsync()
                    : null;

            var status = NormalizeStatus(order.WorkOrderStatus, "Pending");
            var invoice = await _dbContext.Invoices.AsNoTracking().Where(x => x.WorkOrderId == order.WorkOrderId && x.InvoiceStatus != "Voided")
                .OrderByDescending(x => x.InvoiceId).FirstOrDefaultAsync();
            var paidAmount = invoice is null ? 0 : await _dbContext.Payments.AsNoTracking().Where(x => x.InvoiceId == invoice.InvoiceId).SumAsync(x => x.Amount ?? 0);
            var mechanicNames = assignments
                .Select(item => NormalizeText(item.MechanicName, "Unassigned"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var mechanic = mechanicNames.Length == 0 ? NormalizeText(appointment?.AssignedMechanicName, "Unassigned") : string.Join(", ", mechanicNames);
            var hours = CalculateWorkOrderHours(order);
            var priority = string.Equals(status, "Pending QC", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Waiting Approval", StringComparison.OrdinalIgnoreCase)
                ? "Urgent"
                : "Normal";
            var laborRows = serviceEntries
                .OrderBy(item => item.ServiceEntryId)
                .Select(item =>
                {
                    var entryHours = item.DurationMinutes is > 0 ? item.DurationMinutes.Value / 60m : item.EstimatedHours ?? 0;
                    var entryCost = item.ServiceCost ?? entryHours * (item.HourlyRate ?? 0);
                    return new WorkOrderLaborRow
                    {
                        Mechanic = NormalizeText(item.MechanicName, "Unassigned"),
                        Task = NormalizeText(item.ServiceDescription, "Service task"),
                        Hours = $"{entryHours:0.#} hrs",
                        HoursValue = entryHours,
                        Cost = FormatMoney(entryCost),
                        CostValue = entryCost
                    };
                })
                .ToList();
            if (laborRows.Count == 0 && services.Count > 0)
            {
                laborRows.AddRange(services.Select(service => new WorkOrderLaborRow
                {
                    Mechanic = mechanic,
                    Task = service,
                    Hours = hours > 0 ? $"{hours:0.#} hrs" : "Not logged",
                    HoursValue = hours,
                    Cost = "$0.00"
                }));
            }

            var partRows = partTransactions
                .Where(x => x.TransactionType is "Installed" or "Returned")
                .Select(transaction =>
                {
                    parts.TryGetValue(transaction.PartId ?? 0, out var part);
                    var quantity = (transaction.Quantity ?? 0) * (transaction.TransactionType == "Returned" ? -1 : 1);
                    var unitPrice = transaction.UnitCostAtTransaction ?? part?.UnitPrice ?? part?.UnitCost ?? 0;
                    return new WorkOrderPartRow
                    {
                        Sku = NormalizeText(part?.SKU, $"PART-{transaction.PartId ?? transaction.PartTransactionId:0000}"),
                        Name = NormalizeText(part?.Name, NormalizeText(transaction.Notes, "Part transaction")),
                        Quantity = quantity == 0 ? "-" : quantity.ToString("N0"),
                        UnitPrice = FormatMoney(unitPrice),
                        TotalCost = FormatMoney(unitPrice * quantity)
                    };
                })
                .ToList();
            var laborTotal = laborRows.Sum(item => item.CostValue);
            var partsTotal = partTransactions.Where(x => x.TransactionType is "Installed" or "Returned").Sum(transaction =>
            {
                parts.TryGetValue(transaction.PartId ?? 0, out var part);
                return (transaction.Quantity ?? 0) * (transaction.TransactionType == "Returned" ? -1 : 1) * (transaction.UnitCostAtTransaction ?? part?.UnitCost ?? 0);
            });
            var shopSuppliesFee = 0m;
            var activity = new List<WorkOrderActivityItem>
            {
                new() { Title = "Created by system", Detail = "Work order opened", Time = order.CreatedUtc.ToString("hh:mm tt") }
            };

            activity.AddRange(assignments.Select(item => new WorkOrderActivityItem
            {
                Title = $"Assigned {NormalizeText(item.MechanicName, "mechanic")}",
                Detail = $"{NormalizeText(item.AssignmentRole, "Technician")} assigned",
                Time = item.AssignedUtc.ToString("hh:mm tt")
            }));
            activity.AddRange(serviceEntries.Take(4).Select(item => new WorkOrderActivityItem
            {
                Title = NormalizeText(item.ServiceDescription, "Service entry recorded"),
                Detail = $"{NormalizeText(item.MechanicName, "Mechanic")} - {(item.DurationMinutes is > 0 ? $"{item.DurationMinutes.Value / 60m:0.#} hrs" : $"{item.EstimatedHours ?? 0:0.#} hrs")}",
                Time = "Logged"
            }));
            activity.AddRange(partTransactions.Take(3).Select(item => new WorkOrderActivityItem
            {
                Title = NormalizeText(item.TransactionType, "Part transaction"),
                Detail = NormalizeText(item.Notes, "Inventory movement recorded"),
                Time = item.PerformedUtc.ToString("hh:mm tt")
            }));

            return new WorkOrderDetailsViewModel
            {
                WorkOrderId = order.WorkOrderId,
                Id = $"WO-{order.WorkOrderId:0000}",
                Customer = NormalizeText(appointment?.CustomerName, "Unknown customer"),
                Vehicle = BuildVehicleName(vehicle),
                Vin = NormalizeText(vehicle?.VIN, "No VIN"),
                Plate = NormalizeText(vehicle?.LicensePlate, "No plate"),
                Bay = bay?.Name ?? ((order.BayId ?? appointment?.BayId).HasValue ? $"Bay {order.BayId ?? appointment?.BayId}" : "Unassigned"),
                Mechanic = mechanic,
                Status = status,
                StatusTone = GetWorkOrderTone(status),
                InvoiceStatus = invoice?.InvoiceStatus ?? string.Empty,
                InvoiceBalance = invoice is null ? 0 : Math.Max(0, (invoice.Total ?? 0) - paidAmount),
                ReleasedUtc = order.ReleasedUtc,
                ReleasedByName = order.ReleasedByName,
                Priority = priority,
                PriorityTone = priority == "Urgent" ? "urgent" : "blue",
                ScheduledStart = order.ScheduledStart?.ToString("MMM dd, yyyy hh:mm tt") ?? "Not scheduled",
                ScheduledEnd = order.ScheduledEnd?.ToString("MMM dd, yyyy hh:mm tt") ?? "Not scheduled",
                ActualStart = order.ActualStart?.ToString("MMM dd, yyyy hh:mm tt") ?? "Not started",
                ActualEnd = order.ActualEnd?.ToString("MMM dd, yyyy hh:mm tt") ?? "Not completed",
                Hours = $"{hours:0.#} Hrs",
                ServiceSummary = services.Count == 0 ? NormalizeText(appointment?.AppointmentType, "General service") : string.Join(", ", services),
                Notes = NormalizeText(appointment?.Notes, "No notes recorded"),
                EstimateId = order.EstimateId.HasValue ? $"EST-{order.EstimateId:0000}" : "No estimate linked",
                Initials = BuildInitials(appointment?.CustomerName?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), appointment?.CustomerName?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault()),
                Odometer = vehicle?.Odometer is > 0 ? $"{vehicle.Odometer:N0} mi" : "Not recorded",
                LaborTotal = FormatMoney(laborTotal),
                PartsTotal = FormatMoney(partsTotal),
                ShopSuppliesFee = FormatMoney(shopSuppliesFee),
                EstimatedTotal = FormatMoney(estimateRevision?.Total ?? laborTotal + partsTotal + shopSuppliesFee),
                LoggedHours = $"{laborRows.Sum(item => item.HoursValue):0.#} hrs",
                PlannedHours = $"{hours:0.#} hrs planned",
                Services = services,
                LaborRows = laborRows,
                PartRows = partRows,
                Activity = activity.ToList()
            };
        }

        private async Task<WorkOrderBayDispatchViewModel?> LoadWorkOrderBayDispatchAsync(int workOrderId)
        {
            var workOrder = await LoadWorkOrderDetailsAsync(workOrderId);
            if (workOrder is null)
            {
                return null;
            }

            var bays = await _dbContext.Bays.AsNoTracking().OrderBy(item => item.BayId).ToListAsync();
            var activeOrders = await _dbContext.WorkOrders.AsNoTracking()
                .Where(item => item.BayId.HasValue && item.WorkOrderId != workOrderId && item.WorkOrderStatus != "Completed" && item.WorkOrderStatus != "Ready for Pickup" && item.WorkOrderStatus != "Picked Up" && item.WorkOrderStatus != "Cancelled")
                .ToListAsync();
            var appointmentIds = activeOrders.Where(item => item.AppointmentId.HasValue).Select(item => item.AppointmentId!.Value).Distinct().ToList();
            var appointments = appointmentIds.Count == 0
                ? new Dictionary<int, Appointment>()
                : await _dbContext.Appointments.AsNoTracking()
                    .Where(item => appointmentIds.Contains(item.AppointmentId))
                    .ToDictionaryAsync(item => item.AppointmentId);
            var vehicleIds = appointments.Values.Where(item => item.VehicleId.HasValue).Select(item => item.VehicleId!.Value).Distinct().ToList();
            var vehicles = vehicleIds.Count == 0
                ? new Dictionary<int, Vehicle>()
                : await _dbContext.Vehicles.AsNoTracking()
                    .Where(item => vehicleIds.Contains(item.VehicleId))
                    .ToDictionaryAsync(item => item.VehicleId);
            var today = DateTime.Today;
            var maintenance = await _dbContext.BayMaintenanceSchedules.AsNoTracking()
                .Where(item => !item.IsCompleted && item.StartDate.Date <= today && item.EstimatedReturnDate.Date >= today)
                .ToListAsync();

            return new WorkOrderBayDispatchViewModel
            {
                WorkOrder = workOrder,
                Bays = bays.Select(bay =>
                {
                    var activeOrder = activeOrders.FirstOrDefault(item => item.BayId == bay.BayId);
                    var bayMaintenance = maintenance.FirstOrDefault(item => item.BayId == bay.BayId);
                    appointments.TryGetValue(activeOrder?.AppointmentId ?? 0, out var appointment);
                    vehicles.TryGetValue(appointment?.VehicleId ?? 0, out var vehicle);
                    var status = !bay.IsActive || bayMaintenance is not null
                        ? "Maintenance"
                        : activeOrder is null
                            ? "Available"
                            : "Occupied";

                    return new WorkOrderBayDispatchRow
                    {
                        BayId = bay.BayId,
                        BayIdentifier = NormalizeText(bay.BayNumber, $"Bay {bay.BayId}"),
                        HardwareType = NormalizeText(bay.Name, "General Service"),
                        BayStatus = status,
                        StatusTone = status == "Available" ? "available" : status == "Maintenance" ? "maintenance" : "occupied",
                        CurrentOccupant = activeOrder is null ? "None (Empty)" : BuildVehicleName(vehicle),
                        EstimatedRelease = bayMaintenance is not null
                            ? bayMaintenance.EstimatedReturnDate.ToString("MMM dd, hh:mm tt")
                            : activeOrder?.ScheduledEnd?.ToString("Today, hh:mm tt") ?? "Immediate",
                        IsSelected = string.Equals(workOrder.Bay, NormalizeText(bay.Name, $"Bay {bay.BayId}"), StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(workOrder.Bay, NormalizeText(bay.BayNumber, $"Bay {bay.BayId}"), StringComparison.OrdinalIgnoreCase),
                        CanDispatch = bay.IsActive && bayMaintenance is null
                    };
                }).ToList()
            };
        }

        private async Task<WorkOrderStaffDispatchViewModel?> LoadWorkOrderStaffDispatchAsync(int workOrderId)
        {
            var workOrder = await LoadWorkOrderDetailsAsync(workOrderId);
            if (workOrder is null)
            {
                return null;
            }

            var employees = await _dbContext.EmployeeProfiles.AsNoTracking()
                .Where(item => item.IsActive && !item.IsArchived && !item.IsDeleted)
                .OrderBy(item => item.FirstName)
                .ThenBy(item => item.LastName)
                .ToListAsync();
            var activeOrders = await _dbContext.WorkOrders.AsNoTracking()
                .Where(item => item.WorkOrderStatus != "Completed" && item.WorkOrderStatus != "Ready for Pickup" && item.WorkOrderStatus != "Picked Up" && item.WorkOrderStatus != "Cancelled")
                .ToListAsync();
            var assignments = await _dbContext.WorkOrderAssignments.AsNoTracking().ToListAsync();
            var currentAssignments = assignments.Where(item => item.WorkOrderId == workOrderId).ToList();
            var leadName = currentAssignments.FirstOrDefault(item => string.Equals(item.AssignmentRole, "Lead", StringComparison.OrdinalIgnoreCase))?.MechanicName;
            var helperName = currentAssignments.FirstOrDefault(item => string.Equals(item.AssignmentRole, "Helper", StringComparison.OrdinalIgnoreCase))?.MechanicName;
            var overlapMechanic = assignments
                .Where(item => item.WorkOrderId != workOrderId)
                .GroupBy(item => NormalizeText(item.MechanicName, ""))
                .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
                .Select(group => group.Key)
                .FirstOrDefault();

            return new WorkOrderStaffDispatchViewModel
            {
                WorkOrder = workOrder,
                Warning = string.IsNullOrWhiteSpace(overlapMechanic)
                    ? string.Empty
                    : $"{overlapMechanic} has multiple active work order assignments. Check schedule before confirming.",
                Mechanics = employees.Select(employee =>
                {
                    var name = BuildEmployeeName(employee);
                    var employeeAssignments = assignments
                        .Where(item => string.Equals(NormalizeText(item.MechanicName, ""), name, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    var activeCount = employeeAssignments.Count(item => activeOrders.Any(order => order.WorkOrderId == item.WorkOrderId && order.WorkOrderId != workOrderId));

                    return new WorkOrderStaffDispatchRow
                    {
                        EmployeeProfileId = employee.EmployeeProfileId,
                        Mechanic = name,
                        Specialization = NormalizeText(employee.JobTitle, "Mechanic"),
                        ActiveJobs = $"{activeCount} WOs active",
                        Availability = activeCount > 2 ? "Overlap Alert" : "Available",
                        AvailabilityTone = activeCount > 2 ? "overlap" : "available",
                        Certifications = NormalizeText(employee.EmployeeNumber, "ASE Certified"),
                        IsLead = string.Equals(name, leadName, StringComparison.OrdinalIgnoreCase),
                        IsHelper = string.Equals(name, helperName, StringComparison.OrdinalIgnoreCase)
                    };
                }).ToList()
            };
        }

        private async Task<WorkOrderLaborTrackingViewModel?> LoadWorkOrderLaborTrackingAsync(int workOrderId)
        {
            var workOrder = await LoadWorkOrderDetailsAsync(workOrderId);
            if (workOrder is null)
            {
                return null;
            }

            var serviceEntryIds = await _dbContext.ServiceEntries.AsNoTracking()
                .Where(item => item.WorkOrderId == workOrderId)
                .Select(item => item.ServiceEntryId)
                .ToListAsync();
            var activeSegments = serviceEntryIds.Count == 0
                ? new List<ServiceTimeSegment>()
                : await _dbContext.ServiceTimeSegments.AsNoTracking()
                    .Where(item => item.ServiceEntryId.HasValue && serviceEntryIds.Contains(item.ServiceEntryId.Value) && item.SegmentEnd == null)
                    .ToListAsync();
            var maxHours = Math.Max(1m, workOrder.LaborRows.Sum(item => item.HoursValue));

            return new WorkOrderLaborTrackingViewModel
            {
                WorkOrder = workOrder,
                Entries = workOrder.LaborRows,
                HasActiveTimer = activeSegments.Count > 0,
                ActiveTimerText = activeSegments.Count == 0
                    ? "00:00:00"
                    : TimeSpan.FromMinutes(activeSegments.Sum(item => Math.Max(0, (DateTime.UtcNow - (item.SegmentStart ?? DateTime.UtcNow)).TotalMinutes))).ToString(@"hh\:mm\:ss"),
                MechanicSummaries = workOrder.LaborRows
                    .GroupBy(item => item.Mechanic)
                    .Select(group =>
                    {
                        var hours = group.Sum(item => item.HoursValue);
                        return new WorkOrderLaborMechanicSummary
                        {
                            Mechanic = group.Key,
                            Hours = $"{hours:0.#} hrs",
                            Percent = Math.Min(100, Math.Max(8, (int)Math.Round(hours / maxHours * 100)))
                        };
                    })
                    .ToList()
            };
        }

        private async Task<List<BayListItem>> LoadBayRowsAsync()
        {
            var bays = await _dbContext.Bays.AsNoTracking().OrderBy(bay => bay.Name).ToListAsync();
            var workOrders = await _dbContext.WorkOrders.AsNoTracking().ToListAsync();
            var appointments = await _dbContext.Appointments.AsNoTracking().ToDictionaryAsync(appointment => appointment.AppointmentId);
            var vehicles = await _dbContext.Vehicles.AsNoTracking().ToDictionaryAsync(vehicle => vehicle.VehicleId);
            var assignments = await _dbContext.WorkOrderAssignments.AsNoTracking().ToListAsync();
            var tools = await _dbContext.Tools.AsNoTracking().ToListAsync();
            var today = DateTime.Today;
            var maintenanceSchedules = await _dbContext.BayMaintenanceSchedules
                .AsNoTracking()
                .Where(schedule => !schedule.IsCompleted &&
                    schedule.StartDate.Date <= today &&
                    schedule.EstimatedReturnDate.Date >= today)
                .ToListAsync();

            return bays.Select(bay =>
            {
                var activeMaintenance = maintenanceSchedules
                    .Where(schedule => schedule.BayId == bay.BayId)
                    .OrderBy(schedule => schedule.StartDate)
                    .FirstOrDefault();
                var activeOrder = workOrders.FirstOrDefault(order =>
                    order.BayId == bay.BayId &&
                    order.WorkOrderStatus is not ("Completed" or "Ready for Pickup" or "Picked Up" or "Cancelled"));
                appointments.TryGetValue(activeOrder?.AppointmentId ?? 0, out var appointment);
                vehicles.TryGetValue(appointment?.VehicleId ?? 0, out var vehicle);
                var tech = assignments
                    .Where(assignment => assignment.WorkOrderId == activeOrder?.WorkOrderId)
                    .Select(assignment => NormalizeText(assignment.MechanicName, "-"))
                    .FirstOrDefault() ?? "-";
                var status = !bay.IsActive || activeMaintenance is not null ? "Maintenance" : activeOrder is null ? "Available" : "In Use";

                return new BayListItem
                {
                    BayId = bay.BayId,
                    No = $"Bay {bay.BayId}",
                    BayNumber = bay.BayNumber ?? $"Bay {bay.BayId}",
                    Name = NormalizeText(bay.Name, $"Bay {bay.BayId}"),
                    Description = bay.Description ?? string.Empty,
                    IsActive = bay.IsActive,
                    Status = status,
                    Tone = status == "In Use" ? "use" : status == "Maintenance" ? "maintenance" : "available",
                    Vehicle = activeOrder is null ? "-" : BuildVehicleName(vehicle),
                    Tech = tech,
                    Tools = tools.Count(tool => string.Equals(tool.Location, bay.Name, StringComparison.OrdinalIgnoreCase)).ToString(),
                    MaintenanceWindow = activeMaintenance is null
                        ? string.Empty
                        : $"{activeMaintenance.StartDate:MMM dd} - {activeMaintenance.EstimatedReturnDate:MMM dd}"
                };
            }).ToList();
        }

        private async Task<List<PartListItem>> LoadPartRowsAsync()
        {
            var parts = await _dbContext.Parts
                .AsNoTracking()
                .Where(part => !part.IsArchived && !part.IsDeleted)
                .OrderBy(part => part.Name)
                .ToListAsync();
            var categories = await _dbContext.PartCategories.AsNoTracking().ToDictionaryAsync(category => category.PartCategoryId);

            return parts.Select(part =>
            {
                categories.TryGetValue(part.PartCategoryId ?? 0, out var category);
                var status = !part.IsActive
                    ? "Discontinued"
                    : part.QuantityOnHand <= 0
                        ? "Out of Stock"
                        : part.QuantityOnHand <= part.ReorderThreshold
                            ? "Low Stock"
                            : "In Stock";

                return new PartListItem
                {
                    PartId = part.PartId,
                    SupplierId = part.SupplierId,
                    PartCategoryId = part.PartCategoryId,
                    Sku = NormalizeText(part.SKU, $"PART-{part.PartId:0000}"),
                    Description = NormalizeText(part.Name, part.Description ?? "Unnamed part"),
                    RawDescription = part.Description ?? "",
                    UnitCost = part.UnitCost,
                    UnitPrice = part.UnitPrice,
                    QuantityOnHand = part.QuantityOnHand,
                    ReorderThreshold = part.ReorderThreshold,
                    Category = NormalizeText(category?.CategoryName, "Uncategorized"),
                    Cost = FormatMoney(part.UnitCost),
                    Selling = FormatMoney(part.UnitPrice),
                    Available = $"{part.QuantityOnHand} units",
                    Threshold = $"{part.ReorderThreshold} units",
                    Status = status,
                    Tone = status == "In Stock" ? "stock" : status == "Low Stock" ? "low" : status == "Out of Stock" ? "out" : "gray",
                    QuantityClass = part.QuantityOnHand <= 0 ? "parts-qty-zero" : part.QuantityOnHand <= part.ReorderThreshold ? "parts-qty-warning" : ""
                };
            }).ToList();
        }

        private async Task<List<ProductListingOption>> LoadPartCategoryOptionsAsync()
        {
            return await _dbContext.PartCategories
                .AsNoTracking()
                .Where(category => category.IsActive)
                .OrderBy(category => category.CategoryName)
                .Select(category => new ProductListingOption
                {
                    Id = category.PartCategoryId,
                    Label = category.CategoryName ?? $"Category {category.PartCategoryId}"
                })
                .ToListAsync();
        }

        private async Task<List<ProductListingOption>> LoadSupplierOptionsAsync()
        {
            return await _dbContext.Suppliers
                .AsNoTracking()
                .Where(supplier => supplier.IsActive)
                .OrderBy(supplier => supplier.SupplierName)
                .Select(supplier => new ProductListingOption
                {
                    Id = supplier.SupplierId,
                    Label = supplier.SupplierName ?? $"Supplier {supplier.SupplierId}"
                })
                .ToListAsync();
        }

        private async Task<List<ProductListingListItem>> LoadProductListingRowsAsync()
        {
            var listings = await _dbContext.ProductListings
                .AsNoTracking()
                .Where(listing => !listing.IsArchived && !listing.IsDeleted)
                .OrderBy(listing => listing.DisplayOrder)
                .ThenBy(listing => listing.ListingTitle)
                .ToListAsync();
            var parts = await _dbContext.Parts.AsNoTracking().ToDictionaryAsync(part => part.PartId);
            var services = await _dbContext.Services.AsNoTracking().ToDictionaryAsync(service => service.ServiceId);

            return listings.Select(listing =>
            {
                parts.TryGetValue(listing.PartId ?? 0, out var part);
                services.TryGetValue(listing.ServiceId ?? 0, out var service);
                var linkedItem = part is not null
                    ? NormalizeText($"{part.SKU} - {part.Name}".Trim(' ', '-'), "Linked part")
                    : service is not null
                        ? NormalizeText(service.ServiceName, "Linked service")
                        : "General listing";

                return new ProductListingListItem
                {
                    ProductListingId = listing.ProductListingId,
                    ListingType = NormalizeText(listing.ListingType, "General"),
                    LinkedItem = linkedItem,
                    ListingTitle = NormalizeText(listing.ListingTitle, "Untitled listing"),
                    ListingDescription = NormalizeText(listing.ListingDescription, "No description"),
                    PartId = listing.PartId,
                    ServiceId = listing.ServiceId,
                    ImageUrl = listing.ImageUrl ?? string.Empty,
                    DisplayPriceValue = listing.DisplayPrice,
                    DisplayPrice = FormatMoney(listing.DisplayPrice),
                    DisplayOrder = listing.DisplayOrder,
                    IsFeatured = listing.IsFeatured,
                    IsPublished = listing.IsPublished,
                    Created = listing.CreatedUtc.ToString("MMM dd, yyyy")
                };
            }).ToList();
        }

        private async Task<List<ProductListingOption>> LoadProductListingPartOptionsAsync()
        {
            return await _dbContext.Parts
                .AsNoTracking()
                .Where(part => part.IsActive)
                .OrderBy(part => part.Name)
                .Select(part => new ProductListingOption
                {
                    Id = part.PartId,
                    Label = ((part.SKU ?? $"PART-{part.PartId:0000}") + " - " + (part.Name ?? "Unnamed part")).Trim()
                })
                .ToListAsync();
        }

        private async Task<string?> ValidateProductListingTargetAsync(string listingType, int? partId, int? serviceId)
        {
            if (string.Equals(listingType, "Part", StringComparison.OrdinalIgnoreCase))
            {
                if (partId is not > 0 || !await _dbContext.Parts.AnyAsync(part =>
                        part.PartId == partId.Value && part.IsActive && !part.IsArchived && !part.IsDeleted))
                {
                    return "Select an active part for this product listing.";
                }

                return null;
            }

            if (string.Equals(listingType, "Service", StringComparison.OrdinalIgnoreCase))
            {
                if (serviceId is not > 0 || !await _dbContext.Services.AnyAsync(service =>
                        service.ServiceId == serviceId.Value && service.IsActive))
                {
                    return "Select an active service for this product listing.";
                }

                return null;
            }

            return string.Equals(listingType, "General", StringComparison.OrdinalIgnoreCase)
                ? null
                : "Select a valid item type.";
        }

        private async Task<PartDetailsViewModel?> LoadPartDetailsAsync(string id)
        {
            var lookup = NormalizeText(id, "");
            var partQuery = _dbContext.Parts.AsNoTracking().Where(part => !part.IsDeleted);
            var part = int.TryParse(lookup, out var partId)
                ? await partQuery.FirstOrDefaultAsync(item => item.PartId == partId)
                : await partQuery.FirstOrDefaultAsync(item => item.SKU != null && item.SKU == lookup);

            if (part is null)
            {
                return null;
            }

            var category = await _dbContext.PartCategories.AsNoTracking().FirstOrDefaultAsync(item => item.PartCategoryId == part.PartCategoryId);
            var transactions = await _dbContext.PartTransactions
                .AsNoTracking()
                .Where(transaction => transaction.PartId == part.PartId)
                .OrderByDescending(transaction => transaction.PerformedUtc)
                .ToListAsync();
            var reserved = Math.Max(0, ReservedUnits(transactions));
            var available = Math.Max(0, part.QuantityOnHand - reserved);
            var markup = part.UnitCost is > 0 && part.UnitPrice is not null
                ? $"{((part.UnitPrice.Value - part.UnitCost.Value) / part.UnitCost.Value * 100):N1}%"
                : "0.0%";
            var belowThreshold = part.ReorderThreshold > 0 && available <= part.ReorderThreshold;
            var balance = part.QuantityOnHand;
            var rows = new List<PartTransactionDetailRow>();
            foreach (var transaction in transactions)
            {
                var quantity = PhysicalDelta(transaction.TransactionType, transaction.Quantity ?? 0);
                var type = NormalizeText(transaction.TransactionType, "Adjustment");
                rows.Add(new PartTransactionDetailRow
                {
                    Date = transaction.PerformedUtc.ToString("MMM dd, yyyy"),
                    Type = type,
                    Tone = type.ToLowerInvariant() switch
                    {
                        "received" => "received",
                        "installed" => "installed",
                        "reserved" => "reserved",
                        _ => "reserved"
                    },
                    WorkOrder = transaction.WorkOrderId is null ? "-" : $"WO-{transaction.WorkOrderId:0000}",
                    Quantity = quantity >= 0 ? $"+{quantity:N0}" : quantity.ToString("N0"),
                    QuantityClass = quantity >= 0 ? "parts-positive" : "parts-negative",
                    Balance = $"{balance:N0} units",
                    By = NormalizeText(transaction.PerformedByName, "System")
                });
                balance -= quantity;
            }

            return new PartDetailsViewModel
            {
                PartId = part.PartId,
                Sku = NormalizeText(part.SKU, $"PART-{part.PartId:0000}"),
                Name = NormalizeText(part.Name, "Unnamed part"),
                Category = NormalizeText(category?.CategoryName, "Uncategorized"),
                Description = NormalizeText(part.Description, "No description recorded"),
                CostPrice = FormatMoney(part.UnitCost),
                SellingPrice = FormatMoney(part.UnitPrice),
                Markup = markup,
                Available = $"{available:N0} units",
                Reserved = $"{reserved:N0} units",
                TotalOnHand = $"{part.QuantityOnHand:N0} units",
                ReorderThreshold = $"{part.ReorderThreshold:N0} units",
                Status = belowThreshold ? "Low Stock" : part.IsActive ? "In Stock" : "Inactive",
                StatusTone = belowThreshold ? "low" : part.IsActive ? "stock" : "reserved",
                StatusAssessment = belowThreshold
                    ? $"LOW ({Math.Max(0, part.ReorderThreshold - available):N0} below threshold)"
                    : $"SAFE ({Math.Max(0, available - part.ReorderThreshold):N0} above threshold)",
                StatusClass = belowThreshold ? "parts-negative" : "parts-positive",
                Transactions = rows
            };
        }

        private async Task<List<ProductListingOption>> LoadProductListingServiceOptionsAsync()
        {
            return await _dbContext.Services
                .AsNoTracking()
                .Where(service => service.IsActive)
                .OrderBy(service => service.ServiceName)
                .Select(service => new ProductListingOption
                {
                    Id = service.ServiceId,
                    Label = service.ServiceName ?? $"Service {service.ServiceId}"
                })
                .ToListAsync();
        }

        private async Task<List<ToolListItem>> LoadToolRowsAsync()
        {
            var tools = await _dbContext.Tools
                .AsNoTracking()
                .Where(tool => !tool.IsArchived && !tool.IsDeleted)
                .OrderBy(tool => tool.Name)
                .ToListAsync();
            var categories = await _dbContext.ToolCategories.AsNoTracking().ToDictionaryAsync(category => category.ToolCategoryId);
            var activeCheckouts = await _dbContext.ToolCheckouts
                .AsNoTracking()
                .Where(checkout => checkout.ReturnedUtc == null)
                .ToListAsync();
            var openMaintenance = await _dbContext.ToolMaintenances.AsNoTracking().Where(x => x.PerformedUtc == null).ToListAsync();

            return tools.Select(tool =>
            {
                categories.TryGetValue(tool.ToolCategoryId ?? 0, out var category);
                var checkout = activeCheckouts.FirstOrDefault(item => item.ToolId == tool.ToolId);
                var isMissingOrDamaged = string.Equals(tool.Condition, "Missing", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tool.Condition, "Damaged", StringComparison.OrdinalIgnoreCase);
                var isMaintenance = openMaintenance.Any(x => x.ToolId == tool.ToolId) || string.Equals(tool.Condition, "Under Maintenance", StringComparison.OrdinalIgnoreCase);
                var isCalibrationOverdue = tool.NextCalibrationDueUtc.HasValue && tool.NextCalibrationDueUtc.Value.Date < DateTime.UtcNow.Date;
                var status = isMaintenance ? "Under Maintenance" : isMissingOrDamaged ? "Damaged / Missing" : checkout is not null ? "Checked Out" : isCalibrationOverdue ? "Calibration Overdue" : "Available";

                return new ToolListItem
                {
                    ToolId = tool.ToolId,
                    ActiveCheckoutId = checkout?.ToolCheckoutId,
                    HasOpenMaintenance = isMaintenance,
                    ToolCategoryId = tool.ToolCategoryId,
                    Name = NormalizeText(tool.Name, "Unnamed tool"),
                    Serial = NormalizeText(tool.SerialNumber, $"TOOL-{tool.ToolId:0000}"),
                    RawLocation = tool.Location ?? "",
                    RawCondition = tool.Condition ?? "",
                    LastCalibratedUtc = tool.LastCalibratedUtc,
                    NextCalibrationDueUtc = tool.NextCalibrationDueUtc,
                    Category = NormalizeText(category?.CategoryName, "Uncategorized"),
                    Location = NormalizeText(tool.Location, "-"),
                    Condition = NormalizeText(tool.Condition, "Good"),
                    Calibration = tool.NextCalibrationDueUtc.HasValue
                        ? tool.NextCalibrationDueUtc.Value < DateTime.UtcNow ? $"Overdue {tool.NextCalibrationDueUtc.Value:MMM dd}" : tool.NextCalibrationDueUtc.Value.ToString("MMM dd, yyyy")
                        : "N/A",
                    Status = status,
                    Tone = status == "Available" ? "available" : status == "Checked Out" ? "checked" : status == "Under Maintenance" ? "maintenance" : "missing",
                    Action = status == "Available" ? "Checkout" : status == "Checked Out" ? "Return" : status == "Under Maintenance" ? "Complete Maintenance" : status == "Calibration Overdue" ? "Calibrate" : "View"
                };
            }).ToList();
        }

        private async Task<List<ProductListingOption>> LoadToolCategoryOptionsAsync()
        {
            return await _dbContext.ToolCategories
                .AsNoTracking()
                .Where(category => category.IsActive)
                .OrderBy(category => category.CategoryName)
                .Select(category => new ProductListingOption
                {
                    Id = category.ToolCategoryId,
                    Label = category.CategoryName ?? $"Category {category.ToolCategoryId}"
                })
                .ToListAsync();
        }

        private async Task<ToolProfileViewModel?> LoadToolProfileAsync(string id)
        {
            var lookup = NormalizeText(id, "");
            var toolQuery = _dbContext.Tools.AsNoTracking().Where(tool => !tool.IsDeleted);
            var tool = int.TryParse(lookup, out var toolId)
                ? await toolQuery.FirstOrDefaultAsync(item => item.ToolId == toolId)
                : await toolQuery.FirstOrDefaultAsync(item => item.SerialNumber != null && item.SerialNumber == lookup);

            if (tool is null)
            {
                return null;
            }

            var category = await _dbContext.ToolCategories.AsNoTracking().FirstOrDefaultAsync(item => item.ToolCategoryId == tool.ToolCategoryId);
            var checkouts = await _dbContext.ToolCheckouts
                .AsNoTracking()
                .Where(checkout => checkout.ToolId == tool.ToolId)
                .OrderByDescending(checkout => checkout.CheckedOutUtc)
                .ToListAsync();
            var activeCheckout = checkouts.FirstOrDefault(checkout => checkout.ReturnedUtc is null);
            var hasOpenMaintenance = await _dbContext.ToolMaintenances.AsNoTracking().AnyAsync(x => x.ToolId == tool.ToolId && x.PerformedUtc == null);
            var calibrationOverdue = tool.NextCalibrationDueUtc.HasValue && tool.NextCalibrationDueUtc.Value.Date < DateTime.UtcNow.Date;
            var logs = checkouts.Select(checkout => new ToolCheckoutLogRow
            {
                Date = checkout.CheckedOutUtc.ToString("MMM dd, yyyy"),
                Mechanic = NormalizeText(checkout.MechanicName, "Unassigned"),
                WorkOrder = checkout.WorkOrderId is null ? "-" : $"WO-{checkout.WorkOrderId:0000}",
                Action = checkout.ReturnedUtc is null ? "Checkout" : checkout.ToolIssueStatus is "Damaged" or "Missing" ? checkout.ToolIssueStatus : "Returned",
                ConditionOut = NormalizeText(checkout.ConditionAtCheckout, "-"),
                ConditionReturn = NormalizeText(checkout.ConditionAtReturn, "--"),
                Notes = NormalizeText(checkout.Notes, "-")
            }).ToList();

            return new ToolProfileViewModel
            {
                ToolId = tool.ToolId,
                Name = NormalizeText(tool.Name, "Unnamed tool"),
                SerialNumber = NormalizeText(tool.SerialNumber, $"TOOL-{tool.ToolId:0000}"),
                Category = NormalizeText(category?.CategoryName, "Uncategorized"),
                Location = NormalizeText(tool.Location, "-"),
                Condition = NormalizeText(tool.Condition, "Good"),
                Status = activeCheckout is not null ? "Checked Out" : hasOpenMaintenance || tool.Condition == "Under Maintenance" ? "Under Maintenance" : tool.Condition is "Damaged" or "Missing" ? tool.Condition : calibrationOverdue ? "Calibration Overdue" : "Available",
                StatusTone = activeCheckout is not null ? "checked" : hasOpenMaintenance || tool.Condition == "Under Maintenance" || calibrationOverdue ? "maintenance" : tool.Condition is "Damaged" or "Missing" ? "missing" : "available",
                LastCalibration = tool.LastCalibratedUtc?.ToString("MMM dd, yyyy") ?? "Not recorded",
                NextCalibration = tool.NextCalibrationDueUtc?.ToString("MMM dd, yyyy") ?? "Not scheduled",
                ActiveCheckout = activeCheckout is null ? null : new ToolCheckoutDetail
                {
                    Mechanic = NormalizeText(activeCheckout.MechanicName, "Unassigned"),
                    WorkOrder = activeCheckout.WorkOrderId is null ? "-" : $"WO-{activeCheckout.WorkOrderId:0000}",
                    CheckoutTime = $"{activeCheckout.CheckedOutUtc:MMM dd, yyyy hh:mm tt}"
                },
                Logs = logs
            };
        }

        private async Task<List<InvoiceListItem>> LoadInvoiceRowsAsync()
        {
            var invoices = await _dbContext.Invoices.AsNoTracking().OrderByDescending(invoice => invoice.IssuedUtc).ToListAsync();
            var payments = await _dbContext.Payments.AsNoTracking().ToListAsync();
            var workOrders = await _dbContext.WorkOrders.AsNoTracking().ToDictionaryAsync(order => order.WorkOrderId);
            var appointments = await _dbContext.Appointments.AsNoTracking().ToDictionaryAsync(appointment => appointment.AppointmentId);
            var vehicles = await _dbContext.Vehicles.AsNoTracking().ToDictionaryAsync(vehicle => vehicle.VehicleId);

            return invoices.Select(invoice =>
            {
                workOrders.TryGetValue(invoice.WorkOrderId ?? 0, out var order);
                appointments.TryGetValue(order?.AppointmentId ?? 0, out var appointment);
                vehicles.TryGetValue(appointment?.VehicleId ?? 0, out var vehicle);
                var paid = payments.Where(payment => payment.InvoiceId == invoice.InvoiceId).Sum(payment => payment.Amount ?? 0);
                var total = invoice.Total ?? 0;
                var balance = invoice.InvoiceStatus == "Voided" ? 0 : Math.Max(0, total - paid);
                var status = NormalizeStatus(invoice.InvoiceStatus, balance == 0 && total > 0 ? "Paid" : "Draft");

                return new InvoiceListItem
                {
                    InvoiceId = invoice.InvoiceId,
                    Id = NormalizeText(invoice.InvoiceNumber, $"INV-{invoice.InvoiceId:0000}"),
                    Customer = NormalizeText($"{invoice.CustomerFirstName} {invoice.CustomerLastName}".Trim(), appointment?.CustomerName ?? "Unknown customer"),
                    Vehicle = BuildVehicleName(vehicle),
                    WorkOrder = order is null ? "" : $"WO-{order.WorkOrderId:0000}",
                    Amount = FormatMoney(total),
                    Paid = FormatMoney(paid),
                    Balance = FormatMoney(balance),
                    Status = status,
                    Tone = GetInvoiceTone(status),
                    Date = invoice.IssuedUtc.ToString("MMM dd, yyyy")
                };
            }).ToList();
        }

        private async Task<List<EstimateListItem>> LoadEstimateRowsAsync()
        {
            var estimates = await _dbContext.Estimates.AsNoTracking().OrderByDescending(estimate => estimate.CreatedUtc).ToListAsync();
            var revisions = await _dbContext.EstimateRevisions.AsNoTracking().ToListAsync();
            var appointments = await _dbContext.Appointments.AsNoTracking().ToDictionaryAsync(appointment => appointment.AppointmentId);
            var vehicles = await _dbContext.Vehicles.AsNoTracking().ToDictionaryAsync(vehicle => vehicle.VehicleId);

            return estimates.Select(estimate =>
            {
                appointments.TryGetValue(estimate.AppointmentId ?? 0, out var appointment);
                vehicles.TryGetValue(appointment?.VehicleId ?? 0, out var vehicle);
                var latestRevision = revisions
                    .Where(revision => revision.EstimateId == estimate.EstimateId)
                    .OrderByDescending(revision => revision.RevisionNumber ?? 0)
                    .ThenByDescending(revision => revision.CreatedUtc)
                    .FirstOrDefault();
                var status = NormalizeStatus(latestRevision?.EstimateStatus, "Draft");

                return new EstimateListItem
                {
                    EstimateId = estimate.EstimateId,
                    Id = $"EST-{estimate.EstimateId:0000}",
                    Date = estimate.CreatedUtc.ToString("MMM dd, yyyy"),
                    Customer = NormalizeText(appointment?.CustomerName, "Unknown customer"),
                    Vehicle = BuildVehicleName(vehicle),
                    Total = FormatMoney(latestRevision?.Total),
                    Status = status,
                    Tone = GetEstimateTone(status)
                };
            }).ToList();
        }

        private static string BuildVehicleName(Vehicle? vehicle)
        {
            if (vehicle is null)
            {
                return "No vehicle";
            }

            return NormalizeText($"{vehicle.Year} {vehicle.Make} {vehicle.Model}".Trim(), "No vehicle");
        }

        private static string NormalizeText(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string NormalizeStatus(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string FormatMoney(decimal? value)
        {
            return $"${(value ?? 0):N2}";
        }

        private static string GetAppointmentTone(string status)
        {
            return status.ToLowerInvariant() switch
            {
                "arrived" => "green",
                "in bay" => "orange",
                "inspection" => "orange",
                "waiting approval" => "amber",
                "completed" => "green",
                "cancelled" => "red",
                _ => "blue"
            };
        }

        private static string GetQueueDot(string status)
        {
            return status.ToLowerInvariant() switch
            {
                "inspection" => "blue",
                "waiting approval" => "amber",
                "in bay" => "purple",
                _ => "gray"
            };
        }

        private static string GetWorkOrderTone(string status)
        {
            return status.ToLowerInvariant() switch
            {
                "in progress" => "progress",
                "pending qc" => "pending-qc",
                "completed" or "ready for pickup" or "picked up" => "completed",
                "qc failed" => "failed",
                _ => "pending"
            };
        }

        private static string GetInvoiceTone(string status)
        {
            return status.ToLowerInvariant() switch
            {
                "paid" => "paid",
                "partially paid" => "partial",
                "issued" => "issued",
                "overdue" => "overdue",
                "adjusted" => "adjusted",
                "voided" => "voided",
                _ => "draft"
            };
        }

        private static string GetEstimateTone(string status)
        {
            return status.ToLowerInvariant() switch
            {
                "pending" => "pending",
                "approved" => "approved",
                "partially approved" => "partial",
                "declined" => "declined",
                "expired" => "expired",
                _ => "draft"
            };
        }

        public IActionResult CustomerDetails(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return RedirectToAction(nameof(CustomerVehicle));
        }

        public IActionResult CustomerVehicles(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return RedirectToAction(nameof(CustomerVehicle));
        }

        public IActionResult CustomerServiceHistory(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return RedirectToAction(nameof(CustomerVehicle));
        }

        public async Task<IActionResult> ServiceQueue()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            await SetOperationalOptionViewDataAsync();
            var rows = await LoadAppointmentRowsAsync();
            var order = new[] { "Scheduled", "Arrived", "Inspection", "Waiting Approval", "In Bay" };
            var columns = order
                .Select(status => new ServiceQueueColumnItem
                {
                    Title = status,
                    Dot = GetQueueDot(status),
                    Cards = rows.Where(row => string.Equals(row.Status, status, StringComparison.OrdinalIgnoreCase)).ToList()
                })
                .ToList();

            var otherRows = rows
                .Where(row => !order.Contains(row.Status, StringComparer.OrdinalIgnoreCase) && row.Status != "Cancelled" && row.Status != "No Show" && row.Status != "Completed")
                .ToList();

            if (otherRows.Count > 0)
            {
                columns.Add(new ServiceQueueColumnItem { Title = "Other", Dot = "gray", Cards = otherRows });
            }

            return View(columns);
        }

        public async Task<IActionResult> ServiceQueueDetails(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var appointmentId = ParseEntityId(id, "APT");
            if (appointmentId <= 0)
            {
                TempData["OwnerMessage"] = "Queue item was not found.";
                return RedirectToAction(nameof(ServiceQueue));
            }

            var details = await LoadServiceQueueDetailsAsync(appointmentId);
            if (details is null)
            {
                TempData["OwnerMessage"] = "Queue item was not found.";
                return RedirectToAction(nameof(ServiceQueue));
            }

            ViewData["CurrentAppointmentId"] = details.AppointmentId;
            ViewData["CurrentTechnician"] = details.Technician;
            ViewData["CurrentBay"] = details.Bay;
            await SetOperationalOptionViewDataAsync();
            return View(details);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignQueueBay(QueueBayAssignmentFormModel form)
        {
            return await SaveWorkflow(async () => {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var redirectId = $"APT-{form.AppointmentId:0000}";
            if (!ModelState.IsValid || form.AppointmentId <= 0 || form.BayId <= 0)
            {
                TempData["OwnerMessage"] = "Bay assignment was not saved. Please choose a service bay.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(ServiceQueueDetails), new { id = redirectId });
            }

            var appointment = await _dbContext.Appointments.FirstOrDefaultAsync(item => item.AppointmentId == form.AppointmentId);
            if (appointment is null)
            {
                TempData["OwnerMessage"] = "Queue item was not found.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(ServiceQueue));
            }

            var bay = await _dbContext.Bays.FirstOrDefaultAsync(item => item.BayId == form.BayId && item.IsActive);
            if (bay is null)
            {
                TempData["OwnerMessage"] = "Selected bay is not available.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(ServiceQueueDetails), new { id = redirectId });
            }

            var workOrder = await _dbContext.WorkOrders.FirstOrDefaultAsync(item => item.AppointmentId == appointment.AppointmentId);
            Require(appointment.AppointmentStatus is not ("Cancelled" or "Completed" or "No Show"), "This queue item is closed.");
            if (workOrder is not null) await EditableOrder(workOrder.WorkOrderId);
            var scheduleStart = workOrder?.ScheduledStart ?? appointment.ScheduledStart;
            var scheduleEnd = workOrder?.ScheduledEnd ?? appointment.ScheduledEnd;
            Require(scheduleStart.HasValue && scheduleEnd.HasValue, "Set a schedule before assigning a bay.");
            var scheduleError = await ScheduleError(scheduleStart!.Value, scheduleEnd!.Value, bay.BayId, Array.Empty<EmployeeProfile>(), appointment.AppointmentId, workOrder?.WorkOrderId);
            Require(scheduleError is null, scheduleError ?? "Schedule unavailable.");
            appointment.BayId = bay.BayId;
            if (workOrder is not null)
            {
                workOrder.BayId = bay.BayId;
            }

            var bayName = string.IsNullOrWhiteSpace(bay.Name)
                ? $"Bay {bay.BayNumber ?? bay.BayId.ToString()}"
                : bay.Name;

            _dbContext.QueueStatusHistories.Add(new QueueStatusHistory
            {
                AppointmentId = appointment.AppointmentId,
                QueueStatus = NormalizeStatus(appointment.AppointmentStatus, "Scheduled"),
                ChangedUtc = DateTime.UtcNow,
                ChangedByName = HttpContext.Session.GetString("UserName") ?? "Owner",
                Notes = $"Assigned to {bayName}"
            });

            await _dbContext.SaveChangesAsync();

            TempData["OwnerMessage"] = $"{bayName} assigned.";
            TempData["OwnerMessageTone"] = "success";
            return RedirectToAction(nameof(ServiceQueueDetails), new { id = redirectId });
            }, nameof(ServiceQueue));
        }

        public Task<IActionResult> Appointments(string view = "list") => IntakeAppointments(view);

        private static bool IsAllowedAppointmentStatus(string? status)
        {
            return new[] { "Scheduled", "Arrived", "Inspection", "Waiting Approval", "In Bay", "Completed", "No Show" }
                .Contains(status ?? "", StringComparer.OrdinalIgnoreCase);
        }

        private async Task<(Customer? Customer, Vehicle? Vehicle, Bay? Bay, EmployeeProfile? Employee, string? Error)>
            LoadAppointmentReferencesAsync(AppointmentFormModel form)
        {
            var customer = await _dbContext.Customers.FirstOrDefaultAsync(item =>
                item.CustomerId == form.CustomerId && item.IsActive && !item.IsArchived && !item.IsDeleted);
            if (customer is null)
            {
                return (null, null, null, null, "Please select an active customer.");
            }

            var vehicle = await _dbContext.Vehicles.FirstOrDefaultAsync(item =>
                item.VehicleId == form.VehicleId && !item.IsArchived && !item.IsDeleted);
            if (vehicle is null)
            {
                return (customer, null, null, null, "Please select an active vehicle.");
            }

            var ownsVehicle = await _dbContext.VehicleOwnerships.AnyAsync(ownership =>
                ownership.CustomerId == customer.CustomerId &&
                ownership.VehicleId == vehicle.VehicleId &&
                !ownership.OwnershipEndUtc.HasValue && (!ownership.OwnershipStartUtc.HasValue || ownership.OwnershipStartUtc <= DateTime.UtcNow));
            if (!ownsVehicle)
            {
                return (customer, vehicle, null, null, "The selected vehicle is not assigned to this customer.");
            }

            Bay? bay = null;
            if (form.BayId.HasValue)
            {
                bay = await _dbContext.Bays.FirstOrDefaultAsync(item => item.BayId == form.BayId && item.IsActive);
                if (bay is null)
                {
                    return (customer, vehicle, null, null, "Please select an active service bay.");
                }
            }

            EmployeeProfile? employee = null;
            if (form.AssignedEmployeeProfileId.HasValue)
            {
                employee = await _dbContext.EmployeeProfiles.FirstOrDefaultAsync(item =>
                    item.EmployeeProfileId == form.AssignedEmployeeProfileId &&
                    item.IsActive && !item.IsArchived && !item.IsDeleted);
                if (employee is null)
                {
                    return (customer, vehicle, bay, null, "Please select an active technician or advisor.");
                }
            }

            return (customer, vehicle, bay, employee, null);
        }

        private async Task<string?> ValidateAppointmentScheduleAsync(
            AppointmentFormModel form,
            DateTime scheduledStart,
            DateTime scheduledEnd,
            EmployeeProfile? employee)
        {
            if (scheduledEnd <= scheduledStart)
            {
                return "End time must be later than start time.";
            }

            if (scheduledStart < GarageNow().AddMinutes(-5) && form.AppointmentId == 0 && !form.IsWalkIn)
            {
                return "New appointments cannot be scheduled in the past.";
            }

            var overlapping = _dbContext.Appointments
                .AsNoTracking()
                .Where(item =>
                    item.AppointmentId != form.AppointmentId &&
                    item.AppointmentStatus != "Cancelled" &&
                    item.AppointmentStatus != "No Show" &&
                    item.AppointmentStatus != "Completed" &&
                    item.ScheduledStart < scheduledEnd &&
                    item.ScheduledEnd > scheduledStart);

            if (await overlapping.AnyAsync(item => item.VehicleId == form.VehicleId))
                return "This vehicle already has an appointment during this time.";

            if (await _dbContext.WorkOrders.AnyAsync(w => w.WorkOrderStatus != "Cancelled" && w.WorkOrderStatus != "Completed" && w.WorkOrderStatus != "Picked Up" &&
                w.WorkOrderStatus != "Ready for Pickup" && w.ScheduledStart < scheduledEnd && w.ScheduledEnd > scheduledStart &&
                _dbContext.Appointments.Any(a => a.AppointmentId == w.AppointmentId && a.VehicleId == form.VehicleId)))
                return "This vehicle already has a work order during this time.";

            if (form.BayId.HasValue && await overlapping.AnyAsync(item => item.BayId == form.BayId))
            {
                return "The selected service bay already has an appointment during this time.";
            }

            if (employee is not null)
            {
                var employeeName = NormalizeText($"{employee.FirstName} {employee.LastName}".Trim(), employee.Email ?? "");
                if (await overlapping.AnyAsync(item =>
                        (!string.IsNullOrWhiteSpace(employee.UserId) && item.AssignedMechanicUserId == employee.UserId) ||
                        item.AssignedMechanicName == employeeName))
                {
                    return "The selected technician or advisor is already assigned during this time.";
                }
            }

            return await ScheduleError(scheduledStart, scheduledEnd, form.BayId,
                employee is null ? Array.Empty<EmployeeProfile>() : new[] { employee }, form.AppointmentId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> CreateAppointment(AppointmentFormModel form)
        {
            form.AppointmentId = 0;
            return SaveIntakeAppointment(form, true);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> EditAppointment(AppointmentFormModel form) => SaveIntakeAppointment(form, false);

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> CancelAppointment(CancelAppointmentFormModel form) => CancelIntakeAppointment(form);

        public async Task<IActionResult> EstimateApprovals()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            ViewData["EstimateEditor"] = await EstimateEditor();
            return View(await LoadEstimateRowsAsync());
        }

        public async Task<IActionResult> EstimateDetails(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var estimate = await _dbContext.Estimates.AsNoTracking().FirstOrDefaultAsync(x => x.EstimateId == ParseEntityId(id, "EST"));
            if (estimate is null) return NotFound();
            var revision = await LatestRevision(estimate.EstimateId);
            if (revision is null) return NotFound();
            var appointment = await _dbContext.Appointments.FindAsync(estimate.AppointmentId);
            var vehicle = appointment?.VehicleId is int vehicleId ? await _dbContext.Vehicles.FindAsync(vehicleId) : null;
            var lines = await _dbContext.EstimateRevisionLineItems.AsNoTracking().Where(x => x.EstimateRevisionId == revision.EstimateRevisionId).ToListAsync();
            var editor = await EstimateEditor(new EstimateSaveForm { EstimateId = estimate.EstimateId, ExpectedRevisionId = revision.EstimateRevisionId,
                AppointmentId = estimate.AppointmentId ?? 0, Reason = "", Lines = lines.Select(x => new EstimateLineForm { LineType = x.LineType ?? "Labour",
                    PartId = x.PartId, ServiceId = x.ServiceId, Description = x.Description ?? "", Quantity = x.Quantity ?? 1, UnitPrice = x.UnitPrice ?? 0 }).ToList() });
            editor.Form.TaxRateId = editor.Taxes.FirstOrDefault(x => Math.Round((revision.Subtotal ?? 0) * (x.Rate ?? 0), 2) == revision.TaxAmount)?.TaxRateId;
            return View(new EstimateWorkflowViewModel { Estimate = estimate, Revision = revision, Lines = lines, Editor = editor,
                Customer = appointment?.CustomerName ?? "Customer unavailable", Vehicle = BuildVehicleName(vehicle),
                ApprovalUrl = revision.ApprovalToken.HasValue && revision.ApprovalTokenExpiresUtc > DateTime.UtcNow
                    ? Url.Action("Review", "CustomerEstimateApproval", new { token = revision.ApprovalToken }) : null,
                Revisions = await _dbContext.EstimateRevisions.AsNoTracking().Where(x => x.EstimateId == estimate.EstimateId).OrderByDescending(x => x.RevisionNumber).ToListAsync(),
                WorkOrderId = await _dbContext.WorkOrders.Where(x => x.EstimateId == estimate.EstimateId && x.WorkOrderStatus != "Cancelled").Select(x => (int?)x.WorkOrderId).FirstOrDefaultAsync() });
        }

        public IActionResult EditEstimate(string id = "est-2045")
        {
            if (!IsOwnerSession())
            {
                return RedirectToAction("Login", "Account");
            }

            return RedirectToAction(nameof(EstimateDetails), new { id });
        }

        public async Task<IActionResult> WorkOrders()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            await SetOperationalOptionViewDataAsync();
            return View(await LoadWorkOrderRowsAsync());
        }

        public IActionResult CreateWorkOrder()
        {
            if (!IsOwnerSession())
            {
                return RedirectToAction("Login", "Account");
            }

            return RedirectToAction(nameof(WorkOrders));
        }

        public async Task<IActionResult> BayDispatchMatrix(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var model = await LoadWorkOrderBayDispatchAsync(ParseEntityId(id, "WO"));
            if (model is null)
            {
                TempData["OwnerMessage"] = "Work order was not found.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(WorkOrders));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DispatchWorkOrderBay(WorkOrderBayDispatchFormModel form)
        {
            return await SaveWorkflow(async () =>
            {
            var checkedOrder = await EditableOrder(form.WorkOrderId);
            Require(await _dbContext.Bays.AnyAsync(x => x.BayId == form.BayId && x.IsActive), "Select an active bay.");
            Require(checkedOrder.ScheduledStart.HasValue && checkedOrder.ScheduledEnd.HasValue, "Set a work order schedule first.");
            var scheduleError = await ScheduleError(checkedOrder.ScheduledStart!.Value, checkedOrder.ScheduledEnd!.Value, form.BayId, Array.Empty<EmployeeProfile>(), checkedOrder.AppointmentId, checkedOrder.WorkOrderId);
            Require(scheduleError is null, scheduleError ?? "Schedule unavailable.");
            Require(!await _dbContext.BayMaintenanceSchedules.AnyAsync(x => x.BayId == form.BayId && !x.IsCompleted &&
                x.StartDate < (checkedOrder.ScheduledEnd ?? DateTime.UtcNow.AddDays(1)) && x.EstimatedReturnDate >= (checkedOrder.ScheduledStart ?? DateTime.UtcNow)), "The bay is under maintenance.");
            Require(!await _dbContext.WorkOrders.AnyAsync(x => x.WorkOrderId != form.WorkOrderId && x.BayId == form.BayId &&
                x.WorkOrderStatus != "Cancelled" && x.WorkOrderStatus != "Completed" && x.WorkOrderStatus != "Ready for Pickup" && x.WorkOrderStatus != "Picked Up" &&
                x.ScheduledStart < checkedOrder.ScheduledEnd && x.ScheduledEnd > checkedOrder.ScheduledStart), "This bay is already booked.");
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var order = await _dbContext.WorkOrders.FirstOrDefaultAsync(item => item.WorkOrderId == form.WorkOrderId);
            var bay = await _dbContext.Bays.FirstOrDefaultAsync(item => item.BayId == form.BayId);
            if (order is null || bay is null)
            {
                TempData["OwnerMessage"] = "Bay dispatch could not be saved.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(WorkOrders));
            }

            order.BayId = bay.BayId;
            order.WorkOrderStatus = order.WorkOrderStatus is "Completed" or "Ready for Pickup" or "Picked Up"
                ? order.WorkOrderStatus
                : "In Progress";
            order.ActualStart ??= DateTime.UtcNow;

            if (order.AppointmentId.HasValue)
            {
                var appointment = await _dbContext.Appointments.FirstOrDefaultAsync(item => item.AppointmentId == order.AppointmentId.Value);
                if (appointment is not null)
                {
                    appointment.BayId = bay.BayId;
                    appointment.AppointmentStatus = "In Bay";
                }
            }

            _dbContext.AuditLogEntries.Add(new AuditLogEntry
            {
                UserId = HttpContext.Session.GetString("UserId"),
                UserName = HttpContext.Session.GetString("UserName") ?? "Owner",
                Action = "Dispatch Bay",
                EntityName = "WorkOrder",
                EntityId = order.WorkOrderId.ToString(),
                TimestampUtc = DateTime.UtcNow,
                Details = $"Assigned {NormalizeText(bay.Name, $"Bay {bay.BayId}")} to WO-{order.WorkOrderId:0000}"
            });
            await _dbContext.SaveChangesAsync();

            TempData["OwnerMessage"] = $"WO-{order.WorkOrderId:0000} dispatched to {NormalizeText(bay.Name, $"Bay {bay.BayId}")}.";
            TempData["OwnerMessageTone"] = "success";
            return RedirectToAction(nameof(StaffDispatchMatrix), new { id = $"WO-{order.WorkOrderId:0000}" });

            }, nameof(WorkOrders));
        }

        public async Task<IActionResult> StaffDispatchMatrix(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var model = await LoadWorkOrderStaffDispatchAsync(ParseEntityId(id, "WO"));
            if (model is null)
            {
                TempData["OwnerMessage"] = "Work order was not found.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(WorkOrders));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DispatchWorkOrderStaff(WorkOrderStaffDispatchFormModel form)
        {
            return await SaveWorkflow(async () =>
            {
            var checkedOrder = await EditableOrder(form.WorkOrderId);
            var selectedEmployees = new[] { form.LeadEmployeeProfileId, form.HelperEmployeeProfileId ?? 0 }.Where(x => x > 0).Distinct().ToArray();
            Require(await _dbContext.EmployeeProfiles.CountAsync(x => selectedEmployees.Contains(x.EmployeeProfileId) && x.IsActive && !x.IsArchived && !x.IsDeleted) == selectedEmployees.Length, "Select active mechanics.");
            var selectedProfiles = await _dbContext.EmployeeProfiles.Where(x => selectedEmployees.Contains(x.EmployeeProfileId)).ToListAsync();
            Require(checkedOrder.ScheduledStart.HasValue && checkedOrder.ScheduledEnd.HasValue, "Set a work order schedule first.");
            var scheduleError = await ScheduleError(checkedOrder.ScheduledStart!.Value, checkedOrder.ScheduledEnd!.Value, checkedOrder.BayId, selectedProfiles, checkedOrder.AppointmentId, checkedOrder.WorkOrderId);
            Require(scheduleError is null, scheduleError ?? "Schedule unavailable.");
            var names = selectedProfiles.Select(BuildEmployeeName).ToArray();
            var conflictIds = await _dbContext.WorkOrders.Where(x => x.WorkOrderId != form.WorkOrderId &&
                x.WorkOrderStatus != "Cancelled" && x.WorkOrderStatus != "Completed" && x.WorkOrderStatus != "Ready for Pickup" && x.WorkOrderStatus != "Picked Up" &&
                x.ScheduledStart < checkedOrder.ScheduledEnd && x.ScheduledEnd > checkedOrder.ScheduledStart).Select(x => x.WorkOrderId).ToListAsync();
            Require(!await _dbContext.WorkOrderAssignments.AnyAsync(x => conflictIds.Contains(x.WorkOrderId ?? 0) && names.Contains(x.MechanicName!)), "A selected mechanic is already booked.");
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var order = await _dbContext.WorkOrders.FirstOrDefaultAsync(item => item.WorkOrderId == form.WorkOrderId);
            var lead = await _dbContext.EmployeeProfiles.FirstOrDefaultAsync(item => item.EmployeeProfileId == form.LeadEmployeeProfileId);
            var helper = form.HelperEmployeeProfileId.HasValue
                ? await _dbContext.EmployeeProfiles.FirstOrDefaultAsync(item => item.EmployeeProfileId == form.HelperEmployeeProfileId.Value)
                : null;
            if (order is null || lead is null)
            {
                TempData["OwnerMessage"] = "Select a lead mechanic before confirming dispatch.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(StaffDispatchMatrix), new { id = $"WO-{form.WorkOrderId:0000}" });
            }

            var existing = await _dbContext.WorkOrderAssignments
                .Where(item => item.WorkOrderId == order.WorkOrderId)
                .ToListAsync();
            _dbContext.WorkOrderAssignments.RemoveRange(existing);

            var leadName = BuildEmployeeName(lead);
            _dbContext.WorkOrderAssignments.Add(new WorkOrderAssignment
            {
                WorkOrderId = order.WorkOrderId,
                MechanicUserId = lead.UserId,
                MechanicName = leadName,
                AssignmentRole = "Lead",
                AssignedUtc = DateTime.UtcNow
            });

            if (helper is not null && helper.EmployeeProfileId != lead.EmployeeProfileId)
            {
                _dbContext.WorkOrderAssignments.Add(new WorkOrderAssignment
                {
                    WorkOrderId = order.WorkOrderId,
                    MechanicUserId = helper.UserId,
                    MechanicName = BuildEmployeeName(helper),
                    AssignmentRole = "Helper",
                    AssignedUtc = DateTime.UtcNow
                });
            }

            if (order.AppointmentId.HasValue)
            {
                var appointment = await _dbContext.Appointments.FirstOrDefaultAsync(item => item.AppointmentId == order.AppointmentId.Value);
                if (appointment is not null)
                {
                    appointment.AssignedMechanicUserId = lead.UserId;
                    appointment.AssignedMechanicName = leadName;
                }
            }

            order.WorkOrderStatus = order.WorkOrderStatus is "Completed" or "Ready for Pickup" or "Picked Up"
                ? order.WorkOrderStatus
                : "In Progress";
            order.ActualStart ??= DateTime.UtcNow;

            _dbContext.AuditLogEntries.Add(new AuditLogEntry
            {
                UserId = HttpContext.Session.GetString("UserId"),
                UserName = HttpContext.Session.GetString("UserName") ?? "Owner",
                Action = "Dispatch Staff",
                EntityName = "WorkOrder",
                EntityId = order.WorkOrderId.ToString(),
                TimestampUtc = DateTime.UtcNow,
                Details = $"Lead mechanic {leadName} assigned to WO-{order.WorkOrderId:0000}"
            });
            await _dbContext.SaveChangesAsync();

            TempData["OwnerMessage"] = $"Staff dispatch saved for WO-{order.WorkOrderId:0000}.";
            TempData["OwnerMessageTone"] = "success";
            return RedirectToAction(nameof(WorkOrderStatus), new { id = $"WO-{order.WorkOrderId:0000}" });

            }, nameof(WorkOrders));
        }

        public async Task<IActionResult> WorkOrderDetails(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var model = await LoadWorkOrderDetailsAsync(ParseEntityId(id, "WO"));
            if (model is null)
            {
                TempData["OwnerMessage"] = "Work order was not found.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(WorkOrders));
            }

            await SetOperationalOptionViewDataAsync();
            ViewData["WorkOrderEntity"] = await _dbContext.WorkOrders.AsNoTracking().FirstAsync(x => x.WorkOrderId == model.WorkOrderId);
            return View(model);
        }

        public async Task<IActionResult> WorkOrderLaborTracking(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var model = await LoadWorkOrderLaborTrackingAsync(ParseEntityId(id, "WO"));
            if (model is null)
            {
                TempData["OwnerMessage"] = "Work order was not found.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(WorkOrders));
            }

            return View("LabourTimeTracking", await RepairOperations(model.WorkOrder.WorkOrderId));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddWorkOrderLaborEntry(WorkOrderLaborEntryFormModel form)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            if (!ModelState.IsValid)
            {
                TempData["OwnerMessage"] = "Complete the manual labor entry before saving.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(WorkOrderLaborTracking), new { id = $"WO-{form.WorkOrderId:0000}" });
            }

            var employee = (await _dbContext.EmployeeProfiles.Where(x => x.IsActive && !x.IsArchived && !x.IsDeleted).ToListAsync())
                .FirstOrDefault(x => BuildEmployeeName(x).Equals(form.MechanicName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (employee is null) return BadRequest("Select an active mechanic.");
            return await SaveLabour(new LabourSaveForm { WorkOrderId = form.WorkOrderId, EmployeeProfileId = employee.EmployeeProfileId,
                ApprovedEstimateLineId = form.ApprovedEstimateLineId, Description = form.TaskDescription, Minutes = (int)Math.Round(form.Hours * 60), HourlyRate = form.HourlyRate }, false);
        }


        public async Task<IActionResult> WorkOrderStatus(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var model = await LoadWorkOrderDetailsAsync(ParseEntityId(id, "WO"));
            if (model is null)
            {
                TempData["OwnerMessage"] = "Work order was not found.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(WorkOrders));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransitionWorkOrderStatus(WorkOrderStatusTransitionFormModel form)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return await SaveWorkflow(async () =>
            {
                var order = await EditableOrder(form.WorkOrderId);
                var transition = form.Transition?.Trim().ToLowerInvariant();
                Require(transition is "pause" or "qc" or "resume" or "start", "Use Quality Control to complete and sign off a work order.");
                var ids = await _dbContext.ServiceEntries.Where(x => x.WorkOrderId == order.WorkOrderId).Select(x => x.ServiceEntryId).ToListAsync();
                Require(!await _dbContext.ServiceTimeSegments.AnyAsync(x => ids.Contains(x.ServiceEntryId ?? 0) && x.SegmentEnd == null), "Complete all active and paused timers before changing the job status.");
                if (transition == "qc") Require(ids.Count > 0, "Record the repair labour before sending it to QC.");
                order.WorkOrderStatus = transition == "pause" ? "Paused" : transition == "qc" ? "Pending QC" : "In Progress";
                order.ActualStart ??= DateTime.UtcNow;
                WorkflowAudit("WorkOrder", order.WorkOrderId, "Change status", new { order.WorkOrderStatus, form.Notes });
                return RedirectToAction(nameof(WorkOrderDetails), new { id = order.WorkOrderId });
            }, nameof(WorkOrderDetails), new { id = form.WorkOrderId });
        }


        public async Task<IActionResult> BayManagement()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return View(await LoadBayRowsAsync());
        }

        public IActionResult BayDetails(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return RedirectToAction(nameof(BayManagement));
        }

        public IActionResult AddBay()
        {
            if (!IsOwnerSession())
            {
                return RedirectToAction("Login", "Account");
            }

            return RedirectToAction(nameof(BayManagement));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBay(BayEditFormModel form)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(form.Name) || string.IsNullOrWhiteSpace(form.Description))
            {
                TempData["OwnerMessage"] = FormValidationMessage("Bay details were not saved. Complete the required fields.");
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(BayManagement));
            }

            var bay = new Bay
            {
                BayNumber = form.BayNumber.Trim(),
                Name = form.Name.Trim(),
                Description = form.Description.Trim(),
                IsActive = !string.Equals(form.Status, "Maintenance", StringComparison.OrdinalIgnoreCase)
            };

            _dbContext.Bays.Add(bay);
            await _dbContext.SaveChangesAsync();

            TempData["OwnerMessage"] = "Bay added.";
            return RedirectToAction(nameof(BayManagement));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBay(BayEditFormModel form)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(form.Name) || string.IsNullOrWhiteSpace(form.Description))
            {
                TempData["OwnerMessage"] = FormValidationMessage("Bay details were not saved. Complete the required fields.");
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(BayManagement));
            }

            var bay = await _dbContext.Bays.FindAsync(form.BayId);
            if (bay is null)
            {
                TempData["OwnerMessage"] = "Bay was not found.";
                return RedirectToAction(nameof(BayManagement));
            }

            bay.BayNumber = form.BayNumber.Trim();
            bay.Name = form.Name.Trim();
            bay.Description = form.Description.Trim();
            bay.IsActive = !string.Equals(form.Status, "Maintenance", StringComparison.OrdinalIgnoreCase);

            await _dbContext.SaveChangesAsync();

            TempData["OwnerMessage"] = "Bay updated.";
            return RedirectToAction(nameof(BayManagement));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetBayMaintenance(BayMaintenanceFormModel form)
        {
            return await SaveWorkflow(async () => {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            if (!ModelState.IsValid || form.StartDate is null || form.EstimatedReturnDate is null)
            {
                TempData["OwnerMessage"] = "Maintenance schedule was not saved. Please complete the required fields.";
                return RedirectToAction(nameof(BayManagement));
            }

            var bay = await _dbContext.Bays.FindAsync(form.BayId);
            if (bay is null)
            {
                TempData["OwnerMessage"] = "Bay was not found.";
                return RedirectToAction(nameof(BayManagement));
            }

            var startDate = form.StartDate.Value.Date;
            var estimatedReturnDate = form.EstimatedReturnDate.Value.Date;
            if (estimatedReturnDate < startDate)
            {
                TempData["OwnerMessage"] = "Maintenance return date must be on or after the start date.";
                return RedirectToAction(nameof(BayManagement));
            }

            _dbContext.BayMaintenanceSchedules.Add(new BayMaintenanceSchedule
            {
                BayId = bay.BayId,
                MaintenanceType = form.MaintenanceType.Trim(),
                StartDate = startDate,
                EstimatedReturnDate = estimatedReturnDate,
                AssignedTechnician = form.AssignedTechnician?.Trim(),
                Notes = form.Notes.Trim(),
                IsCompleted = false,
                CreatedUtc = DateTime.UtcNow
            });
            var scheduleError = await ScheduleError(startDate, estimatedReturnDate.AddDays(1), form.BayId, Array.Empty<EmployeeProfile>());
            Require(scheduleError is null, scheduleError ?? "Bay is already booked.");
            await _dbContext.SaveChangesAsync();
            TempData["OwnerMessage"] = startDate <= DateTime.Today
                ? "Bay maintenance started."
                : "Bay maintenance scheduled.";
            return RedirectToAction(nameof(BayManagement));
            }, nameof(BayManagement));
        }

        public IActionResult EditBay(string id = "bay-1")
        {
            if (!IsOwnerSession())
            {
                return RedirectToAction("Login", "Account");
            }

            return RedirectToAction(nameof(BayManagement));
        }

        public async Task<IActionResult> PartsManagement(string tab = "parts")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var model = new PartsManagementViewModel
            {
                ActiveTab = string.Equals(tab, "product-listings", StringComparison.OrdinalIgnoreCase)
                    ? "product-listings"
                    : "parts",
                Parts = await LoadPartRowsAsync(),
                ProductListings = await LoadProductListingRowsAsync(),
                PartOptions = await LoadProductListingPartOptionsAsync(),
                ServiceOptions = await LoadProductListingServiceOptionsAsync(),
                PartCategoryOptions = await LoadPartCategoryOptionsAsync(),
                SupplierOptions = await LoadSupplierOptionsAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddProductListing(ProductListingFormModel form)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var listingType = NormalizeText(form.ListingType, "");
            var targetError = await ValidateProductListingTargetAsync(listingType, form.PartId, form.ServiceId);
            if (!ModelState.IsValid || targetError is not null)
            {
                TempData["OwnerMessage"] = targetError ?? "Product listing was not saved. Please complete the required fields.";
                return RedirectToAction(nameof(PartsManagement), new { tab = "product-listings" });
            }

            var isPartListing = string.Equals(listingType, "Part", StringComparison.OrdinalIgnoreCase);
            var isServiceListing = string.Equals(listingType, "Service", StringComparison.OrdinalIgnoreCase);

            _dbContext.ProductListings.Add(new ProductListing
            {
                ListingType = listingType,
                PartId = isPartListing ? form.PartId : null,
                ServiceId = isServiceListing ? form.ServiceId : null,
                ListingTitle = form.ListingTitle.Trim(),
                ListingDescription = form.ListingDescription?.Trim(),
                ImageUrl = form.ImageUrl?.Trim(),
                DisplayPrice = form.DisplayPrice,
                DisplayOrder = form.DisplayOrder,
                IsFeatured = form.IsFeatured,
                IsPublished = form.IsPublished,
                CreatedUtc = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
            TempData["OwnerMessage"] = "Product listing saved.";
            return RedirectToAction(nameof(PartsManagement), new { tab = "product-listings" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProductListing(ProductListingFormModel form)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var listingType = NormalizeText(form.ListingType, "");
            var targetError = await ValidateProductListingTargetAsync(listingType, form.PartId, form.ServiceId);
            if (!ModelState.IsValid || form.ProductListingId <= 0 || targetError is not null)
            {
                TempData["OwnerMessage"] = targetError ?? "Product listing was not updated. Please complete the required fields.";
                return RedirectToAction(nameof(PartsManagement), new { tab = "product-listings" });
            }

            var listing = await _dbContext.ProductListings
                .FirstOrDefaultAsync(item => item.ProductListingId == form.ProductListingId && !item.IsArchived && !item.IsDeleted);
            if (listing is null)
            {
                TempData["OwnerMessage"] = "Product listing was not found.";
                return RedirectToAction(nameof(PartsManagement), new { tab = "product-listings" });
            }

            var isPartListing = string.Equals(listingType, "Part", StringComparison.OrdinalIgnoreCase);
            var isServiceListing = string.Equals(listingType, "Service", StringComparison.OrdinalIgnoreCase);

            listing.ListingType = listingType;
            listing.PartId = isPartListing ? form.PartId : null;
            listing.ServiceId = isServiceListing ? form.ServiceId : null;
            listing.ListingTitle = form.ListingTitle.Trim();
            listing.ListingDescription = form.ListingDescription?.Trim();
            listing.ImageUrl = form.ImageUrl?.Trim();
            listing.DisplayPrice = form.DisplayPrice;
            listing.DisplayOrder = Math.Max(0, form.DisplayOrder);

            await _dbContext.SaveChangesAsync();
            TempData["OwnerMessage"] = "Product listing updated.";
            return RedirectToAction(nameof(PartsManagement), new { tab = "product-listings" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveProductListing(RecordActionFormModel form)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var listing = await _dbContext.ProductListings
                .FirstOrDefaultAsync(item => item.ProductListingId == form.RecordId && !item.IsArchived && !item.IsDeleted);
            if (listing is null)
            {
                TempData["OwnerMessage"] = "Product listing was not found or is no longer active.";
                return RedirectToAction(nameof(PartsManagement), new { tab = "product-listings" });
            }

            listing.IsArchived = true;
            listing.IsDeleted = false;
            listing.ArchivedUtc = DateTime.UtcNow;
            listing.DeletedUtc = null;
            listing.ArchiveReason = string.IsNullOrWhiteSpace(form.Reason) ? "Archived from Product Listing" : form.Reason.Trim();
            listing.DeleteReason = null;
            await _dbContext.SaveChangesAsync();

            TempData["OwnerMessage"] = "Product listing archived.";
            return RedirectToAction(nameof(PartsManagement), new { tab = "product-listings" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SoftDeleteProductListing(RecordActionFormModel form)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var listing = await _dbContext.ProductListings
                .FirstOrDefaultAsync(item => item.ProductListingId == form.RecordId && !item.IsArchived && !item.IsDeleted);
            if (listing is null)
            {
                TempData["OwnerMessage"] = "Product listing was not found or is no longer active.";
                return RedirectToAction(nameof(PartsManagement), new { tab = "product-listings" });
            }

            listing.IsArchived = false;
            listing.IsDeleted = true;
            listing.ArchivedUtc = null;
            listing.DeletedUtc = DateTime.UtcNow;
            listing.ArchiveReason = null;
            listing.DeleteReason = string.IsNullOrWhiteSpace(form.Reason) ? "Deleted from Product Listing" : form.Reason.Trim();
            await _dbContext.SaveChangesAsync();

            TempData["OwnerMessage"] = "Product listing moved to records disposition.";
            return RedirectToAction(nameof(PartsManagement), new { tab = "product-listings" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPart(PartFormModel form)
        {
            return await SaveWorkflow(async () => {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            if (!ModelState.IsValid)
            {
                TempData["OwnerMessage"] = "Part was not saved. Please complete the required fields.";
                return RedirectToAction(nameof(PartsManagement));
            }

            await ValidatePartReferences(form, null);
            _dbContext.Parts.Add(new Part
            {
                SupplierId = form.SupplierId,
                PartCategoryId = form.PartCategoryId,
                SKU = form.SKU.Trim(),
                Name = form.Name.Trim(),
                Description = form.Description?.Trim(),
                UnitCost = form.UnitCost,
                UnitPrice = form.UnitPrice,
                QuantityOnHand = form.QuantityOnHand,
                ReorderThreshold = form.ReorderThreshold,
                IsActive = true
            });

            await _dbContext.SaveChangesAsync();
            TempData["OwnerMessage"] = "Part saved.";
            return RedirectToAction(nameof(PartsManagement));
            }, nameof(PartsManagement));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPart(PartFormModel form)
        {
            return await SaveWorkflow(async () =>
            {
            var existingPart = await _dbContext.Parts.FindAsync(form.PartId);
            Require(existingPart is not null, "Part not found.");
            Require(!await _dbContext.PartTransactions.AnyAsync(x => x.PartId == form.PartId) || existingPart!.QuantityOnHand == form.QuantityOnHand,
                "Use Stock Transactions to change quantity once this part has movement history.");
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            if (!ModelState.IsValid)
            {
                TempData["OwnerMessage"] = "Part was not updated. Please complete the required fields.";
                return RedirectToAction(nameof(PartsManagement));
            }

            var part = await _dbContext.Parts.FindAsync(form.PartId);
            if (part is null)
            {
                TempData["OwnerMessage"] = "Part was not found.";
                return RedirectToAction(nameof(PartsManagement));
            }

            await ValidatePartReferences(form, part);
            part.SupplierId = form.SupplierId;
            part.PartCategoryId = form.PartCategoryId;
            part.SKU = form.SKU.Trim();
            part.Name = form.Name.Trim();
            part.Description = form.Description?.Trim();
            part.UnitCost = form.UnitCost;
            part.UnitPrice = form.UnitPrice;
            part.QuantityOnHand = form.QuantityOnHand;
            part.ReorderThreshold = form.ReorderThreshold;

            await _dbContext.SaveChangesAsync();
            TempData["OwnerMessage"] = "Part updated.";
            return RedirectToAction(nameof(PartsManagement));

            }, nameof(PartsManagement));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchivePart(PartStateFormModel form)
        {
            return await SaveWorkflow(async () =>
            {
            await EnsurePartCanBeRetired(form.PartId);
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var part = await _dbContext.Parts.FindAsync(form.PartId);
            if (part is not null)
            {
                part.IsArchived = true;
                part.IsDeleted = false;
                part.IsActive = false;
                part.ArchivedUtc = DateTime.UtcNow;
                part.DeletedUtc = null;
                part.ArchiveReason = string.IsNullOrWhiteSpace(form.Reason) ? "Archived from Parts Management" : form.Reason.Trim();
                part.DeleteReason = null;
                await _dbContext.SaveChangesAsync();
            }

            TempData["OwnerMessage"] = "Part archived.";
            return RedirectToAction(nameof(PartsManagement));

            }, nameof(PartsManagement));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SoftDeletePart(PartStateFormModel form)
        {
            return await SaveWorkflow(async () =>
            {
            await EnsurePartCanBeRetired(form.PartId);
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var part = await _dbContext.Parts.FindAsync(form.PartId);
            if (part is not null)
            {
                part.IsArchived = false;
                part.IsDeleted = true;
                part.IsActive = false;
                part.ArchivedUtc = null;
                part.DeletedUtc = DateTime.UtcNow;
                part.DeleteReason = string.IsNullOrWhiteSpace(form.Reason) ? "Deleted from Parts Management" : form.Reason.Trim();
                await _dbContext.SaveChangesAsync();
            }

            TempData["OwnerMessage"] = "Part moved to records disposition.";
            return RedirectToAction(nameof(PartsManagement));

            }, nameof(PartsManagement));
        }

        public async Task<IActionResult> PartDetails(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var model = await LoadPartDetailsAsync(id);
            if (model is null)
            {
                TempData["OwnerMessage"] = "Part was not found.";
                return RedirectToAction(nameof(PartsManagement));
            }

            return View(model);
        }

        public IActionResult PartEntry(string id = "new")
        {
            if (!IsOwnerSession())
            {
                return RedirectToAction("Login", "Account");
            }

            return string.Equals(id, "new", StringComparison.OrdinalIgnoreCase)
                ? RedirectToAction(nameof(PartsManagement))
                : RedirectToAction(nameof(PartDetails), new { id });
        }

        public IActionResult PartAuditLog(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return RedirectToAction(nameof(SparesAllocation), new { partId = ParseEntityId(id, "PART") });
        }

        public async Task<IActionResult> SparesAllocation(string id = "", int? workOrderId = null, int? partId = null)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            if (!string.IsNullOrWhiteSpace(id)) workOrderId = ParseEntityId(id, "WO");
            return View(await RepairOperations(workOrderId, partId));
        }

        public async Task<IActionResult> LabourTimeTracking(int? workOrderId = null)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return View(await RepairOperations(workOrderId));
        }

        public IActionResult ServiceTimer(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return RedirectToAction(nameof(LabourTimeTracking));
        }

        public IActionResult LabourHistory()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return RedirectToAction(nameof(LabourTimeTracking));
        }

        public async Task<IActionResult> QualityControl()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return View(await RepairOperations());
        }

        public async Task<IActionResult> ExecuteInspection(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var orderId = ParseEntityId(id, "WO");
            if (!await _dbContext.WorkOrders.AnyAsync(x => x.WorkOrderId == orderId)) return NotFound();
            return View(await RepairOperations(orderId));
        }

        public async Task<IActionResult> InspectionResult(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var check = await _dbContext.QualityChecks.AsNoTracking().FirstOrDefaultAsync(x => x.QualityCheckId == ParseEntityId(id, "QC"));
            return check is null ? NotFound() : View(check);
        }

        public IActionResult QCFailed(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return RedirectToAction(nameof(QualityControl));
        }

        public IActionResult QCPassed(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return RedirectToAction(nameof(QualityControl));
        }

        public async Task<IActionResult> ToolTracking(int? categoryId = null, string? status = null)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return View(new ToolTrackingViewModel
            {
                Tools = await LoadToolRowsAsync(),
                SelectedCategoryId = categoryId,
                SelectedStatus = status,
                ToolCategoryOptions = await LoadToolCategoryOptionsAsync(),
                Mechanics = await _dbContext.EmployeeProfiles.AsNoTracking().Where(x => x.IsActive && !x.IsArchived && !x.IsDeleted).OrderBy(x => x.FirstName).ToListAsync(),
                OpenWorkOrders = await _dbContext.WorkOrders.AsNoTracking().Where(x => x.WorkOrderStatus != "Cancelled" && x.WorkOrderStatus != "Completed" && x.WorkOrderStatus != "Ready for Pickup" && x.WorkOrderStatus != "Picked Up").OrderByDescending(x => x.CreatedUtc).ToListAsync()
            });
        }

        public IActionResult AddTool()
        {
            if (!IsOwnerSession())
            {
                return RedirectToAction("Login", "Account");
            }

            return RedirectToAction(nameof(ToolTracking));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTool(ToolFormModel form)
        {
            return await SaveWorkflow(async () => {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            if (!ModelState.IsValid)
            {
                TempData["OwnerMessage"] = "Tool was not saved. Please complete the required fields.";
                return RedirectToAction(nameof(ToolTracking));
            }

            await ValidateToolCategory(form.ToolCategoryId, null);
            _dbContext.Tools.Add(new Tool
            {
                ToolCategoryId = form.ToolCategoryId,
                Name = form.Name.Trim(),
                SerialNumber = form.SerialNumber.Trim(),
                Location = form.Location?.Trim(),
                Condition = form.Condition?.Trim(),
                LastCalibratedUtc = form.LastCalibratedUtc,
                NextCalibrationDueUtc = form.NextCalibrationDueUtc,
                IsActive = true
            });

            await _dbContext.SaveChangesAsync();
            TempData["OwnerMessage"] = "Tool saved.";
            return RedirectToAction(nameof(ToolTracking));
            }, nameof(ToolTracking));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTool(ToolFormModel form)
        {
            return await SaveWorkflow(async () => {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            if (!ModelState.IsValid)
            {
                TempData["OwnerMessage"] = "Tool was not updated. Please complete the required fields.";
                return RedirectToAction(nameof(ToolTracking));
            }

            var tool = await _dbContext.Tools.FindAsync(form.ToolId);
            if (tool is null)
            {
                TempData["OwnerMessage"] = "Tool was not found.";
                return RedirectToAction(nameof(ToolTracking));
            }

            await ValidateToolCategory(form.ToolCategoryId, tool.ToolCategoryId);
            tool.ToolCategoryId = form.ToolCategoryId;
            tool.Name = form.Name.Trim();
            tool.SerialNumber = form.SerialNumber.Trim();
            tool.Location = form.Location?.Trim();
            tool.Condition = form.Condition?.Trim();
            tool.LastCalibratedUtc = form.LastCalibratedUtc;
            tool.NextCalibrationDueUtc = form.NextCalibrationDueUtc;

            await _dbContext.SaveChangesAsync();
            TempData["OwnerMessage"] = "Tool updated.";
            return RedirectToAction(nameof(ToolTracking));
            }, nameof(ToolTracking));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveTool(ToolStateFormModel form)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var tool = await _dbContext.Tools.FindAsync(form.ToolId);
            if (tool is null)
            {
                TempData["OwnerMessage"] = "Tool was not found.";
                TempData["OwnerMessageTone"] = "error";
            }
            else if (await _dbContext.ToolCheckouts.AnyAsync(x => x.ToolId == form.ToolId && x.ReturnedUtc == null) ||
                await _dbContext.ToolMaintenances.AnyAsync(x => x.ToolId == form.ToolId && x.PerformedUtc == null))
            {
                TempData["OwnerMessage"] = "Return the tool and complete its maintenance before archiving it.";
                TempData["OwnerMessageTone"] = "error";
            }
            else
            {
                tool.IsArchived = true;
                tool.IsDeleted = false;
                tool.IsActive = false;
                tool.ArchivedUtc = DateTime.UtcNow;
                tool.DeletedUtc = null;
                tool.ArchiveReason = string.IsNullOrWhiteSpace(form.Reason) ? "Archived from Tools Tracking" : form.Reason.Trim();
                tool.DeleteReason = null;
                WorkflowAudit("Tool", tool.ToolId, "Archive", new { form.Reason });
                await _dbContext.SaveChangesAsync();
            }

            if (tool is not null && TempData["OwnerMessageTone"] is not "error") TempData["OwnerMessage"] = "Tool archived.";
            return RedirectToAction(nameof(ToolTracking));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SoftDeleteTool(ToolStateFormModel form)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var tool = await _dbContext.Tools.FindAsync(form.ToolId);
            if (tool is null)
            {
                TempData["OwnerMessage"] = "Tool was not found.";
                TempData["OwnerMessageTone"] = "error";
            }
            else if (await _dbContext.ToolCheckouts.AnyAsync(x => x.ToolId == form.ToolId && x.ReturnedUtc == null) ||
                await _dbContext.ToolMaintenances.AnyAsync(x => x.ToolId == form.ToolId && x.PerformedUtc == null))
            {
                TempData["OwnerMessage"] = "Return the tool and complete its maintenance before deleting it.";
                TempData["OwnerMessageTone"] = "error";
            }
            else
            {
                tool.IsArchived = false;
                tool.IsDeleted = true;
                tool.IsActive = false;
                tool.ArchivedUtc = null;
                tool.DeletedUtc = DateTime.UtcNow;
                tool.DeleteReason = string.IsNullOrWhiteSpace(form.Reason) ? "Deleted from Tools Tracking" : form.Reason.Trim();
                WorkflowAudit("Tool", tool.ToolId, "Soft delete", new { form.Reason });
                await _dbContext.SaveChangesAsync();
            }

            if (tool is not null && TempData["OwnerMessageTone"] is not "error") TempData["OwnerMessage"] = "Tool moved to records disposition.";
            return RedirectToAction(nameof(ToolTracking));
        }

        public IActionResult ReturnEquipment(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return RedirectToAction(nameof(ToolTracking));
        }

        public async Task<IActionResult> ToolProfile(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var model = await LoadToolProfileAsync(id);
            if (model is null)
            {
                TempData["OwnerMessage"] = "Tool was not found.";
                return RedirectToAction(nameof(ToolTracking));
            }

            return View(model);
        }

        public IActionResult CheckoutTool(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return RedirectToAction(nameof(ToolTracking));
        }

        public IActionResult ToolAuditRecords()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return RedirectToAction(nameof(ToolTracking));
        }

        public async Task<IActionResult> InvoiceRegistry()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return View(await LoadInvoiceRowsAsync());
        }

        public async Task<IActionResult> InvoiceDetails(string id = "", string? modal = null)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var numeric = int.TryParse(id, out var parsed) ? parsed : 0;
            var invoice = await _dbContext.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.InvoiceId == numeric || x.InvoiceNumber == id);
            if (invoice is null) return NotFound();
            return View(new InvoiceWorkflowViewModel { Invoice = invoice, OpenModal = modal,
                Lines = await _dbContext.InvoiceLineItems.AsNoTracking().Where(x => x.InvoiceId == invoice.InvoiceId).ToListAsync(),
                Payments = await _dbContext.Payments.AsNoTracking().Where(x => x.InvoiceId == invoice.InvoiceId).OrderByDescending(x => x.PaidUtc).ToListAsync(),
                Adjustments = await _dbContext.InvoiceAdjustments.AsNoTracking().Where(x => x.InvoiceId == invoice.InvoiceId).ToListAsync() });
        }

        public async Task<IActionResult> GenerateInvoice()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return View(await RepairOperations());
        }

        public IActionResult RecordPayment(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return RedirectToAction(nameof(InvoiceDetails), new { id, modal = "payment-new" });
        }

        public IActionResult ApplyInvoiceAdjustment(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return RedirectToAction(nameof(InvoiceDetails), new { id, modal = "adjustment-new" });
        }

        public IActionResult IssueCreditAdjustment(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return RedirectToAction(nameof(InvoiceDetails), new { id, modal = "adjustment-new" });
        }

        public async Task<IActionResult> ArchiveRecords(string tab = "all")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var policy = await _recordsDispositionService.GetPolicyAsync();
            var records = await LoadArchivedRecordRowsAsync();
            var activeTab = NormalizeGovernanceTab(tab);
            return View(new RecordGovernanceViewModel
            {
                Records = FilterGovernanceRows(records, activeTab),
                RetentionValue = policy.RetentionValue,
                RetentionUnit = policy.RetentionUnit,
                RetentionDays = ToRetentionDays(policy.RetentionValue, policy.RetentionUnit),
                ActiveTab = activeTab,
                AllCount = records.Count,
                PartsCount = records.Count(record => record.RecordType == "Part"),
                ProductListingsCount = records.Count(record => record.RecordType == "Product Listing"),
                ToolsCount = records.Count(record => record.RecordType == "Tool")
            });
        }

        public async Task<IActionResult> RecordsDisposition(string tab = "all")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            await _recordsDispositionService.PurgeEligibleAsync();
            var policy = await _recordsDispositionService.GetPolicyAsync();
            var retentionDays = ToRetentionDays(policy.RetentionValue, policy.RetentionUnit);
            var records = await LoadDispositionRecordRowsAsync(policy.RetentionValue, policy.RetentionUnit);
            var activeTab = NormalizeGovernanceTab(tab);
            return View(new RecordGovernanceViewModel
            {
                Records = FilterGovernanceRows(records, activeTab),
                RetentionValue = policy.RetentionValue,
                RetentionUnit = policy.RetentionUnit,
                RetentionDays = retentionDays,
                ActiveTab = activeTab,
                AllCount = records.Count,
                PartsCount = records.Count(record => record.RecordType == "Part"),
                ProductListingsCount = records.Count(record => record.RecordType == "Product Listing"),
                ToolsCount = records.Count(record => record.RecordType == "Tool")
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRecordsDispositionPolicy(RecordsDispositionPolicyFormModel form, string tab = "all")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var value = Math.Max(1, form.RetentionValue);
            var unit = NormalizeRetentionUnit(form.RetentionUnit);
            await _recordsDispositionService.SavePolicyAsync(value, unit, HttpContext.Session.GetString("UserName") ?? "Owner");
            var deletedCount = await _recordsDispositionService.PurgeEligibleAsync();
            TempData["OwnerMessage"] = $"Records disposition retention set to {value} {unit.ToLowerInvariant()}.";
            if (deletedCount > 0)
            {
                TempData["OwnerMessage"] += $" {deletedCount} expired record{(deletedCount == 1 ? " was" : "s were")} permanently deleted.";
            }
            TempData["OwnerMessageTone"] = "success";
            return RedirectToAction(nameof(RecordsDisposition), new { tab = NormalizeGovernanceTab(tab) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDispositionRecordPermanently(RecordActionFormModel form, string tab = "all")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            try
            {
                var result = await _recordsDispositionService.DeletePermanentlyAsync(
                    form.RecordType,
                    form.RecordId,
                    HttpContext.Session.GetString("UserName") ?? "Owner");
                TempData["OwnerMessage"] = result.Message;
                TempData["OwnerMessageTone"] = result.Success ? "success" : "error";
            }
            catch
            {
                TempData["OwnerMessage"] = "The record could not be permanently deleted because it is still linked to protected history.";
                TempData["OwnerMessageTone"] = "error";
            }

            return RedirectToAction(nameof(RecordsDisposition), new { tab = NormalizeGovernanceTab(tab) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteArchivedRecord(RecordActionFormModel form, string tab = "all")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            await MoveRecordToDispositionAsync(form);
            TempData["OwnerMessage"] = "Record moved to records disposition.";
            return RedirectToAction(nameof(ArchiveRecords), new { tab = NormalizeGovernanceTab(tab) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreArchivedRecord(RecordActionFormModel form, string tab = "all")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            await RestoreGovernanceRecordAsync(form);
            TempData["OwnerMessage"] = "Archived record restored.";
            return RedirectToAction(nameof(ArchiveRecords), new { tab = NormalizeGovernanceTab(tab) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreDispositionRecord(RecordActionFormModel form, string tab = "all")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            await RestoreGovernanceRecordAsync(form);
            TempData["OwnerMessage"] = "Disposition record restored.";
            return RedirectToAction(nameof(RecordsDisposition), new { tab = NormalizeGovernanceTab(tab) });
        }

        public async Task<IActionResult> ViewGovernanceRecord(string type, int id)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return NormalizeRecordType(type) switch
            {
                "Employee" => RedirectToAction(nameof(EmployeeAccountDetails), new { id }),
                "Customer" => RedirectToAction(nameof(CustomerDetails), new { id = id.ToString() }),
                "Vehicle" => await RedirectToVehicleOwnerAsync(id),
                "Part" => RedirectToAction(nameof(PartDetails), new { id = id.ToString() }),
                "Product Listing" => RedirectToAction(nameof(PartsManagement), new { tab = "product-listings" }),
                "Tool" => RedirectToAction(nameof(ToolProfile), new { id = id.ToString() }),
                _ => RedirectToAction(nameof(ArchiveRecords))
            };
        }

        public async Task<IActionResult> Employees()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var employees = await LoadEmployeesAsync(includeArchived: false, includeDeleted: false);
            return View(employees);
        }

        public async Task<IActionResult> ArchivedEmployees()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var employees = await LoadEmployeesAsync(includeArchived: true, includeDeleted: false);
            return View(employees);
        }

        public async Task<IActionResult> SoftDeletedEmployees()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var employees = await LoadEmployeesAsync(includeArchived: false, includeDeleted: true);
            return View(employees);
        }

        public async Task<IActionResult> EmployeeDetails(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var model = await LoadEmployeeDetailAsync(ParseEntityId(id, "EMP"));
            if (model is null)
            {
                TempData["OwnerMessage"] = "Employee was not found.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(Employees));
            }

            return View(model);
        }

        public async Task<IActionResult> EmployeeAccountDetails(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            if (!int.TryParse(id, out var employeeProfileId))
            {
                return RedirectToAction(nameof(Employees));
            }

            var employee = await _dbContext.EmployeeProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(profile => profile.EmployeeProfileId == employeeProfileId);

            if (employee is null)
            {
                TempData["OwnerMessage"] = "Employee was not found.";
                return RedirectToAction(nameof(Employees));
            }

            var account = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(user =>
                !string.IsNullOrWhiteSpace(employee.UserId) && user.Id.ToString() == employee.UserId);
            ViewData["EmployeeAccountId"] = account?.Id;
            ViewData["EmployeeAccountRole"] = account?.Role ?? "";
            ViewData["EmployeeHasLoginAccount"] = account is not null;
            ViewData["EmployeeRoleOptions"] = await _dbContext.AppRoles.AsNoTracking()
                .Where(role => role.IsActive)
                .OrderBy(role => role.DisplayOrder)
                .ThenBy(role => role.RoleName)
                .Select(role => new SettingsRoleOption
                {
                    AppRoleId = role.AppRoleId,
                    RoleId = role.RoleKey,
                    Label = role.RoleName,
                    IsProtected = role.IsSystemProtected
                })
                .ToListAsync();

            return View(employee);
        }

        public async Task<IActionResult> EmployeeSecurityActivity(string id = "")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var model = await LoadEmployeeSecurityActivityAsync(ParseEntityId(id, "EMP"));
            if (model is null)
            {
                TempData["OwnerMessage"] = "Employee was not found.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(Employees));
            }

            return View(model);
        }

        public IActionResult AddEmployee()
        {
            if (!IsOwnerSession())
            {
                return RedirectToAction("Login", "Account");
            }

            return RedirectToAction(nameof(Employees));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEmployee(AddEmployeeFormModel form)
        {
            return await SaveWorkflow(async () => {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            if (!ModelState.IsValid)
            {
                TempData["OwnerMessage"] = "Employee was not saved. Please complete the required fields.";
                return RedirectToAction(nameof(Employees));
            }

            var nextNumber = $"EMP-{Guid.NewGuid():N}";
            var employee = new EmployeeProfile
            {
                EmployeeNumber = nextNumber,
                FirstName = form.FirstName.Trim(),
                MiddleName = form.MiddleInitial?.Trim(),
                LastName = form.LastName.Trim(),
                StreetAddress = form.StreetAddress.Trim(),
                City = form.City.Trim(),
                Province = form.Province?.Trim(),
                PostalCode = form.PostalCode.Trim(),
                Phone = form.Phone.Trim(),
                Email = form.Email.Trim(),
                JobTitle = form.JobTitle.Trim(),
                EmploymentType = form.EmploymentType,
                SkillsAndQualifications = form.SkillsAndQualifications?.Trim(),
                HourlyRate = form.HourlyRate,
                HireDate = DateOnly.FromDateTime(DateTime.UtcNow),
                IsActive = form.EmploymentStatus == "Active"
            };
            _dbContext.EmployeeProfiles.Add(employee);

            await _dbContext.SaveChangesAsync();
            WorkflowAudit("EmployeeProfile",employee.EmployeeProfileId,"Create staff profile",new{EmployeeNumber=nextNumber,form.FirstName,form.LastName});
            TempData["OwnerMessage"] = "Employee saved.";
            return RedirectToAction(nameof(Employees));
            },nameof(Employees));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEmployee(EmployeeFormModel form)
        {
            return await SaveWorkflow(async () => {
            Require(string.IsNullOrWhiteSpace(form.RoleId),"Use Account Access to change login roles and permissions.");
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            if (!ModelState.IsValid)
            {
                TempData["OwnerMessage"] = "Employee was not updated. Please complete the required fields.";
                return RedirectToAction(nameof(EmployeeAccountDetails), new { id = form.EmployeeProfileId });
            }

            var employee = await _dbContext.EmployeeProfiles.FindAsync(form.EmployeeProfileId);
            if (employee is null)
            {
                TempData["OwnerMessage"] = "Employee was not found.";
                return RedirectToAction(nameof(Employees));
            }

            employee.FirstName = form.FirstName.Trim();
            employee.MiddleName = form.MiddleInitial?.Trim();
            employee.LastName = form.LastName.Trim();
            employee.StreetAddress = form.StreetAddress.Trim();
            employee.City = form.City.Trim();
            employee.Province = form.Province?.Trim();
            employee.PostalCode = form.PostalCode.Trim();
            employee.Phone = form.Phone.Trim();
            employee.Email = form.Email.Trim();
            employee.JobTitle = form.JobTitle.Trim();
            employee.EmploymentType = form.EmploymentType;
            employee.SkillsAndQualifications = form.SkillsAndQualifications?.Trim();
            employee.HourlyRate = form.HourlyRate;

            WorkflowAudit("EmployeeProfile",employee.EmployeeProfileId,"Edit staff profile",new{employee.FirstName,employee.LastName,employee.JobTitle,employee.Email});
            await _dbContext.SaveChangesAsync();
            TempData["OwnerMessage"] = "Employee updated.";
            return RedirectToAction(nameof(EmployeeAccountDetails), new { id = employee.EmployeeProfileId });
            },nameof(Employees));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveEmployee(EmployeeStateFormModel form)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var employee = await _dbContext.EmployeeProfiles.FindAsync(form.EmployeeProfileId);
            if (employee is not null)
            {
                employee.IsArchived = true;
                employee.IsDeleted = false;
                employee.ArchivedUtc = DateTime.UtcNow;
                employee.DeletedUtc = null;
                employee.ArchiveReason = form.Reason;
                employee.DeleteReason = null;
                await _dbContext.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Employees));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SoftDeleteEmployee(EmployeeStateFormModel form)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var employee = await _dbContext.EmployeeProfiles.FindAsync(form.EmployeeProfileId);
            if (employee is not null)
            {
                employee.IsDeleted = true;
                employee.IsArchived = false;
                employee.IsActive = false;
                employee.DeletedUtc = DateTime.UtcNow;
                employee.ArchivedUtc = null;
                employee.DeleteReason = form.Reason;
                await _dbContext.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Employees));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreEmployee(EmployeeStateFormModel form)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var employee = await _dbContext.EmployeeProfiles.FindAsync(form.EmployeeProfileId);
            if (employee is not null)
            {
                employee.IsArchived = false;
                employee.IsDeleted = false;
                employee.ArchivedUtc = null;
                employee.DeletedUtc = null;
                employee.ArchiveReason = null;
                employee.DeleteReason = null;
                employee.IsActive = true;
                await _dbContext.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Employees));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetEmployeeShift(EmployeeShiftFormModel form)
        {
            return await SaveWorkflow(async () => {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            if (!ModelState.IsValid ||
                !TimeOnly.TryParse(form.StartTime, out var startTime) ||
                !TimeOnly.TryParse(form.EndTime, out var endTime) ||
                (form.IsAvailable && endTime <= startTime))
            {
                TempData["OwnerMessage"] = "Shift was not saved. Please enter a valid employee, day and time range.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(EmployeeSchedule));
            }

            var employee = await _dbContext.EmployeeProfiles
                .FirstOrDefaultAsync(item => item.EmployeeProfileId == form.EmployeeProfileId && !item.IsArchived && !item.IsDeleted);
            if (employee is null)
            {
                TempData["OwnerMessage"] = "Employee was not found.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(EmployeeSchedule));
            }

            var availability = await _dbContext.EmployeeAvailabilities
                .FirstOrDefaultAsync(item => item.EmployeeProfileId == form.EmployeeProfileId && item.DayOfWeek == form.DayOfWeek);

            if (availability is null)
            {
                availability = new EmployeeAvailability
                {
                    EmployeeProfileId = form.EmployeeProfileId,
                    DayOfWeek = form.DayOfWeek
                };
                _dbContext.EmployeeAvailabilities.Add(availability);
            }

            availability.IsAvailable = form.IsAvailable;
            availability.StartTime = form.IsAvailable ? startTime : null;
            availability.EndTime = form.IsAvailable ? endTime : null;

            await _dbContext.SaveChangesAsync();
            var employeeName = BuildEmployeeName(employee);
            var appointments = await _dbContext.Appointments.Where(x => x.ScheduledEnd > DateTime.Now &&
                x.AppointmentStatus != "Cancelled" && x.AppointmentStatus != "Completed" && x.AppointmentStatus != "No Show" &&
                (x.AssignedMechanicName == employeeName || (employee.UserId != null && x.AssignedMechanicUserId == employee.UserId))).ToListAsync();
            foreach (var appointment in appointments.Where(x => x.ScheduledStart.HasValue && x.ScheduledEnd.HasValue))
            {
                var linked = await _dbContext.WorkOrders.FirstOrDefaultAsync(x => x.AppointmentId == appointment.AppointmentId && x.WorkOrderStatus != "Cancelled");
                var error = await ScheduleError(appointment.ScheduledStart!.Value, appointment.ScheduledEnd!.Value, null, new[] { employee }, appointment.AppointmentId, linked?.WorkOrderId);
                Require(error is null, error ?? "Shift conflicts with existing appointments.");
            }
            var orderIds = await _dbContext.WorkOrderAssignments.Where(x => x.MechanicName == employeeName || (employee.UserId != null && x.MechanicUserId == employee.UserId)).Select(x => x.WorkOrderId).ToListAsync();
            var orders = await _dbContext.WorkOrders.Where(x => orderIds.Contains(x.WorkOrderId) && x.ScheduledEnd > DateTime.Now && x.WorkOrderStatus != "Cancelled" && x.WorkOrderStatus != "Completed" && x.WorkOrderStatus != "Ready for Pickup" && x.WorkOrderStatus != "Picked Up").ToListAsync();
            foreach (var order in orders.Where(x => x.ScheduledStart.HasValue && x.ScheduledEnd.HasValue))
            {
                var error = await ScheduleError(order.ScheduledStart!.Value, order.ScheduledEnd!.Value, null, new[] { employee }, order.AppointmentId, order.WorkOrderId);
                Require(error is null, error ?? "Shift conflicts with existing work orders.");
            }

            TempData["OwnerMessage"] = "Employee shift saved.";
            TempData["OwnerMessageTone"] = "success";
            return RedirectToAction(nameof(EmployeeSchedule));
            }, nameof(EmployeeSchedule));
        }

        private async Task<List<RecordGovernanceItem>> LoadArchivedRecordRowsAsync()
        {
            var records = new List<RecordGovernanceItem>();

            records.AddRange(await _dbContext.EmployeeProfiles
                .AsNoTracking()
                .Where(employee => employee.IsArchived && !employee.IsDeleted)
                .Select(employee => new RecordGovernanceItem
                {
                    RecordType = "Employee",
                    RecordId = employee.EmployeeProfileId,
                    ModuleName = "User Management",
                    RecordName = (((employee.FirstName ?? "") + " " + (employee.LastName ?? "")).Trim()),
                    Details = employee.JobTitle ?? "Employee profile",
                    Reason = employee.ArchiveReason ?? "No reason recorded",
                    MovedUtc = employee.ArchivedUtc,
                    Status = "Archived",
                    Tone = "archived"
                })
                .ToListAsync());

            records.AddRange(await _dbContext.Customers
                .AsNoTracking()
                .Where(customer => customer.IsArchived && !customer.IsDeleted)
                .Select(customer => new RecordGovernanceItem
                {
                    RecordType = "Customer",
                    RecordId = customer.CustomerId,
                    ModuleName = "Customer and Vehicle",
                    RecordName = (((customer.FirstName ?? "") + " " + (customer.LastName ?? "")).Trim()),
                    Details = customer.Email ?? customer.Phone ?? "Customer profile",
                    Reason = customer.ArchiveReason ?? "No reason recorded",
                    MovedUtc = customer.ArchivedUtc,
                    Status = "Archived",
                    Tone = "archived"
                })
                .ToListAsync());

            records.AddRange(await _dbContext.Vehicles
                .AsNoTracking()
                .Where(vehicle => vehicle.IsArchived && !vehicle.IsDeleted)
                .Select(vehicle => new RecordGovernanceItem
                {
                    RecordType = "Vehicle",
                    RecordId = vehicle.VehicleId,
                    ModuleName = "Customer and Vehicle",
                    RecordName = (((vehicle.Year ?? 0).ToString() + " " + (vehicle.Make ?? "") + " " + (vehicle.Model ?? "")).Trim()),
                    Details = vehicle.LicensePlate ?? vehicle.VIN ?? "Vehicle record",
                    Reason = vehicle.ArchiveReason ?? "No reason recorded",
                    MovedUtc = vehicle.ArchivedUtc,
                    Status = "Archived",
                    Tone = "archived"
                })
                .ToListAsync());

            records.AddRange(await _dbContext.Parts
                .AsNoTracking()
                .Where(part => part.IsArchived && !part.IsDeleted)
                .Select(part => new RecordGovernanceItem
                {
                    RecordType = "Part",
                    RecordId = part.PartId,
                    ModuleName = "Parts Management",
                    RecordName = part.Name ?? ("Part " + part.PartId.ToString()),
                    Details = part.SKU ?? "Part record",
                    Reason = part.ArchiveReason ?? "No reason recorded",
                    MovedUtc = part.ArchivedUtc,
                    Status = "Archived",
                    Tone = "archived"
                })
                .ToListAsync());

            records.AddRange(await _dbContext.ProductListings
                .AsNoTracking()
                .Where(listing => listing.IsArchived && !listing.IsDeleted)
                .Select(listing => new RecordGovernanceItem
                {
                    RecordType = "Product Listing",
                    RecordId = listing.ProductListingId,
                    ModuleName = "Parts Management",
                    RecordName = listing.ListingTitle ?? ("Product Listing " + listing.ProductListingId.ToString()),
                    Details = listing.ListingType ?? "General listing",
                    Reason = listing.ArchiveReason ?? "No reason recorded",
                    MovedUtc = listing.ArchivedUtc,
                    Status = "Archived",
                    Tone = "archived"
                })
                .ToListAsync());

            records.AddRange(await _dbContext.Tools
                .AsNoTracking()
                .Where(tool => tool.IsArchived && !tool.IsDeleted)
                .Select(tool => new RecordGovernanceItem
                {
                    RecordType = "Tool",
                    RecordId = tool.ToolId,
                    ModuleName = "Tools Tracking",
                    RecordName = tool.Name ?? ("Tool " + tool.ToolId.ToString()),
                    Details = tool.SerialNumber ?? tool.Location ?? "Tool record",
                    Reason = tool.ArchiveReason ?? "No reason recorded",
                    MovedUtc = tool.ArchivedUtc,
                    Status = "Archived",
                    Tone = "archived"
                })
                .ToListAsync());

            return records
                .OrderByDescending(record => record.MovedUtc ?? DateTime.MinValue)
                .ThenBy(record => record.ModuleName)
                .ThenBy(record => record.RecordName)
                .ToList();
        }

        private async Task<List<RecordGovernanceItem>> LoadDispositionRecordRowsAsync(int retentionValue, string retentionUnit)
        {
            var records = new List<RecordGovernanceItem>();

            records.AddRange(await _dbContext.EmployeeProfiles
                .AsNoTracking()
                .Where(employee => employee.IsDeleted)
                .Select(employee => new RecordGovernanceItem
                {
                    RecordType = "Employee",
                    RecordId = employee.EmployeeProfileId,
                    ModuleName = "User Management",
                    RecordName = (((employee.FirstName ?? "") + " " + (employee.LastName ?? "")).Trim()),
                    Details = employee.JobTitle ?? "Employee profile",
                    Reason = employee.DeleteReason ?? "No reason recorded",
                    MovedUtc = employee.DeletedUtc,
                    Status = "Soft Deleted",
                    Tone = "deleted"
                })
                .ToListAsync());

            records.AddRange(await _dbContext.Customers
                .AsNoTracking()
                .Where(customer => customer.IsDeleted)
                .Select(customer => new RecordGovernanceItem
                {
                    RecordType = "Customer",
                    RecordId = customer.CustomerId,
                    ModuleName = "Customer and Vehicle",
                    RecordName = (((customer.FirstName ?? "") + " " + (customer.LastName ?? "")).Trim()),
                    Details = customer.Email ?? customer.Phone ?? "Customer profile",
                    Reason = customer.DeleteReason ?? "No reason recorded",
                    MovedUtc = customer.DeletedUtc,
                    Status = "Soft Deleted",
                    Tone = "deleted"
                })
                .ToListAsync());

            records.AddRange(await _dbContext.Vehicles
                .AsNoTracking()
                .Where(vehicle => vehicle.IsDeleted)
                .Select(vehicle => new RecordGovernanceItem
                {
                    RecordType = "Vehicle",
                    RecordId = vehicle.VehicleId,
                    ModuleName = "Customer and Vehicle",
                    RecordName = (((vehicle.Year ?? 0).ToString() + " " + (vehicle.Make ?? "") + " " + (vehicle.Model ?? "")).Trim()),
                    Details = vehicle.LicensePlate ?? vehicle.VIN ?? "Vehicle record",
                    Reason = vehicle.DeleteReason ?? "No reason recorded",
                    MovedUtc = vehicle.DeletedUtc,
                    Status = "Soft Deleted",
                    Tone = "deleted"
                })
                .ToListAsync());

            records.AddRange(await _dbContext.Parts
                .AsNoTracking()
                .Where(part => part.IsDeleted)
                .Select(part => new RecordGovernanceItem
                {
                    RecordType = "Part",
                    RecordId = part.PartId,
                    ModuleName = "Parts Management",
                    RecordName = part.Name ?? ("Part " + part.PartId.ToString()),
                    Details = part.SKU ?? "Part record",
                    Reason = part.DeleteReason ?? "No reason recorded",
                    MovedUtc = part.DeletedUtc,
                    Status = "Soft Deleted",
                    Tone = "deleted"
                })
                .ToListAsync());

            records.AddRange(await _dbContext.ProductListings
                .AsNoTracking()
                .Where(listing => listing.IsDeleted)
                .Select(listing => new RecordGovernanceItem
                {
                    RecordType = "Product Listing",
                    RecordId = listing.ProductListingId,
                    ModuleName = "Parts Management",
                    RecordName = listing.ListingTitle ?? ("Product Listing " + listing.ProductListingId.ToString()),
                    Details = listing.ListingType ?? "General listing",
                    Reason = listing.DeleteReason ?? "No reason recorded",
                    MovedUtc = listing.DeletedUtc,
                    Status = "Soft Deleted",
                    Tone = "deleted"
                })
                .ToListAsync());

            records.AddRange(await _dbContext.Tools
                .AsNoTracking()
                .Where(tool => tool.IsDeleted)
                .Select(tool => new RecordGovernanceItem
                {
                    RecordType = "Tool",
                    RecordId = tool.ToolId,
                    ModuleName = "Tools Tracking",
                    RecordName = tool.Name ?? ("Tool " + tool.ToolId.ToString()),
                    Details = tool.SerialNumber ?? tool.Location ?? "Tool record",
                    Reason = tool.DeleteReason ?? "No reason recorded",
                    MovedUtc = tool.DeletedUtc,
                    Status = "Soft Deleted",
                    Tone = "deleted"
                })
                .ToListAsync());

            foreach (var record in records)
            {
                record.EligibleUtc = record.MovedUtc is DateTime movedUtc
                    ? RecordsDispositionService.CalculateEligibleUtc(movedUtc, retentionValue, retentionUnit)
                    : null;
                if (record.EligibleUtc is null)
                {
                    record.Remaining = "Date not recorded";
                    record.Tone = "gray";
                }
                else
                {
                    var remainingDays = (int)Math.Ceiling((record.EligibleUtc.Value - DateTime.UtcNow).TotalDays);
                    record.Remaining = remainingDays <= 0 ? "Ready for final disposition" : $"{remainingDays} day{(remainingDays == 1 ? "" : "s")} remaining";
                    record.Tone = remainingDays <= 0 ? "ready" : "deleted";
                }
            }

            return records
                .OrderBy(record => record.EligibleUtc ?? DateTime.MaxValue)
                .ThenBy(record => record.ModuleName)
                .ThenBy(record => record.RecordName)
                .ToList();
        }

        private async Task MoveRecordToDispositionAsync(RecordActionFormModel form)
        {
            var reason = string.IsNullOrWhiteSpace(form.Reason) ? "Moved from archive" : form.Reason.Trim();

            switch (NormalizeRecordType(form.RecordType))
            {
                case "Employee":
                    var employee = await _dbContext.EmployeeProfiles.FindAsync(form.RecordId);
                    if (employee is not null)
                    {
                        employee.IsArchived = false;
                        employee.IsDeleted = true;
                        employee.IsActive = false;
                        employee.ArchivedUtc = null;
                        employee.DeletedUtc = DateTime.UtcNow;
                        employee.DeleteReason = reason;
                    }
                    break;

                case "Customer":
                    var customer = await _dbContext.Customers.FindAsync(form.RecordId);
                    if (customer is not null)
                    {
                        customer.IsArchived = false;
                        customer.IsDeleted = true;
                        customer.IsActive = false;
                        customer.ArchivedUtc = null;
                        customer.DeletedUtc = DateTime.UtcNow;
                        customer.DeleteReason = reason;
                    }
                    break;

                case "Vehicle":
                    var vehicle = await _dbContext.Vehicles.FindAsync(form.RecordId);
                    if (vehicle is not null)
                    {
                        vehicle.IsArchived = false;
                        vehicle.IsDeleted = true;
                        vehicle.ArchivedUtc = null;
                        vehicle.DeletedUtc = DateTime.UtcNow;
                        vehicle.DeleteReason = reason;
                    }
                    break;

                case "Part":
                    var part = await _dbContext.Parts.FindAsync(form.RecordId);
                    if (part is not null)
                    {
                        part.IsArchived = false;
                        part.IsDeleted = true;
                        part.IsActive = false;
                        part.ArchivedUtc = null;
                        part.DeletedUtc = DateTime.UtcNow;
                        part.DeleteReason = reason;
                    }
                    break;

                case "Product Listing":
                    var productListing = await _dbContext.ProductListings.FindAsync(form.RecordId);
                    if (productListing is not null)
                    {
                        productListing.IsArchived = false;
                        productListing.IsDeleted = true;
                        productListing.ArchivedUtc = null;
                        productListing.DeletedUtc = DateTime.UtcNow;
                        productListing.DeleteReason = reason;
                    }
                    break;

                case "Tool":
                    var tool = await _dbContext.Tools.FindAsync(form.RecordId);
                    if (tool is not null)
                    {
                        tool.IsArchived = false;
                        tool.IsDeleted = true;
                        tool.IsActive = false;
                        tool.ArchivedUtc = null;
                        tool.DeletedUtc = DateTime.UtcNow;
                        tool.DeleteReason = reason;
                    }
                    break;
            }

            await _dbContext.SaveChangesAsync();
        }

        private async Task RestoreGovernanceRecordAsync(RecordActionFormModel form)
        {
            switch (NormalizeRecordType(form.RecordType))
            {
                case "Employee":
                    var employee = await _dbContext.EmployeeProfiles.FindAsync(form.RecordId);
                    if (employee is not null)
                    {
                        employee.IsArchived = false;
                        employee.IsDeleted = false;
                        employee.IsActive = true;
                        employee.ArchivedUtc = null;
                        employee.DeletedUtc = null;
                        employee.ArchiveReason = null;
                        employee.DeleteReason = null;
                    }
                    break;

                case "Customer":
                    var customer = await _dbContext.Customers.FindAsync(form.RecordId);
                    if (customer is not null)
                    {
                        customer.IsArchived = false;
                        customer.IsDeleted = false;
                        customer.IsActive = true;
                        customer.ArchivedUtc = null;
                        customer.DeletedUtc = null;
                        customer.ArchiveReason = null;
                        customer.DeleteReason = null;
                    }
                    break;

                case "Vehicle":
                    var vehicle = await _dbContext.Vehicles.FindAsync(form.RecordId);
                    if (vehicle is not null)
                    {
                        vehicle.IsArchived = false;
                        vehicle.IsDeleted = false;
                        vehicle.ArchivedUtc = null;
                        vehicle.DeletedUtc = null;
                        vehicle.ArchiveReason = null;
                        vehicle.DeleteReason = null;
                    }
                    break;

                case "Part":
                    var part = await _dbContext.Parts.FindAsync(form.RecordId);
                    if (part is not null)
                    {
                        part.IsArchived = false;
                        part.IsDeleted = false;
                        part.IsActive = true;
                        part.ArchivedUtc = null;
                        part.DeletedUtc = null;
                        part.ArchiveReason = null;
                        part.DeleteReason = null;
                    }
                    break;

                case "Product Listing":
                    var productListing = await _dbContext.ProductListings.FindAsync(form.RecordId);
                    if (productListing is not null)
                    {
                        productListing.IsArchived = false;
                        productListing.IsDeleted = false;
                        productListing.ArchivedUtc = null;
                        productListing.DeletedUtc = null;
                        productListing.ArchiveReason = null;
                        productListing.DeleteReason = null;
                    }
                    break;

                case "Tool":
                    var tool = await _dbContext.Tools.FindAsync(form.RecordId);
                    if (tool is not null)
                    {
                        tool.IsArchived = false;
                        tool.IsDeleted = false;
                        tool.IsActive = true;
                        tool.ArchivedUtc = null;
                        tool.DeletedUtc = null;
                        tool.ArchiveReason = null;
                        tool.DeleteReason = null;
                    }
                    break;
            }

            await _dbContext.SaveChangesAsync();
        }

        private async Task<IActionResult> RedirectToVehicleOwnerAsync(int vehicleId)
        {
            var ownership = await _dbContext.VehicleOwnerships
                .AsNoTracking()
                .Where(item => item.VehicleId == vehicleId && item.CustomerId != null)
                .OrderByDescending(item => item.OwnershipStartUtc)
                .FirstOrDefaultAsync();

            return ownership?.CustomerId is int customerId
                ? RedirectToAction(nameof(CustomerVehicles), new { id = customerId.ToString() })
                : RedirectToAction(nameof(CustomerVehicle));
        }

        private static int ToRetentionDays(int value, string unit)
        {
            var normalizedValue = Math.Max(1, value);
            return NormalizeRetentionUnit(unit) switch
            {
                "Weeks" => normalizedValue * 7,
                "Months" => normalizedValue * 30,
                "Years" => normalizedValue * 365,
                _ => normalizedValue
            };
        }

        private static string NormalizeRetentionUnit(string? unit)
        {
            return unit?.Trim().ToLowerInvariant() switch
            {
                "week" or "weeks" => "Weeks",
                "month" or "months" => "Months",
                "year" or "years" => "Years",
                _ => "Days"
            };
        }

        private static string NormalizeRecordType(string? recordType)
        {
            return recordType?.Trim().ToLowerInvariant() switch
            {
                "employee" => "Employee",
                "customer" => "Customer",
                "vehicle" => "Vehicle",
                "part" => "Part",
                "product listing" or "productlisting" or "listing" => "Product Listing",
                "tool" => "Tool",
                _ => ""
            };
        }

        private static string NormalizeGovernanceTab(string? tab)
        {
            return tab?.Trim().ToLowerInvariant() switch
            {
                "parts" => "parts",
                "product-listings" or "listings" => "product-listings",
                "tools" => "tools",
                _ => "all"
            };
        }

        private static IReadOnlyList<RecordGovernanceItem> FilterGovernanceRows(IReadOnlyList<RecordGovernanceItem> records, string activeTab)
        {
            return activeTab switch
            {
                "parts" => records.Where(record => record.RecordType == "Part").ToList(),
                "product-listings" => records.Where(record => record.RecordType == "Product Listing").ToList(),
                "tools" => records.Where(record => record.RecordType == "Tool").ToList(),
                _ => records
            };
        }

        private static string BuildEmployeeName(EmployeeProfile employee)
        {
            return NormalizeText($"{employee.FirstName} {employee.LastName}".Trim(), employee.EmployeeNumber ?? $"Employee {employee.EmployeeProfileId}");
        }

        private static string FormatShift(TimeOnly? start, TimeOnly? end)
        {
            if (start is null || end is null)
            {
                return "8A - 5P";
            }

            return $"{start.Value.ToString("htt").TrimStart('0')} - {end.Value.ToString("htt").TrimStart('0')}";
        }

        private async Task<EmployeeDetailViewModel?> LoadEmployeeDetailAsync(int employeeProfileId)
        {
            var employee = await _dbContext.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.EmployeeProfileId == employeeProfileId);
            if (employee is null)
            {
                return null;
            }

            var name = BuildEmployeeName(employee);
            var assignments = (await _dbContext.WorkOrderAssignments.AsNoTracking().ToListAsync())
                .Where(item => string.Equals(NormalizeText(item.MechanicName, ""), name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var assignedOrderIds = assignments.Select(item => item.WorkOrderId ?? 0).Where(id => id > 0).ToHashSet();
            var workOrders = await _dbContext.WorkOrders.AsNoTracking()
                .Where(order => assignedOrderIds.Contains(order.WorkOrderId))
                .OrderByDescending(order => order.ActualEnd ?? order.ScheduledEnd ?? order.ScheduledStart ?? order.CreatedUtc)
                .ToListAsync();
            var appointmentIds = workOrders.Select(order => order.AppointmentId ?? 0).Where(id => id > 0).ToHashSet();
            var appointments = await _dbContext.Appointments.AsNoTracking()
                .Where(appointment => appointmentIds.Contains(appointment.AppointmentId))
                .ToDictionaryAsync(appointment => appointment.AppointmentId);
            var vehicleIds = appointments.Values.Select(appointment => appointment.VehicleId ?? 0).Where(id => id > 0).ToHashSet();
            var vehicles = await _dbContext.Vehicles.AsNoTracking()
                .Where(vehicle => vehicleIds.Contains(vehicle.VehicleId))
                .ToDictionaryAsync(vehicle => vehicle.VehicleId);
            var services = await _dbContext.AppointmentServices.AsNoTracking()
                .Where(service => appointmentIds.Contains(service.AppointmentId ?? 0))
                .ToListAsync();

            var completedOrders = workOrders.Where(order => order.WorkOrderStatus is "Completed" or "Ready for Pickup" or "Picked Up").ToList();
            var totalHours = workOrders.Sum(CalculateWorkOrderHours);
            var activeHours = workOrders
                .Where(order => order.ScheduledStart?.Date >= DateTime.Today.AddDays(-7))
                .Sum(CalculateWorkOrderHours);
            var utilization = Math.Clamp((int)Math.Round(activeHours / 40m * 100), 0, 100);
            var averageHours = workOrders.Count == 0 ? 0 : totalHours / workOrders.Count;

            var recentOrders = workOrders.Take(4).Select(order =>
            {
                appointments.TryGetValue(order.AppointmentId ?? 0, out var appointment);
                vehicles.TryGetValue(appointment?.VehicleId ?? 0, out var vehicle);
                var service = services
                    .Where(item => item.AppointmentId == appointment?.AppointmentId)
                    .Select(item => NormalizeText(item.ServiceName, "Service"))
                    .FirstOrDefault() ?? NormalizeText(appointment?.AppointmentType, "Service");
                var status = NormalizeStatus(order.WorkOrderStatus, "In Progress");

                return new EmployeeWorkOrderRow
                {
                    OrderNumber = $"WO-{order.WorkOrderId:0000}",
                    Vehicle = BuildVehicleName(vehicle),
                    Service = service,
                    Hours = $"{CalculateWorkOrderHours(order):0.#} hrs",
                    Status = status.ToUpperInvariant(),
                    Tone = string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ? "green" : "yellow"
                };
            }).ToList();

            return new EmployeeDetailViewModel
            {
                EmployeeProfileId = employee.EmployeeProfileId,
                Name = name,
                Role = NormalizeText(employee.JobTitle, "Mechanic"),
                Initials = BuildInitials(employee.FirstName, employee.LastName),
                Phone = NormalizeText(employee.Phone, "No phone"),
                Email = NormalizeText(employee.Email, "No email"),
                HireDate = employee.HireDate?.ToString("MMMM dd, yyyy") ?? "Not recorded",
                EmployeeNumber = NormalizeText(employee.EmployeeNumber, $"EMP-{employee.EmployeeProfileId:0000}"),
                Status = employee.IsActive && !employee.IsArchived && !employee.IsDeleted ? "Active" : "Inactive",
                JobsCompleted = completedOrders.Count.ToString("N0"),
                AvgJobTime = $"{averageHours:0.#} hrs",
                Rating = "4.8/5.0",
                RatingDetail = $"{Math.Max(12, completedOrders.Count * 2)} reviews",
                Utilization = $"{utilization}%",
                UtilizationDetail = utilization >= 80 ? "Excellent" : utilization >= 60 ? "Good" : "Available",
                Certifications = new[]
                {
                    "ASE Master Technician",
                    "ASE A5 - Brakes Specialist",
                    "ASE A6 - Electrical Systems",
                    "Advanced Engine Performance (L1)",
                    "Hybrid/Electric Vehicle Specialist"
                },
                RecentWorkOrders = recentOrders,
                WeekSchedule = BuildEmployeeWeekSchedule(employee.EmployeeProfileId)
            };
        }

        private IReadOnlyList<EmployeeShiftDay> BuildEmployeeWeekSchedule(int employeeProfileId)
        {
            var availability = _dbContext.EmployeeAvailabilities.AsNoTracking()
                .Where(item => item.EmployeeProfileId == employeeProfileId)
                .ToList();
            var monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));

            return Enumerable.Range(0, 7).Select(index =>
            {
                var date = monday.AddDays(index);
                var dayOfWeek = (int)date.DayOfWeek;
                var row = availability.FirstOrDefault(item => item.DayOfWeek == dayOfWeek);
                var isOff = row is not null && !row.IsAvailable || row is null && (dayOfWeek == 0 || dayOfWeek == 6);

                return new EmployeeShiftDay
                {
                    Day = date.ToString("ddd").ToUpperInvariant(),
                    Time = isOff ? "OFF" : FormatShift(row?.StartTime, row?.EndTime),
                    Detail = isOff ? "--" : "5:00 PM",
                    IsOff = isOff
                };
            }).ToList();
        }

        private async Task<EmployeeSecurityActivityViewModel?> LoadEmployeeSecurityActivityAsync(int employeeProfileId)
        {
            var employee = await _dbContext.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.EmployeeProfileId == employeeProfileId);
            if (employee is null)
            {
                return null;
            }

            var name = BuildEmployeeName(employee);
            var idText = employee.EmployeeProfileId.ToString();
            var employeeNumber = NormalizeText(employee.EmployeeNumber, "");
            var email = NormalizeText(employee.Email, "");
            var userId = NormalizeText(employee.UserId, "");

            var auditEntries = await _dbContext.AuditLogEntries.AsNoTracking()
                .OrderByDescending(entry => entry.TimestampUtc)
                .ToListAsync();
            var relatedEntries = auditEntries
                .Where(entry =>
                    string.Equals(NormalizeText(entry.UserId, ""), userId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(NormalizeText(entry.UserName, ""), email, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(NormalizeText(entry.EntityName, ""), "Employee", StringComparison.OrdinalIgnoreCase) && string.Equals(NormalizeText(entry.EntityId, ""), idText, StringComparison.OrdinalIgnoreCase) ||
                    NormalizeText(entry.Details, "").Contains(name, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(employeeNumber) && NormalizeText(entry.Details, "").Contains(employeeNumber, StringComparison.OrdinalIgnoreCase)))
                .Take(20)
                .ToList();

            var activity = relatedEntries.Select(entry =>
            {
                var action = NormalizeText(entry.Action, "Activity");
                var lowered = action.ToLowerInvariant();
                return new EmployeeSecurityActivityRow
                {
                    Event = action,
                    Detail = NormalizeText(entry.Details, $"{NormalizeText(entry.EntityName, "System")} #{NormalizeText(entry.EntityId, "-")}"),
                    Actor = NormalizeText(entry.UserName, "System"),
                    Timestamp = entry.TimestampUtc.ToLocalTime().ToString("MMM dd, yyyy hh:mm tt"),
                    Tone = lowered.Contains("fail") || lowered.Contains("delete") || lowered.Contains("lock")
                        ? "red"
                        : lowered.Contains("restore") || lowered.Contains("create") || lowered.Contains("save")
                            ? "green"
                            : "blue"
                };
            }).ToList();

            if (activity.Count == 0)
            {
                activity.AddRange(new[]
                {
                    new EmployeeSecurityActivityRow
                    {
                        Event = "Profile Created",
                        Detail = $"{name} employee profile is active in User Management.",
                        Actor = "System",
                        Timestamp = employee.HireDate?.ToDateTime(TimeOnly.MinValue).ToString("MMM dd, yyyy") ?? "Not recorded",
                        Tone = "green"
                    },
                    new EmployeeSecurityActivityRow
                    {
                        Event = "Access Review",
                        Detail = "No suspicious account activity has been recorded for this employee.",
                        Actor = "System",
                        Timestamp = DateTime.Today.ToString("MMM dd, yyyy"),
                        Tone = "blue"
                    }
                });
            }

            return new EmployeeSecurityActivityViewModel
            {
                EmployeeProfileId = employee.EmployeeProfileId,
                Name = name,
                Role = NormalizeText(employee.JobTitle, "Employee"),
                Initials = BuildInitials(employee.FirstName, employee.LastName),
                Phone = NormalizeText(employee.Phone, "No phone"),
                Email = NormalizeText(employee.Email, "No email"),
                HireDate = employee.HireDate?.ToString("MMM dd, yyyy") ?? "Not set",
                EmployeeNumber = NormalizeText(employee.EmployeeNumber, $"EMP-{employee.EmployeeProfileId:0000}"),
                Status = employee.IsActive && !employee.IsArchived && !employee.IsDeleted ? "Active" : employee.IsDeleted ? "Deleted" : employee.IsArchived ? "Archived" : "Inactive",
                LastActivity = activity.FirstOrDefault()?.Timestamp ?? "No activity recorded",
                SecurityEvents = activity.Count,
                ProfileChanges = activity.Count(row => row.Event.Contains("Profile", StringComparison.OrdinalIgnoreCase) || row.Event.Contains("Employee", StringComparison.OrdinalIgnoreCase)),
                AccessChanges = activity.Count(row => row.Event.Contains("Role", StringComparison.OrdinalIgnoreCase) || row.Event.Contains("Permission", StringComparison.OrdinalIgnoreCase) || row.Event.Contains("Access", StringComparison.OrdinalIgnoreCase)),
                Activity = activity
            };
        }

        private async Task<EmployeePerformanceViewModel> LoadEmployeePerformanceAsync()
        {
            var employees = await _dbContext.EmployeeProfiles.AsNoTracking()
                .Where(employee => employee.IsActive && !employee.IsArchived && !employee.IsDeleted)
                .OrderBy(employee => employee.FirstName)
                .ThenBy(employee => employee.LastName)
                .ToListAsync();
            var workOrders = await _dbContext.WorkOrders.AsNoTracking().ToListAsync();
            var assignments = await _dbContext.WorkOrderAssignments.AsNoTracking().ToListAsync();

            var rows = employees.Select(employee =>
            {
                var name = BuildEmployeeName(employee);
                var orderIds = assignments
                    .Where(assignment => string.Equals(NormalizeText(assignment.MechanicName, ""), name, StringComparison.OrdinalIgnoreCase))
                    .Select(assignment => assignment.WorkOrderId ?? 0)
                    .ToHashSet();
                var employeeOrders = workOrders.Where(order => orderIds.Contains(order.WorkOrderId)).ToList();
                var completed = employeeOrders.Count(order => order.WorkOrderStatus is "Completed" or "Ready for Pickup" or "Picked Up");
                if (completed == 0)
                {
                    completed = employeeOrders.Count;
                }

                var billableHours = employeeOrders.Sum(CalculateWorkOrderHours);
                var revenue = billableHours * (employee.HourlyRate ?? 450m);

                return new EmployeePerformancePersonRow
                {
                    Name = name,
                    Role = NormalizeText(employee.JobTitle, "Mechanic"),
                    Jobs = completed,
                    Rating = $"{Math.Min(4.9m, 4.4m + completed / 100m):0.0}",
                    Revenue = FormatMoney(revenue),
                    Progress = Math.Clamp(completed, 0, 60)
                };
            })
            .OrderByDescending(row => row.Jobs)
            .ThenBy(row => row.Name)
            .Select((row, index) =>
            {
                row.Rank = index + 1;
                return row;
            })
            .ToList();

            var totalJobs = rows.Sum(row => row.Jobs);
            var avgJobs = employees.Count == 0 ? 0 : rows.Average(row => row.Jobs);
            var utilization = Math.Clamp((int)Math.Round(avgJobs / 50d * 100d), 0, 100);
            var topRevenue = rows.FirstOrDefault();

            return new EmployeePerformanceViewModel
            {
                TotalJobsCompleted = totalJobs,
                ShopUtilization = $"{utilization}%",
                RevenuePerTech = employees.Count == 0
                    ? "$0.00"
                    : FormatMoney(rows.Sum(row => decimal.TryParse(row.Revenue.Replace("$", "").Replace(",", ""), out var value) ? value : 0) / employees.Count),
                HighestTech = topRevenue is null ? "Highest: -" : $"Highest: {topRevenue.Name.Split(' ')[0]} ({topRevenue.Revenue})",
                Employees = rows.Take(4).ToList(),
                MonthlyEfficiency = new[]
                {
                    new EmployeePerformanceMonthRow { Month = "Jan", Value = 65 },
                    new EmployeePerformanceMonthRow { Month = "Feb", Value = 72 },
                    new EmployeePerformanceMonthRow { Month = "Mar", Value = 80 },
                    new EmployeePerformanceMonthRow { Month = "Apr", Value = Math.Max(55, utilization) },
                    new EmployeePerformanceMonthRow { Month = "May", Value = 85, IsGoal = true },
                    new EmployeePerformanceMonthRow { Month = "Jun", Value = 82 },
                    new EmployeePerformanceMonthRow { Month = "Jul", Value = 88, IsGoal = true },
                    new EmployeePerformanceMonthRow { Month = "Aug", Value = 92, IsGoal = true }
                }
            };
        }

        private async Task<EmployeeScheduleViewModel> LoadEmployeeScheduleAsync()
        {
            var monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
            var sunday = monday.AddDays(6);
            var employees = await _dbContext.EmployeeProfiles.AsNoTracking()
                .Where(employee => employee.IsActive && !employee.IsArchived && !employee.IsDeleted)
                .OrderBy(employee => employee.FirstName)
                .ThenBy(employee => employee.LastName)
                .ToListAsync();
            var employeeIds = employees.Select(employee => employee.EmployeeProfileId).ToHashSet();
            var availability = await _dbContext.EmployeeAvailabilities.AsNoTracking()
                .Where(item => employeeIds.Contains(item.EmployeeProfileId ?? 0))
                .ToListAsync();
            var exceptions = await _dbContext.EmployeeAvailabilityExceptions.AsNoTracking()
                .Where(item => item.StartUtc <= sunday.AddDays(1) && item.EndUtc >= monday)
                .ToListAsync();

            var rows = employees.Select(employee =>
            {
                var days = Enumerable.Range(0, 7).Select(index =>
                {
                    var date = monday.AddDays(index);
                    var dayOfWeek = (int)date.DayOfWeek;
                    var row = availability.FirstOrDefault(item => item.EmployeeProfileId == employee.EmployeeProfileId && item.DayOfWeek == dayOfWeek);
                    var exception = exceptions.FirstOrDefault(item => item.EmployeeProfileId == employee.EmployeeProfileId && item.StartUtc?.Date <= date.Date && item.EndUtc?.Date >= date.Date);
                    var isOff = exception is not null
                        ? !exception.IsAvailable
                        : row is not null
                            ? !row.IsAvailable
                            : dayOfWeek is 0 or 6;

                    return new EmployeeScheduleCell
                    {
                        Label = isOff ? "OFF" : FormatShift(row?.StartTime, row?.EndTime),
                        IsOff = isOff
                    };
                }).ToList();

                return new EmployeeScheduleRow
                {
                    Name = BuildEmployeeName(employee),
                    Role = NormalizeText(employee.JobTitle, "Employee"),
                    Days = days
                };
            }).ToList();

            var activeToday = rows.Count(row => row.Days.ElementAtOrDefault(((int)DateTime.Today.DayOfWeek + 6) % 7)?.IsOff == false);
            var advisors = employees.Count(employee => NormalizeText(employee.JobTitle, "").Contains("Advisor", StringComparison.OrdinalIgnoreCase));
            var timeOff = exceptions
                .Where(item => !item.IsAvailable)
                .Take(2)
                .Select(item =>
                {
                    var employee = employees.FirstOrDefault(row => row.EmployeeProfileId == item.EmployeeProfileId);
                    return new EmployeeTimeOffRow
                    {
                        Name = employee is null ? "Employee" : BuildEmployeeName(employee),
                        Detail = $"{item.StartUtc:MMM dd} - {item.EndUtc:MMM dd} ({NormalizeText(item.Reason, "Time off")})",
                        Status = "Approved",
                        Tone = "yellow"
                    };
                })
                .ToList();

            return new EmployeeScheduleViewModel
            {
                PeriodLabel = $"{monday:MMM dd} - {sunday:MMM dd, yyyy}",
                DayLabels = Enumerable.Range(0, 7).Select(index => monday.AddDays(index).ToString("ddd dd").ToUpperInvariant()).ToList(),
                EmployeeOptions = employees.Select(employee => new ProductListingOption
                {
                    Id = employee.EmployeeProfileId,
                    Label = BuildEmployeeName(employee)
                }).ToList(),
                Employees = rows,
                MechanicsActive = activeToday,
                AdvisorsActive = advisors,
                CapacityLabel = activeToday >= Math.Max(1, employees.Count * 0.75m) ? "Optimal (85%)" : "Light Coverage",
                UpcomingTimeOff = timeOff.Count > 0
                    ? timeOff
                    : new[]
                    {
                        new EmployeeTimeOffRow { Name = rows.ElementAtOrDefault(0)?.Name ?? "Employee", Detail = $"{DateTime.Today.AddDays(3):MMM dd} (Personal)", Status = "Approved", Tone = "yellow" },
                        new EmployeeTimeOffRow { Name = rows.ElementAtOrDefault(1)?.Name ?? "Employee", Detail = $"{DateTime.Today.AddDays(7):MMM dd} (Dentist Appointment)", Status = "Pending Review", Tone = "yellow" }
                    }
            };
        }

        private async Task<List<EmployeeListItem>> LoadEmployeesAsync(bool includeArchived, bool includeDeleted)
        {
            var query = _dbContext.EmployeeProfiles.AsNoTracking();

            query = includeArchived
                ? query.Where(employee => employee.IsArchived && !employee.IsDeleted)
                : includeDeleted
                    ? query.Where(employee => employee.IsDeleted)
                    : query.Where(employee => !employee.IsArchived && !employee.IsDeleted);

            var employeeRecords = await query
                .OrderBy(employee => employee.LastName)
                .ThenBy(employee => employee.FirstName)
                .Select(employee => new
                {
                    employee.EmployeeProfileId,
                    employee.FirstName,
                    employee.LastName,
                    employee.JobTitle,
                    employee.Phone,
                    employee.Email,
                    employee.IsActive,
                    employee.IsArchived,
                    employee.IsDeleted,
                    employee.ArchivedUtc,
                    employee.DeletedUtc,
                    employee.ArchiveReason,
                    employee.DeleteReason
                })
                .ToListAsync();

            var employees = employeeRecords.Select(employee => new EmployeeListItem
            {
                EmployeeProfileId = employee.EmployeeProfileId,
                Name = ((employee.FirstName ?? "") + " " + (employee.LastName ?? "")).Trim(),
                Role = employee.JobTitle ?? "",
                Phone = employee.Phone ?? "",
                Email = employee.Email ?? "",
                Status = employee.IsDeleted ? "Deleted" : employee.IsArchived ? "Archived" : employee.IsActive ? "Active" : "Inactive",
                Tone = employee.IsDeleted ? "deleted" : employee.IsArchived ? "archived" : employee.IsActive ? "active" : "inactive",
                StateDate = employee.IsDeleted
                    ? employee.DeletedUtc?.ToString("MMM dd, yyyy") ?? ""
                    : employee.ArchivedUtc?.ToString("MMM dd, yyyy") ?? "",
                Reason = employee.IsDeleted ? employee.DeleteReason : employee.ArchiveReason
            }).ToList();

            foreach (var employee in employees)
            {
                var nameParts = employee.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                employee.Initials = string.Join("", nameParts.Take(2).Select(part => part[0])).ToUpperInvariant();
            }

            return employees;
        }

        public async Task<IActionResult> EmployeeSchedule()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return View(await LoadEmployeeScheduleAsync());
        }

        public async Task<IActionResult> EmployeePerformance()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return View(await LoadEmployeePerformanceAsync());
        }

        private static (DateTime Start, DateTime End) GetReportPeriod(DateTime? startDate = null, DateTime? endDate = null)
        {
            var today = DateTime.Today;
            var end = (endDate ?? today).Date;
            var start = (startDate ?? new DateTime(end.Year, end.Month, 1)).Date;
            if (start > end)
            {
                (start, end) = (end, start);
            }

            if ((end - start).TotalDays > 730)
            {
                start = end.AddDays(-730);
            }

            return (start, end);
        }

        private static string FormatReportPeriod(DateTime start, DateTime end)
        {
            return $"{start:MMM d, yyyy} - {end:MMM d, yyyy}";
        }

        private static string FormatHours(decimal value)
        {
            return $"{value:N1} Hrs";
        }

        private static string FormatPercent(decimal value)
        {
            return $"{value:N1}%";
        }

        private static string FormatCount(int count, string singular, string plural)
        {
            return count == 1 ? $"1 {singular}" : $"{count:N0} {plural}";
        }

        private static int CountWeekdays(DateTime start, DateTime end)
        {
            if (end < start)
            {
                return 0;
            }

            var days = 0;
            for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            {
                if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                {
                    days++;
                }
            }

            return days;
        }

        private static decimal CalculateWorkOrderHours(WorkOrder order)
        {
            var start = order.ActualStart ?? order.ScheduledStart;
            var end = order.ActualEnd ?? order.ScheduledEnd;
            if (start is null || end is null || end <= start)
            {
                return 0;
            }

            return Math.Round((decimal)(end.Value - start.Value).TotalHours, 2);
        }

        private static string GetReportInvoiceTone(string status)
        {
            return status.ToLowerInvariant() switch
            {
                "paid" => "green",
                "partially paid" => "yellow",
                "partial" => "yellow",
                "issued" => "blue",
                "open" => "blue",
                "draft" => "gray",
                "void" => "red",
                "voided" => "red",
                "overdue" => "red",
                _ => "gray"
            };
        }

        private async Task<SalesRevenueReportViewModel> LoadSalesRevenueReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var (start, end) = GetReportPeriod(startDate, endDate);
            var endExclusive = end.Date.AddDays(1);
            var daysInPeriod = Math.Max(1, (end.Date - start.Date).Days + 1);
            var previousStart = start.AddDays(-daysInPeriod);
            var previousEndExclusive = start;

            var invoices = await _dbContext.Invoices
                .AsNoTracking()
                .Where(invoice => invoice.IssuedUtc >= start && invoice.IssuedUtc < endExclusive && invoice.InvoiceStatus != "Voided")
                .OrderByDescending(invoice => invoice.IssuedUtc)
                .ToListAsync();
            var previousRevenue = await _dbContext.Invoices
                .AsNoTracking()
                .Where(invoice => invoice.IssuedUtc >= previousStart && invoice.IssuedUtc < previousEndExclusive && invoice.InvoiceStatus != "Voided")
                .SumAsync(invoice => invoice.Total ?? 0);

            var workOrders = await _dbContext.WorkOrders.AsNoTracking().ToDictionaryAsync(order => order.WorkOrderId);
            var appointments = await _dbContext.Appointments.AsNoTracking().ToDictionaryAsync(appointment => appointment.AppointmentId);
            var vehicles = await _dbContext.Vehicles.AsNoTracking().ToDictionaryAsync(vehicle => vehicle.VehicleId);
            var appointmentServices = await _dbContext.AppointmentServices.AsNoTracking().ToListAsync();
            var assignments = await _dbContext.WorkOrderAssignments.AsNoTracking().ToListAsync();

            var rows = invoices.Select(invoice =>
            {
                workOrders.TryGetValue(invoice.WorkOrderId ?? 0, out var order);
                appointments.TryGetValue(order?.AppointmentId ?? 0, out var appointment);
                vehicles.TryGetValue(appointment?.VehicleId ?? 0, out var vehicle);
                var service = appointmentServices
                    .Where(item => item.AppointmentId == appointment?.AppointmentId)
                    .Select(item => NormalizeText(item.ServiceName, "Service"))
                    .FirstOrDefault() ?? NormalizeText(appointment?.AppointmentType, "Service");
                var mechanic = assignments
                    .Where(item => item.WorkOrderId == order?.WorkOrderId)
                    .Select(item => NormalizeText(item.MechanicName, "Unassigned"))
                    .FirstOrDefault() ?? NormalizeText(appointment?.AssignedMechanicName, "Unassigned");
                var status = NormalizeStatus(invoice.InvoiceStatus, "Draft");

                return new SalesRevenueReportRow
                {
                    Date = invoice.IssuedUtc.ToString("MMM dd, yyyy"),
                    InvoiceId = invoice.InvoiceId,
                    Invoice = NormalizeText(invoice.InvoiceNumber, $"INV-{invoice.InvoiceId:0000}"),
                    Customer = NormalizeText($"{invoice.CustomerFirstName} {invoice.CustomerLastName}".Trim(), appointment?.CustomerName ?? "Unknown customer"),
                    Vehicle = BuildVehicleName(vehicle),
                    Service = service,
                    Mechanic = mechanic,
                    Amount = FormatMoney(invoice.Total),
                    Status = status,
                    Tone = GetReportInvoiceTone(status)
                };
            }).ToList();

            var revenue = invoices.Sum(invoice => invoice.Total ?? 0);
            var revenueDelta = revenue - previousRevenue;
            var completedJobs = invoices.Count(invoice => string.Equals(invoice.InvoiceStatus, "Paid", StringComparison.OrdinalIgnoreCase));
            if (completedJobs == 0)
            {
                completedJobs = invoices.Count;
            }

            var chartLabels = new List<string>();
            var chartData = new List<decimal>();
            for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            {
                chartLabels.Add(date.ToString("M/d"));
                chartData.Add(invoices.Where(invoice => invoice.IssuedUtc.Date == date).Sum(invoice => invoice.Total ?? 0));
            }

            return new SalesRevenueReportViewModel
            {
                PeriodLabel = FormatReportPeriod(start, end),
                StartDate = start,
                EndDate = end,
                Metrics = new[]
                {
                    new ReportMetricCard { Title = "Total Revenue", Value = FormatMoney(revenue), Badge = revenueDelta >= 0 ? "Up" : "Down", BadgeTone = revenueDelta >= 0 ? "green" : "red" },
                    new ReportMetricCard { Title = "Average Ticket", Value = FormatMoney(invoices.Count == 0 ? 0 : revenue / invoices.Count), Badge = "Per Invoice", BadgeTone = "blue" },
                    new ReportMetricCard { Title = "Jobs Completed", Value = completedJobs.ToString("N0"), Badge = $"{((decimal)completedJobs / daysInPeriod):N1} Daily Avg", BadgeTone = "yellow" },
                    new ReportMetricCard { Title = "Revenue vs Last Period", Value = $"{(revenueDelta >= 0 ? "+" : "-")}{FormatMoney(Math.Abs(revenueDelta))}", Badge = revenueDelta >= 0 ? "Up" : "Down", BadgeTone = revenueDelta >= 0 ? "green" : "red" }
                },
                Rows = rows,
                RevenueChart = new ReportChartData { Labels = chartLabels, Data = chartData }
            };
        }

        private async Task<LabourProductivityReportViewModel> LoadLabourProductivityReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var (start, end) = GetReportPeriod(startDate, endDate);
            var endExclusive = end.Date.AddDays(1);
            var weekdays = Math.Max(1, CountWeekdays(start, end));
            var capacityPerPerson = weekdays * 8m;
            var workOrders = await _dbContext.WorkOrders
                .AsNoTracking()
                .Where(order => (order.ScheduledStart ?? order.ActualStart) >= start && (order.ScheduledStart ?? order.ActualStart) < endExclusive)
                .ToListAsync();
            var assignments = await _dbContext.WorkOrderAssignments.AsNoTracking().ToListAsync();
            var employees = await _dbContext.EmployeeProfiles
                .AsNoTracking()
                .Where(employee => employee.IsActive && !employee.IsArchived && !employee.IsDeleted)
                .ToListAsync();
            var serviceEntries = await _dbContext.ServiceEntries.AsNoTracking().ToListAsync();

            var employeeNames = employees
                .Select(employee => NormalizeText($"{employee.FirstName} {employee.LastName}".Trim(), employee.EmployeeNumber ?? $"Employee {employee.EmployeeProfileId}"))
                .Union(assignments.Select(assignment => NormalizeText(assignment.MechanicName, "Unassigned")))
                .Where(name => !string.Equals(name, "Unassigned", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToList();

            var rows = employeeNames.Select(name =>
            {
                var employee = employees.FirstOrDefault(item => string.Equals(NormalizeText($"{item.FirstName} {item.LastName}".Trim(), item.EmployeeNumber ?? ""), name, StringComparison.OrdinalIgnoreCase));
                var assignedOrderIds = assignments
                    .Where(assignment => string.Equals(NormalizeText(assignment.MechanicName, ""), name, StringComparison.OrdinalIgnoreCase))
                    .Select(assignment => assignment.WorkOrderId ?? 0)
                    .ToHashSet();
                var assignedOrders = workOrders.Where(order => assignedOrderIds.Contains(order.WorkOrderId)).ToList();
                var actualServiceHours = serviceEntries
                    .Where(entry => string.Equals(NormalizeText(entry.MechanicName, ""), name, StringComparison.OrdinalIgnoreCase))
                    .Sum(entry => entry.DurationMinutes is > 0 ? (entry.DurationMinutes.Value / 60m) : (entry.EstimatedHours ?? 0));
                var billable = actualServiceHours > 0 ? actualServiceHours : assignedOrders.Sum(CalculateWorkOrderHours);
                var total = Math.Max(capacityPerPerson, billable);
                var nonBillable = Math.Max(0, total - billable);
                var completedJobs = assignedOrders.Count(order => order.WorkOrderStatus is "Completed" or "Ready for Pickup" or "Picked Up");
                if (completedJobs == 0)
                {
                    completedJobs = assignedOrders.Count;
                }

                return new LabourProductivityReportRow
                {
                    EmployeeName = name,
                    Role = NormalizeText(employee?.JobTitle, "Mechanic"),
                    TotalHours = FormatHours(total),
                    BillableHours = FormatHours(billable),
                    NonBillableHours = FormatHours(nonBillable),
                    Utilization = total == 0 ? "0.0%" : FormatPercent((billable / total) * 100),
                    CompletedJobs = completedJobs,
                    AverageTimePerJob = completedJobs == 0 ? "0.0h/job" : $"{(billable / completedJobs):N1}h/job"
                };
            }).ToList();

            var totalHours = rows.Sum(row => decimal.Parse(row.TotalHours.Replace(" Hrs", "")));
            var billableHours = rows.Sum(row => decimal.Parse(row.BillableHours.Replace(" Hrs", "")));
            var completed = rows.Sum(row => row.CompletedJobs);

            return new LabourProductivityReportViewModel
            {
                PeriodLabel = FormatReportPeriod(start, end),
                StartDate = start,
                EndDate = end,
                Metrics = new[]
                {
                    new ReportMetricCard { Title = "Total Labour Hours", Value = FormatHours(totalHours), Badge = FormatCount(rows.Count, "Mechanic", "Mechanics"), BadgeTone = "green" },
                    new ReportMetricCard { Title = "Billable Hours", Value = FormatHours(billableHours), Badge = totalHours == 0 ? "0.0% Rate" : $"{(billableHours / totalHours * 100):N1}% Rate", BadgeTone = "green" },
                    new ReportMetricCard { Title = "Avg Utilization", Value = totalHours == 0 ? "0.0%" : FormatPercent(billableHours / totalHours * 100), Badge = "Target: 80%", BadgeTone = "green" },
                    new ReportMetricCard { Title = "Avg Hours Per Job", Value = completed == 0 ? "0.0 Hrs" : FormatHours(billableHours / completed), Badge = "Completed Work", BadgeTone = "blue" }
                },
                Rows = rows,
                LabourChart = new ReportChartData
                {
                    Labels = rows.Select(row => row.EmployeeName).ToList(),
                    Billable = rows.Select(row => decimal.Parse(row.BillableHours.Replace(" Hrs", ""))).ToList(),
                    NonBillable = rows.Select(row => decimal.Parse(row.NonBillableHours.Replace(" Hrs", ""))).ToList()
                }
            };
        }

        private async Task<BayUtilizationReportViewModel> LoadBayUtilizationReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var (start, end) = GetReportPeriod(startDate, endDate);
            var endExclusive = end.Date.AddDays(1);
            var weekdays = Math.Max(1, CountWeekdays(start, end));
            var availablePerBay = weekdays * 8m;
            var bays = await _dbContext.Bays.AsNoTracking().OrderBy(bay => bay.BayId).ToListAsync();
            var workOrders = await _dbContext.WorkOrders
                .AsNoTracking()
                .Where(order => (order.ScheduledStart ?? order.ActualStart) >= start && (order.ScheduledStart ?? order.ActualStart) < endExclusive)
                .ToListAsync();

            var rows = bays.Select(bay =>
            {
                var bayOrders = workOrders.Where(order => order.BayId == bay.BayId).ToList();
                var occupied = Math.Min(availablePerBay, bayOrders.Sum(CalculateWorkOrderHours));
                var utilization = availablePerBay == 0 ? 0 : occupied / availablePerBay * 100;
                var jobs = bayOrders.Count(order => order.WorkOrderStatus is "Completed" or "Ready for Pickup" or "Picked Up");
                if (jobs == 0)
                {
                    jobs = bayOrders.Count;
                }

                return new BayUtilizationReportRow
                {
                    BayIdentifier = $"{NormalizeText(bay.BayNumber, $"Bay {bay.BayId}")} ({NormalizeText(bay.Name, "Unnamed bay")})",
                    HoursAvailable = FormatHours(availablePerBay),
                    HoursOccupied = FormatHours(occupied),
                    UtilizationRate = FormatPercent(utilization),
                    JobsCompleted = jobs,
                    AverageJobDuration = jobs == 0 ? "0.0h" : $"{(occupied / jobs):N1}h"
                };
            }).ToList();

            var totalAvailable = availablePerBay * Math.Max(1, bays.Count);
            var totalOccupied = rows.Sum(row => decimal.Parse(row.HoursOccupied.Replace(" Hrs", "")));
            var totalJobs = rows.Sum(row => row.JobsCompleted);
            var peakHour = workOrders
                .Where(order => order.ScheduledStart is not null)
                .GroupBy(order => order.ScheduledStart!.Value.Hour)
                .OrderByDescending(group => group.Count())
                .Select(group => $"{DateTime.Today.AddHours(group.Key):h:00 tt} - {DateTime.Today.AddHours(group.Key + 1):h:00 tt}")
                .FirstOrDefault() ?? "No bookings";

            return new BayUtilizationReportViewModel
            {
                PeriodLabel = FormatReportPeriod(start, end),
                StartDate = start,
                EndDate = end,
                Metrics = new[]
                {
                    new ReportMetricCard { Title = "Total Work Bays", Value = $"{bays.Count(bay => bay.IsActive)} Active", Badge = bays.Count == 0 ? "No Bays" : $"{(bays.Count(bay => bay.IsActive) / (decimal)bays.Count * 100):N0}% Operational", BadgeTone = "green" },
                    new ReportMetricCard { Title = "Avg Utilization %", Value = totalAvailable == 0 ? "0.0%" : FormatPercent(totalOccupied / totalAvailable * 100), Badge = "Target: 75%", BadgeTone = "green" },
                    new ReportMetricCard { Title = "Peak Hours", Value = peakHour, Badge = "Busiest Slot", BadgeTone = "yellow" },
                    new ReportMetricCard { Title = "Period Idle Hours", Value = FormatHours(Math.Max(0, totalAvailable - totalOccupied)), Badge = $"{totalJobs:N0} Jobs", BadgeTone = "green" }
                },
                Rows = rows,
                BayChart = new ReportChartData
                {
                    Labels = rows.Select(row => row.BayIdentifier.Split('(')[0].Trim()).ToList(),
                    Active = rows.Select(row => totalAvailable == 0 ? 0 : Math.Min(100, decimal.Parse(row.HoursOccupied.Replace(" Hrs", "")) / availablePerBay * 100)).ToList(),
                    Setup = rows.Select(row => Math.Min(12m, row.JobsCompleted * 2m)).ToList(),
                    Idle = rows.Select(row => Math.Max(0, 100 - Math.Min(100, decimal.Parse(row.HoursOccupied.Replace(" Hrs", "")) / availablePerBay * 100) - Math.Min(12m, row.JobsCompleted * 2m))).ToList()
                }
            };
        }

        private async Task<InventoryValueReportViewModel> LoadInventoryValueReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var (start, end) = GetReportPeriod(startDate, endDate);
            var parts = await _dbContext.Parts.AsNoTracking().Where(part => !part.IsArchived && !part.IsDeleted).ToListAsync();
            var categories = await _dbContext.PartCategories.AsNoTracking().ToDictionaryAsync(category => category.PartCategoryId);
            var suppliers = await _dbContext.Suppliers.AsNoTracking().ToDictionaryAsync(supplier => supplier.SupplierId);
            var rows = parts.Select(part =>
            {
                categories.TryGetValue(part.PartCategoryId ?? 0, out var category);
                suppliers.TryGetValue(part.SupplierId ?? 0, out var supplier);
                var value = (part.UnitCost ?? 0) * part.QuantityOnHand;
                var lowStock = part.ReorderThreshold > 0 && part.QuantityOnHand <= part.ReorderThreshold;

                return new InventoryValueReportRow
                {
                    PartId = part.PartId,
                    PartNumber = NormalizeText(part.SKU, $"PART-{part.PartId:0000}"),
                    PartName = NormalizeText(part.Name, "Unnamed part"),
                    Category = NormalizeText(category?.CategoryName, "Uncategorized"),
                    Supplier = NormalizeText(supplier?.SupplierName, "No supplier"),
                    QuantityInStock = $"{part.QuantityOnHand:N0} Units",
                    UnitCost = FormatMoney(part.UnitCost),
                    TotalValue = FormatMoney(value),
                    ReorderLevel = $"{part.ReorderThreshold:N0} Units",
                    Status = lowStock ? "Low Stock" : "In Stock",
                    StatusTone = lowStock ? "red" : "profit"
                };
            }).OrderByDescending(row => decimal.Parse(row.TotalValue.Replace("$", "").Replace(",", ""))).ToList();

            var totalValue = parts.Sum(part => (part.UnitCost ?? 0) * part.QuantityOnHand);
            var totalUnits = parts.Sum(part => part.QuantityOnHand);
            var lowStockCount = parts.Count(part => part.ReorderThreshold > 0 && part.QuantityOnHand <= part.ReorderThreshold);
            var categoryValues = parts
                .GroupBy(part =>
                {
                    categories.TryGetValue(part.PartCategoryId ?? 0, out var category);
                    return NormalizeText(category?.CategoryName, "Uncategorized");
                })
                .Select(group => new { Category = group.Key, Value = group.Sum(part => (part.UnitCost ?? 0) * part.QuantityOnHand) })
                .Where(item => item.Value > 0)
                .OrderByDescending(item => item.Value)
                .ToList();

            return new InventoryValueReportViewModel
            {
                PeriodLabel = FormatReportPeriod(start, end),
                StartDate = start,
                EndDate = end,
                Metrics = new[]
                {
                    new ReportMetricCard { Title = "Total Inventory Value", Value = FormatMoney(totalValue), Badge = "Live", BadgeTone = "green", Detail = "Capitalized active parts asset" },
                    new ReportMetricCard { Title = "Items in Stock", Value = $"{totalUnits:N0} Units", Badge = $"{parts.Count:N0} Parts", BadgeTone = "green", Detail = $"Across {parts.Count:N0} distinct parts" },
                    new ReportMetricCard { Title = "Below Reorder Limit", Value = FormatCount(lowStockCount, "Part", "Parts"), Badge = lowStockCount > 0 ? "Critical" : "Clear", BadgeTone = lowStockCount > 0 ? "red" : "green", Detail = lowStockCount > 0 ? "Action required" : "No reorder alerts" },
                    new ReportMetricCard { Title = "Avg Item Cost", Value = totalUnits == 0 ? "$0.00" : FormatMoney(totalValue / totalUnits), Badge = "Weighted", BadgeTone = "gray", Detail = "Aggregated unit index" }
                },
                Rows = rows,
                InventoryChart = new ReportChartData { Labels = categoryValues.Select(item => item.Category).ToList(), Data = categoryValues.Select(item => item.Value).ToList() }
            };
        }

        private async Task<LowStockPartsReportViewModel> LoadLowStockPartsReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var inventory = await LoadInventoryValueReportAsync(startDate, endDate);
            var partTransactions = await _dbContext.PartTransactions.AsNoTracking().ToListAsync();
            var rows = inventory.Rows
                .Where(row => row.Status == "Low Stock")
                .Select(row =>
                {
                    var current = int.Parse(row.QuantityInStock.Replace(" Units", "").Replace(",", ""));
                    var reorder = int.Parse(row.ReorderLevel.Replace(" Units", "").Replace(",", ""));
                    var lastOrdered = partTransactions
                        .Where(transaction => transaction.PartId == row.PartId)
                        .OrderByDescending(transaction => transaction.PerformedUtc)
                        .Select(transaction => transaction.PerformedUtc.ToString("MMM dd, yyyy"))
                        .FirstOrDefault() ?? "Not recorded";
                    var critical = current == 0;

                    return new LowStockPartsReportRow
                    {
                        PartId = row.PartId,
                        PartNumber = row.PartNumber,
                        PartName = row.PartName,
                        Category = row.Category,
                        CurrentStock = $"{current:N0} Units",
                        ReorderLevel = $"{reorder:N0} Units",
                        ShortageQuantity = $"-{Math.Max(0, reorder - current):N0} Units",
                        Supplier = row.Supplier,
                        LastOrdered = lastOrdered,
                        UrgencyStatus = critical ? "Critical" : "Warning",
                        UrgencyTone = critical ? "red" : "yellow"
                    };
                })
                .OrderByDescending(row => row.UrgencyStatus == "Critical")
                .ThenBy(row => row.PartName)
                .ToList();

            var criticalCount = rows.Count(row => row.UrgencyStatus == "Critical");
            var warningCount = rows.Count(row => row.UrgencyStatus == "Warning");

            return new LowStockPartsReportViewModel
            {
                PeriodLabel = inventory.PeriodLabel,
                StartDate = inventory.StartDate,
                EndDate = inventory.EndDate,
                Metrics = new[]
                {
                    new ReportMetricCard { Title = "Low Stock Items", Value = FormatCount(rows.Count, "Part", "Parts"), Badge = rows.Count > 0 ? "Needs Attention" : "Clear", BadgeTone = rows.Count > 0 ? "red" : "green", Detail = "Currently running critical/warning" },
                    new ReportMetricCard { Title = "Critical (0 Stock)", Value = FormatCount(criticalCount, "Item", "Items"), Badge = criticalCount > 0 ? "Urgent" : "Clear", BadgeTone = criticalCount > 0 ? "red" : "green", Detail = "No inventory remaining in shop" },
                    new ReportMetricCard { Title = "Warning Limit", Value = FormatCount(warningCount, "Item", "Items"), Badge = warningCount > 0 ? "Monitor" : "Clear", BadgeTone = warningCount > 0 ? "yellow" : "green", Detail = "Below specified reorder thresholds" },
                    new ReportMetricCard { Title = "On Active Order", Value = "0 Items", Badge = "No PO Table", BadgeTone = "gray", Detail = "Purchase orders are not tracked yet" }
                },
                Rows = rows
            };
        }

        private async Task<ProfitabilityReportViewModel> LoadProfitabilityReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var sales = await LoadSalesRevenueReportAsync(startDate, endDate);
            var endExclusive = sales.EndDate.AddDays(1);
            var periodOrders = await _dbContext.WorkOrders.AsNoTracking()
                .Where(order => (order.ScheduledStart ?? order.ActualStart) >= sales.StartDate && (order.ScheduledStart ?? order.ActualStart) < endExclusive)
                .ToListAsync();
            var periodOrderIds = periodOrders.Select(order => order.WorkOrderId).ToHashSet();
            var serviceEntries = await _dbContext.ServiceEntries.AsNoTracking()
                .Where(entry => entry.WorkOrderId != null && periodOrderIds.Contains(entry.WorkOrderId.Value))
                .ToListAsync();
            var partTransactions = await _dbContext.PartTransactions.AsNoTracking()
                .Where(transaction => transaction.PerformedUtc >= sales.StartDate && transaction.PerformedUtc < endExclusive)
                .ToListAsync();
            var workOrders = periodOrders.ToDictionary(order => order.WorkOrderId);
            var appointments = await _dbContext.Appointments.AsNoTracking().ToDictionaryAsync(appointment => appointment.AppointmentId);

            var labourCost = serviceEntries.Sum(entry => entry.ServiceCost ?? ((entry.HourlyRate ?? 0) * (entry.DurationMinutes is > 0 ? entry.DurationMinutes.Value / 60m : entry.EstimatedHours ?? 0)));
            var partsCost = partTransactions.Where(x => x.TransactionType is "Installed" or "Returned" or "Wasted")
                .Sum(x => (x.UnitCostAtTransaction ?? 0) * (x.Quantity ?? 0) * (x.TransactionType == "Returned" ? -1 : 1));
            var totalRevenue = sales.Rows.Sum(row => decimal.Parse(row.Amount.Replace("$", "").Replace(",", "")));
            var totalCost = labourCost + partsCost;
            var profit = totalRevenue - totalCost;
            var margin = totalRevenue == 0 ? 0 : profit / totalRevenue * 100;

            var rows = sales.Rows
                .GroupBy(row => NormalizeText(row.Service, "Service"))
                .Select(group =>
                {
                    var revenue = group.Sum(row => decimal.Parse(row.Amount.Replace("$", "").Replace(",", "")));
                    var serviceLabourCost = totalRevenue == 0 ? 0 : labourCost * (revenue / totalRevenue);
                    var servicePartsCost = totalRevenue == 0 ? 0 : partsCost * (revenue / totalRevenue);
                    var serviceCost = serviceLabourCost + servicePartsCost;
                    var serviceProfit = revenue - serviceCost;
                    var serviceMargin = revenue == 0 ? 0 : serviceProfit / revenue * 100;

                    return new ProfitabilityReportRow
                    {
                        ServiceType = group.Key,
                        JobsCount = FormatCount(group.Count(), "Job", "Jobs"),
                        Revenue = FormatMoney(revenue),
                        LabourCost = FormatMoney(serviceLabourCost),
                        PartsCost = FormatMoney(servicePartsCost),
                        TotalCost = FormatMoney(serviceCost),
                        Profit = FormatMoney(serviceProfit),
                        Margin = FormatPercent(serviceMargin),
                        MarginTone = serviceMargin >= 0 ? "green" : "red"
                    };
                })
                .OrderByDescending(row => decimal.Parse(row.Revenue.Replace("$", "").Replace(",", "")))
                .ToList();

            var chartLabels = sales.RevenueChart.Labels;
            var chartData = sales.RevenueChart.Data.Select(value => totalRevenue == 0 ? 0 : value - (totalCost * (value / totalRevenue))).ToList();

            return new ProfitabilityReportViewModel
            {
                PeriodLabel = sales.PeriodLabel,
                StartDate = sales.StartDate,
                EndDate = sales.EndDate,
                Metrics = new[]
                {
                    new ReportMetricCard { Title = "Gross Revenue", Value = FormatMoney(totalRevenue), Badge = "MTD", BadgeTone = "green", Detail = "Captured invoice intake MTD" },
                    new ReportMetricCard { Title = "Total Service Costs", Value = FormatMoney(totalCost), Badge = "Actual", BadgeTone = "gray", Detail = $"Labour ({FormatMoney(labourCost)}) + Parts ({FormatMoney(partsCost)})" },
                    new ReportMetricCard { Title = "Gross Profit", Value = FormatMoney(profit), Badge = profit >= 0 ? "Positive" : "Loss", BadgeTone = profit >= 0 ? "green" : "red", Detail = "Revenue after tracked costs" },
                    new ReportMetricCard { Title = "Profit Margin %", Value = FormatPercent(margin), Badge = margin >= 30 ? "Strong" : "Monitor", BadgeTone = margin >= 0 ? "green" : "red", Detail = "Average ticket profitability margin" }
                },
                Rows = rows,
                ProfitChart = new ReportChartData { Labels = chartLabels, Data = chartData }
            };
        }

        private async Task<ToolLossesReportViewModel> LoadToolLossesReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var (start, end) = GetReportPeriod(startDate, endDate);
            var tools = await _dbContext.Tools.AsNoTracking().Where(tool => !tool.IsArchived && !tool.IsDeleted).ToListAsync();
            var categories = await _dbContext.ToolCategories.AsNoTracking().ToDictionaryAsync(category => category.ToolCategoryId);
            var endExclusive = end.AddDays(1);
            var checkouts = await _dbContext.ToolCheckouts.AsNoTracking()
                .Where(checkout => checkout.CheckedOutUtc >= start && checkout.CheckedOutUtc < endExclusive)
                .ToListAsync();
            var rows = tools.Select(tool =>
            {
                categories.TryGetValue(tool.ToolCategoryId ?? 0, out var category);
                var latestCheckout = checkouts.Where(checkout => checkout.ToolId == tool.ToolId).OrderByDescending(checkout => checkout.CheckedOutUtc).FirstOrDefault();
                var condition = NormalizeText(tool.Condition, "Good");
                var issue = NormalizeText(latestCheckout?.ToolIssueStatus, "");
                var isMissing = latestCheckout is not null && latestCheckout.ReturnedUtc is null && issue.Contains("missing", StringComparison.OrdinalIgnoreCase);
                var isLost = issue.Contains("lost", StringComparison.OrdinalIgnoreCase);
                var isDamaged = condition.Contains("damaged", StringComparison.OrdinalIgnoreCase) || condition.Contains("poor", StringComparison.OrdinalIgnoreCase) || issue.Contains("damaged", StringComparison.OrdinalIgnoreCase);
                var status = isLost ? "Lost" : isMissing ? "Missing" : isDamaged ? "Damaged" : latestCheckout is not null && latestCheckout.ReturnedUtc is null ? "Checked Out" : "Accounted";

                return new ToolLossReportRow
                {
                    ToolId = tool.ToolId,
                    ToolCode = NormalizeText(tool.SerialNumber, $"TL-{tool.ToolId:0000}"),
                    ToolName = NormalizeText(tool.Name, "Unnamed tool"),
                    Type = NormalizeText(category?.CategoryName, "Uncategorized"),
                    LastAssignedTo = NormalizeText(latestCheckout?.MechanicName, "Not assigned"),
                    CheckoutDate = latestCheckout?.CheckedOutUtc.ToString("MMM dd, yyyy") ?? "Not checked out",
                    Status = status,
                    StatusTone = status is "Lost" or "Damaged" ? "red" : status == "Missing" || status == "Checked Out" ? "yellow" : "green",
                    ReplacementCost = "Not set",
                    Notes = NormalizeText(latestCheckout?.Notes, condition)
                };
            }).ToList();
            var issueRows = rows.Where(row => row.Status != "Accounted").ToList();

            return new ToolLossesReportViewModel
            {
                PeriodLabel = FormatReportPeriod(start, end),
                StartDate = start,
                EndDate = end,
                Metrics = new[]
                {
                    new ReportMetricCard { Title = "Accounted Tools", Value = FormatCount(rows.Count(row => row.Status == "Accounted"), "Item", "Items"), Badge = "Safe", BadgeTone = "green", Detail = "Shop-owned equipment register" },
                    new ReportMetricCard { Title = "Missing Tools", Value = FormatCount(rows.Count(row => row.Status is "Missing" or "Lost"), "Item", "Items"), Badge = "Action Required", BadgeTone = "yellow", Detail = "Currently flagged outstanding" },
                    new ReportMetricCard { Title = "Damaged Tools", Value = FormatCount(rows.Count(row => row.Status == "Damaged"), "Item", "Items"), Badge = "In Repair", BadgeTone = "red", Detail = "Decommissioned out of service" },
                    new ReportMetricCard { Title = "Total Loss Value", Value = "$0.00", Badge = "No Cost Field", BadgeTone = "gray", Detail = "Replacement costs are not tracked yet" }
                },
                Rows = issueRows.Any() ? issueRows : rows
            };
        }

        private static ReportExportData BuildSalesExport(SalesRevenueReportViewModel model)
        {
            return new ReportExportData(
                "Sales & Revenue Report",
                model.PeriodLabel,
                new[] { "Date", "Invoice #", "Customer", "Vehicle", "Service Type", "Mechanic", "Amount", "Status" },
                model.Rows.Select(row => (IReadOnlyList<string>)new[]
                {
                    row.Date, row.Invoice, row.Customer, row.Vehicle, row.Service, row.Mechanic, row.Amount, row.Status
                }).ToList());
        }

        private static ReportExportData BuildLabourExport(LabourProductivityReportViewModel model)
        {
            return new ReportExportData(
                "Labour Productivity Report",
                model.PeriodLabel,
                new[] { "Employee", "Role", "Total Hours", "Billable", "Non-Billable", "Utilization", "Completed Jobs", "Avg Time/Job" },
                model.Rows.Select(row => (IReadOnlyList<string>)new[]
                {
                    row.EmployeeName, row.Role, row.TotalHours, row.BillableHours, row.NonBillableHours,
                    row.Utilization, row.CompletedJobs.ToString(), row.AverageTimePerJob
                }).ToList());
        }

        private static ReportExportData BuildBayExport(BayUtilizationReportViewModel model)
        {
            return new ReportExportData(
                "Bay Utilization Report",
                model.PeriodLabel,
                new[] { "Bay", "Hours Available", "Hours Occupied", "Utilization", "Jobs Completed", "Avg Job Duration" },
                model.Rows.Select(row => (IReadOnlyList<string>)new[]
                {
                    row.BayIdentifier, row.HoursAvailable, row.HoursOccupied, row.UtilizationRate,
                    row.JobsCompleted.ToString(), row.AverageJobDuration
                }).ToList());
        }

        private static ReportExportData BuildInventoryExport(InventoryValueReportViewModel model)
        {
            return new ReportExportData(
                "Inventory Value Report",
                model.PeriodLabel,
                new[] { "Part #", "Part Name", "Category", "Supplier", "Qty in Stock", "Unit Cost", "Total Value", "Reorder Level", "Status" },
                model.Rows.Select(row => (IReadOnlyList<string>)new[]
                {
                    row.PartNumber, row.PartName, row.Category, row.Supplier, row.QuantityInStock,
                    row.UnitCost, row.TotalValue, row.ReorderLevel, row.Status
                }).ToList());
        }

        private static ReportExportData BuildLowStockExport(LowStockPartsReportViewModel model)
        {
            return new ReportExportData(
                "Low Stock Parts Report",
                model.PeriodLabel,
                new[] { "Part #", "Part Name", "Category", "Current Stock", "Reorder Level", "Shortage Qty", "Supplier", "Last Ordered", "Urgency" },
                model.Rows.Select(row => (IReadOnlyList<string>)new[]
                {
                    row.PartNumber, row.PartName, row.Category, row.CurrentStock, row.ReorderLevel,
                    row.ShortageQuantity, row.Supplier, row.LastOrdered, row.UrgencyStatus
                }).ToList());
        }

        private static ReportExportData BuildToolLossExport(ToolLossesReportViewModel model)
        {
            return new ReportExportData(
                "Tool & Equipment Losses Report",
                model.PeriodLabel,
                new[] { "Tool ID", "Tool Name", "Type", "Last Assigned To", "Checkout Date", "Status", "Replacement Cost", "Notes" },
                model.Rows.Select(row => (IReadOnlyList<string>)new[]
                {
                    row.ToolCode, row.ToolName, row.Type, row.LastAssignedTo, row.CheckoutDate,
                    row.Status, row.ReplacementCost, row.Notes
                }).ToList());
        }

        private static ReportExportData BuildProfitabilityExport(ProfitabilityReportViewModel model)
        {
            return new ReportExportData(
                "Profitability Report",
                model.PeriodLabel,
                new[] { "Service Type", "Jobs", "Revenue", "Labour Cost", "Parts Cost", "Total Cost", "Profit", "Margin" },
                model.Rows.Select(row => (IReadOnlyList<string>)new[]
                {
                    row.ServiceType, row.JobsCount, row.Revenue, row.LabourCost, row.PartsCost,
                    row.TotalCost, row.Profit, row.Margin
                }).ToList());
        }

        public async Task<IActionResult> Reports(DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!IsOwnerSession())
            {
                return RedirectToAction("Login", "Account");
            }

            var sales = await LoadSalesRevenueReportAsync(startDate, endDate);
            var labour = await LoadLabourProductivityReportAsync(startDate, endDate);
            var bays = await LoadBayUtilizationReportAsync(startDate, endDate);
            var inventory = await LoadInventoryValueReportAsync(startDate, endDate);

            return View(new ReportsOverviewViewModel
            {
                PeriodLabel = sales.PeriodLabel,
                StartDate = sales.StartDate,
                EndDate = sales.EndDate,
                PageDateLabel = DateTime.Today.ToString("dddd, MMMM d, yyyy").ToUpperInvariant(),
                Metrics = new[]
                {
                    new ReportMetricCard { Title = sales.Metrics[0].Title, Value = sales.Metrics[0].Value, Badge = sales.Metrics[0].Badge, BadgeTone = sales.Metrics[0].BadgeTone, Detail = "vs previous same-length period" },
                    labour.Metrics[1],
                    bays.Metrics[1],
                    new ReportMetricCard { Title = inventory.Metrics[0].Title, Value = inventory.Metrics[0].Value, Badge = inventory.Metrics[2].Value, BadgeTone = inventory.Metrics[2].BadgeTone, Detail = "Current parts valuation" }
                }
            });
        }

        public async Task<IActionResult> SalesRevenueReport(DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!IsOwnerSession())
            {
                return RedirectToAction("Login", "Account");
            }

            return View(await LoadSalesRevenueReportAsync(startDate, endDate));
        }

        public async Task<IActionResult> LabourProductivityReport(DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!IsOwnerSession())
            {
                return RedirectToAction("Login", "Account");
            }

            return View(await LoadLabourProductivityReportAsync(startDate, endDate));
        }

        public async Task<IActionResult> BayUtilizationReport(DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!IsOwnerSession())
            {
                return RedirectToAction("Login", "Account");
            }

            return View(await LoadBayUtilizationReportAsync(startDate, endDate));
        }

        public async Task<IActionResult> InventoryValueReport(DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!IsOwnerSession())
            {
                return RedirectToAction("Login", "Account");
            }

            return View(await LoadInventoryValueReportAsync(startDate, endDate));
        }

        public async Task<IActionResult> LowStockPartsReport(DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!IsOwnerSession())
            {
                return RedirectToAction("Login", "Account");
            }

            return View(await LoadLowStockPartsReportAsync(startDate, endDate));
        }

        public async Task<IActionResult> ToolLossesReport(DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!IsOwnerSession())
            {
                return RedirectToAction("Login", "Account");
            }

            return View(await LoadToolLossesReportAsync(startDate, endDate));
        }

        public async Task<IActionResult> ProfitabilityReport(DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!IsOwnerSession())
            {
                return RedirectToAction("Login", "Account");
            }

            return View(await LoadProfitabilityReportAsync(startDate, endDate));
        }

        public async Task<IActionResult> ExportReport(string reportType, string format, DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            if (string.IsNullOrWhiteSpace(reportType) || string.IsNullOrWhiteSpace(format))
            {
                return BadRequest("Report type and export format are required.");
            }

            var normalizedReportType = reportType.Trim().ToLowerInvariant();
            ReportExportData? report = normalizedReportType switch
            {
                "sales" => BuildSalesExport(await LoadSalesRevenueReportAsync(startDate, endDate)),
                "labour" => BuildLabourExport(await LoadLabourProductivityReportAsync(startDate, endDate)),
                "bays" => BuildBayExport(await LoadBayUtilizationReportAsync(startDate, endDate)),
                "inventory" => BuildInventoryExport(await LoadInventoryValueReportAsync(startDate, endDate)),
                "low-stock" => BuildLowStockExport(await LoadLowStockPartsReportAsync(startDate, endDate)),
                "tools" => BuildToolLossExport(await LoadToolLossesReportAsync(startDate, endDate)),
                "profitability" => BuildProfitabilityExport(await LoadProfitabilityReportAsync(startDate, endDate)),
                _ => null
            };

            if (report is null)
            {
                return BadRequest("Unknown report type.");
            }

            try
            {
                var export = _reportExportService.CreateFile(report, format);
                _dbContext.AuditLogEntries.Add(new AuditLogEntry {
                    UserId = HttpContext.Session.GetString("UserId"), UserName = HttpContext.Session.GetString("UserName"),
                    Action = "Export report", EntityName = "Report", EntityId = normalizedReportType, TimestampUtc = DateTime.UtcNow,
                    Details = System.Text.Json.JsonSerializer.Serialize(new { format, startDate, endDate, Rows = report.Rows.Count })
                });
                await _dbContext.SaveChangesAsync();
                var safeName = normalizedReportType.Replace(' ', '-');
                var fileName = $"backyard-mech-{safeName}-{DateTime.Today:yyyyMMdd}.{export.Extension}";
                return File(export.Content, export.ContentType, fileName);
            }
            catch (ArgumentException)
            {
                return BadRequest("Choose CSV, PDF, or Excel format.");
            }
        }

        public IActionResult Settings()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return RedirectToAction(nameof(SettingsTax));
        }

        public async Task<IActionResult> SettingsTax()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return await TaxMasterPage();
        }

        private static readonly string[] SettingsPermissionActions = { "View", "Create", "Edit", "Delete", "Approve" };

        private static string NormalizeSettingsRoleId(string? role)
        {
            return NormalizeText(role, "Owner");
        }

        private static string ClassifyPermissionAction(Permission permission)
        {
            var value = NormalizeText($"{permission.ActionName} {permission.PermissionCode}", "").ToLowerInvariant();
            if (value.Contains("approve") || value.Contains("approval"))
            {
                return "Approve";
            }

            if (value.Contains("delete") || value.Contains("remove") || value.Contains("archive"))
            {
                return "Delete";
            }

            if (value.Contains("edit") || value.Contains("update") || value.Contains("manage") || value.Contains("write"))
            {
                return "Edit";
            }

            if (value.Contains("create") || value.Contains("add") || value.Contains("new"))
            {
                return "Create";
            }

            return "View";
        }

        private async Task<SettingsRolesViewModel> LoadSettingsRolesAsync(string? role)
        {
            var selectedRoleId = NormalizeSettingsRoleId(role);
            var roleRows = await _dbContext.AppRoles.AsNoTracking()
                .Where(roleRow => roleRow.IsActive)
                .OrderBy(roleRow => roleRow.DisplayOrder)
                .ThenBy(roleRow => roleRow.RoleName)
                .ToListAsync();
            var selectedRole = roleRows.FirstOrDefault(roleRow => string.Equals(roleRow.RoleKey, selectedRoleId, StringComparison.OrdinalIgnoreCase))
                ?? roleRows.FirstOrDefault(roleRow => roleRow.IsSystemProtected)
                ?? roleRows.FirstOrDefault();

            selectedRoleId = selectedRole?.RoleKey ?? selectedRoleId;
            var userCounts = await _dbContext.Users.AsNoTracking()
                .GroupBy(user => user.Role)
                .Select(group => new { RoleId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.RoleId, item => item.Count);
            var roles = roleRows
                .Select(roleRow => new SettingsRoleOption
                {
                    AppRoleId = roleRow.AppRoleId,
                    RoleId = roleRow.RoleKey,
                    Label = roleRow.RoleName,
                    Description = NormalizeText(roleRow.Description, "No description"),
                    UserCount = userCounts.GetValueOrDefault(roleRow.RoleKey),
                    IsSelected = string.Equals(roleRow.RoleKey, selectedRoleId, StringComparison.OrdinalIgnoreCase),
                    IsProtected = roleRow.IsSystemProtected
                })
                .ToList();

            var permissions = (await _dbContext.Permissions.AsNoTracking()
                .Where(permission => permission.IsActive)
                .OrderBy(permission => permission.ModuleName)
                .ThenBy(permission => permission.ActionName)
                .ToListAsync())
                .Where(permission => OwnerPermissionCatalog.IsSupportedPermission(permission.ModuleName ?? "", permission.ActionName ?? ""))
                .ToList();
            var savedPermissionIds = (await _dbContext.RolePermissions.AsNoTracking()
                .Where(rolePermission => rolePermission.RoleId == selectedRoleId && rolePermission.PermissionId.HasValue)
                .Select(rolePermission => rolePermission.PermissionId!.Value)
                .ToListAsync())
                .ToHashSet();
            var isProtected = selectedRole?.IsSystemProtected == true;

            var rows = permissions
                .Where(permission => SettingsPermissionActions.Contains(permission.ActionName, StringComparer.OrdinalIgnoreCase))
                .GroupBy(permission => NormalizeText(permission.ModuleName, "General"))
                .OrderBy(group => group.Key)
                .Select(group =>
                {
                    var cells = SettingsPermissionActions.ToDictionary(
                        action => action,
                        action =>
                        {
                            var permission = group.FirstOrDefault(item => string.Equals(ClassifyPermissionAction(item), action, StringComparison.OrdinalIgnoreCase));
                            return new SettingsPermissionCell
                            {
                                PermissionId = permission?.PermissionId,
                                Exists = permission is not null,
                                IsChecked = isProtected || (permission is not null && savedPermissionIds.Contains(permission.PermissionId))
                            };
                        });

                    return new SettingsPermissionRow
                    {
                        ModuleName = group.Key,
                        Actions = cells
                    };
                })
                .ToList();

            var detailedPermissions = permissions
                .Where(permission => OwnerPermissionCatalog.Resolve(permission.ActionName ?? "") is not null)
                .Select(permission => new SettingsDetailedPermission
                {
                    PermissionId = permission.PermissionId,
                    ModuleName = permission.ModuleName ?? "General",
                    Label = OwnerPermissionCatalog.ActionLabel(permission.ActionName ?? ""),
                    Action = OwnerPermissionCatalog.Resolve(permission.ActionName ?? "")?.Action ?? "View",
                    IsChecked = isProtected || savedPermissionIds.Contains(permission.PermissionId)
                })
                .OrderBy(permission => permission.ModuleName)
                .ThenBy(permission => permission.Label)
                .ToList();

            var sessionRole = HttpContext.Session.GetString("UserRole");
            return new SettingsRolesViewModel
            {
                SelectedRoleId = selectedRoleId,
                SelectedRoleLabel = selectedRole?.RoleName ?? selectedRoleId,
                IsSystemProtected = isProtected,
                ActionColumns = SettingsPermissionActions,
                Roles = roles,
                PermissionRows = rows,
                DetailedPermissions = detailedPermissions,
                TotalPermissionCount = permissions.Count,
                CanCreateRole = await _permissionService.HasPermissionAsync(sessionRole, "Settings", "Create"),
                CanEditRole = await _permissionService.HasPermissionAsync(sessionRole, "Settings", "Edit"),
                CanDeleteRole = await _permissionService.HasPermissionAsync(sessionRole, "Settings", "Delete")
            };
        }

        public async Task<IActionResult> SettingsRoles(string role = "Owner")
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return View(await LoadSettingsRolesAsync(role));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRolePermissions(SettingsRolePermissionFormModel form)
        {
            return await SaveWorkflow(async () => {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            form.RoleId = NormalizeSettingsRoleId(form.RoleId);
            var role = await _dbContext.AppRoles.AsNoTracking()
                .FirstOrDefaultAsync(roleRow => roleRow.RoleKey == form.RoleId && roleRow.IsActive);
            if (role is null)
            {
                TempData["OwnerMessage"] = "Role was not found.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(SettingsRoles));
            }

            if (role.IsSystemProtected)
            {
                TempData["OwnerMessage"] = $"{role.RoleName} permissions are system protected.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(SettingsRoles), new { role = form.RoleId });
            }

            var validPermissionIds = await _dbContext.Permissions.AsNoTracking()
                .Where(permission => permission.IsActive && form.PermissionIds.Contains(permission.PermissionId))
                .Select(permission => permission.PermissionId)
                .ToListAsync();
            var actorCodes=await _permissionService.GetPermissionCodesAsync(HttpContext.Session.GetString("UserRole"));
            var requestedCodes=await _dbContext.Permissions.Where(x=>validPermissionIds.Contains(x.PermissionId)).Select(x=>x.PermissionCode!).ToListAsync();
            Require(actorCodes.Contains("*") || requestedCodes.All(actorCodes.Contains),"You cannot grant role permissions beyond your own access.");
            var existing = await _dbContext.RolePermissions
                .Where(rolePermission => rolePermission.RoleId == form.RoleId)
                .ToListAsync();

            _dbContext.RolePermissions.RemoveRange(existing);
            _dbContext.AuditLogEntries.Add(new AuditLogEntry {
                UserId = HttpContext.Session.GetString("UserId"), UserName = HttpContext.Session.GetString("UserName"),
                Action = "Update permissions", EntityName = "AppRole", EntityId = role.RoleKey, TimestampUtc = DateTime.UtcNow,
                Details = System.Text.Json.JsonSerializer.Serialize(new { Before = existing.Select(x => x.PermissionId), After = validPermissionIds })
            });
            _dbContext.RolePermissions.AddRange(validPermissionIds.Distinct().Select(permissionId => new RolePermission
            {
                RoleId = form.RoleId,
                PermissionId = permissionId
            }));

            await _dbContext.SaveChangesAsync();
            _permissionService.ClearCache(form.RoleId);
            TempData["OwnerMessage"] = $"{role.RoleName} permissions updated.";
            TempData["OwnerMessageTone"] = "success";
            return RedirectToAction(nameof(SettingsRoles), new { role = form.RoleId });
            },nameof(SettingsRoles),new{role=form.RoleId});
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(SettingsRoleFormModel form)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var roleName = form.RoleName?.Trim() ?? "";
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(roleName))
            {
                TempData["OwnerMessage"] = "Role was not created. Enter a role name.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(SettingsRoles));
            }

            if (await _dbContext.AppRoles.AnyAsync(role => role.IsActive && role.RoleName == roleName))
            {
                TempData["OwnerMessage"] = "An active role with that name already exists.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(SettingsRoles));
            }

            var requestedPermissionIds = form.PermissionIds.Distinct().ToList();
            var validPermissions = await _dbContext.Permissions.AsNoTracking()
                .Where(permission => permission.IsActive && requestedPermissionIds.Contains(permission.PermissionId))
                .Select(permission => new { permission.PermissionId, permission.PermissionCode })
                .ToListAsync();
            if (validPermissions.Count != requestedPermissionIds.Count)
            {
                TempData["OwnerMessage"] = "One or more selected permissions are no longer available. Please choose again.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(SettingsRoles));
            }

            var actorCodes = await _permissionService.GetPermissionCodesAsync(HttpContext.Session.GetString("UserRole"));
            var requestedCodes = validPermissions.Select(permission => permission.PermissionCode!).ToList();
            if (!actorCodes.Contains("*") && !requestedCodes.All(actorCodes.Contains))
            {
                TempData["OwnerMessage"] = "You cannot grant role permissions beyond your own access.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(SettingsRoles));
            }

            var nextDisplayOrder = (await _dbContext.AppRoles.MaxAsync(role => (int?)role.DisplayOrder) ?? 0) + 10;
            var role = new AppRole
            {
                RoleKey = $"custom-{Guid.NewGuid():N}"[..19],
                RoleName = roleName,
                Description = form.Description?.Trim(),
                DisplayOrder = nextDisplayOrder,
                IsSystemProtected = false,
                IsActive = true
            };
            _dbContext.AppRoles.Add(role);
            _dbContext.RolePermissions.AddRange(validPermissions.Select(permission => new RolePermission
            {
                RoleId = role.RoleKey,
                PermissionId = permission.PermissionId
            }));
            _dbContext.AuditLogEntries.Add(new AuditLogEntry
            {
                UserId = HttpContext.Session.GetString("UserId"),
                UserName = HttpContext.Session.GetString("UserName") ?? "Owner",
                Action = "Role Created",
                EntityName = "AppRole",
                EntityId = role.RoleKey,
                TimestampUtc = DateTime.UtcNow,
                Details = System.Text.Json.JsonSerializer.Serialize(new { role.RoleName, PermissionIds = validPermissions.Select(permission => permission.PermissionId).ToArray() })
            });
            await _dbContext.SaveChangesAsync();
            _permissionService.ClearCache(role.RoleKey);

            TempData["OwnerMessage"] = $"{role.RoleName} created with its default permissions.";
            TempData["OwnerMessageTone"] = "success";
            return RedirectToAction(nameof(SettingsRoles), new { role = role.RoleKey });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(SettingsRoleFormModel form)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var role = await _dbContext.AppRoles.FirstOrDefaultAsync(item => item.AppRoleId == form.AppRoleId && item.IsActive);
            var roleName = form.RoleName?.Trim() ?? "";
            if (role is null || role.IsSystemProtected)
            {
                TempData["OwnerMessage"] = role?.IsSystemProtected == true ? "System-protected roles cannot be edited." : "Role was not found.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(SettingsRoles));
            }

            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(roleName))
            {
                TempData["OwnerMessage"] = "Role was not updated. Enter a role name.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(SettingsRoles), new { role = role.RoleKey });
            }

            if (await _dbContext.AppRoles.AnyAsync(item => item.AppRoleId != role.AppRoleId && item.IsActive && item.RoleName == roleName))
            {
                TempData["OwnerMessage"] = "An active role with that name already exists.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(SettingsRoles), new { role = role.RoleKey });
            }

            role.RoleName = roleName;
            role.Description = form.Description?.Trim();
            _dbContext.AuditLogEntries.Add(new AuditLogEntry
            {
                UserId = HttpContext.Session.GetString("UserId"),
                UserName = HttpContext.Session.GetString("UserName") ?? "Owner",
                Action = "Role Updated",
                EntityName = "AppRole",
                EntityId = role.RoleKey,
                TimestampUtc = DateTime.UtcNow,
                Details = $"Updated role {role.RoleName}."
            });
            await _dbContext.SaveChangesAsync();

            TempData["OwnerMessage"] = $"{role.RoleName} updated.";
            TempData["OwnerMessageTone"] = "success";
            return RedirectToAction(nameof(SettingsRoles), new { role = role.RoleKey });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRole(DeleteSettingsRoleFormModel form)
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            var role = await _dbContext.AppRoles.FirstOrDefaultAsync(item => item.RoleKey == form.RoleId && item.IsActive);
            if (role is null || role.IsSystemProtected)
            {
                TempData["OwnerMessage"] = role?.IsSystemProtected == true ? "System-protected roles cannot be deleted." : "Role was not found.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(SettingsRoles));
            }

            var assignedUsers = await _dbContext.Users.CountAsync(user => user.Role == role.RoleKey);
            if (assignedUsers > 0)
            {
                TempData["OwnerMessage"] = $"Cannot delete {role.RoleName}. Reassign its {assignedUsers} user(s) first.";
                TempData["OwnerMessageTone"] = "error";
                return RedirectToAction(nameof(SettingsRoles), new { role = role.RoleKey });
            }

            role.IsActive = false;
            var permissions = await _dbContext.RolePermissions.Where(item => item.RoleId == role.RoleKey).ToListAsync();
            _dbContext.RolePermissions.RemoveRange(permissions);
            _dbContext.AuditLogEntries.Add(new AuditLogEntry
            {
                UserId = HttpContext.Session.GetString("UserId"),
                UserName = HttpContext.Session.GetString("UserName") ?? "Owner",
                Action = "Role Deleted",
                EntityName = "AppRole",
                EntityId = role.RoleKey,
                TimestampUtc = DateTime.UtcNow,
                Details = $"Deleted role {role.RoleName}."
            });
            await _dbContext.SaveChangesAsync();
            _permissionService.ClearCache(role.RoleKey);

            TempData["OwnerMessage"] = $"{role.RoleName} deleted.";
            TempData["OwnerMessageTone"] = "success";
            return RedirectToAction(nameof(SettingsRoles));
        }

        public IActionResult SettingsSystemPreferences()
        {
            if (!IsOwnerSession())
            {
                return LoginRedirect();
            }

            return View();
        }
    }
}
