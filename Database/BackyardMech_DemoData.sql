USE [Bakyard_Mech];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

SET NOCOUNT ON;

DECLARE @Now DATETIME2 = SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM dbo.AppRole WHERE RoleKey = N'Owner')
BEGIN
    INSERT INTO dbo.AppRole (RoleKey, RoleName, Description, DisplayOrder, IsSystemProtected, IsActive)
    VALUES
        (N'Owner', N'Garage Owner', N'Full system owner access.', 10, 1, 1),
        (N'Administrator', N'Administrator', N'Administrative garage operations access.', 20, 0, 1),
        (N'Service Advisor', N'Service Advisor', N'Customer intake and service queue access.', 30, 0, 1),
        (N'Mechanic/Technician', N'Mechanic/Technician', N'Work order and repair workflow access.', 40, 0, 1),
        (N'Parts Clerk', N'Parts Clerk', N'Inventory and parts workflow access.', 50, 0, 1),
        (N'Supervisor/QC Inspector', N'Supervisor/QC Inspector', N'Quality control and supervisor access.', 60, 0, 1),
        (N'Accounting/Cashier', N'Accounting/Cashier', N'Invoice and payment workflow access.', 70, 0, 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Permission WHERE PermissionCode = N'dashboard.view')
BEGIN
    INSERT INTO dbo.Permission (ModuleName, ActionName, PermissionCode, Description, IsActive)
    SELECT module.ModuleName, action.ActionName,
           LOWER(REPLACE(module.ModuleName, N' ', N'')) + N'.' + LOWER(action.ActionName),
           action.ActionName + N' access for ' + module.ModuleName + N'.',
           1
    FROM (
        VALUES
            (N'Dashboard'),
            (N'Customers'),
            (N'Vehicles'),
            (N'Appointments'),
            (N'Service Queue'),
            (N'Inspections'),
            (N'Estimates'),
            (N'Work Orders'),
            (N'Inventory'),
            (N'Invoices'),
            (N'Reports'),
            (N'Employees'),
            (N'Settings')
    ) module(ModuleName)
    CROSS JOIN (
        VALUES
            (N'View'),
            (N'Create'),
            (N'Edit'),
            (N'Delete'),
            (N'Approve')
    ) action(ActionName)
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.Permission permission
        WHERE permission.PermissionCode = LOWER(REPLACE(module.ModuleName, N' ', N'')) + N'.' + LOWER(action.ActionName)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Bay WHERE Name = N'General Service Bay')
BEGIN
    INSERT INTO dbo.Bay (BayNumber, Name, Description, IsActive)
    VALUES
        (N'Bay 1', N'General Service Bay', N'Oil changes, inspections, and light mechanical work', 1),
        (N'Bay 2', N'Alignment Pit', N'Alignment and suspension service station', 1),
        (N'Bay 3', N'Tire & Brake Station', N'Tire, brake, and wheel service bay', 1),
        (N'Bay 4', N'Diagnostic Hub', N'Electrical and scan-tool diagnostic bay', 1),
        (N'Bay 5', N'Heavy Duty Lift', N'Truck and SUV lift bay', 1),
        (N'Bay 6', N'Paint Prep Bay', N'Prep and detailing bay', 0);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.EmployeeProfile WHERE EmployeeNumber = N'EMP-0001')
