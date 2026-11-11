using Backyard_Mech.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    private DateTime GarageNow() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GarageZone());
    private static string OwnerName(Customer c) => string.Join(" ", new[] { c.FirstName, c.MiddleName, c.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
    private static string StaffName(EmployeeProfile e) => NormalizeText($"{e.FirstName} {e.LastName}".Trim(), e.Email ?? "Employee");
    private Task<bool> IntakePermission(string module, string action) => _permissionService.HasPermissionAsync(HttpContext.Session.GetString("UserRole"), module, action);
    private async Task<string> VehicleStamp(Vehicle v) => MasterStamp(new { Vehicle = v,
        LastReadingId = await _dbContext.VehicleOdometerHistories.Where(x => x.VehicleId == v.VehicleId).MaxAsync(x => (int?)x.VehicleOdometerHistoryId),
        Owners = await _dbContext.VehicleOwnerships.AsNoTracking().Where(x => x.VehicleId == v.VehicleId).OrderBy(x => x.VehicleOwnershipId).ToListAsync() });
    private async Task<string> AppointmentStamp(Appointment a) => MasterStamp(new { Appointment = a, Services = await _dbContext.AppointmentServices.AsNoTracking().Where(x => x.AppointmentId == a.AppointmentId).OrderBy(x => x.AppointmentServiceId).ToListAsync() });

    public async Task<IActionResult> VehicleHistory(int? id = null, int? customerId = null)
    {
        var vehicles = _dbContext.Vehicles.AsNoTracking().Where(x => !x.IsDeleted);
        if (customerId.HasValue) vehicles = vehicles.Where(v => _dbContext.VehicleOwnerships.Any(o => o.VehicleId == v.VehicleId && o.CustomerId == customerId));
        var page = new VehicleHistoryPage { Vehicles = await vehicles.OrderBy(x => x.LicensePlate).ToListAsync(),
            Customers = await _dbContext.Customers.AsNoTracking().Where(x => x.IsActive && !x.IsArchived && !x.IsDeleted).OrderBy(x => x.FirstName).ToListAsync(),
            CanCreate = await IntakePermission("Vehicles", "Create"), CanEdit = await IntakePermission("Vehicles", "Edit") };
        if (id.HasValue)
        {
            page.Selected = await _dbContext.Vehicles.AsNoTracking().FirstOrDefaultAsync(x => x.VehicleId == id && !x.IsDeleted);
            if (page.Selected is null) return NotFound();
            page.Expected = await VehicleStamp(page.Selected);
            page.Owners = await _dbContext.VehicleOwnerships.AsNoTracking().Where(x => x.VehicleId == id).OrderByDescending(x => x.OwnershipStartUtc).ToListAsync();
            page.Readings = await _dbContext.VehicleOdometerHistories.AsNoTracking().Where(x => x.VehicleId == id).OrderByDescending(x => x.RecordedUtc).ToListAsync();
        }
        return View("~/Views/Owner/Customers/VehicleHistory.cshtml", page);
    }

    private Task<IActionResult> RegisterVehicle(VehicleFormModel f) => SaveWorkflow(async () => {
        var customer = await ActiveIntakeCustomer(f.CustomerId);
        Require(f.Year is >= 1886 && f.Year <= GarageNow().Year + 1, "Enter a valid model year.");
        Require(f.Odometer is null or >= 0, "Odometer cannot be negative.");
        Require(f.Make.Trim().Length <= 100 && f.Model.Trim().Length <= 100 && (f.Color?.Length ?? 0) <= 50 && f.VIN.Trim().Length <= 50 && f.LicensePlate.Trim().Length <= 50, "One or more vehicle fields are too long.");
        var vin = f.VIN.Trim().ToUpperInvariant(); var plate = f.LicensePlate.Trim().ToUpperInvariant();
        Require(!await _dbContext.Vehicles.AnyAsync(x => x.VIN == vin || x.LicensePlate == plate), "This VIN or plate is already registered, including archived records. Find the existing vehicle instead.");
        var vehicle = new Vehicle { VIN = vin, LicensePlate = plate, Make = f.Make.Trim(), Model = f.Model.Trim(), Year = f.Year, Color = Clean(f.Color), OwnerFullName = OwnerName(customer), CreatedUtc = DateTime.UtcNow };
        _dbContext.Vehicles.Add(vehicle); await _dbContext.SaveChangesAsync();
        AddOwnership(vehicle, customer, DateTime.UtcNow);
        if (f.Odometer.HasValue) await AppendOdometer(vehicle, f.Odometer.Value);
        WorkflowAudit("Vehicle", vehicle.VehicleId, "Register", new { vehicle.VIN, vehicle.LicensePlate, vehicle.OwnerFullName });
        return RedirectToAction(nameof(VehicleHistory), new { id = vehicle.VehicleId });
    }, nameof(VehicleHistory));

    private async Task<Customer> ActiveIntakeCustomer(int id)
    {
        var row = await _dbContext.Customers.FirstOrDefaultAsync(x => x.CustomerId == id && x.IsActive && !x.IsArchived && !x.IsDeleted);
        Require(row is not null, "Choose an active customer."); return row;
    }
    private async Task<Vehicle> ActiveIntakeVehicle(int id)
    {
        var row = await _dbContext.Vehicles.FirstOrDefaultAsync(x => x.VehicleId == id && !x.IsArchived && !x.IsDeleted);
        Require(row is not null, "Choose an active vehicle."); return row;
    }
    private void AddOwnership(Vehicle v, Customer c, DateTime now) => _dbContext.VehicleOwnerships.Add(new VehicleOwnership {
        VehicleId = v.VehicleId, CustomerId = c.CustomerId, OwnerFirstName = c.FirstName, OwnerMiddleName = c.MiddleName, OwnerLastName = c.LastName, OwnershipStartUtc = now });

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> TransferVehicleOwnership(TransferVehicleForm f) => SaveWorkflow(async () => {
        var v = await ActiveIntakeVehicle(f.VehicleId);
        Require(await VehicleStamp(v) == f.Expected, "Vehicle ownership changed. Refresh before saving.");
        Require(f.AuthorityVerified, "Verify the customer's authority to transfer ownership first.");
        var customer = await ActiveIntakeCustomer(f.CustomerId);
        var owners = await _dbContext.VehicleOwnerships.Where(x => x.VehicleId == f.VehicleId && x.OwnershipEndUtc == null).ToListAsync();
        Require(!owners.Any(x => x.CustomerId == customer.CustomerId), "This customer is already the current owner.");
        Require(!await _dbContext.Appointments.AnyAsync(x => x.VehicleId == f.VehicleId && x.AppointmentStatus != "Cancelled" && x.AppointmentStatus != "Completed" && x.AppointmentStatus != "No Show"), "Complete or cancel open appointments and repair visits before transferring ownership.");
        Require(!await _dbContext.WorkOrders.AnyAsync(w => w.WorkOrderStatus != "Cancelled" && w.WorkOrderStatus != "Completed" && w.WorkOrderStatus != "Picked Up" &&
            _dbContext.Appointments.Any(a => a.AppointmentId == w.AppointmentId && a.VehicleId == f.VehicleId)), "Close the vehicle's work orders before transferring ownership.");
        var now = DateTime.UtcNow;
        foreach (var owner in owners) owner.OwnershipEndUtc = now;
        AddOwnership(v, customer, now); v.OwnerFullName = OwnerName(customer);
        WorkflowAudit("Vehicle", v.VehicleId, "Ownership transfer", new { PreviousOwners = owners, NewOwner = v.OwnerFullName, f.Reason, f.AuthorityVerified });
        return RedirectToAction(nameof(VehicleHistory), new { id = v.VehicleId });
    }, nameof(VehicleHistory), new { id = f.VehicleId });

    private async Task AppendOdometer(Vehicle v, int value, int? appointmentId = null)
    {
        var highest = await _dbContext.VehicleOdometerHistories.Where(x => x.VehicleId == v.VehicleId).MaxAsync(x => x.Odometer);
        Require(value >= 0 && value >= Math.Max(v.Odometer ?? 0, highest ?? 0), "Odometer cannot be lower than the vehicle's recorded mileage. Verify the reading.");
        v.Odometer = value;
        _dbContext.VehicleOdometerHistories.Add(new VehicleOdometerHistory { VehicleId = v.VehicleId, AppointmentId = appointmentId, Odometer = value,
            RecordedUtc = DateTime.UtcNow, RecordedByUserId = HttpContext.Session.GetString("UserId"), RecordedByName = HttpContext.Session.GetString("UserName") });
        WorkflowAudit("Vehicle", v.VehicleId, "Odometer recorded", new { Odometer = value, AppointmentId = appointmentId });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> RecordVehicleOdometer(RecordOdometerForm f) => SaveWorkflow(async () => {
        var v = await ActiveIntakeVehicle(f.VehicleId); Require(await VehicleStamp(v) == f.Expected, "Vehicle changed. Refresh before recording mileage.");
        await AppendOdometer(v, f.Odometer!.Value);
        return RedirectToAction(nameof(VehicleHistory), new { id = v.VehicleId });
    }, nameof(VehicleHistory), new { id = f.VehicleId });

    private async Task<bool> HasRepairScope(int id) => await _dbContext.Inspections.AnyAsync(x => x.AppointmentId == id)
        || await _dbContext.Estimates.AnyAsync(x => x.AppointmentId == id)
        || await _dbContext.WorkOrders.AnyAsync(x => x.AppointmentId == id);
    private void IntakeHistory(Appointment a, string note)
    {
        _dbContext.QueueStatusHistories.Add(new QueueStatusHistory { AppointmentId = a.AppointmentId, QueueStatus = a.AppointmentStatus,
            ChangedUtc = DateTime.UtcNow, ChangedByUserId = HttpContext.Session.GetString("UserId"), ChangedByName = HttpContext.Session.GetString("UserName"), Notes = note });
        WorkflowAudit("Appointment", a.AppointmentId, note, new { a.CustomerName, a.VehicleId, a.AppointmentStatus, a.ScheduledStart, a.ScheduledEnd });
    }
    private async Task<IActionResult> IntakeAppointments(string view)
    {
        var page = new IntakeAppointmentsPage { Today = GarageNow().Date, View = view == "calendar" ? "calendar" : "list",
            CanCreate = await IntakePermission("Appointments", "Create"), CanEdit = await IntakePermission("Appointments", "Edit"), CanDelete = await IntakePermission("Appointments", "Delete"),
            CanInspect = await IntakePermission("Inspections", "Create"),
            Customers = await _dbContext.Customers.AsNoTracking().Where(x => x.IsActive && !x.IsArchived && !x.IsDeleted).OrderBy(x => x.FirstName).ToListAsync(),
            Bays = await _dbContext.Bays.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(),
            Employees = await _dbContext.EmployeeProfiles.AsNoTracking().Where(x => x.IsActive && !x.IsArchived && !x.IsDeleted).OrderBy(x => x.FirstName).ToListAsync(),
            Services = await _dbContext.Services.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.ServiceName).ToListAsync() };
        var vehicles = await _dbContext.Vehicles.AsNoTracking().ToDictionaryAsync(x => x.VehicleId);
        var owners = await _dbContext.VehicleOwnerships.AsNoTracking().Where(x => x.OwnershipEndUtc == null && (x.OwnershipStartUtc == null || x.OwnershipStartUtc <= DateTime.UtcNow)).ToListAsync();
        page.Vehicles = owners.Where(x => x.CustomerId.HasValue && vehicles.TryGetValue(x.VehicleId ?? 0, out var v) && !v.IsArchived && !v.IsDeleted)
            .Select(x => new AppointmentVehicleOption { VehicleId = x.VehicleId!.Value, CustomerId = x.CustomerId!.Value, Label = $"{BuildVehicleName(vehicles[x.VehicleId.Value])} - {vehicles[x.VehicleId.Value].LicensePlate}" }).ToList();
        var rows = await _dbContext.Appointments.AsNoTracking().OrderByDescending(x => x.ScheduledStart).ToListAsync();
        var rowIds = rows.Select(x => x.AppointmentId).ToArray();
        var initialInspections = await _dbContext.Inspections.AsNoTracking().Where(x => rowIds.Contains(x.AppointmentId ?? 0))
            .OrderByDescending(x => x.CompletedUtc).ToListAsync();
        var inspectionsByAppointment = initialInspections.Where(x => x.AppointmentId.HasValue).GroupBy(x => x.AppointmentId!.Value).ToDictionary(x => x.Key, x => x.ToList());
        var initialInspectionIds = initialInspections.Select(x => x.InspectionId).ToArray();
        var inspectionMedia = await _dbContext.InspectionMedia.AsNoTracking().Where(x => initialInspectionIds.Contains(x.InspectionId ?? 0)).ToListAsync();
        var mediaByAppointment = inspectionMedia.GroupBy(x => initialInspections.First(i => i.InspectionId == x.InspectionId).AppointmentId ?? 0)
            .ToDictionary(x => x.Key, x => x.ToList());
        var servicesByAppointment = await _dbContext.AppointmentServices.AsNoTracking().Where(x => rowIds.Contains(x.AppointmentId ?? 0))
            .OrderBy(x => x.CreatedUtc).ToListAsync();
        var serviceGroups = servicesByAppointment.GroupBy(x => x.AppointmentId ?? 0).ToDictionary(x => x.Key, x => x.ToList());
        var scopedIds = (await _dbContext.Inspections.AsNoTracking().Where(x => rowIds.Contains(x.AppointmentId ?? 0)).Select(x => x.AppointmentId!.Value).Distinct().ToListAsync())
            .Concat(await _dbContext.Estimates.AsNoTracking().Where(x => rowIds.Contains(x.AppointmentId ?? 0)).Select(x => x.AppointmentId!.Value).Distinct().ToListAsync())
            .Concat(await _dbContext.WorkOrders.AsNoTracking().Where(x => rowIds.Contains(x.AppointmentId ?? 0)).Select(x => x.AppointmentId!.Value).Distinct().ToListAsync()).ToHashSet();
        foreach (var a in rows)
        {
            var services = serviceGroups.GetValueOrDefault(a.AppointmentId) ?? new();
            var f = new AppointmentFormModel { AppointmentId = a.AppointmentId, Expected = MasterStamp(new { Appointment = a, Services = services }), CustomerId = a.CustomerId ?? 0, VehicleId = a.VehicleId ?? 0,
                BayId = a.BayId, ScheduledDate = a.ScheduledStart?.Date ?? page.Today, StartTime = a.ScheduledStart?.TimeOfDay ?? TimeSpan.Zero, EndTime = a.ScheduledEnd?.TimeOfDay ?? TimeSpan.Zero,
                AssignedEmployeeProfileId = page.Employees.FirstOrDefault(x => !string.IsNullOrEmpty(x.UserId) && x.UserId == a.AssignedMechanicUserId || StaffName(x) == a.AssignedMechanicName)?.EmployeeProfileId,
                PreferredEmployeeProfileId = page.Employees.FirstOrDefault(x => !string.IsNullOrEmpty(x.UserId) && x.UserId == a.PreferredMechanicUserId || StaffName(x) == a.PreferredMechanicName)?.EmployeeProfileId,
                ServiceIds = services.Where(x => x.ServiceId.HasValue).Select(x => x.ServiceId!.Value).ToList(), Notes = a.Notes, AppointmentType = a.AppointmentType ?? "Scheduled", Odometer = vehicles.GetValueOrDefault(a.VehicleId ?? 0)?.Odometer };
            page.Rows.Add(new IntakeAppointment(a, f, BuildVehicleName(vehicles.GetValueOrDefault(a.VehicleId ?? 0)), string.Join(", ", services.Select(x => x.ServiceName)),
                a.AppointmentStatus == "Scheduled" && !a.CheckedInUtc.HasValue && !scopedIds.Contains(a.AppointmentId))
                { Inspections = inspectionsByAppointment.GetValueOrDefault(a.AppointmentId) ?? new(), Media = mediaByAppointment.GetValueOrDefault(a.AppointmentId) ?? new() });
        }
        return View("~/Views/Owner/Appointments/Appointments.cshtml", page);
    }

    private Task<IActionResult> SaveIntakeAppointment(AppointmentFormModel f, bool creating) => SaveWorkflow(async () => {
        var a = creating ? new Appointment() : await _dbContext.Appointments.FindAsync(f.AppointmentId);
        Require(a is not null, "Appointment not found.");
        if (!creating) {
            Require(await AppointmentStamp(a) == f.Expected, "Appointment changed. Refresh before saving.");
            Require(a.AppointmentStatus == "Scheduled" && !a.CheckedInUtc.HasValue && !await HasRepairScope(a.AppointmentId), "Only scheduled appointments without repair records can be edited.");
            Require(!f.IsWalkIn, "Use Check In to record the customer's arrival.");
        }
        var now = GarageNow();
        if (creating && f.IsWalkIn) { f.ScheduledDate = now.Date; f.StartTime = now.TimeOfDay; f.EndTime = now.AddHours(1).TimeOfDay; }
        Require(f.ScheduledDate != default && f.StartTime >= TimeSpan.Zero && f.StartTime < TimeSpan.FromDays(1) && f.EndTime >= TimeSpan.Zero && f.EndTime < TimeSpan.FromDays(1), "Enter a valid date and time.");
        var start = f.ScheduledDate.Date.Add(f.StartTime); var end = f.IsWalkIn ? start.AddHours(1) : f.ScheduledDate.Date.Add(f.EndTime);
        Require(f.IsWalkIn || start >= now.AddMinutes(-5), "Schedule the appointment in the future.");
        var references = await LoadAppointmentReferencesAsync(f); Require(references.Error is null, references.Error ?? "Invalid appointment references.");
        var conflict = await ValidateAppointmentScheduleAsync(f, start, end, references.Employee); Require(conflict is null, conflict ?? "Schedule conflict.");
        var ids = f.ServiceIds.Distinct().ToList(); Require(ids.Count is > 0 and <= 30, "Select at least one requested service (up to 30).");
        var services = await _dbContext.Services.Where(x => ids.Contains(x.ServiceId) && x.IsActive).ToListAsync(); Require(services.Count == ids.Count, "Choose active services from the catalog.");
        EmployeeProfile? preferred = null;
        if (f.PreferredEmployeeProfileId.HasValue) {
            preferred = await _dbContext.EmployeeProfiles.FirstOrDefaultAsync(x => x.EmployeeProfileId == f.PreferredEmployeeProfileId && x.IsActive && !x.IsArchived && !x.IsDeleted);
            Require(preferred is not null, "Choose an active preferred mechanic.");
        }
        if (!creating) WorkflowAudit("Appointment", a.AppointmentId, "Before edit", a);
        a.CustomerId = f.CustomerId; a.CustomerName = OwnerName(references.Customer!); a.VehicleId = f.VehicleId; a.BayId = f.BayId;
        a.AssignedMechanicUserId = references.Employee?.UserId; a.AssignedMechanicName = references.Employee is null ? null : StaffName(references.Employee);
        a.PreferredMechanicUserId = preferred?.UserId; a.PreferredMechanicName = preferred is null ? null : StaffName(preferred);
        a.ScheduledStart = start; a.ScheduledEnd = end; a.Notes = Clean(f.Notes);
        if (creating) {
            a.AppointmentType = f.IsWalkIn ? "Walk-in" : "Scheduled"; a.AppointmentStatus = "Scheduled"; a.CreatedUtc = DateTime.UtcNow;
            a.CreatedByUserId = HttpContext.Session.GetString("UserId"); a.CreatedByName = HttpContext.Session.GetString("UserName");
            _dbContext.Appointments.Add(a); await _dbContext.SaveChangesAsync();
        }
        else _dbContext.AppointmentServices.RemoveRange(await _dbContext.AppointmentServices.Where(x => x.AppointmentId == a.AppointmentId).ToListAsync());
        foreach (var s in services) _dbContext.AppointmentServices.Add(new AppointmentService { AppointmentId = a.AppointmentId, ServiceId = s.ServiceId, ServiceName = s.ServiceName, CreatedUtc = DateTime.UtcNow });
        if (creating && f.IsWalkIn) await RecordIntakeArrival(a, references.Vehicle!, f.IdentityVerified, f.Odometer);
        else IntakeHistory(a, creating ? "Appointment created" : "Appointment updated");
        return RedirectToAction(nameof(Appointments), new { view = f.ViewMode });
    }, nameof(Appointments), new { view = f.ViewMode });

    private async Task RecordIntakeArrival(Appointment a, Vehicle v, bool verified, int? odometer)
    {
        Require(verified, "Confirm the customer's identity and vehicle before check-in."); Require(odometer.HasValue, "Record the odometer reading at check-in.");
        await AppendOdometer(v, odometer.Value, a.AppointmentId);
        a.CheckedInUtc = DateTime.UtcNow; a.CheckedInByUserId = HttpContext.Session.GetString("UserId"); a.CheckedInByName = HttpContext.Session.GetString("UserName");
        a.AppointmentStatus = "Arrived"; IntakeHistory(a, "Customer and vehicle verified; checked in");
    }
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> CheckInAppointment(CheckInAppointmentForm f) => SaveWorkflow(async () => {
        var a = await _dbContext.Appointments.FindAsync(f.AppointmentId); Require(a is not null, "Appointment not found.");
        Require(await AppointmentStamp(a) == f.Expected, "Appointment changed. Refresh before checking in.");
        Require(a.AppointmentStatus == "Scheduled" && !a.CheckedInUtc.HasValue && !await HasRepairScope(a.AppointmentId), "Only a scheduled appointment can be checked in once.");
        Require(a.ScheduledStart?.Date == GarageNow().Date, "Check-in is available on the appointment date. Reschedule first if needed.");
        await ActiveIntakeCustomer(a.CustomerId ?? 0); var v = await ActiveIntakeVehicle(a.VehicleId ?? 0);
        Require(await _dbContext.VehicleOwnerships.AnyAsync(x => x.VehicleId == v.VehicleId && x.CustomerId == a.CustomerId && x.OwnershipEndUtc == null && (x.OwnershipStartUtc == null || x.OwnershipStartUtc <= DateTime.UtcNow)), "Verify the current vehicle owner before check-in.");
        var assigned = string.IsNullOrWhiteSpace(a.AssignedMechanicName) && string.IsNullOrWhiteSpace(a.AssignedMechanicUserId) ? null :
            await _dbContext.EmployeeProfiles.FirstOrDefaultAsync(x =>
                (!string.IsNullOrEmpty(a.AssignedMechanicUserId) && x.UserId == a.AssignedMechanicUserId) || (x.FirstName + " " + x.LastName) == a.AssignedMechanicName);
        Require((string.IsNullOrWhiteSpace(a.AssignedMechanicName) && string.IsNullOrWhiteSpace(a.AssignedMechanicUserId)) || assigned is { IsActive: true, IsArchived: false, IsDeleted: false }, "The assigned mechanic is no longer available. Edit the appointment first.");
        Require(a.ScheduledStart.HasValue && a.ScheduledEnd.HasValue, "Reschedule this appointment with a valid start and end time.");
        var scheduleError = await ValidateAppointmentScheduleAsync(new AppointmentFormModel { AppointmentId = a.AppointmentId, VehicleId = v.VehicleId, BayId = a.BayId }, a.ScheduledStart.Value, a.ScheduledEnd.Value, assigned);
        Require(scheduleError is null, scheduleError ?? "Schedule conflict.");
        await RecordIntakeArrival(a, v, f.IdentityVerified, f.Odometer);
        return RedirectToAction(nameof(Appointments), new { view = "list" });
    }, nameof(Appointments));

    private Task<IActionResult> CancelIntakeAppointment(CancelAppointmentFormModel f) => SaveWorkflow(async () => {
        var a = await _dbContext.Appointments.FindAsync(f.AppointmentId); Require(a is not null, "Appointment not found.");
        Require(await AppointmentStamp(a) == f.Expected, "Appointment changed. Refresh before cancelling.");
        Require(a.AppointmentStatus == "Scheduled" && !a.CheckedInUtc.HasValue && !await HasRepairScope(a.AppointmentId), "Only scheduled appointments without repair records can be cancelled here.");
        a.AppointmentStatus = "Cancelled"; IntakeHistory(a, $"Appointment cancelled: {f.Reason!.Trim()}");
        return RedirectToAction(nameof(Appointments), new { view = f.ViewMode });
    }, nameof(Appointments));
}
