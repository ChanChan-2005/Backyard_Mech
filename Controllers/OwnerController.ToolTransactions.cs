using Backyard_Mech.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> CheckoutTool(ToolCheckoutForm form) => SaveWorkflow(async () =>
    {
        var tool = await _dbContext.Tools.FindAsync(form.ToolId);
        Require(tool is { IsActive: true, IsArchived: false, IsDeleted: false }, "Choose an available tool.");
        Require(tool!.Condition is "Excellent" or "Good" or "Fair", "This tool must pass inspection before it can be checked out.");
        Require(!tool.NextCalibrationDueUtc.HasValue || tool.NextCalibrationDueUtc.Value.Date >= DateTime.UtcNow.Date, "This tool is overdue for calibration and cannot be checked out.");
        Require(!await _dbContext.ToolCheckouts.AnyAsync(x => x.ToolId == tool.ToolId && x.ReturnedUtc == null), "This tool already has an open checkout.");
        var employee = await _dbContext.EmployeeProfiles.FindAsync(form.EmployeeProfileId);
        Require(employee is { IsActive: true, IsArchived: false, IsDeleted: false }, "Choose an active mechanic.");
        if (form.WorkOrderId.HasValue)
        {
            var order = await _dbContext.WorkOrders.FindAsync(form.WorkOrderId.Value);
        Require(order is not null && order.WorkOrderStatus is not ("Cancelled" or "Completed" or "Ready for Pickup" or "Picked Up"), "Choose an active work order.");
            var employeeName = BuildEmployeeName(employee!);
            Require(await _dbContext.WorkOrderAssignments.AnyAsync(x => x.WorkOrderId == order!.WorkOrderId &&
                (x.MechanicName == employeeName || (employee!.UserId != null && x.MechanicUserId == employee.UserId))), "Assign this mechanic to the work order before checking out a tool.");
        }
        var checkout = new ToolCheckout { ToolId = tool.ToolId, WorkOrderId = form.WorkOrderId, MechanicUserId = employee!.UserId,
            MechanicName = BuildEmployeeName(employee), CheckedOutUtc = DateTime.UtcNow, ConditionAtCheckout = form.ConditionAtCheckout,
            ToolIssueStatus = "Checked Out", Notes = form.Notes?.Trim() };
        _dbContext.ToolCheckouts.Add(checkout);
        WorkflowAudit("ToolCheckout", 0, "Checkout", new { tool.Name, checkout.MechanicName, checkout.WorkOrderId, checkout.ConditionAtCheckout });
        return RedirectToAction(nameof(ToolTracking));
    }, nameof(ToolTracking));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> ReturnEquipment(ToolReturnForm form) => SaveWorkflow(async () =>
    {
        var checkout = await _dbContext.ToolCheckouts.FindAsync(form.ToolCheckoutId);
        Require(checkout is { ReturnedUtc: null, ToolId: not null }, "This checkout has already been returned or was not found.");
        var tool = await _dbContext.Tools.FindAsync(checkout!.ToolId);
        Require(tool is not null, "The checked-out tool was not found.");
        var missing = form.ConditionAtReturn == "Missing";
        checkout.ReturnedUtc = DateTime.UtcNow;
        checkout.ConditionAtReturn = form.ConditionAtReturn;
        checkout.ToolIssueStatus = missing ? "Missing" : form.ConditionAtReturn == "Damaged" ? "Damaged" : "Returned";
        checkout.Notes = string.IsNullOrWhiteSpace(checkout.Notes) ? form.Notes.Trim() : $"{checkout.Notes} | Return: {form.Notes.Trim()}";
        tool!.Condition = missing ? "Missing" : form.ConditionAtReturn;
        if (missing || form.ConditionAtReturn == "Damaged") tool.IsActive = false;
        WorkflowAudit("ToolCheckout", checkout.ToolCheckoutId, "Return and inspect", new { checkout.ToolId, checkout.WorkOrderId, form.ConditionAtReturn, Notes = form.Notes.Trim() });
        if (!missing && form.ConditionAtReturn == "Damaged")
        {
            _dbContext.ToolMaintenances.Add(new ToolMaintenance { ToolId = tool.ToolId, MaintenanceType = "Repair required",
                PerformedByUserId = HttpContext.Session.GetString("UserId"), PerformedByName = HttpContext.Session.GetString("UserName"), Notes = "Return inspection: " + form.Notes.Trim() });
            tool.Condition = "Under Maintenance";
        }
        return RedirectToAction(nameof(ToolTracking));
    }, nameof(ToolTracking));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> StartToolMaintenance(ToolMaintenanceForm form) => SaveWorkflow(async () =>
    {
        var tool = await _dbContext.Tools.FindAsync(form.ToolId);
        Require(tool is { IsArchived: false, IsDeleted: false }, "Tool not found.");
        Require(!await _dbContext.ToolCheckouts.AnyAsync(x => x.ToolId == form.ToolId && x.ReturnedUtc == null), "Return this tool before starting maintenance.");
        Require(!await _dbContext.ToolMaintenances.AnyAsync(x => x.ToolId == form.ToolId && x.PerformedUtc == null), "This tool already has an open maintenance record.");
        var maintenance = new ToolMaintenance { ToolId = form.ToolId, MaintenanceType = form.MaintenanceType.Trim(), Notes = form.Notes.Trim() };
        _dbContext.ToolMaintenances.Add(maintenance);
        tool!.Condition = "Under Maintenance";
        tool.IsActive = false;
        WorkflowAudit("Tool", tool.ToolId, "Maintenance started", new { maintenance.MaintenanceType, maintenance.Notes });
        return RedirectToAction(nameof(ToolTracking));
    }, nameof(ToolTracking));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> CompleteToolMaintenance(ToolMaintenanceForm form) => SaveWorkflow(async () =>
    {
        var tool = await _dbContext.Tools.FindAsync(form.ToolId);
        Require(tool is { IsArchived: false, IsDeleted: false }, "Tool not found.");
        var maintenance = await _dbContext.ToolMaintenances.Where(x => x.ToolId == form.ToolId && x.PerformedUtc == null)
            .OrderByDescending(x => x.ToolMaintenanceId).FirstOrDefaultAsync();
        Require(maintenance is not null, "There is no open maintenance record for this tool.");
        Require(form.NextDueUtc is null || form.NextDueUtc.Value.Date >= DateTime.UtcNow.Date, "Next calibration date cannot be in the past.");
        Require(form.ConditionAfter is "Excellent" or "Good" or "Fair", "A damaged tool cannot be returned to service.");
        maintenance!.PerformedUtc = DateTime.UtcNow;
        maintenance.PerformedByUserId = HttpContext.Session.GetString("UserId");
        maintenance.PerformedByName = HttpContext.Session.GetString("UserName");
        maintenance.Cost = form.Cost;
        maintenance.NextDueUtc = form.NextDueUtc;
        maintenance.Notes = string.IsNullOrWhiteSpace(maintenance.Notes) ? form.Notes.Trim() : $"{maintenance.Notes} | Completed: {form.Notes.Trim()}";
        tool!.Condition = form.ConditionAfter;
        tool.IsActive = true;
        if (maintenance.MaintenanceType?.Contains("calibration", StringComparison.OrdinalIgnoreCase) == true)
        {
            tool.LastCalibratedUtc = DateTime.UtcNow;
            tool.NextCalibrationDueUtc = form.NextDueUtc;
        }
        else if (form.NextDueUtc.HasValue) tool.NextCalibrationDueUtc = form.NextDueUtc;
        WorkflowAudit("ToolMaintenance", maintenance.ToolMaintenanceId, "Maintenance completed", new { form.Cost, form.ConditionAfter, form.NextDueUtc, Notes = form.Notes.Trim() });
        return RedirectToAction(nameof(ToolTracking));
    }, nameof(ToolTracking));
}