BEGIN
    INSERT INTO dbo.EmployeeProfile
        (EmployeeNumber, FirstName, MiddleName, LastName, Email, JobTitle, HourlyRate, HireDate, Phone, StreetAddress, City, Province, PostalCode, IsActive, IsArchived, IsDeleted)
    VALUES
        (N'EMP-0001', N'Marc', NULL, N'Tan', N'marc.tan@backyardmech.local', N'Shop Manager', 46.00, '2023-02-06', N'(709) 555-0101', N'15 Water Street', N'St. John''s', N'NL', N'A1C 1A1', 1, 0, 0),
        (N'EMP-0002', N'Christian Harold', NULL, N'Bandong', N'christian.bandong@backyardmech.local', N'Lead Mechanic', 42.50, '2022-09-12', N'(709) 555-0102', N'22 Duckworth Street', N'St. John''s', N'NL', N'A1C 2B2', 1, 0, 0),
        (N'EMP-0003', N'Randy', NULL, N'Tuazon', N'randy.tuazon@backyardmech.local', N'Diagnostics Technician', 39.00, '2024-01-15', N'(709) 555-0103', N'48 Kenmount Road', N'St. John''s', N'NL', N'A1B 3C3', 1, 0, 0),
        (N'EMP-0004', N'Rasheed', NULL, N'Altar', N'rasheed.altar@backyardmech.local', N'Service Advisor', 32.00, '2023-07-24', N'(709) 555-0104', N'9 Empire Avenue', N'St. John''s', N'NL', N'A1C 4D4', 1, 0, 0),
        (N'EMP-0005', N'Luis Allen', NULL, N'Ibarra', N'luis.ibarra@backyardmech.local', N'Parts Specialist', 29.50, '2024-05-02', N'(709) 555-0105', N'31 Torbay Road', N'St. John''s', N'NL', N'A1A 5E5', 1, 0, 0),
        (N'EMP-0006', N'Artlloyd', NULL, N'Balagosa', N'artlloyd.balagosa@backyardmech.local', N'Quality Inspector', 37.00, '2022-11-18', N'(709) 555-0106', N'77 Topsail Road', N'Mount Pearl', N'NL', N'A1N 1F1', 1, 0, 0),
        (N'EMP-0007', N'Matthew', NULL, N'Laluan', N'matthew.laluan@backyardmech.local', N'Apprentice Mechanic', 24.00, '2025-03-10', N'(709) 555-0107', N'63 Commonwealth Avenue', N'Mount Pearl', N'NL', N'A1N 2G2', 1, 0, 0),
        (N'EMP-0008', N'Chooey', NULL, N'Tan', N'chooey.tan@backyardmech.local', N'Mechanic', 35.00, '2023-10-03', N'(709) 555-0108', N'101 Elizabeth Avenue', N'St. John''s', N'NL', N'A1B 3H3', 1, 0, 0);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Customer WHERE Email = N'handzon.corpuz@example.com')
BEGIN
    INSERT INTO dbo.Customer
        (FirstName, LastName, Email, Phone, StreetAddress, City, Province, PostalCode, Notes, CreatedUtc, IsActive, IsArchived, IsDeleted)
    VALUES
        (N'Handzon', N'Corpuz', N'handzon.corpuz@example.com', N'(709) 555-0201', N'120 LeMarchant Road', N'St. John''s', N'NL', N'A1C 2H3', N'Prefers SMS reminders.', DATEADD(day, -28, SYSUTCDATETIME()), 1, 0, 0),
        (N'Jaypee', N'Francisco', N'jaypee.francisco@example.com', N'(709) 555-0202', N'15 Blackmarsh Road', N'St. John''s', N'NL', N'A1E 1S4', N'Fleet customer, asks for morning appointments.', DATEADD(day, -24, SYSUTCDATETIME()), 1, 0, 0),
        (N'Charles', N'Almirol', N'charles.almirol@example.com', N'(709) 555-0203', N'88 Portugal Cove Road', N'St. John''s', N'NL', N'A1B 2N8', N'Needs written estimates before work starts.', DATEADD(day, -20, SYSUTCDATETIME()), 1, 0, 0),
        (N'Alexander', N'Lababo', N'alexander.lababo@example.com', N'(709) 555-0204', N'44 Topsail Road', N'Mount Pearl', N'NL', N'A1N 2K2', N'Weekend pickup preferred.', DATEADD(day, -18, SYSUTCDATETIME()), 1, 0, 0),
        (N'Denise', N'Labasbas', N'denise.labasbas@example.com', N'(709) 555-0205', N'67 Freshwater Road', N'St. John''s', N'NL', N'A1C 5M2', N'Call before ordering special parts.', DATEADD(day, -14, SYSUTCDATETIME()), 1, 0, 0),
        (N'Jhanlaine', N'Basco', N'jhanlaine.basco@example.com', N'(709) 555-0206', N'5 Ropewalk Lane', N'St. John''s', N'NL', N'A1E 4P1', N'Returning customer, oil change package.', DATEADD(day, -10, SYSUTCDATETIME()), 1, 0, 0),
        (N'Gerain', N'Regero', N'gerain.regero@example.com', N'(709) 555-0207', N'72 Thorburn Road', N'St. John''s', N'NL', N'A1B 4R1', N'Drop-off after 8 AM.', DATEADD(day, -7, SYSUTCDATETIME()), 1, 0, 0),
        (N'Josh', N'Arceo', N'josh.arceo@example.com', N'(709) 555-0208', N'19 Park Avenue', N'Mount Pearl', N'NL', N'A1N 1J1', N'Needs invoice emailed.', DATEADD(day, -5, SYSUTCDATETIME()), 1, 0, 0);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Vehicle WHERE VIN = N'1FTFW1E58NFA00001')
