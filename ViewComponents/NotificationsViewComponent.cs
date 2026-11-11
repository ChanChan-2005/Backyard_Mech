using Backyard_Mech.Data;
using Backyard_Mech.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.ViewComponents;

public sealed class NotificationsViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    private readonly PermissionService _permissions;

    public NotificationsViewComponent(ApplicationDbContext db, PermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var role = HttpContext.Session.GetString("UserRole");
        var permissionCodes = await _permissions.GetPermissionCodesAsync(role);
        bool CanView(string module) => permissionCodes.Contains("*") ||
            permissionCodes.Contains(PermissionService.BuildPermissionCode(module, "View"));

        var items = new List<NotificationItem>();
        var now = DateTime.UtcNow;

        if (CanView("Inventory"))
        {
            var count = await _db.Parts.AsNoTracking().CountAsync(part => part.IsActive && !part.IsArchived && !part.IsDeleted &&
                part.ReorderThreshold > 0 && part.QuantityOnHand <= part.ReorderThreshold);
            if (count > 0) items.Add(new("Low stock", $"{count} part{(count == 1 ? " is" : "s are")} at or below reorder level.", "/Owner/PartsManagement", count));
        }

        if (CanView("Tools"))
        {
            var count = await _db.Tools.AsNoTracking().CountAsync(tool => tool.IsActive && !tool.IsArchived && !tool.IsDeleted &&
                tool.NextCalibrationDueUtc.HasValue && tool.NextCalibrationDueUtc.Value.Date < now.Date);
            if (count > 0) items.Add(new("Calibration overdue", $"{count} tool{(count == 1 ? " is" : "s are")} past its calibration date.", "/Owner/ToolTracking", count));
        }

        if (CanView("Estimates"))
        {
            var count = await _db.EstimateRevisions.AsNoTracking().CountAsync(revision => revision.EstimateStatus == "Pending" &&
                revision.ApprovalToken != null && revision.ApprovalTokenExpiresUtc > now);
            if (count > 0) items.Add(new("Awaiting customer approval", $"{count} estimate{(count == 1 ? " is" : "s are")} awaiting a customer response.", "/Owner/EstimateApprovals", count));
        }

        if (CanView("Appointments"))
        {
            var until = now.AddHours(24);
            var count = await _db.Appointments.AsNoTracking().CountAsync(appointment => appointment.AppointmentStatus == "Scheduled" &&
                !appointment.CheckedInUtc.HasValue && appointment.ScheduledStart >= now && appointment.ScheduledStart < until);
            if (count > 0) items.Add(new("Appointments coming up", $"{count} scheduled appointment{(count == 1 ? " is" : "s are")} due in the next 24 hours.", "/Owner/Appointments", count));
        }

        return View(items);
    }
}

public sealed record NotificationItem(string Title, string Message, string Url, int Count);
