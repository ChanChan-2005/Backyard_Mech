/*
Backyard Mech business schema migration/setup script.

Safe behavior:
- Creates the Bakyard_Mech database when run from master and the database is missing.
- Creates only the 42 custom/business tables listed in the requirements.
- Does not drop or recreate tables.
- Adds foreign keys only when both tables exist.
- Adds ASP.NET Identity foreign keys only when AspNetUsers/AspNetRoles exist.
- Does not modify or recreate ASP.NET Identity tables.
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF DB_ID(N'Bakyard_Mech') IS NULL
BEGIN
    CREATE DATABASE [Bakyard_Mech];
END
GO

USE [Bakyard_Mech];
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
CREATE TABLE dbo.Users (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Email NVARCHAR(256) NOT NULL,
    UserName NVARCHAR(120) NOT NULL,
    PasswordHash NVARCHAR(512) NOT NULL,
    Role NVARCHAR(50) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (GETUTCDATE())
);
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Users_Email'
      AND object_id = OBJECT_ID(N'dbo.Users')
)
CREATE UNIQUE INDEX IX_Users_Email ON dbo.Users (Email);
GO

IF OBJECT_ID(N'dbo.AppRole', N'U') IS NULL
CREATE TABLE dbo.AppRole (
    AppRoleId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppRole PRIMARY KEY,
    RoleKey NVARCHAR(80) NOT NULL,
    RoleName NVARCHAR(120) NOT NULL,
    Description NVARCHAR(500) NULL,
    DisplayOrder INT NOT NULL CONSTRAINT DF_AppRole_DisplayOrder DEFAULT (0),
    IsSystemProtected BIT NOT NULL CONSTRAINT DF_AppRole_IsSystemProtected DEFAULT (0),
    IsActive BIT NOT NULL CONSTRAINT DF_AppRole_IsActive DEFAULT (1)
);
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_AppRole_RoleKey'
      AND object_id = OBJECT_ID(N'dbo.AppRole')
)
CREATE UNIQUE INDEX UX_AppRole_RoleKey ON dbo.AppRole (RoleKey);
GO

IF OBJECT_ID(N'dbo.EmployeeProfile', N'U') IS NULL
CREATE TABLE dbo.EmployeeProfile (
    EmployeeProfileId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmployeeProfile PRIMARY KEY,
    UserId NVARCHAR(450) NULL,
    EmployeeNumber NVARCHAR(50) NULL,
    FirstName NVARCHAR(120) NULL,
    MiddleName NVARCHAR(120) NULL,
    LastName NVARCHAR(120) NULL,
    Email NVARCHAR(256) NULL,
    JobTitle NVARCHAR(120) NULL,
    HourlyRate DECIMAL(18,2) NULL,
    HireDate DATE NULL,
    Phone NVARCHAR(40) NULL,
    StreetAddress NVARCHAR(250) NULL,
    City NVARCHAR(120) NULL,
    Province NVARCHAR(120) NULL,
    PostalCode NVARCHAR(20) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_EmployeeProfile_IsActive DEFAULT (1),
    IsArchived BIT NOT NULL CONSTRAINT DF_EmployeeProfile_IsArchived DEFAULT (0),
    IsDeleted BIT NOT NULL CONSTRAINT DF_EmployeeProfile_IsDeleted DEFAULT (0),
    ArchivedUtc DATETIME2 NULL,
    DeletedUtc DATETIME2 NULL,
    ArchiveReason NVARCHAR(500) NULL,
    DeleteReason NVARCHAR(500) NULL,
    CONSTRAINT CK_EmployeeProfile_HourlyRate CHECK (HourlyRate IS NULL OR HourlyRate >= 0)
);
GO

IF OBJECT_ID(N'dbo.EmployeeRateHistory', N'U') IS NULL
CREATE TABLE dbo.EmployeeRateHistory (
    EmployeeRateHistoryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmployeeRateHistory PRIMARY KEY,
    EmployeeProfileId INT NULL,
    HourlyRate DECIMAL(18,2) NULL,
    EffectiveFrom DATETIME2 NULL,
    EffectiveTo DATETIME2 NULL,
    ChangedByUserId NVARCHAR(450) NULL,
    ChangedByName NVARCHAR(256) NULL,
    CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_EmployeeRateHistory_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_EmployeeRateHistory_HourlyRate CHECK (HourlyRate IS NULL OR HourlyRate >= 0)
);
GO

IF OBJECT_ID(N'dbo.EmployeeAvailability', N'U') IS NULL
CREATE TABLE dbo.EmployeeAvailability (
    EmployeeAvailabilityId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmployeeAvailability PRIMARY KEY,
    EmployeeProfileId INT NULL,
    DayOfWeek INT NULL,
    StartTime TIME NULL,
    EndTime TIME NULL,
    IsAvailable BIT NOT NULL CONSTRAINT DF_EmployeeAvailability_IsAvailable DEFAULT (1),
    CONSTRAINT CK_EmployeeAvailability_DayOfWeek CHECK (DayOfWeek IS NULL OR DayOfWeek BETWEEN 0 AND 6)
);
GO

IF OBJECT_ID(N'dbo.EmployeeAvailabilityException', N'U') IS NULL
CREATE TABLE dbo.EmployeeAvailabilityException (
    EmployeeAvailabilityExceptionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmployeeAvailabilityException PRIMARY KEY,
    EmployeeProfileId INT NULL,
    StartUtc DATETIME2 NULL,
    EndUtc DATETIME2 NULL,
    Reason NVARCHAR(500) NULL,
    IsAvailable BIT NOT NULL CONSTRAINT DF_EmployeeAvailabilityException_IsAvailable DEFAULT (0)
);
GO

IF OBJECT_ID(N'dbo.Permission', N'U') IS NULL
CREATE TABLE dbo.Permission (
    PermissionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Permission PRIMARY KEY,
    ModuleName NVARCHAR(120) NULL,
    ActionName NVARCHAR(120) NULL,
    PermissionCode NVARCHAR(200) NULL,
    Description NVARCHAR(500) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Permission_IsActive DEFAULT (1)
);
GO

IF OBJECT_ID(N'dbo.RolePermission', N'U') IS NULL
CREATE TABLE dbo.RolePermission (
    RolePermissionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RolePermission PRIMARY KEY,
    RoleId NVARCHAR(450) NULL,
    PermissionId INT NULL
);
GO

IF OBJECT_ID(N'dbo.AuditLogEntry', N'U') IS NULL
CREATE TABLE dbo.AuditLogEntry (
    AuditLogEntryId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLogEntry PRIMARY KEY,
    UserId NVARCHAR(450) NULL,
    UserName NVARCHAR(256) NULL,
    Action NVARCHAR(120) NULL,
    EntityName NVARCHAR(120) NULL,
    EntityId NVARCHAR(120) NULL,
    TimestampUtc DATETIME2 NOT NULL CONSTRAINT DF_AuditLogEntry_TimestampUtc DEFAULT (SYSUTCDATETIME()),
    Details NVARCHAR(MAX) NULL
);
GO

IF OBJECT_ID(N'dbo.RecordsDispositionPolicy', N'U') IS NULL
CREATE TABLE dbo.RecordsDispositionPolicy (
    RecordsDispositionPolicyId INT NOT NULL CONSTRAINT PK_RecordsDispositionPolicy PRIMARY KEY,
    RetentionValue INT NOT NULL CONSTRAINT DF_RecordsDispositionPolicy_RetentionValue DEFAULT (30),
    RetentionUnit NVARCHAR(20) NOT NULL CONSTRAINT DF_RecordsDispositionPolicy_RetentionUnit DEFAULT (N'Days'),
    UpdatedUtc DATETIME2 NOT NULL CONSTRAINT DF_RecordsDispositionPolicy_UpdatedUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedByName NVARCHAR(256) NULL,
    CONSTRAINT CK_RecordsDispositionPolicy_RetentionValue CHECK (RetentionValue > 0),
    CONSTRAINT CK_RecordsDispositionPolicy_RetentionUnit CHECK (RetentionUnit IN (N'Days', N'Weeks', N'Months', N'Years'))
);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.RecordsDispositionPolicy WHERE RecordsDispositionPolicyId = 1)
INSERT INTO dbo.RecordsDispositionPolicy
    (RecordsDispositionPolicyId, RetentionValue, RetentionUnit, UpdatedUtc, UpdatedByName)
VALUES
    (1, 30, N'Days', SYSUTCDATETIME(), N'System Default');
GO

IF OBJECT_ID(N'dbo.Customer', N'U') IS NULL
CREATE TABLE dbo.Customer (
    CustomerId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customer PRIMARY KEY,
    FirstName NVARCHAR(120) NULL,
    MiddleName NVARCHAR(120) NULL,
    LastName NVARCHAR(120) NULL,
    Email NVARCHAR(256) NULL,
    Phone NVARCHAR(40) NULL,
    StreetAddress NVARCHAR(250) NULL,
    City NVARCHAR(120) NULL,
    Province NVARCHAR(120) NULL,
    PostalCode NVARCHAR(20) NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_Customer_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    IsActive BIT NOT NULL CONSTRAINT DF_Customer_IsActive DEFAULT (1),
    IsArchived BIT NOT NULL CONSTRAINT DF_Customer_IsArchived DEFAULT (0),
    IsDeleted BIT NOT NULL CONSTRAINT DF_Customer_IsDeleted DEFAULT (0),
    ArchivedUtc DATETIME2 NULL,
    DeletedUtc DATETIME2 NULL,
    ArchiveReason NVARCHAR(500) NULL,
    DeleteReason NVARCHAR(500) NULL
);
GO

IF OBJECT_ID(N'dbo.Vehicle', N'U') IS NULL
CREATE TABLE dbo.Vehicle (
    VehicleId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Vehicle PRIMARY KEY,
    VIN NVARCHAR(40) NULL,
    Make NVARCHAR(120) NULL,
    Model NVARCHAR(120) NULL,
    Year INT NULL,
    LicensePlate NVARCHAR(40) NULL,
    Color NVARCHAR(80) NULL,
    OwnerFullName NVARCHAR(260) NULL,
    Odometer INT NULL,
    CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_Vehicle_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    IsArchived BIT NOT NULL CONSTRAINT DF_Vehicle_IsArchived DEFAULT (0),
    IsDeleted BIT NOT NULL CONSTRAINT DF_Vehicle_IsDeleted DEFAULT (0),
    ArchivedUtc DATETIME2 NULL,
    DeletedUtc DATETIME2 NULL,
    ArchiveReason NVARCHAR(500) NULL,
    DeleteReason NVARCHAR(500) NULL,
    CONSTRAINT CK_Vehicle_Odometer CHECK (Odometer IS NULL OR Odometer >= 0)
);
GO

IF OBJECT_ID(N'dbo.Vehicle', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Vehicle', N'OwnerFullName') IS NULL ALTER TABLE dbo.Vehicle ADD OwnerFullName NVARCHAR(260) NULL;
    IF COL_LENGTH(N'dbo.Vehicle', N'ArchivedUtc') IS NULL ALTER TABLE dbo.Vehicle ADD ArchivedUtc DATETIME2 NULL;
    IF COL_LENGTH(N'dbo.Vehicle', N'DeletedUtc') IS NULL ALTER TABLE dbo.Vehicle ADD DeletedUtc DATETIME2 NULL;
    IF COL_LENGTH(N'dbo.Vehicle', N'ArchiveReason') IS NULL ALTER TABLE dbo.Vehicle ADD ArchiveReason NVARCHAR(500) NULL;
    IF COL_LENGTH(N'dbo.Vehicle', N'DeleteReason') IS NULL ALTER TABLE dbo.Vehicle ADD DeleteReason NVARCHAR(500) NULL;
END
GO

IF OBJECT_ID(N'dbo.VehicleOwnership', N'U') IS NULL
CREATE TABLE dbo.VehicleOwnership (
    VehicleOwnershipId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VehicleOwnership PRIMARY KEY,
    VehicleId INT NULL,
    CustomerId INT NULL,
    OwnerFirstName NVARCHAR(120) NULL,
    OwnerMiddleName NVARCHAR(120) NULL,
    OwnerLastName NVARCHAR(120) NULL,
    OwnershipStartUtc DATETIME2 NULL,
    OwnershipEndUtc DATETIME2 NULL
);
GO

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
GO

IF OBJECT_ID(N'dbo.EmployeeProfile', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.EmployeeProfile', N'FirstName') IS NULL ALTER TABLE dbo.EmployeeProfile ADD FirstName NVARCHAR(120) NULL;
    IF COL_LENGTH(N'dbo.EmployeeProfile', N'MiddleName') IS NULL ALTER TABLE dbo.EmployeeProfile ADD MiddleName NVARCHAR(120) NULL;
    IF COL_LENGTH(N'dbo.EmployeeProfile', N'LastName') IS NULL ALTER TABLE dbo.EmployeeProfile ADD LastName NVARCHAR(120) NULL;
    IF COL_LENGTH(N'dbo.EmployeeProfile', N'Email') IS NULL ALTER TABLE dbo.EmployeeProfile ADD Email NVARCHAR(256) NULL;
    IF COL_LENGTH(N'dbo.EmployeeProfile', N'StreetAddress') IS NULL ALTER TABLE dbo.EmployeeProfile ADD StreetAddress NVARCHAR(250) NULL;
    IF COL_LENGTH(N'dbo.EmployeeProfile', N'City') IS NULL ALTER TABLE dbo.EmployeeProfile ADD City NVARCHAR(120) NULL;
    IF COL_LENGTH(N'dbo.EmployeeProfile', N'Province') IS NULL ALTER TABLE dbo.EmployeeProfile ADD Province NVARCHAR(120) NULL;
    IF COL_LENGTH(N'dbo.EmployeeProfile', N'PostalCode') IS NULL ALTER TABLE dbo.EmployeeProfile ADD PostalCode NVARCHAR(20) NULL;
    IF COL_LENGTH(N'dbo.EmployeeProfile', N'IsArchived') IS NULL ALTER TABLE dbo.EmployeeProfile ADD IsArchived BIT NOT NULL CONSTRAINT DF_EmployeeProfile_IsArchived DEFAULT (0);
    IF COL_LENGTH(N'dbo.EmployeeProfile', N'IsDeleted') IS NULL ALTER TABLE dbo.EmployeeProfile ADD IsDeleted BIT NOT NULL CONSTRAINT DF_EmployeeProfile_IsDeleted DEFAULT (0);
    IF COL_LENGTH(N'dbo.EmployeeProfile', N'ArchivedUtc') IS NULL ALTER TABLE dbo.EmployeeProfile ADD ArchivedUtc DATETIME2 NULL;
    IF COL_LENGTH(N'dbo.EmployeeProfile', N'DeletedUtc') IS NULL ALTER TABLE dbo.EmployeeProfile ADD DeletedUtc DATETIME2 NULL;
    IF COL_LENGTH(N'dbo.EmployeeProfile', N'ArchiveReason') IS NULL ALTER TABLE dbo.EmployeeProfile ADD ArchiveReason NVARCHAR(500) NULL;
    IF COL_LENGTH(N'dbo.EmployeeProfile', N'DeleteReason') IS NULL ALTER TABLE dbo.EmployeeProfile ADD DeleteReason NVARCHAR(500) NULL;
END
GO

IF OBJECT_ID(N'dbo.Customer', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Customer', N'IsActive') IS NULL ALTER TABLE dbo.Customer ADD IsActive BIT NOT NULL CONSTRAINT DF_Customer_IsActive DEFAULT (1);
    IF COL_LENGTH(N'dbo.Customer', N'IsArchived') IS NULL ALTER TABLE dbo.Customer ADD IsArchived BIT NOT NULL CONSTRAINT DF_Customer_IsArchived DEFAULT (0);
    IF COL_LENGTH(N'dbo.Customer', N'IsDeleted') IS NULL ALTER TABLE dbo.Customer ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Customer_IsDeleted DEFAULT (0);
    IF COL_LENGTH(N'dbo.Customer', N'ArchivedUtc') IS NULL ALTER TABLE dbo.Customer ADD ArchivedUtc DATETIME2 NULL;
    IF COL_LENGTH(N'dbo.Customer', N'DeletedUtc') IS NULL ALTER TABLE dbo.Customer ADD DeletedUtc DATETIME2 NULL;
    IF COL_LENGTH(N'dbo.Customer', N'ArchiveReason') IS NULL ALTER TABLE dbo.Customer ADD ArchiveReason NVARCHAR(500) NULL;
    IF COL_LENGTH(N'dbo.Customer', N'DeleteReason') IS NULL ALTER TABLE dbo.Customer ADD DeleteReason NVARCHAR(500) NULL;
END
GO

IF OBJECT_ID(N'dbo.Vehicle', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Vehicle', N'IsArchived') IS NULL ALTER TABLE dbo.Vehicle ADD IsArchived BIT NOT NULL CONSTRAINT DF_Vehicle_IsArchived DEFAULT (0);
    IF COL_LENGTH(N'dbo.Vehicle', N'IsDeleted') IS NULL ALTER TABLE dbo.Vehicle ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Vehicle_IsDeleted DEFAULT (0);
END
GO

IF OBJECT_ID(N'dbo.Bay', N'U') IS NULL
CREATE TABLE dbo.Bay (
    BayId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Bay PRIMARY KEY,
    BayNumber NVARCHAR(50) NULL,
    Name NVARCHAR(120) NULL,
    Description NVARCHAR(500) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Bay_IsActive DEFAULT (1)
);
GO

IF OBJECT_ID(N'dbo.Bay', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Bay', N'BayNumber') IS NULL ALTER TABLE dbo.Bay ADD BayNumber NVARCHAR(50) NULL;

    EXEC(N'UPDATE dbo.Bay
        SET BayNumber = CONCAT(N''Bay '', BayId)
        WHERE BayNumber IS NULL OR LTRIM(RTRIM(BayNumber)) = N'''';');
END
GO

IF OBJECT_ID(N'dbo.BayMaintenanceSchedule', N'U') IS NULL
CREATE TABLE dbo.BayMaintenanceSchedule (
    BayMaintenanceScheduleId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BayMaintenanceSchedule PRIMARY KEY,
    BayId INT NOT NULL,
    MaintenanceType NVARCHAR(80) NOT NULL,
    StartDate DATE NOT NULL,
    EstimatedReturnDate DATE NOT NULL,
    AssignedTechnician NVARCHAR(120) NULL,
    Notes NVARCHAR(1000) NOT NULL,
    IsCompleted BIT NOT NULL CONSTRAINT DF_BayMaintenanceSchedule_IsCompleted DEFAULT (0),
    CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_BayMaintenanceSchedule_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_BayMaintenanceSchedule_DateRange CHECK (EstimatedReturnDate >= StartDate)
);
GO

IF OBJECT_ID(N'dbo.ServiceCategory', N'U') IS NULL
CREATE TABLE dbo.ServiceCategory (
    ServiceCategoryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ServiceCategory PRIMARY KEY,
    ParentServiceCategoryId INT NULL,
    CategoryName NVARCHAR(120) NULL,
    Description NVARCHAR(500) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_ServiceCategory_IsActive DEFAULT (1)
);
GO

IF OBJECT_ID(N'dbo.Service', N'U') IS NULL
CREATE TABLE dbo.Service (
    ServiceId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Service PRIMARY KEY,
    ServiceCategoryId INT NULL,
    ServiceName NVARCHAR(160) NULL,
    Description NVARCHAR(MAX) NULL,
    DefaultPrice DECIMAL(18,2) NULL,
    DefaultEstimatedHours DECIMAL(8,2) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Service_IsActive DEFAULT (1),
    CONSTRAINT CK_Service_DefaultPrice CHECK (DefaultPrice IS NULL OR DefaultPrice >= 0),
    CONSTRAINT CK_Service_DefaultEstimatedHours CHECK (DefaultEstimatedHours IS NULL OR DefaultEstimatedHours >= 0)
);
GO

IF OBJECT_ID(N'dbo.Supplier', N'U') IS NULL
CREATE TABLE dbo.Supplier (
    SupplierId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Supplier PRIMARY KEY,
    SupplierName NVARCHAR(160) NULL,
    ContactFirstName NVARCHAR(120) NULL,
    ContactMiddleName NVARCHAR(120) NULL,
    ContactLastName NVARCHAR(120) NULL,
    Phone NVARCHAR(40) NULL,
    Email NVARCHAR(256) NULL,
    StreetAddress NVARCHAR(250) NULL,
    City NVARCHAR(120) NULL,
    Province NVARCHAR(120) NULL,
    PostalCode NVARCHAR(20) NULL,
    AccountNumber NVARCHAR(80) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Supplier_IsActive DEFAULT (1)
);
GO

IF OBJECT_ID(N'dbo.PartCategory', N'U') IS NULL
CREATE TABLE dbo.PartCategory (
    PartCategoryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PartCategory PRIMARY KEY,
    ParentPartCategoryId INT NULL,
    CategoryName NVARCHAR(120) NULL,
    Description NVARCHAR(500) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_PartCategory_IsActive DEFAULT (1)
);
GO

IF OBJECT_ID(N'dbo.Part', N'U') IS NULL
CREATE TABLE dbo.Part (
    PartId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Part PRIMARY KEY,
    SupplierId INT NULL,
    PartCategoryId INT NULL,
    SKU NVARCHAR(80) NULL,
    Name NVARCHAR(160) NULL,
    Description NVARCHAR(MAX) NULL,
    UnitCost DECIMAL(18,2) NULL,
    UnitPrice DECIMAL(18,2) NULL,
    QuantityOnHand INT NOT NULL CONSTRAINT DF_Part_QuantityOnHand DEFAULT (0),
    ReorderThreshold INT NOT NULL CONSTRAINT DF_Part_ReorderThreshold DEFAULT (0),
    IsActive BIT NOT NULL CONSTRAINT DF_Part_IsActive DEFAULT (1),
    IsArchived BIT NOT NULL CONSTRAINT DF_Part_IsArchived DEFAULT (0),
    IsDeleted BIT NOT NULL CONSTRAINT DF_Part_IsDeleted DEFAULT (0),
    ArchivedUtc DATETIME2 NULL,
    DeletedUtc DATETIME2 NULL,
    ArchiveReason NVARCHAR(500) NULL,
    DeleteReason NVARCHAR(500) NULL,
    CONSTRAINT CK_Part_UnitCost CHECK (UnitCost IS NULL OR UnitCost >= 0),
    CONSTRAINT CK_Part_UnitPrice CHECK (UnitPrice IS NULL OR UnitPrice >= 0),
    CONSTRAINT CK_Part_QuantityOnHand CHECK (QuantityOnHand >= 0),
    CONSTRAINT CK_Part_ReorderThreshold CHECK (ReorderThreshold >= 0)
);
GO

IF OBJECT_ID(N'dbo.Part', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Part', N'IsArchived') IS NULL ALTER TABLE dbo.Part ADD IsArchived BIT NOT NULL CONSTRAINT DF_Part_IsArchived DEFAULT (0);
    IF COL_LENGTH(N'dbo.Part', N'IsDeleted') IS NULL ALTER TABLE dbo.Part ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Part_IsDeleted DEFAULT (0);
    IF COL_LENGTH(N'dbo.Part', N'ArchivedUtc') IS NULL ALTER TABLE dbo.Part ADD ArchivedUtc DATETIME2 NULL;
    IF COL_LENGTH(N'dbo.Part', N'DeletedUtc') IS NULL ALTER TABLE dbo.Part ADD DeletedUtc DATETIME2 NULL;
    IF COL_LENGTH(N'dbo.Part', N'ArchiveReason') IS NULL ALTER TABLE dbo.Part ADD ArchiveReason NVARCHAR(500) NULL;
    IF COL_LENGTH(N'dbo.Part', N'DeleteReason') IS NULL ALTER TABLE dbo.Part ADD DeleteReason NVARCHAR(500) NULL;
END
GO

IF OBJECT_ID(N'dbo.Appointment', N'U') IS NULL
CREATE TABLE dbo.Appointment (
    AppointmentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Appointment PRIMARY KEY,
    CustomerId INT NULL,
    CustomerName NVARCHAR(260) NULL,
    VehicleId INT NULL,
    BayId INT NULL,
    PreferredMechanicUserId NVARCHAR(450) NULL,
    PreferredMechanicName NVARCHAR(256) NULL,
    AssignedMechanicUserId NVARCHAR(450) NULL,
    AssignedMechanicName NVARCHAR(256) NULL,
    ScheduledStart DATETIME2 NULL,
    ScheduledEnd DATETIME2 NULL,
    AppointmentType NVARCHAR(80) NULL,
    AppointmentStatus NVARCHAR(80) NULL,
    CreatedByUserId NVARCHAR(450) NULL,
    CreatedByName NVARCHAR(256) NULL,
    CheckedInUtc DATETIME2 NULL,
    CheckedInByUserId NVARCHAR(450) NULL,
    CheckedInByName NVARCHAR(256) NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_Appointment_CreatedUtc DEFAULT (SYSUTCDATETIME())
);
GO

IF OBJECT_ID(N'dbo.Estimate', N'U') IS NULL
CREATE TABLE dbo.Estimate (
    EstimateId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Estimate PRIMARY KEY,
    AppointmentId INT NULL,
    CreatedByUserId NVARCHAR(450) NULL,
    CreatedByName NVARCHAR(256) NULL,
    CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_Estimate_CreatedUtc DEFAULT (SYSUTCDATETIME())
);
GO

IF OBJECT_ID(N'dbo.EstimateRevision', N'U') IS NULL
CREATE TABLE dbo.EstimateRevision (
    EstimateRevisionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EstimateRevision PRIMARY KEY,
    EstimateId INT NULL,
    RevisionNumber INT NULL,
    EstimateStatus NVARCHAR(80) NULL,
    ApprovalToken UNIQUEIDENTIFIER NULL,
    ApprovalTokenExpiresUtc DATETIME2 NULL,
    SentToCustomerUtc DATETIME2 NULL,
    RespondedUtc DATETIME2 NULL,
    Subtotal DECIMAL(18,2) NULL,
    TaxAmount DECIMAL(18,2) NULL,
    Total DECIMAL(18,2) NULL,
    RevisionReason NVARCHAR(MAX) NULL,
    CreatedByUserId NVARCHAR(450) NULL,
    CreatedByName NVARCHAR(256) NULL,
    CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_EstimateRevision_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_EstimateRevision_Subtotal CHECK (Subtotal IS NULL OR Subtotal >= 0),
    CONSTRAINT CK_EstimateRevision_TaxAmount CHECK (TaxAmount IS NULL OR TaxAmount >= 0),
    CONSTRAINT CK_EstimateRevision_Total CHECK (Total IS NULL OR Total >= 0)
);
GO

IF OBJECT_ID(N'dbo.EstimateRevisionLineItem', N'U') IS NULL
CREATE TABLE dbo.EstimateRevisionLineItem (
    EstimateRevisionLineItemId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EstimateRevisionLineItem PRIMARY KEY,
    EstimateRevisionId INT NULL,
    PartId INT NULL,
    ServiceId INT NULL,
    Description NVARCHAR(MAX) NULL,
    LineType NVARCHAR(40) NULL,
    Quantity DECIMAL(18,2) NULL,
    UnitPrice DECIMAL(18,2) NULL,
    LineTotal DECIMAL(18,2) NULL,
    ApprovalStatus NVARCHAR(80) NULL,
    CONSTRAINT CK_EstimateRevisionLineItem_Quantity CHECK (Quantity IS NULL OR Quantity >= 0),
    CONSTRAINT CK_EstimateRevisionLineItem_UnitPrice CHECK (UnitPrice IS NULL OR UnitPrice >= 0),
    CONSTRAINT CK_EstimateRevisionLineItem_LineTotal CHECK (LineTotal IS NULL OR LineTotal >= 0)
);
GO

IF OBJECT_ID(N'dbo.WorkOrder', N'U') IS NULL
CREATE TABLE dbo.WorkOrder (
    WorkOrderId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkOrder PRIMARY KEY,
    AppointmentId INT NULL,
    EstimateId INT NULL,
    ApprovedEstimateRevisionId INT NULL,
    BayId INT NULL,
    WorkOrderStatus NVARCHAR(80) NULL,
    ScheduledStart DATETIME2 NULL,
    ScheduledEnd DATETIME2 NULL,
    ActualStart DATETIME2 NULL,
    ActualEnd DATETIME2 NULL,
    CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_WorkOrder_CreatedUtc DEFAULT (SYSUTCDATETIME())
);
GO

IF OBJECT_ID(N'dbo.WorkOrderAssignment', N'U') IS NULL
CREATE TABLE dbo.WorkOrderAssignment (
    WorkOrderAssignmentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkOrderAssignment PRIMARY KEY,
    WorkOrderId INT NULL,
    MechanicUserId NVARCHAR(450) NULL,
    MechanicName NVARCHAR(256) NULL,
    AssignmentRole NVARCHAR(80) NULL,
    AssignedUtc DATETIME2 NOT NULL CONSTRAINT DF_WorkOrderAssignment_AssignedUtc DEFAULT (SYSUTCDATETIME())
);
GO

IF OBJECT_ID(N'dbo.AppointmentService', N'U') IS NULL
CREATE TABLE dbo.AppointmentService (
    AppointmentServiceId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppointmentService PRIMARY KEY,
    AppointmentId INT NULL,
    ServiceId INT NULL,
    ServiceName NVARCHAR(160) NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_AppointmentService_CreatedUtc DEFAULT (SYSUTCDATETIME())
);
GO

IF OBJECT_ID(N'dbo.QueueStatusHistory', N'U') IS NULL
CREATE TABLE dbo.QueueStatusHistory (
    QueueStatusHistoryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_QueueStatusHistory PRIMARY KEY,
    AppointmentId INT NULL,
    QueueStatus NVARCHAR(80) NULL,
    ChangedUtc DATETIME2 NOT NULL CONSTRAINT DF_QueueStatusHistory_ChangedUtc DEFAULT (SYSUTCDATETIME()),
    ChangedByUserId NVARCHAR(450) NULL,
    ChangedByName NVARCHAR(256) NULL,
    Notes NVARCHAR(MAX) NULL
);
GO

IF OBJECT_ID(N'dbo.Inspection', N'U') IS NULL
CREATE TABLE dbo.Inspection (
    InspectionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Inspection PRIMARY KEY,
    AppointmentId INT NULL,
    MechanicUserId NVARCHAR(450) NULL,
    MechanicName NVARCHAR(256) NULL,
    StartedUtc DATETIME2 NULL,
    CompletedUtc DATETIME2 NULL,
    Notes NVARCHAR(MAX) NULL
);
GO

IF OBJECT_ID(N'dbo.InspectionMedia', N'U') IS NULL
CREATE TABLE dbo.InspectionMedia (
    InspectionMediaId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InspectionMedia PRIMARY KEY,
    InspectionId INT NULL,
    FileType NVARCHAR(80) NULL,
    BlobUrl NVARCHAR(1000) NULL,
    UploadedUtc DATETIME2 NOT NULL CONSTRAINT DF_InspectionMedia_UploadedUtc DEFAULT (SYSUTCDATETIME()),
    UploadedByUserId NVARCHAR(450) NULL,
    UploadedByName NVARCHAR(256) NULL
);
GO

IF OBJECT_ID(N'dbo.PartTransaction', N'U') IS NULL
CREATE TABLE dbo.PartTransaction (
    PartTransactionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PartTransaction PRIMARY KEY,
    PartId INT NULL,
    WorkOrderId INT NULL,
    TransactionType NVARCHAR(80) NULL,
    Quantity INT NULL,
    UnitCostAtTransaction DECIMAL(18,2) NULL,
    PerformedByUserId NVARCHAR(450) NULL,
    PerformedByName NVARCHAR(256) NULL,
    PerformedUtc DATETIME2 NOT NULL CONSTRAINT DF_PartTransaction_PerformedUtc DEFAULT (SYSUTCDATETIME()),
    Notes NVARCHAR(MAX) NULL,
    CONSTRAINT CK_PartTransaction_Quantity CHECK (Quantity IS NULL OR Quantity >= 0),
    CONSTRAINT CK_PartTransaction_UnitCostAtTransaction CHECK (UnitCostAtTransaction IS NULL OR UnitCostAtTransaction >= 0)
);
GO

IF OBJECT_ID(N'dbo.ServiceEntry', N'U') IS NULL
CREATE TABLE dbo.ServiceEntry (
    ServiceEntryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ServiceEntry PRIMARY KEY,
    WorkOrderId INT NULL,
    ServiceId INT NULL,
    MechanicUserId NVARCHAR(450) NULL,
    MechanicName NVARCHAR(256) NULL,
    ServiceDescription NVARCHAR(MAX) NULL,
    EstimatedHours DECIMAL(8,2) NULL,
    DurationMinutes INT NULL,
    HourlyRate DECIMAL(18,2) NULL,
    ServiceCost DECIMAL(18,2) NULL,
    Notes NVARCHAR(MAX) NULL,
    CONSTRAINT CK_ServiceEntry_EstimatedHours CHECK (EstimatedHours IS NULL OR EstimatedHours >= 0),
    CONSTRAINT CK_ServiceEntry_DurationMinutes CHECK (DurationMinutes IS NULL OR DurationMinutes >= 0),
    CONSTRAINT CK_ServiceEntry_HourlyRate CHECK (HourlyRate IS NULL OR HourlyRate >= 0),
    CONSTRAINT CK_ServiceEntry_ServiceCost CHECK (ServiceCost IS NULL OR ServiceCost >= 0)
);
GO

IF OBJECT_ID(N'dbo.ServiceTimeSegment', N'U') IS NULL
CREATE TABLE dbo.ServiceTimeSegment (
    ServiceTimeSegmentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ServiceTimeSegment PRIMARY KEY,
    ServiceEntryId INT NULL,
    SegmentStart DATETIME2 NULL,
    SegmentEnd DATETIME2 NULL,
    SegmentType NVARCHAR(80) NULL
);
GO

IF OBJECT_ID(N'dbo.ToolCategory', N'U') IS NULL
CREATE TABLE dbo.ToolCategory (
    ToolCategoryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ToolCategory PRIMARY KEY,
    ParentToolCategoryId INT NULL,
    CategoryName NVARCHAR(120) NULL,
    Description NVARCHAR(500) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_ToolCategory_IsActive DEFAULT (1)
);
GO

IF OBJECT_ID(N'dbo.Tool', N'U') IS NULL
CREATE TABLE dbo.Tool (
    ToolId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Tool PRIMARY KEY,
    ToolCategoryId INT NULL,
    Name NVARCHAR(160) NULL,
    SerialNumber NVARCHAR(120) NULL,
    Location NVARCHAR(160) NULL,
    Condition NVARCHAR(80) NULL,
    LastCalibratedUtc DATETIME2 NULL,
    NextCalibrationDueUtc DATETIME2 NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Tool_IsActive DEFAULT (1),
    IsArchived BIT NOT NULL CONSTRAINT DF_Tool_IsArchived DEFAULT (0),
    IsDeleted BIT NOT NULL CONSTRAINT DF_Tool_IsDeleted DEFAULT (0),
    ArchivedUtc DATETIME2 NULL,
    DeletedUtc DATETIME2 NULL,
    ArchiveReason NVARCHAR(500) NULL,
    DeleteReason NVARCHAR(500) NULL
);
GO

IF OBJECT_ID(N'dbo.Tool', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Tool', N'IsArchived') IS NULL ALTER TABLE dbo.Tool ADD IsArchived BIT NOT NULL CONSTRAINT DF_Tool_IsArchived DEFAULT (0);
    IF COL_LENGTH(N'dbo.Tool', N'IsDeleted') IS NULL ALTER TABLE dbo.Tool ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Tool_IsDeleted DEFAULT (0);
    IF COL_LENGTH(N'dbo.Tool', N'ArchivedUtc') IS NULL ALTER TABLE dbo.Tool ADD ArchivedUtc DATETIME2 NULL;
    IF COL_LENGTH(N'dbo.Tool', N'DeletedUtc') IS NULL ALTER TABLE dbo.Tool ADD DeletedUtc DATETIME2 NULL;
    IF COL_LENGTH(N'dbo.Tool', N'ArchiveReason') IS NULL ALTER TABLE dbo.Tool ADD ArchiveReason NVARCHAR(500) NULL;
    IF COL_LENGTH(N'dbo.Tool', N'DeleteReason') IS NULL ALTER TABLE dbo.Tool ADD DeleteReason NVARCHAR(500) NULL;
END
GO

IF OBJECT_ID(N'dbo.ToolCheckout', N'U') IS NULL
CREATE TABLE dbo.ToolCheckout (
    ToolCheckoutId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ToolCheckout PRIMARY KEY,
    ToolId INT NULL,
    WorkOrderId INT NULL,
    MechanicUserId NVARCHAR(450) NULL,
    MechanicName NVARCHAR(256) NULL,
    CheckedOutUtc DATETIME2 NOT NULL CONSTRAINT DF_ToolCheckout_CheckedOutUtc DEFAULT (SYSUTCDATETIME()),
    ReturnedUtc DATETIME2 NULL,
    ConditionAtCheckout NVARCHAR(80) NULL,
    ConditionAtReturn NVARCHAR(80) NULL,
    ToolIssueStatus NVARCHAR(80) NULL,
    Notes NVARCHAR(MAX) NULL
);
GO

IF OBJECT_ID(N'dbo.ToolMaintenance', N'U') IS NULL
CREATE TABLE dbo.ToolMaintenance (
    ToolMaintenanceId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ToolMaintenance PRIMARY KEY,
    ToolId INT NULL,
    MaintenanceType NVARCHAR(80) NULL,
    PerformedUtc DATETIME2 NULL,
    NextDueUtc DATETIME2 NULL,
    PerformedByUserId NVARCHAR(450) NULL,
    PerformedByName NVARCHAR(256) NULL,
    Cost DECIMAL(18,2) NULL,
    Notes NVARCHAR(MAX) NULL,
    CONSTRAINT CK_ToolMaintenance_Cost CHECK (Cost IS NULL OR Cost >= 0)
);
GO

IF OBJECT_ID(N'dbo.QualityCheck', N'U') IS NULL
CREATE TABLE dbo.QualityCheck (
    QualityCheckId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_QualityCheck PRIMARY KEY,
    WorkOrderId INT NULL,
    InspectorUserId NVARCHAR(450) NULL,
    InspectorName NVARCHAR(256) NULL,
    Passed BIT NULL,
    PerformedUtc DATETIME2 NOT NULL CONSTRAINT DF_QualityCheck_PerformedUtc DEFAULT (SYSUTCDATETIME()),
    Notes NVARCHAR(MAX) NULL
);
GO

IF OBJECT_ID(N'dbo.TaxRate', N'U') IS NULL
CREATE TABLE dbo.TaxRate (
    TaxRateId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TaxRate PRIMARY KEY,
    Description NVARCHAR(250) NULL,
    Rate DECIMAL(6,4) NULL,
    EffectiveFrom DATETIME2 NULL,
    EffectiveTo DATETIME2 NULL,
    CONSTRAINT CK_TaxRate_Rate CHECK (Rate IS NULL OR Rate >= 0)
);
GO

IF OBJECT_ID(N'dbo.Invoice', N'U') IS NULL
CREATE TABLE dbo.Invoice (
    InvoiceId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Invoice PRIMARY KEY,
    WorkOrderId INT NULL,
    CustomerId INT NULL,
    CustomerFirstName NVARCHAR(120) NULL,
    CustomerMiddleName NVARCHAR(120) NULL,
    CustomerLastName NVARCHAR(120) NULL,
    InvoiceNumber NVARCHAR(40) NULL,
    IssuedUtc DATETIME2 NOT NULL CONSTRAINT DF_Invoice_IssuedUtc DEFAULT (SYSUTCDATETIME()),
    TaxRateId INT NULL,
    Subtotal DECIMAL(18,2) NULL,
    TaxAmount DECIMAL(18,2) NULL,
    Total DECIMAL(18,2) NULL,
    InvoiceStatus NVARCHAR(80) NULL,
    PaidUtc DATETIME2 NULL,
    CONSTRAINT CK_Invoice_Subtotal CHECK (Subtotal IS NULL OR Subtotal >= 0),
    CONSTRAINT CK_Invoice_TaxAmount CHECK (TaxAmount IS NULL OR TaxAmount >= 0),
    CONSTRAINT CK_Invoice_Total CHECK (Total IS NULL OR Total >= 0)
);
GO

IF OBJECT_ID(N'dbo.InvoiceLineItem', N'U') IS NULL
CREATE TABLE dbo.InvoiceLineItem (
    InvoiceLineItemId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InvoiceLineItem PRIMARY KEY,
    InvoiceId INT NULL,
    Description NVARCHAR(MAX) NULL,
    SourceType NVARCHAR(80) NULL,
    SourceId INT NULL,
    Quantity DECIMAL(18,2) NULL,
    UnitPrice DECIMAL(18,2) NULL,
    LineTotal DECIMAL(18,2) NULL,
    CONSTRAINT CK_InvoiceLineItem_Quantity CHECK (Quantity IS NULL OR Quantity >= 0),
    CONSTRAINT CK_InvoiceLineItem_UnitPrice CHECK (UnitPrice IS NULL OR UnitPrice >= 0),
    CONSTRAINT CK_InvoiceLineItem_LineTotal CHECK (LineTotal IS NULL OR LineTotal >= 0)
);
GO

IF OBJECT_ID(N'dbo.InvoiceAdjustment', N'U') IS NULL
CREATE TABLE dbo.InvoiceAdjustment (
    InvoiceAdjustmentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InvoiceAdjustment PRIMARY KEY,
    InvoiceId INT NULL,
    AdjustmentType NVARCHAR(80) NULL,
    Amount DECIMAL(18,2) NULL,
    Reason NVARCHAR(MAX) NULL,
    PerformedByUserId NVARCHAR(450) NULL,
    PerformedByName NVARCHAR(256) NULL,
    PerformedUtc DATETIME2 NOT NULL CONSTRAINT DF_InvoiceAdjustment_PerformedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_InvoiceAdjustment_Amount CHECK (Amount IS NULL OR Amount >= 0)
);
GO

IF OBJECT_ID(N'dbo.Payment', N'U') IS NULL
CREATE TABLE dbo.Payment (
    PaymentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payment PRIMARY KEY,
    InvoiceId INT NULL,
    Amount DECIMAL(18,2) NULL,
    PaymentMethod NVARCHAR(80) NULL,
    ReferenceNumber NVARCHAR(120) NULL,
    PaidUtc DATETIME2 NULL,
    ReceivedByUserId NVARCHAR(450) NULL,
    ReceivedByName NVARCHAR(256) NULL,
    Notes NVARCHAR(MAX) NULL,
    CONSTRAINT CK_Payment_Amount CHECK (Amount IS NULL OR Amount >= 0)
);
GO

IF OBJECT_ID(N'dbo.InvoiceNumberCounter', N'U') IS NULL
CREATE TABLE dbo.InvoiceNumberCounter (
    InvoiceNumberCounterId INT NOT NULL CONSTRAINT PK_InvoiceNumberCounter PRIMARY KEY,
    Prefix NVARCHAR(10) NOT NULL CONSTRAINT DF_InvoiceNumberCounter_Prefix DEFAULT (N'INV-'),
    LastNumber INT NOT NULL CONSTRAINT DF_InvoiceNumberCounter_LastNumber DEFAULT (0),
    CONSTRAINT CK_InvoiceNumberCounter_LastNumber CHECK (LastNumber >= 0)
);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.InvoiceNumberCounter WHERE InvoiceNumberCounterId = 1)
INSERT INTO dbo.InvoiceNumberCounter (InvoiceNumberCounterId, Prefix, LastNumber)
VALUES (1, N'INV-', 0);
GO

IF OBJECT_ID(N'dbo.VehicleOdometerHistory', N'U') IS NULL
CREATE TABLE dbo.VehicleOdometerHistory (
    VehicleOdometerHistoryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VehicleOdometerHistory PRIMARY KEY,
    VehicleId INT NULL,
    AppointmentId INT NULL,
    Odometer INT NULL,
    RecordedUtc DATETIME2 NOT NULL CONSTRAINT DF_VehicleOdometerHistory_RecordedUtc DEFAULT (SYSUTCDATETIME()),
    RecordedByUserId NVARCHAR(450) NULL,
    RecordedByName NVARCHAR(256) NULL,
    CONSTRAINT CK_VehicleOdometerHistory_Odometer CHECK (Odometer IS NULL OR Odometer >= 0)
);
GO

IF OBJECT_ID(N'dbo.ProductListing', N'U') IS NULL
CREATE TABLE dbo.ProductListing (
    ProductListingId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductListing PRIMARY KEY,
    ListingType NVARCHAR(40) NULL,
    PartId INT NULL,
    ServiceId INT NULL,
    ListingTitle NVARCHAR(200) NULL,
    ListingDescription NVARCHAR(MAX) NULL,
    ImageUrl NVARCHAR(1000) NULL,
    DisplayPrice DECIMAL(18,2) NULL,
    DisplayOrder INT NOT NULL CONSTRAINT DF_ProductListing_DisplayOrder DEFAULT (0),
    IsFeatured BIT NOT NULL CONSTRAINT DF_ProductListing_IsFeatured DEFAULT (0),
    IsPublished BIT NOT NULL CONSTRAINT DF_ProductListing_IsPublished DEFAULT (0),
    CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_ProductListing_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    IsArchived BIT NOT NULL CONSTRAINT DF_ProductListing_IsArchived DEFAULT (0),
    IsDeleted BIT NOT NULL CONSTRAINT DF_ProductListing_IsDeleted DEFAULT (0),
    ArchivedUtc DATETIME2 NULL,
    DeletedUtc DATETIME2 NULL,
    ArchiveReason NVARCHAR(500) NULL,
    DeleteReason NVARCHAR(500) NULL,
    CONSTRAINT CK_ProductListing_DisplayPrice CHECK (DisplayPrice IS NULL OR DisplayPrice >= 0),
    CONSTRAINT CK_ProductListing_DisplayOrder CHECK (DisplayOrder >= 0),
    CONSTRAINT CK_ProductListing_PartOrService CHECK (
        (PartId IS NOT NULL AND ServiceId IS NULL) OR
        (PartId IS NULL AND ServiceId IS NOT NULL) OR
        (PartId IS NULL AND ServiceId IS NULL)
    )
);
GO

CREATE OR ALTER PROCEDURE dbo.AddBackyardMechForeignKey
    @ConstraintName SYSNAME,
    @ParentTable SYSNAME,
    @ParentColumn SYSNAME,
    @ReferencedTable SYSNAME,
    @ReferencedColumn SYSNAME
AS
BEGIN
    SET NOCOUNT ON;

    IF OBJECT_ID(N'dbo.' + @ParentTable, N'U') IS NULL OR OBJECT_ID(N'dbo.' + @ReferencedTable, N'U') IS NULL
        RETURN;

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = @ConstraintName AND parent_object_id = OBJECT_ID(N'dbo.' + @ParentTable))
        RETURN;

    DECLARE @Sql NVARCHAR(MAX) =
        N'ALTER TABLE dbo.' + QUOTENAME(@ParentTable) +
        N' WITH CHECK ADD CONSTRAINT ' + QUOTENAME(@ConstraintName) +
        N' FOREIGN KEY (' + QUOTENAME(@ParentColumn) + N') REFERENCES dbo.' +
        QUOTENAME(@ReferencedTable) + N' (' + QUOTENAME(@ReferencedColumn) + N');';

    EXEC sys.sp_executesql @Sql;
END
GO

EXEC dbo.AddBackyardMechForeignKey N'FK_EmployeeRateHistory_EmployeeProfile', N'EmployeeRateHistory', N'EmployeeProfileId', N'EmployeeProfile', N'EmployeeProfileId';
EXEC dbo.AddBackyardMechForeignKey N'FK_EmployeeAvailability_EmployeeProfile', N'EmployeeAvailability', N'EmployeeProfileId', N'EmployeeProfile', N'EmployeeProfileId';
EXEC dbo.AddBackyardMechForeignKey N'FK_EmployeeAvailabilityException_EmployeeProfile', N'EmployeeAvailabilityException', N'EmployeeProfileId', N'EmployeeProfile', N'EmployeeProfileId';
EXEC dbo.AddBackyardMechForeignKey N'FK_RolePermission_Permission', N'RolePermission', N'PermissionId', N'Permission', N'PermissionId';
EXEC dbo.AddBackyardMechForeignKey N'FK_VehicleOwnership_Vehicle', N'VehicleOwnership', N'VehicleId', N'Vehicle', N'VehicleId';
EXEC dbo.AddBackyardMechForeignKey N'FK_VehicleOwnership_Customer', N'VehicleOwnership', N'CustomerId', N'Customer', N'CustomerId';
EXEC dbo.AddBackyardMechForeignKey N'FK_Appointment_Customer', N'Appointment', N'CustomerId', N'Customer', N'CustomerId';
EXEC dbo.AddBackyardMechForeignKey N'FK_Appointment_Vehicle', N'Appointment', N'VehicleId', N'Vehicle', N'VehicleId';
EXEC dbo.AddBackyardMechForeignKey N'FK_Appointment_Bay', N'Appointment', N'BayId', N'Bay', N'BayId';
EXEC dbo.AddBackyardMechForeignKey N'FK_BayMaintenanceSchedule_Bay', N'BayMaintenanceSchedule', N'BayId', N'Bay', N'BayId';
EXEC dbo.AddBackyardMechForeignKey N'FK_ServiceCategory_Parent', N'ServiceCategory', N'ParentServiceCategoryId', N'ServiceCategory', N'ServiceCategoryId';
EXEC dbo.AddBackyardMechForeignKey N'FK_Service_ServiceCategory', N'Service', N'ServiceCategoryId', N'ServiceCategory', N'ServiceCategoryId';
EXEC dbo.AddBackyardMechForeignKey N'FK_PartCategory_Parent', N'PartCategory', N'ParentPartCategoryId', N'PartCategory', N'PartCategoryId';
EXEC dbo.AddBackyardMechForeignKey N'FK_Part_Supplier', N'Part', N'SupplierId', N'Supplier', N'SupplierId';
EXEC dbo.AddBackyardMechForeignKey N'FK_Part_PartCategory', N'Part', N'PartCategoryId', N'PartCategory', N'PartCategoryId';
EXEC dbo.AddBackyardMechForeignKey N'FK_Estimate_Appointment', N'Estimate', N'AppointmentId', N'Appointment', N'AppointmentId';
EXEC dbo.AddBackyardMechForeignKey N'FK_EstimateRevision_Estimate', N'EstimateRevision', N'EstimateId', N'Estimate', N'EstimateId';
EXEC dbo.AddBackyardMechForeignKey N'FK_EstimateRevisionLineItem_EstimateRevision', N'EstimateRevisionLineItem', N'EstimateRevisionId', N'EstimateRevision', N'EstimateRevisionId';
EXEC dbo.AddBackyardMechForeignKey N'FK_EstimateRevisionLineItem_Part', N'EstimateRevisionLineItem', N'PartId', N'Part', N'PartId';
EXEC dbo.AddBackyardMechForeignKey N'FK_EstimateRevisionLineItem_Service', N'EstimateRevisionLineItem', N'ServiceId', N'Service', N'ServiceId';
EXEC dbo.AddBackyardMechForeignKey N'FK_WorkOrder_Appointment', N'WorkOrder', N'AppointmentId', N'Appointment', N'AppointmentId';
EXEC dbo.AddBackyardMechForeignKey N'FK_WorkOrder_Estimate', N'WorkOrder', N'EstimateId', N'Estimate', N'EstimateId';
EXEC dbo.AddBackyardMechForeignKey N'FK_WorkOrder_ApprovedEstimateRevision', N'WorkOrder', N'ApprovedEstimateRevisionId', N'EstimateRevision', N'EstimateRevisionId';
EXEC dbo.AddBackyardMechForeignKey N'FK_WorkOrder_Bay', N'WorkOrder', N'BayId', N'Bay', N'BayId';
EXEC dbo.AddBackyardMechForeignKey N'FK_WorkOrderAssignment_WorkOrder', N'WorkOrderAssignment', N'WorkOrderId', N'WorkOrder', N'WorkOrderId';
EXEC dbo.AddBackyardMechForeignKey N'FK_AppointmentService_Appointment', N'AppointmentService', N'AppointmentId', N'Appointment', N'AppointmentId';
EXEC dbo.AddBackyardMechForeignKey N'FK_AppointmentService_Service', N'AppointmentService', N'ServiceId', N'Service', N'ServiceId';
EXEC dbo.AddBackyardMechForeignKey N'FK_QueueStatusHistory_Appointment', N'QueueStatusHistory', N'AppointmentId', N'Appointment', N'AppointmentId';
EXEC dbo.AddBackyardMechForeignKey N'FK_Inspection_Appointment', N'Inspection', N'AppointmentId', N'Appointment', N'AppointmentId';
EXEC dbo.AddBackyardMechForeignKey N'FK_InspectionMedia_Inspection', N'InspectionMedia', N'InspectionId', N'Inspection', N'InspectionId';
EXEC dbo.AddBackyardMechForeignKey N'FK_PartTransaction_Part', N'PartTransaction', N'PartId', N'Part', N'PartId';
EXEC dbo.AddBackyardMechForeignKey N'FK_PartTransaction_WorkOrder', N'PartTransaction', N'WorkOrderId', N'WorkOrder', N'WorkOrderId';
EXEC dbo.AddBackyardMechForeignKey N'FK_ServiceEntry_WorkOrder', N'ServiceEntry', N'WorkOrderId', N'WorkOrder', N'WorkOrderId';
EXEC dbo.AddBackyardMechForeignKey N'FK_ServiceEntry_Service', N'ServiceEntry', N'ServiceId', N'Service', N'ServiceId';
EXEC dbo.AddBackyardMechForeignKey N'FK_ServiceTimeSegment_ServiceEntry', N'ServiceTimeSegment', N'ServiceEntryId', N'ServiceEntry', N'ServiceEntryId';
EXEC dbo.AddBackyardMechForeignKey N'FK_ToolCategory_Parent', N'ToolCategory', N'ParentToolCategoryId', N'ToolCategory', N'ToolCategoryId';
EXEC dbo.AddBackyardMechForeignKey N'FK_Tool_ToolCategory', N'Tool', N'ToolCategoryId', N'ToolCategory', N'ToolCategoryId';
EXEC dbo.AddBackyardMechForeignKey N'FK_ToolCheckout_Tool', N'ToolCheckout', N'ToolId', N'Tool', N'ToolId';
EXEC dbo.AddBackyardMechForeignKey N'FK_ToolCheckout_WorkOrder', N'ToolCheckout', N'WorkOrderId', N'WorkOrder', N'WorkOrderId';
EXEC dbo.AddBackyardMechForeignKey N'FK_ToolMaintenance_Tool', N'ToolMaintenance', N'ToolId', N'Tool', N'ToolId';
EXEC dbo.AddBackyardMechForeignKey N'FK_QualityCheck_WorkOrder', N'QualityCheck', N'WorkOrderId', N'WorkOrder', N'WorkOrderId';
EXEC dbo.AddBackyardMechForeignKey N'FK_Invoice_WorkOrder', N'Invoice', N'WorkOrderId', N'WorkOrder', N'WorkOrderId';
EXEC dbo.AddBackyardMechForeignKey N'FK_Invoice_Customer', N'Invoice', N'CustomerId', N'Customer', N'CustomerId';
EXEC dbo.AddBackyardMechForeignKey N'FK_Invoice_TaxRate', N'Invoice', N'TaxRateId', N'TaxRate', N'TaxRateId';
EXEC dbo.AddBackyardMechForeignKey N'FK_InvoiceLineItem_Invoice', N'InvoiceLineItem', N'InvoiceId', N'Invoice', N'InvoiceId';
EXEC dbo.AddBackyardMechForeignKey N'FK_InvoiceAdjustment_Invoice', N'InvoiceAdjustment', N'InvoiceId', N'Invoice', N'InvoiceId';
EXEC dbo.AddBackyardMechForeignKey N'FK_Payment_Invoice', N'Payment', N'InvoiceId', N'Invoice', N'InvoiceId';
EXEC dbo.AddBackyardMechForeignKey N'FK_VehicleOdometerHistory_Vehicle', N'VehicleOdometerHistory', N'VehicleId', N'Vehicle', N'VehicleId';
EXEC dbo.AddBackyardMechForeignKey N'FK_VehicleOdometerHistory_Appointment', N'VehicleOdometerHistory', N'AppointmentId', N'Appointment', N'AppointmentId';
EXEC dbo.AddBackyardMechForeignKey N'FK_ProductListing_Part', N'ProductListing', N'PartId', N'Part', N'PartId';
EXEC dbo.AddBackyardMechForeignKey N'FK_ProductListing_Service', N'ProductListing', N'ServiceId', N'Service', N'ServiceId';
GO

IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NOT NULL
BEGIN
    EXEC dbo.AddBackyardMechForeignKey N'FK_EmployeeProfile_AspNetUsers_UserId', N'EmployeeProfile', N'UserId', N'AspNetUsers', N'Id';
END
GO

IF OBJECT_ID(N'dbo.AspNetRoles', N'U') IS NOT NULL
BEGIN
    EXEC dbo.AddBackyardMechForeignKey N'FK_RolePermission_AspNetRoles_RoleId', N'RolePermission', N'RoleId', N'AspNetRoles', N'Id';
END
GO

DROP PROCEDURE dbo.AddBackyardMechForeignKey;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_EmployeeProfile_EmployeeNumber' AND object_id = OBJECT_ID(N'dbo.EmployeeProfile'))
CREATE UNIQUE INDEX UX_EmployeeProfile_EmployeeNumber ON dbo.EmployeeProfile(EmployeeNumber) WHERE EmployeeNumber IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Permission_PermissionCode' AND object_id = OBJECT_ID(N'dbo.Permission'))
CREATE UNIQUE INDEX UX_Permission_PermissionCode ON dbo.Permission(PermissionCode) WHERE PermissionCode IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Part_SKU' AND object_id = OBJECT_ID(N'dbo.Part'))
CREATE UNIQUE INDEX UX_Part_SKU ON dbo.Part(SKU) WHERE SKU IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Invoice_InvoiceNumber' AND object_id = OBJECT_ID(N'dbo.Invoice'))
CREATE UNIQUE INDEX UX_Invoice_InvoiceNumber ON dbo.Invoice(InvoiceNumber) WHERE InvoiceNumber IS NOT NULL;
GO

SELECT t.name AS BusinessTable
FROM sys.tables t
WHERE t.name IN (
    N'EmployeeProfile', N'EmployeeRateHistory', N'EmployeeAvailability', N'EmployeeAvailabilityException',
    N'Permission', N'RolePermission', N'AuditLogEntry', N'Customer', N'Vehicle', N'VehicleOwnership',
    N'VehicleOdometerHistory', N'Bay', N'Appointment', N'AppointmentService', N'QueueStatusHistory',
    N'Inspection', N'InspectionMedia', N'Supplier', N'PartCategory', N'Part', N'PartTransaction',
    N'ServiceCategory', N'Service', N'Estimate', N'EstimateRevision', N'EstimateRevisionLineItem',
    N'WorkOrder', N'WorkOrderAssignment', N'ServiceEntry', N'ServiceTimeSegment', N'ToolCategory',
    N'Tool', N'ToolCheckout', N'ToolMaintenance', N'QualityCheck', N'TaxRate', N'Invoice',
    N'InvoiceLineItem', N'InvoiceAdjustment', N'Payment', N'InvoiceNumberCounter', N'ProductListing'
)
ORDER BY t.name;
GO