BEGIN
    INSERT INTO dbo.Vehicle (VIN, Make, Model, Year, LicensePlate, Color, Odometer, CreatedUtc, IsArchived, IsDeleted)
    VALUES
        (N'1FTFW1E58NFA00001', N'Ford', N'F-150', 2022, N'NL-HTC-201', N'Blue', 48120, SYSUTCDATETIME(), 0, 0),
        (N'2T3RFREV8LW000002', N'Toyota', N'RAV4', 2020, N'NL-CTN-202', N'Silver', 63210, SYSUTCDATETIME(), 0, 0),
        (N'1HGCV1F31MA000003', N'Honda', N'Accord', 2021, N'NL-CHA-203', N'White', 38905, SYSUTCDATETIME(), 0, 0),
        (N'3GNAXUEV6NS000004', N'Chevrolet', N'Equinox', 2022, N'NL-ALB-204', N'Black', 27440, SYSUTCDATETIME(), 0, 0),
        (N'5NMS3CAD7LH000005', N'Hyundai', N'Santa Fe', 2020, N'NL-DLB-205', N'Red', 71280, SYSUTCDATETIME(), 0, 0),
        (N'JM3KFBDM8M0000006', N'Mazda', N'CX-5', 2021, N'NL-JHB-206', N'Gray', 45500, SYSUTCDATETIME(), 0, 0),
        (N'1C4HJXDG8MW000007', N'Jeep', N'Wrangler', 2021, N'NL-GRR-207', N'Green', 52990, SYSUTCDATETIME(), 0, 0),
        (N'5YJ3E1EA8NF000008', N'Tesla', N'Model 3', 2022, N'NL-JAR-208', N'Pearl', 31900, SYSUTCDATETIME(), 0, 0);

    INSERT INTO dbo.VehicleOwnership (VehicleId, CustomerId, OwnerFirstName, OwnerLastName, OwnershipStartUtc)
    SELECT v.VehicleId, c.CustomerId, c.FirstName, c.LastName, SYSUTCDATETIME()
    FROM dbo.Vehicle v
    JOIN (
        VALUES
            (N'1FTFW1E58NFA00001', N'handzon.corpuz@example.com'),
            (N'2T3RFREV8LW000002', N'jaypee.francisco@example.com'),
            (N'1HGCV1F31MA000003', N'charles.almirol@example.com'),
            (N'3GNAXUEV6NS000004', N'alexander.lababo@example.com'),
            (N'5NMS3CAD7LH000005', N'denise.labasbas@example.com'),
            (N'JM3KFBDM8M0000006', N'jhanlaine.basco@example.com'),
            (N'1C4HJXDG8MW000007', N'gerain.regero@example.com'),
            (N'5YJ3E1EA8NF000008', N'josh.arceo@example.com')
    ) map(VIN, Email) ON map.VIN = v.VIN
    JOIN dbo.Customer c ON c.Email = map.Email;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.PartCategory WHERE CategoryName = N'Brakes')
