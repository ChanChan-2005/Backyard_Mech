-- One-time migration from legacy invoice numbers to INV-year-block-position.
-- Keep the existing InvoiceId values so payments, work orders, and links remain intact.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock
    @Resource = N'BackyardMech.RepairWorkflow',
    @LockMode = N'Exclusive',
    @LockOwner = N'Transaction',
    @LockTimeout = 10000;
IF @lockResult < 0 THROW 51000, 'Invoice migration could not acquire the workflow lock.', 1;

DECLARE @newFormatCount int = (
    SELECT COUNT(*) FROM dbo.Invoice
    WHERE InvoiceNumber LIKE N'INV-[0-9][0-9][0-9][0-9]-%-%'
);
IF @newFormatCount > 0
BEGIN
    IF @newFormatCount <> (SELECT COUNT(*) FROM dbo.Invoice)
        THROW 51001, 'Invoice migration found a mix of old and new numbers.', 1;
    PRINT 'Invoice numbers already use the new format.';
    COMMIT TRANSACTION;
    RETURN;
END;

;WITH Numbered AS (
    SELECT InvoiceId,
           GarageYear = YEAR(DATEADD(hour, 8, IssuedUtc)),
           Sequence = ROW_NUMBER() OVER (
               PARTITION BY YEAR(DATEADD(hour, 8, IssuedUtc))
               ORDER BY IssuedUtc, InvoiceId
           ) - 1
    FROM dbo.Invoice
)
UPDATE invoice
SET InvoiceNumber = CONCAT(N'INV-', numbered.GarageYear, N'-',
                           numbered.Sequence / 10000 + 1, N'-',
                           RIGHT(CONCAT(N'0000', numbered.Sequence % 10000), 4))
FROM dbo.Invoice AS invoice
JOIN Numbered AS numbered ON numbered.InvoiceId = invoice.InvoiceId;

DECLARE @garageYear int = YEAR(DATEADD(hour, 8, SYSUTCDATETIME()));
DECLARE @lastNumber int = (
    SELECT COUNT(*) - 1 FROM dbo.Invoice
    WHERE YEAR(DATEADD(hour, 8, IssuedUtc)) = @garageYear
);
UPDATE dbo.InvoiceNumberCounter
SET Prefix = CASE WHEN @lastNumber >= 0 THEN CONCAT(N'INV-', @garageYear, N'-') ELSE N'INV-' END,
    LastNumber = CASE WHEN @lastNumber >= 0 THEN @lastNumber ELSE 0 END
WHERE InvoiceNumberCounterId = 1;

IF @@ROWCOUNT <> 1 THROW 51002, 'Invoice numbering counter is missing.', 1;
COMMIT TRANSACTION;
