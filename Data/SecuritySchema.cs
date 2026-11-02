using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Data;

public static class SecuritySchema
{
    public static Task EnsureAsync(ApplicationDbContext db) => db.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID('dbo.UserPermissionOverride', 'U') IS NULL
        CREATE TABLE dbo.UserPermissionOverride (
            UserPermissionOverrideId int IDENTITY PRIMARY KEY,
            UserId int NOT NULL REFERENCES dbo.Users(Id) ON DELETE CASCADE,
            PermissionId int NOT NULL REFERENCES dbo.Permission(PermissionId),
            IsAllowed bit NOT NULL,
            CONSTRAINT UX_UserPermissionOverride UNIQUE(UserId,PermissionId)
        );
        IF COL_LENGTH('dbo.ServiceEntry', 'ApprovedEstimateLineId') IS NULL
            ALTER TABLE dbo.ServiceEntry ADD ApprovedEstimateLineId int NULL;
        IF COL_LENGTH('dbo.WorkOrder', 'ReleasedUtc') IS NULL
            ALTER TABLE dbo.WorkOrder ADD ReleasedUtc datetime2 NULL;
        IF COL_LENGTH('dbo.WorkOrder', 'ReleasedByUserId') IS NULL
            ALTER TABLE dbo.WorkOrder ADD ReleasedByUserId nvarchar(450) NULL;
        IF COL_LENGTH('dbo.WorkOrder', 'ReleasedByName') IS NULL
            ALTER TABLE dbo.WorkOrder ADD ReleasedByName nvarchar(256) NULL;
        IF OBJECT_ID('dbo.AccountSecurity', 'U') IS NULL
        CREATE TABLE dbo.AccountSecurity (
            UserId int NOT NULL PRIMARY KEY REFERENCES dbo.Users(Id),
            FailedAttempts int NOT NULL DEFAULT 0,
            LockedUntilUtc datetime2 NULL,
            AuthenticatorSecret nvarchar(2048) NULL,
            LastOtpStep bigint NOT NULL DEFAULT -1,
            RecoveryHashes nvarchar(max) NULL
        );
        """);
}