BEGIN
    INSERT INTO dbo.PartCategory (CategoryName, Description, IsActive)
    VALUES
        (N'Brakes', N'Pads, rotors, calipers, and brake hardware', 1),
        (N'Filters', N'Oil, air, fuel, and cabin filters', 1),
        (N'Electrical', N'Charging, starting, sensors, and wiring', 1),
        (N'Cooling', N'Radiators, hoses, thermostats, and coolant parts', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Part WHERE SKU = N'BRK-30294')
BEGIN
    INSERT INTO dbo.Part (PartCategoryId, SKU, Name, Description, UnitCost, UnitPrice, QuantityOnHand, ReorderThreshold, IsActive)
    SELECT PartCategoryId, N'BRK-30294', N'Ceramic Front Brake Pads', N'Premium ceramic pad set', 35.00, 65.00, 12, 5, 1 FROM dbo.PartCategory WHERE CategoryName = N'Brakes'
    UNION ALL SELECT PartCategoryId, N'FIL-10492', N'Synthetic Oil Filter', N'Extended-life spin-on oil filter', 8.50, 18.00, 3, 8, 1 FROM dbo.PartCategory WHERE CategoryName = N'Filters'
    UNION ALL SELECT PartCategoryId, N'ALT-88204', N'120A Alternator Assembly', N'Remanufactured alternator with warranty', 120.00, 210.00, 0, 2, 1 FROM dbo.PartCategory WHERE CategoryName = N'Electrical'
    UNION ALL SELECT PartCategoryId, N'RAD-99201', N'Aluminum Radiator Core', N'OE-style radiator replacement', 95.00, 165.00, 2, 1, 1 FROM dbo.PartCategory WHERE CategoryName = N'Cooling';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCategory WHERE CategoryName = N'Diagnostics')
BEGIN
    INSERT INTO dbo.ToolCategory (CategoryName, Description, IsActive)
    VALUES
        (N'Diagnostics', N'Scan tools, electrical meters, and diagnostic tablets', 1),
        (N'Hand Tools', N'Sockets, wrenches, torque tools, and pullers', 1),
        (N'Heavy Equipment', N'Lifts, presses, alignment, and tire machines', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Tool WHERE SerialNumber = N'AU-DIAG-0192')
BEGIN
    INSERT INTO dbo.Tool (ToolCategoryId, Name, SerialNumber, Location, Condition, LastCalibratedUtc, NextCalibrationDueUtc, IsActive)
    SELECT ToolCategoryId, N'Autel MaxiSYS Diagnostic Tablet', N'AU-DIAG-0192', N'Diagnostic Hub', N'Excellent', DATEADD(day, -40, SYSUTCDATETIME()), DATEADD(day, 50, SYSUTCDATETIME()), 1 FROM dbo.ToolCategory WHERE CategoryName = N'Diagnostics'
    UNION ALL SELECT ToolCategoryId, N'Blue-Point Digital Torque Wrench', N'BP-TWRX-2393', N'General Service Bay', N'Good', DATEADD(day, -80, SYSUTCDATETIME()), DATEADD(day, 10, SYSUTCDATETIME()), 1 FROM dbo.ToolCategory WHERE CategoryName = N'Hand Tools'
    UNION ALL SELECT ToolCategoryId, N'Hunter Wheel Balancer', N'HN-WB-3342', N'Tire & Brake Station', N'Fair', DATEADD(day, -120, SYSUTCDATETIME()), DATEADD(day, -5, SYSUTCDATETIME()), 1 FROM dbo.ToolCategory WHERE CategoryName = N'Heavy Equipment';

    INSERT INTO dbo.ToolCheckout (ToolId, MechanicName, CheckedOutUtc, ConditionAtCheckout, ToolIssueStatus, Notes)
    SELECT ToolId, N'Christian Harold Bandong', DATEADD(hour, -3, SYSUTCDATETIME()), N'Excellent', N'Checked Out', N'Used for drivability diagnostic.'
    FROM dbo.Tool WHERE SerialNumber = N'AU-DIAG-0192';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Appointment WHERE CustomerName = N'Handzon Corpuz' AND ScheduledStart >= CAST(SYSUTCDATETIME() AS DATE))
