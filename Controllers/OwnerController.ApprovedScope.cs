using Backyard_Mech.Models;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    private async Task<List<EstimateRevisionLineItem>> ApprovedScope(WorkOrder order)
    {
        var revision = await _dbContext.EstimateRevisions.FindAsync(order.ApprovedEstimateRevisionId);
        Require(revision is not null && revision.EstimateId == order.EstimateId &&
            revision.EstimateStatus is "Approved" or "Partially Approved", "This repair needs an approved estimate revision.");
        return await _dbContext.EstimateRevisionLineItems.Where(x =>
            x.EstimateRevisionId == order.ApprovedEstimateRevisionId && x.ApprovalStatus == "Approved").ToListAsync();
    }

    private async Task ValidateApprovedParts(WorkOrder order, IEnumerable<PartTransaction> movements)
    {
        var scope = await ApprovedScope(order);
        foreach (var group in movements.Where(x => x.WorkOrderId == order.WorkOrderId && x.PartId.HasValue).GroupBy(x => x.PartId!.Value))
        {
            var used = group.Where(x => x.TransactionType is "Installed" or "Wasted").Sum(x => x.Quantity ?? 0)
                - group.Where(x => x.TransactionType == "Returned").Sum(x => x.Quantity ?? 0) + ReservedUnits(group);
            var limit = scope.Where(x => x.LineType == "Part" && x.PartId == group.Key).Sum(x => x.Quantity ?? 0);
            Require(used <= limit, "Part or quantity exceeds the approved estimate. Obtain approval for a revised scope first.");
        }
    }

    private async Task ValidateApprovedLabour(WorkOrder order, IEnumerable<ServiceEntry> entries)
    {
        var scope = await ApprovedScope(order);
        var rows = entries.ToList();
        foreach (var entry in rows)
        {
            var line = scope.SingleOrDefault(x => x.LineType == "Labour" && x.EstimateRevisionLineItemId == entry.ApprovedEstimateLineId);
            Require(line is not null, "Select an approved estimate task for every labour entry before continuing.");
            Require(entry.HourlyRate == line!.UnitPrice, "Labour rate must match the approved estimate.");
        }
        foreach (var group in rows.GroupBy(x => x.ApprovedEstimateLineId))
        {
            var line = scope.Single(x => x.EstimateRevisionLineItemId == group.Key);
            Require(group.Sum(x => x.DurationMinutes ?? 0) <= (line.Quantity ?? 0) * 60,
                "Labour time exceeds the approved task hours. Obtain additional approval before billing.");
            Require(group.Sum(x => x.ServiceCost ?? 0) <= (line.LineTotal ?? 0),
                "Labour charges exceed the approved task amount. Obtain additional approval before billing.");
        }
    }
}
