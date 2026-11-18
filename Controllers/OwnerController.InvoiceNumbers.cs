using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    private async Task<string> NextInvoiceNumberAsync(DateTime issuedUtc)
    {
        var counter = await _dbContext.InvoiceNumberCounters
            .SingleOrDefaultAsync(row => row.InvoiceNumberCounterId == 1);
        Require(counter is not null, "The invoice numbering counter is missing. Run the database setup script.");

        var garageDate = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(issuedUtc, DateTimeKind.Utc), GarageZone());
        var yearPrefix = $"INV-{garageDate.Year.ToString(CultureInfo.InvariantCulture)}-";
        var sequence = counter!.Prefix == yearPrefix
            ? checked(counter.LastNumber + 1)
            : 0;
        while (true)
        {
            var block = checked(sequence / 10000 + 1);
            var position = sequence % 10000;
            var number = $"{yearPrefix}{block.ToString(CultureInfo.InvariantCulture)}-{position.ToString("D4", CultureInfo.InvariantCulture)}";
            if (await _dbContext.Invoices.AnyAsync(invoice => invoice.InvoiceNumber == number))
            {
                sequence = checked(sequence + 1);
                continue;
            }

            counter.Prefix = yearPrefix;
            counter.LastNumber = sequence;
            return number;
        }
    }
}