BEGIN
    DECLARE @Today DATE = CAST(SYSUTCDATETIME() AS DATE);

    INSERT INTO dbo.Appointment
        (CustomerId, CustomerName, VehicleId, BayId, AssignedMechanicName, ScheduledStart, ScheduledEnd, AppointmentType, AppointmentStatus, CreatedByName, Notes, CreatedUtc)
    SELECT c.CustomerId, CONCAT(c.FirstName, N' ', c.LastName), v.VehicleId, b.BayId, map.MechanicName,
           DATEADD(minute, map.StartMinute, CAST(@Today AS DATETIME2)),
           DATEADD(minute, map.EndMinute, CAST(@Today AS DATETIME2)),
           map.AppointmentType, map.AppointmentStatus, map.AdvisorName, map.Notes, SYSUTCDATETIME()
    FROM (
        VALUES
            (N'handzon.corpuz@example.com', N'1FTFW1E58NFA00001', N'General Service Bay', N'Christian Harold Bandong', 480, 600, N'Appointment', N'Scheduled', N'Rasheed Altar', N'Oil change and tire rotation'),
            (N'jaypee.francisco@example.com', N'2T3RFREV8LW000002', N'Alignment Pit', N'Randy Tuazon', 510, 660, N'Walk-in', N'Arrived', N'Marc Tan', N'Pulling to the right'),
            (N'charles.almirol@example.com', N'1HGCV1F31MA000003', N'Diagnostic Hub', N'Randy Tuazon', 555, 690, N'Appointment', N'Inspection', N'Rasheed Altar', N'Check engine light'),
            (N'alexander.lababo@example.com', N'3GNAXUEV6NS000004', N'Tire & Brake Station', N'Jaypee Francisco', 615, 795, N'Appointment', N'Waiting Approval', N'Marc Tan', N'Brake noise estimate'),
            (N'denise.labasbas@example.com', N'5NMS3CAD7LH000005', N'Heavy Duty Lift', N'Matthew Laluan', 660, 840, N'Appointment', N'In Bay', N'Rasheed Altar', N'Suspension clunk')
    ) map(Email, VIN, BayName, MechanicName, StartMinute, EndMinute, AppointmentType, AppointmentStatus, AdvisorName, Notes)
    JOIN dbo.Customer c ON c.Email = map.Email
    JOIN dbo.Vehicle v ON v.VIN = map.VIN
    JOIN dbo.Bay b ON b.Name = map.BayName;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.WorkOrder)
BEGIN
    INSERT INTO dbo.WorkOrder (AppointmentId, BayId, WorkOrderStatus, ScheduledStart, ScheduledEnd, CreatedUtc)
    SELECT AppointmentId, BayId, 
           CASE AppointmentStatus
               WHEN N'In Bay' THEN N'In Progress'
               WHEN N'Inspection' THEN N'Pending QC'
               WHEN N'Waiting Approval' THEN N'Pending'
               ELSE N'Pending'
           END,
           ScheduledStart, ScheduledEnd, SYSUTCDATETIME()
    FROM dbo.Appointment
    WHERE CustomerName IN (N'Handzon Corpuz', N'Jaypee Francisco', N'Charles Almirol', N'Alexander Lababo', N'Denise Labasbas');

    INSERT INTO dbo.WorkOrderAssignment (WorkOrderId, MechanicName, AssignmentRole, AssignedUtc)
    SELECT w.WorkOrderId, a.AssignedMechanicName, N'Primary', SYSUTCDATETIME()
    FROM dbo.WorkOrder w
    JOIN dbo.Appointment a ON a.AppointmentId = w.AppointmentId
    WHERE a.AssignedMechanicName IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Estimate)
