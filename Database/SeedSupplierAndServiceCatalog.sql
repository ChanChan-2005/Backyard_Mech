-- Starter data for the local demo database. Supplier names are placeholders;
-- service prices remain zero until the shop sets its own rates.
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

INSERT INTO dbo.Supplier (SupplierName, IsActive)
SELECT seed.Name, 1
FROM (VALUES
    (N'Sample Parts Supplier'),
    (N'Sample Tire Supplier'),
    (N'Sample Fluids Supplier'),
    (N'Sample Electrical Supplier')
) AS seed(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Supplier existing WHERE existing.SupplierName = seed.Name);

INSERT INTO dbo.ServiceCategory (CategoryName, Description, IsActive)
SELECT seed.Name, seed.Description, 1
FROM (VALUES
    (N'General Maintenance', N'Routine maintenance and inspections'),
    (N'Diagnostics', N'Fault finding and system testing'),
    (N'Brakes', N'Brake inspection and repair'),
    (N'Tires & Wheels', N'Tire and wheel services'),
    (N'Cooling System', N'Cooling system service'),
    (N'Electrical', N'Electrical and charging services'),
    (N'Air Conditioning', N'Air conditioning service')
) AS seed(Name, Description)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ServiceCategory existing WHERE existing.CategoryName = seed.Name);

INSERT INTO dbo.Service (ServiceCategoryId, ServiceName, Description, DefaultPrice, DefaultEstimatedHours, IsActive)
SELECT category.ServiceCategoryId, seed.Name,
    N'Starter catalog entry. Confirm the scope, labour time, and price before quoting.',
    0.00, seed.Hours, 1
FROM (VALUES
    (N'General Maintenance', N'Engine Oil and Filter Change', CAST(1.00 AS decimal(8,2))),
    (N'General Maintenance', N'Multi-Point Inspection', 0.50),
    (N'General Maintenance', N'Scheduled Maintenance Service', 1.50),
    (N'Diagnostics', N'Check Engine Light Diagnosis', 1.00),
    (N'Diagnostics', N'Electrical System Diagnosis', 1.00),
    (N'Diagnostics', N'Pre-Purchase Inspection', 1.50),
    (N'Brakes', N'Brake System Inspection', 0.50),
    (N'Brakes', N'Front Brake Pad Replacement', 1.50),
    (N'Brakes', N'Rear Brake Pad Replacement', 1.50),
    (N'Brakes', N'Brake Fluid Exchange', 1.00),
    (N'Tires & Wheels', N'Tire Rotation', 0.50),
    (N'Tires & Wheels', N'Tire Mount and Balance', 1.50),
    (N'Tires & Wheels', N'Wheel Alignment', 1.50),
    (N'Cooling System', N'Cooling System Inspection', 1.00),
    (N'Cooling System', N'Coolant Flush', 1.50),
    (N'Electrical', N'Battery and Charging Test', 0.50),
    (N'Electrical', N'Battery Replacement', 0.50),
    (N'Air Conditioning', N'Air Conditioning Diagnostic', 1.00)
) AS seed(CategoryName, Name, Hours)
INNER JOIN dbo.ServiceCategory category ON category.CategoryName = seed.CategoryName
WHERE NOT EXISTS (SELECT 1 FROM dbo.Service existing WHERE existing.ServiceName = seed.Name);

COMMIT TRANSACTION;
