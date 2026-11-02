using Backyard_Mech.Models;
using Backyard_Mech.Services;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            await dbContext.Database.EnsureCreatedAsync();
            await EnsureBusinessSchemaAsync(dbContext);
            await SecuritySchema.EnsureAsync(dbContext);
            await StaffAccessSeed.EnsureAsync(dbContext);

            if (await dbContext.Users.AnyAsync())
            {
                return;
            }

            var seedSection = configuration.GetSection("SeedAdminUser");
            var email = seedSection["Email"] ?? "admin123";
            var userName = seedSection["UserName"] ?? "Admin";
            var password = seedSection["Password"]
                ?? throw new InvalidOperationException("Seed admin password is required. Set SeedAdminUser__Password in configuration.");
            var role = seedSection["Role"] ?? "Owner";

            dbContext.Users.Add(new AppUser
            {
                Email = email,
                UserName = userName,
                PasswordHash = PasswordHasher.Hash(password),
                Role = role,
                IsActive = true
            });

            await dbContext.SaveChangesAsync();
        }

        private static async Task EnsureBusinessSchemaAsync(ApplicationDbContext dbContext)
        {
            await dbContext.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'dbo.AppRole', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.AppRole (
                        AppRoleId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppRole PRIMARY KEY,
                        RoleKey NVARCHAR(80) NOT NULL,
                        RoleName NVARCHAR(120) NOT NULL,
                        Description NVARCHAR(500) NULL,
                        DisplayOrder INT NOT NULL CONSTRAINT DF_AppRole_DisplayOrder DEFAULT (0),
                        IsSystemProtected BIT NOT NULL CONSTRAINT DF_AppRole_IsSystemProtected DEFAULT (0),
                        IsActive BIT NOT NULL CONSTRAINT DF_AppRole_IsActive DEFAULT (1)
                    );
                END;

                IF OBJECT_ID(N'dbo.RecordsDispositionPolicy', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.RecordsDispositionPolicy (
                        RecordsDispositionPolicyId INT NOT NULL CONSTRAINT PK_RecordsDispositionPolicy PRIMARY KEY,
                        RetentionValue INT NOT NULL CONSTRAINT DF_RecordsDispositionPolicy_RetentionValue DEFAULT (30),
                        RetentionUnit NVARCHAR(20) NOT NULL CONSTRAINT DF_RecordsDispositionPolicy_RetentionUnit DEFAULT (N'Days'),
                        UpdatedUtc DATETIME2 NOT NULL CONSTRAINT DF_RecordsDispositionPolicy_UpdatedUtc DEFAULT (SYSUTCDATETIME()),
                        UpdatedByName NVARCHAR(256) NULL,
                        CONSTRAINT CK_RecordsDispositionPolicy_RetentionValue CHECK (RetentionValue > 0),
                        CONSTRAINT CK_RecordsDispositionPolicy_RetentionUnit CHECK (RetentionUnit IN (N'Days', N'Weeks', N'Months', N'Years'))
                    );
                END;

                IF OBJECT_ID(N'dbo.Vehicle', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'dbo.Vehicle', N'OwnerFullName') IS NULL
                    BEGIN
                        ALTER TABLE dbo.Vehicle ADD OwnerFullName NVARCHAR(260) NULL;
                    END;

                    UPDATE vehicle
                    SET OwnerFullName = NULLIF(LTRIM(RTRIM(CONCAT(
                        COALESCE(ownership.OwnerFirstName, N''), N' ',
                        COALESCE(ownership.OwnerMiddleName + N' ', N''),
                        COALESCE(ownership.OwnerLastName, N'')
                    ))), N'')
                    FROM dbo.Vehicle vehicle
                    OUTER APPLY (
                        SELECT TOP (1) history.OwnerFirstName, history.OwnerMiddleName, history.OwnerLastName
                        FROM dbo.VehicleOwnership history
                        WHERE history.VehicleId = vehicle.VehicleId
                        ORDER BY history.OwnershipStartUtc DESC, history.VehicleOwnershipId DESC
                    ) ownership
                    WHERE vehicle.OwnerFullName IS NULL;
                END;

                IF OBJECT_ID(N'dbo.ProductListing', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'dbo.ProductListing', N'IsArchived') IS NULL
                        ALTER TABLE dbo.ProductListing ADD IsArchived BIT NOT NULL CONSTRAINT DF_ProductListing_IsArchived DEFAULT (0);
                    IF COL_LENGTH(N'dbo.ProductListing', N'IsDeleted') IS NULL
                        ALTER TABLE dbo.ProductListing ADD IsDeleted BIT NOT NULL CONSTRAINT DF_ProductListing_IsDeleted DEFAULT (0);
                    IF COL_LENGTH(N'dbo.ProductListing', N'ArchivedUtc') IS NULL
                        ALTER TABLE dbo.ProductListing ADD ArchivedUtc DATETIME2 NULL;
                    IF COL_LENGTH(N'dbo.ProductListing', N'DeletedUtc') IS NULL
                        ALTER TABLE dbo.ProductListing ADD DeletedUtc DATETIME2 NULL;
                    IF COL_LENGTH(N'dbo.ProductListing', N'ArchiveReason') IS NULL
                        ALTER TABLE dbo.ProductListing ADD ArchiveReason NVARCHAR(500) NULL;
                    IF COL_LENGTH(N'dbo.ProductListing', N'DeleteReason') IS NULL
                        ALTER TABLE dbo.ProductListing ADD DeleteReason NVARCHAR(500) NULL;
                END;

                IF NOT EXISTS (SELECT 1 FROM dbo.RecordsDispositionPolicy WHERE RecordsDispositionPolicyId = 1)
                BEGIN
                    INSERT INTO dbo.RecordsDispositionPolicy
                        (RecordsDispositionPolicyId, RetentionValue, RetentionUnit, UpdatedUtc, UpdatedByName)
                    VALUES
                        (1, 30, N'Days', SYSUTCDATETIME(), N'System Default');
                END;

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'UX_AppRole_RoleKey'
                      AND object_id = OBJECT_ID(N'dbo.AppRole')
                )
                BEGIN
                    CREATE UNIQUE INDEX UX_AppRole_RoleKey ON dbo.AppRole(RoleKey);
                END;

                MERGE dbo.AppRole AS target
                USING (VALUES
                    (N'Owner', N'Garage Owner', N'Full system owner access.', 10, 1, 1),
                    (N'Administrator', N'Administrator', N'Administrative garage operations access.', 20, 0, 1),
                    (N'Service Advisor', N'Service Advisor', N'Customer intake and service queue access.', 30, 0, 1),
                    (N'Mechanic/Technician', N'Mechanic/Technician', N'Work order and repair workflow access.', 40, 0, 1),
                    (N'Parts Clerk', N'Parts Clerk', N'Inventory and parts workflow access.', 50, 0, 1),
                    (N'Supervisor/QC Inspector', N'Supervisor/QC Inspector', N'Quality control and supervisor access.', 60, 0, 1),
                    (N'Accounting/Cashier', N'Accounting/Cashier', N'Invoice and payment workflow access.', 70, 0, 1)
                ) AS source(RoleKey, RoleName, Description, DisplayOrder, IsSystemProtected, IsActive)
                ON target.RoleKey = source.RoleKey
                WHEN MATCHED THEN
                    UPDATE SET
                        RoleName = source.RoleName,
                        Description = source.Description,
                        DisplayOrder = source.DisplayOrder,
                        IsSystemProtected = source.IsSystemProtected
                WHEN NOT MATCHED THEN
                    INSERT (RoleKey, RoleName, Description, DisplayOrder, IsSystemProtected, IsActive)
                    VALUES (source.RoleKey, source.RoleName, source.Description, source.DisplayOrder, source.IsSystemProtected, source.IsActive);

                INSERT INTO dbo.AppRole (RoleKey, RoleName, Description, DisplayOrder, IsSystemProtected, IsActive)
                SELECT DISTINCT u.Role, u.Role, N'Role discovered from existing user accounts.', 500, 0, 1
                FROM dbo.Users u
                WHERE u.Role IS NOT NULL
                  AND LTRIM(RTRIM(u.Role)) <> N''
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.AppRole role
                      WHERE role.RoleKey = u.Role
                  );

                MERGE dbo.Permission AS target
                USING (VALUES
                    (N'Dashboard', N'View', N'dashboard.view', N'View dashboard metrics and activity.'),
                    (N'Dashboard', N'Create', N'dashboard.create', N'Create dashboard records.'),
                    (N'Dashboard', N'Edit', N'dashboard.edit', N'Edit dashboard records.'),
                    (N'Dashboard', N'Delete', N'dashboard.delete', N'Delete dashboard records.'),
                    (N'Dashboard', N'Approve', N'dashboard.approve', N'Approve dashboard workflow items.'),
                    (N'Customers', N'View', N'customers.view', N'View customer records.'),
                    (N'Customers', N'Create', N'customers.create', N'Create customer records.'),
                    (N'Customers', N'Edit', N'customers.edit', N'Edit customer records.'),
                    (N'Customers', N'Delete', N'customers.delete', N'Delete customer records.'),
                    (N'Customers', N'Approve', N'customers.approve', N'Approve customer workflow items.'),
                    (N'Vehicles', N'View', N'vehicles.view', N'View vehicle records.'),
                    (N'Vehicles', N'Create', N'vehicles.create', N'Create vehicle records.'),
                    (N'Vehicles', N'Edit', N'vehicles.edit', N'Edit vehicle records.'),
                    (N'Vehicles', N'Delete', N'vehicles.delete', N'Delete vehicle records.'),
                    (N'Vehicles', N'Approve', N'vehicles.approve', N'Approve vehicle workflow items.'),
                    (N'Appointments', N'View', N'appointments.view', N'View appointments.'),
                    (N'Appointments', N'Create', N'appointments.create', N'Create appointments.'),
                    (N'Appointments', N'Edit', N'appointments.edit', N'Edit appointments.'),
                    (N'Appointments', N'Delete', N'appointments.delete', N'Delete appointments.'),
                    (N'Appointments', N'Approve', N'appointments.approve', N'Approve appointment workflow items.'),
                    (N'Service Queue', N'View', N'servicequeue.view', N'View service queue.'),
                    (N'Service Queue', N'Create', N'servicequeue.create', N'Create service queue items.'),
                    (N'Service Queue', N'Edit', N'servicequeue.edit', N'Edit service queue items.'),
                    (N'Service Queue', N'Delete', N'servicequeue.delete', N'Delete service queue items.'),
                    (N'Service Queue', N'Approve', N'servicequeue.approve', N'Approve service queue workflow items.'),
                    (N'Inspections', N'View', N'inspections.view', N'View inspections.'),
                    (N'Inspections', N'Create', N'inspections.create', N'Create inspections.'),
                    (N'Inspections', N'Edit', N'inspections.edit', N'Edit inspections.'),
                    (N'Inspections', N'Delete', N'inspections.delete', N'Delete inspections.'),
                    (N'Inspections', N'Approve', N'inspections.approve', N'Approve inspections.'),
                    (N'Estimates', N'View', N'estimates.view', N'View estimates.'),
                    (N'Estimates', N'Create', N'estimates.create', N'Create estimates.'),
                    (N'Estimates', N'Edit', N'estimates.edit', N'Edit estimates.'),
                    (N'Estimates', N'Delete', N'estimates.delete', N'Delete estimates.'),
                    (N'Estimates', N'Approve', N'estimates.approve', N'Approve estimates.'),
                    (N'Work Orders', N'View', N'workorders.view', N'View work orders.'),
                    (N'Work Orders', N'Create', N'workorders.create', N'Create work orders.'),
                    (N'Work Orders', N'Edit', N'workorders.edit', N'Edit work orders.'),
                    (N'Work Orders', N'Delete', N'workorders.delete', N'Delete work orders.'),
                    (N'Work Orders', N'Approve', N'workorders.approve', N'Approve work orders.'),
                    (N'Bays', N'View', N'bays.view', N'View service bays.'),
                    (N'Bays', N'Create', N'bays.create', N'Create service bays.'),
                    (N'Bays', N'Edit', N'bays.edit', N'Edit service bays and maintenance.'),
                    (N'Bays', N'Delete', N'bays.delete', N'Delete service bays.'),
                    (N'Bays', N'Approve', N'bays.approve', N'Approve bay workflow items.'),
                    (N'Inventory', N'View', N'inventory.view', N'View inventory.'),
                    (N'Inventory', N'Create', N'inventory.create', N'Create inventory records.'),
                    (N'Inventory', N'Edit', N'inventory.edit', N'Edit inventory records.'),
                    (N'Inventory', N'Delete', N'inventory.delete', N'Delete inventory records.'),
                    (N'Inventory', N'Approve', N'inventory.approve', N'Approve inventory workflow items.'),
                    (N'Labour', N'View', N'labour.view', N'View labour and time tracking.'),
                    (N'Labour', N'Create', N'labour.create', N'Create labour entries.'),
                    (N'Labour', N'Edit', N'labour.edit', N'Edit labour entries.'),
                    (N'Labour', N'Delete', N'labour.delete', N'Delete labour entries.'),
                    (N'Labour', N'Approve', N'labour.approve', N'Approve labour workflow items.'),
                    (N'Tools', N'View', N'tools.view', N'View tools and equipment.'),
                    (N'Tools', N'Create', N'tools.create', N'Create tools and checkouts.'),
                    (N'Tools', N'Edit', N'tools.edit', N'Edit tools and returns.'),
                    (N'Tools', N'Delete', N'tools.delete', N'Delete tools.'),
                    (N'Tools', N'Approve', N'tools.approve', N'Approve tool workflow items.'),
                    (N'Invoices', N'View', N'invoices.view', N'View invoices.'),
                    (N'Invoices', N'Create', N'invoices.create', N'Create invoices.'),
                    (N'Invoices', N'Edit', N'invoices.edit', N'Edit invoices.'),
                    (N'Invoices', N'Delete', N'invoices.delete', N'Delete invoices.'),
                    (N'Invoices', N'Approve', N'invoices.approve', N'Approve invoice workflow items.'),
                    (N'Records', N'View', N'records.view', N'View archive and records disposition.'),
                    (N'Records', N'Create', N'records.create', N'Create records governance entries.'),
                    (N'Records', N'Edit', N'records.edit', N'Restore records and edit retention settings.'),
                    (N'Records', N'Delete', N'records.delete', N'Permanently delete eligible records.'),
                    (N'Records', N'Approve', N'records.approve', N'Approve records governance actions.'),
                    (N'Reports', N'View', N'reports.view', N'View reports.'),
                    (N'Reports', N'Create', N'reports.create', N'Create reports.'),
                    (N'Reports', N'Edit', N'reports.edit', N'Edit reports.'),
                    (N'Reports', N'Delete', N'reports.delete', N'Delete reports.'),
                    (N'Reports', N'Approve', N'reports.approve', N'Approve reports.'),
                    (N'Employees', N'View', N'employees.view', N'View employees.'),
                    (N'Employees', N'Create', N'employees.create', N'Create employees.'),
                    (N'Employees', N'Edit', N'employees.edit', N'Edit employees.'),
                    (N'Employees', N'Delete', N'employees.delete', N'Delete employees.'),
                    (N'Employees', N'Approve', N'employees.approve', N'Approve employee workflow items.'),
                    (N'Settings', N'View', N'settings.view', N'View settings.'),
                    (N'Suppliers', N'View', N'suppliers.view', N'View suppliers.'),
                    (N'Suppliers', N'Create', N'suppliers.create', N'Create suppliers.'),
                    (N'Suppliers', N'Edit', N'suppliers.edit', N'Edit and deactivate suppliers.'),
                    (N'Services', N'View', N'services.view', N'View service catalog.'),
                    (N'Services', N'Create', N'services.create', N'Create services.'),
                    (N'Services', N'Edit', N'services.edit', N'Edit and deactivate services.'),
                    (N'Settings', N'Create', N'settings.create', N'Create settings records.'),
                    (N'Settings', N'Edit', N'settings.edit', N'Edit settings.'),
                    (N'Settings', N'Delete', N'settings.delete', N'Delete settings records.'),
                    (N'Settings', N'Approve', N'settings.approve', N'Approve settings changes.')
                ) AS source(ModuleName, ActionName, PermissionCode, Description)
                ON target.PermissionCode = source.PermissionCode
                WHEN MATCHED THEN
                    UPDATE SET
                        ModuleName = source.ModuleName,
                        ActionName = source.ActionName,
                        Description = source.Description,
                        IsActive = 1
                WHEN NOT MATCHED THEN
                    INSERT (ModuleName, ActionName, PermissionCode, Description, IsActive)
                    VALUES (source.ModuleName, source.ActionName, source.PermissionCode, source.Description, 1);

                IF OBJECT_ID(N'dbo.Bay', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'dbo.Bay', N'BayNumber') IS NULL
                    BEGIN
                        ALTER TABLE dbo.Bay ADD BayNumber NVARCHAR(50) NULL;
                    END;

                    EXEC(N'UPDATE dbo.Bay
                        SET BayNumber = CONCAT(N''Bay '', BayId)
                        WHERE BayNumber IS NULL OR LTRIM(RTRIM(BayNumber)) = N'''';');
                END;
                """);
        }
    }
}
