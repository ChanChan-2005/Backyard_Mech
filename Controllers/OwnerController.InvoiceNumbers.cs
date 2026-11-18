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
        var dateSuffix = garageDate.ToString("MMddyy", CultureInfo.InvariantCulture);
        string number;
        do
        {
            counter!.LastNumber = checked(counter.LastNumber + 1);
            number = $"INV-{counter.LastNumber.ToString("D4", CultureInfo.InvariantCulture)}-{dateSuffix}";
        }
        while (await _dbContext.Invoices.AnyAsync(invoice => invoice.InvoiceNumber == number));

        return number;
    }
}
