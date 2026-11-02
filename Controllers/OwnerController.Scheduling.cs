using Backyard_Mech.Models;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    private async Task<string?> ScheduleError(DateTime start, DateTime end, int? bayId,
        IEnumerable<EmployeeProfile> employees, int? appointmentId = null, int? workOrderId = null)
    {
        if (end <= start || end - start > TimeSpan.FromDays(31)) return "Choose a valid schedule of at most 31 days.";
        var appointments = await _dbContext.Appointments.Where(x => x.AppointmentId != (appointmentId ?? 0) &&
            x.AppointmentStatus != "Cancelled" && x.AppointmentStatus != "No Show" && x.AppointmentStatus != "Completed" &&
            x.ScheduledStart < end && x.ScheduledEnd > start).ToListAsync();
        var orders = await _dbContext.WorkOrders.Where(x => x.WorkOrderId != (workOrderId ?? 0) &&
            x.WorkOrderStatus != "Cancelled" && x.WorkOrderStatus != "Completed" && x.WorkOrderStatus != "Ready for Pickup" && x.WorkOrderStatus != "Picked Up" &&
            x.ScheduledStart < end && x.ScheduledEnd > start).ToListAsync();
        if (bayId.HasValue)
        {
            if (!await _dbContext.Bays.AnyAsync(x => x.BayId == bayId && x.IsActive)) return "Select an active bay.";
            if (appointments.Any(x => x.BayId == bayId) || orders.Any(x => x.BayId == bayId)) return "The bay is already booked during this time.";
            if (await _dbContext.BayMaintenanceSchedules.AnyAsync(x => x.BayId == bayId && !x.IsCompleted && x.StartDate < end && x.EstimatedReturnDate >= start))
                return "The bay is under maintenance during this time.";
        }
        var orderIds = orders.Select(x => x.WorkOrderId).ToArray();
        var assignments = await _dbContext.WorkOrderAssignments.Where(x => orderIds.Contains(x.WorkOrderId ?? 0)).ToListAsync();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(HttpContext.RequestServices.GetRequiredService<IConfiguration>()["GarageTimeZone"] ?? "Asia/Manila");
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(start, DateTimeKind.Unspecified), zone);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(end, DateTimeKind.Unspecified), zone);
        foreach (var employee in employees.DistinctBy(x => x.EmployeeProfileId))
        {
            var name = BuildEmployeeName(employee);
            bool Matches(string? id, string? n) => (!string.IsNullOrWhiteSpace(employee.UserId) && id == employee.UserId) || n == name;
            if (appointments.Any(x => Matches(x.AssignedMechanicUserId, x.AssignedMechanicName)) || assignments.Any(x => Matches(x.MechanicUserId, x.MechanicName)))
                return $"{name} is already booked during this time.";
            var exceptions = await _dbContext.EmployeeAvailabilityExceptions.Where(x => x.EmployeeProfileId == employee.EmployeeProfileId && x.StartUtc < utcEnd && x.EndUtc > utcStart).ToListAsync();
            if (exceptions.Any(x => !x.IsAvailable)) return $"{name} has an absence during this time.";
            var shifts = await _dbContext.EmployeeAvailabilities.Where(x => x.EmployeeProfileId == employee.EmployeeProfileId && x.IsAvailable).ToListAsync();
            var intervals = new List<(DateTime Start, DateTime End)>();
            for (var day = start.Date.AddDays(-1); day < end; day = day.AddDays(1))
                foreach (var shift in shifts.Where(x => x.DayOfWeek == (int)day.DayOfWeek && x.StartTime.HasValue && x.EndTime.HasValue))
                {
                    var a = day.Add(shift.StartTime!.Value.ToTimeSpan());
                    var b = day.Add(shift.EndTime!.Value.ToTimeSpan());
                    if (b <= a) b = b.AddDays(1);
                    intervals.Add((a, b));
                }
            foreach (var exception in exceptions.Where(x => x.IsAvailable && x.StartUtc.HasValue && x.EndUtc.HasValue))
                intervals.Add((TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(exception.StartUtc!.Value, DateTimeKind.Utc), zone),
                    TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(exception.EndUtc!.Value, DateTimeKind.Utc), zone)));
            var covered = start;
            foreach (var interval in intervals.OrderBy(x => x.Start))
                if (interval.Start <= covered && interval.End > covered) covered = interval.End;
            if (covered < end) return $"{name} is outside working hours. Set an available shift before assigning this schedule.";
        }
        return null;
    }
}
