using Backyard_Mech.Data;
using Backyard_Mech.Models;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Services
{
    public sealed class RecordsDispositionService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<RecordsDispositionService> _logger;

        public RecordsDispositionService(ApplicationDbContext dbContext, ILogger<RecordsDispositionService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<RecordsDispositionPolicy> GetPolicyAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.RecordsDispositionPolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(policy => policy.RecordsDispositionPolicyId == 1, cancellationToken)
                ?? new RecordsDispositionPolicy();
        }

        public async Task SavePolicyAsync(int value, string unit, string updatedByName, CancellationToken cancellationToken = default)
        {
            var policy = await _dbContext.RecordsDispositionPolicies
                .FirstOrDefaultAsync(item => item.RecordsDispositionPolicyId == 1, cancellationToken);

            if (policy is null)
            {
                policy = new RecordsDispositionPolicy { RecordsDispositionPolicyId = 1 };
                _dbContext.RecordsDispositionPolicies.Add(policy);
            }

            policy.RetentionValue = Math.Max(1, value);
            policy.RetentionUnit = NormalizeUnit(unit);
            policy.UpdatedUtc = DateTime.UtcNow;
            policy.UpdatedByName = updatedByName;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> PurgeEligibleAsync(CancellationToken cancellationToken = default)
        {
            var policy = await GetPolicyAsync(cancellationToken);
            var cutoffUtc = CalculateCutoffUtc(DateTime.UtcNow, policy.RetentionValue, policy.RetentionUnit);
            var candidates = new List<(string Type, int Id)>();

            candidates.AddRange((await _dbContext.EmployeeProfiles.AsNoTracking()
                .Where(item => item.IsDeleted && item.DeletedUtc != null && item.DeletedUtc <= cutoffUtc)
                .Select(item => item.EmployeeProfileId)
                .ToListAsync(cancellationToken)).Select(id => ("Employee", id)));
            candidates.AddRange((await _dbContext.Customers.AsNoTracking()
                .Where(item => item.IsDeleted && item.DeletedUtc != null && item.DeletedUtc <= cutoffUtc)
                .Select(item => item.CustomerId)
                .ToListAsync(cancellationToken)).Select(id => ("Customer", id)));
            candidates.AddRange((await _dbContext.Vehicles.AsNoTracking()
                .Where(item => item.IsDeleted && item.DeletedUtc != null && item.DeletedUtc <= cutoffUtc)
                .Select(item => item.VehicleId)
                .ToListAsync(cancellationToken)).Select(id => ("Vehicle", id)));
            candidates.AddRange((await _dbContext.Parts.AsNoTracking()
                .Where(item => item.IsDeleted && item.DeletedUtc != null && item.DeletedUtc <= cutoffUtc)
                .Select(item => item.PartId)
                .ToListAsync(cancellationToken)).Select(id => ("Part", id)));
            candidates.AddRange((await _dbContext.ProductListings.AsNoTracking()
                .Where(item => item.IsDeleted && item.DeletedUtc != null && item.DeletedUtc <= cutoffUtc)
                .Select(item => item.ProductListingId)
                .ToListAsync(cancellationToken)).Select(id => ("Product Listing", id)));
            candidates.AddRange((await _dbContext.Tools.AsNoTracking()
                .Where(item => item.IsDeleted && item.DeletedUtc != null && item.DeletedUtc <= cutoffUtc)
                .Select(item => item.ToolId)
                .ToListAsync(cancellationToken)).Select(id => ("Tool", id)));

            var deletedCount = 0;
            foreach (var candidate in candidates)
            {
                try
                {
                    var result = await DeletePermanentlyAsync(candidate.Type, candidate.Id, "Automatic Retention Cleanup", cancellationToken);
                    if (result.Success)
                    {
                        deletedCount++;
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Automatic permanent deletion failed for {RecordType} {RecordId}.", candidate.Type, candidate.Id);
                }
            }

            return deletedCount;
        }

        public async Task<PermanentDeletionResult> DeletePermanentlyAsync(
            string recordType,
            int recordId,
            string deletedByName,
            CancellationToken cancellationToken = default)
        {
            var normalizedType = NormalizeRecordType(recordType);
            if (recordId <= 0 || string.IsNullOrWhiteSpace(normalizedType))
            {
                return new PermanentDeletionResult(false, "The selected record is invalid.");
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            string? recordName = null;

            switch (normalizedType)
            {
                case "Employee":
                {
                    var employee = await _dbContext.EmployeeProfiles
                        .FirstOrDefaultAsync(item => item.EmployeeProfileId == recordId && item.IsDeleted, cancellationToken);
                    if (employee is null) return new PermanentDeletionResult(false, "Employee record was not found in records disposition.");

                    recordName = $"{employee.FirstName} {employee.LastName}".Trim();
                    await _dbContext.EmployeeRateHistories.Where(item => item.EmployeeProfileId == recordId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.EmployeeProfileId, (int?)null), cancellationToken);
                    await _dbContext.EmployeeAvailabilities.Where(item => item.EmployeeProfileId == recordId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.EmployeeProfileId, (int?)null), cancellationToken);
                    await _dbContext.EmployeeAvailabilityExceptions.Where(item => item.EmployeeProfileId == recordId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.EmployeeProfileId, (int?)null), cancellationToken);
                    _dbContext.EmployeeProfiles.Remove(employee);
                    break;
                }
                case "Customer":
                {
                    var customer = await _dbContext.Customers
                        .FirstOrDefaultAsync(item => item.CustomerId == recordId && item.IsDeleted, cancellationToken);
                    if (customer is null) return new PermanentDeletionResult(false, "Customer record was not found in records disposition.");

                    recordName = $"{customer.FirstName} {customer.LastName}".Trim();
                    var customerName = recordName;
                    var customerVehicleIds = await _dbContext.VehicleOwnerships.AsNoTracking()
                        .Where(item => item.CustomerId == recordId && item.VehicleId != null)
                        .Select(item => item.VehicleId!.Value)
                        .Distinct()
                        .ToListAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(customerName) && customerVehicleIds.Count > 0)
                    {
                        await _dbContext.Vehicles
                            .Where(item => customerVehicleIds.Contains(item.VehicleId) && (item.OwnerFullName == null || item.OwnerFullName == ""))
                            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.OwnerFullName, customerName), cancellationToken);
                    }
                    await _dbContext.VehicleOwnerships.Where(item => item.CustomerId == recordId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.CustomerId, (int?)null), cancellationToken);
                    await _dbContext.Appointments.Where(item => item.CustomerId == recordId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.CustomerId, (int?)null), cancellationToken);
                    await _dbContext.Invoices.Where(item => item.CustomerId == recordId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.CustomerId, (int?)null), cancellationToken);
                    _dbContext.Customers.Remove(customer);
                    break;
                }
                case "Vehicle":
                {
                    var vehicle = await _dbContext.Vehicles
                        .FirstOrDefaultAsync(item => item.VehicleId == recordId && item.IsDeleted, cancellationToken);
                    if (vehicle is null) return new PermanentDeletionResult(false, "Vehicle record was not found in records disposition.");

                    recordName = $"{vehicle.Year} {vehicle.Make} {vehicle.Model}".Trim();
                    await _dbContext.VehicleOwnerships.Where(item => item.VehicleId == recordId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.VehicleId, (int?)null), cancellationToken);
                    await _dbContext.Appointments.Where(item => item.VehicleId == recordId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.VehicleId, (int?)null), cancellationToken);
                    await _dbContext.VehicleOdometerHistories.Where(item => item.VehicleId == recordId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.VehicleId, (int?)null), cancellationToken);
                    _dbContext.Vehicles.Remove(vehicle);
                    break;
                }
                case "Part":
                {
                    var part = await _dbContext.Parts
                        .FirstOrDefaultAsync(item => item.PartId == recordId && item.IsDeleted, cancellationToken);
                    if (part is null) return new PermanentDeletionResult(false, "Part record was not found in records disposition.");

                    recordName = part.Name;
                    await _dbContext.PartTransactions.Where(item => item.PartId == recordId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.PartId, (int?)null), cancellationToken);
                    await _dbContext.EstimateRevisionLineItems.Where(item => item.PartId == recordId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.PartId, (int?)null), cancellationToken);
                    await _dbContext.ProductListings.Where(item => item.PartId == recordId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.PartId, (int?)null), cancellationToken);
                    _dbContext.Parts.Remove(part);
                    break;
                }
                case "Product Listing":
                {
                    var listing = await _dbContext.ProductListings
                        .FirstOrDefaultAsync(item => item.ProductListingId == recordId && item.IsDeleted, cancellationToken);
                    if (listing is null) return new PermanentDeletionResult(false, "Product listing was not found in records disposition.");

                    recordName = listing.ListingTitle;
                    _dbContext.ProductListings.Remove(listing);
                    break;
                }
                case "Tool":
                {
                    var tool = await _dbContext.Tools
                        .FirstOrDefaultAsync(item => item.ToolId == recordId && item.IsDeleted, cancellationToken);
                    if (tool is null) return new PermanentDeletionResult(false, "Tool record was not found in records disposition.");

                    recordName = tool.Name;
                    await _dbContext.ToolCheckouts.Where(item => item.ToolId == recordId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ToolId, (int?)null), cancellationToken);
                    await _dbContext.ToolMaintenances.Where(item => item.ToolId == recordId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ToolId, (int?)null), cancellationToken);
                    _dbContext.Tools.Remove(tool);
                    break;
                }
            }

            _dbContext.AuditLogEntries.Add(new AuditLogEntry
            {
                UserName = deletedByName,
                Action = "Permanent Delete",
                EntityName = normalizedType,
                EntityId = recordId.ToString(),
                TimestampUtc = DateTime.UtcNow,
                Details = $"Permanently deleted {normalizedType.ToLowerInvariant()} record '{recordName ?? recordId.ToString()}' from records disposition."
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PermanentDeletionResult(true, $"{normalizedType} permanently deleted.");
        }

        public static DateTime CalculateEligibleUtc(DateTime deletedUtc, int value, string unit)
        {
            var amount = Math.Max(1, value);
            return NormalizeUnit(unit) switch
            {
                "Weeks" => deletedUtc.AddDays(amount * 7),
                "Months" => deletedUtc.AddMonths(amount),
                "Years" => deletedUtc.AddYears(amount),
                _ => deletedUtc.AddDays(amount)
            };
        }

        private static DateTime CalculateCutoffUtc(DateTime nowUtc, int value, string unit)
        {
            var amount = Math.Max(1, value);
            return NormalizeUnit(unit) switch
            {
                "Weeks" => nowUtc.AddDays(-amount * 7),
                "Months" => nowUtc.AddMonths(-amount),
                "Years" => nowUtc.AddYears(-amount),
                _ => nowUtc.AddDays(-amount)
            };
        }

        public static string NormalizeUnit(string? unit)
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
                _ => string.Empty
            };
        }
    }

    public readonly record struct PermanentDeletionResult(bool Success, string Message);
}