BEGIN
    INSERT INTO dbo.Estimate (AppointmentId, CreatedByName, CreatedUtc)
    SELECT AppointmentId, CreatedByName, DATEADD(hour, -2, SYSUTCDATETIME())
    FROM dbo.Appointment
    WHERE AppointmentStatus IN (N'Inspection', N'Waiting Approval', N'In Bay');

    INSERT INTO dbo.EstimateRevision (EstimateId, RevisionNumber, EstimateStatus, ApprovalToken, ApprovalTokenExpiresUtc, SentToCustomerUtc, Subtotal, TaxAmount, Total, CreatedByName, CreatedUtc)
    SELECT EstimateId, 1,
           CASE ROW_NUMBER() OVER (ORDER BY EstimateId)
               WHEN 1 THEN N'Pending'
               WHEN 2 THEN N'Approved'
               ELSE N'Partially Approved'
           END,
           NEWID(), DATEADD(day, 7, SYSUTCDATETIME()), DATEADD(hour, -1, SYSUTCDATETIME()),
           650.00, 97.50, 747.50, N'Rasheed Altar', SYSUTCDATETIME()
    FROM dbo.Estimate;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Invoice)
BEGIN
    INSERT INTO dbo.Invoice (WorkOrderId, CustomerId, CustomerFirstName, CustomerLastName, InvoiceNumber, IssuedUtc, Subtotal, TaxAmount, Total, InvoiceStatus)
    SELECT w.WorkOrderId, a.CustomerId, c.FirstName, c.LastName, CONCAT(N'INV-', FORMAT(w.WorkOrderId, '0000')),
           DATEADD(day, -1, SYSUTCDATETIME()), 650.00, 97.50, 747.50,
           CASE ROW_NUMBER() OVER (ORDER BY w.WorkOrderId)
               WHEN 1 THEN N'Paid'
               WHEN 2 THEN N'Partially Paid'
               WHEN 3 THEN N'Issued'
               ELSE N'Draft'
           END
    FROM dbo.WorkOrder w
    JOIN dbo.Appointment a ON a.AppointmentId = w.AppointmentId
    LEFT JOIN dbo.Customer c ON c.CustomerId = a.CustomerId;

    INSERT INTO dbo.Payment (InvoiceId, Amount, PaymentMethod, ReferenceNumber, PaidUtc, ReceivedByName, Notes)
    SELECT InvoiceId,
           CASE InvoiceStatus WHEN N'Paid' THEN Total WHEN N'Partially Paid' THEN Total / 2 ELSE 0 END,
           N'Card', CONCAT(N'PAY-', InvoiceId), DATEADD(hour, -8, SYSUTCDATETIME()), N'Marc Tan', N'Demo payment'
    FROM dbo.Invoice
    WHERE InvoiceStatus IN (N'Paid', N'Partially Paid');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.AuditLogEntry WHERE Details = N'Demo data seed')
BEGIN
    INSERT INTO dbo.AuditLogEntry (UserName, Action, EntityName, EntityId, TimestampUtc, Details)
    VALUES
        (N'Marc Tan', N'Seeded', N'DemoData', N'BackyardMech', SYSUTCDATETIME(), N'Demo data seed'),
        (N'Christian Harold Bandong', N'Assigned', N'WorkOrder', N'Demo', SYSUTCDATETIME(), N'Demo data seed'),
        (N'Rasheed Altar', N'Created', N'Appointment', N'Demo', SYSUTCDATETIME(), N'Demo data seed');
END
GO

SELECT 'Demo data seed complete' AS Result;
