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
        -- Older EF-created databases incorrectly generated an independent identity for the user key.
        IF COLUMNPROPERTY(OBJECT_ID(N'dbo.AccountSecurity'), N'UserId', 'IsIdentity') = 1
        BEGIN
            BEGIN TRY
                BEGIN TRANSACTION;
                DECLARE @repairLock int;
                EXEC @repairLock = sys.sp_getapplock @Resource=N'BackyardMech.AccountSecuritySchema',
                    @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
                IF @repairLock < 0 THROW 51000, 'Account security schema is busy. Retry startup.', 1;
                IF COLUMNPROPERTY(OBJECT_ID(N'dbo.AccountSecurity'), N'UserId', 'IsIdentity') = 1
                BEGIN
                    CREATE TABLE dbo.AccountSecurity_KeyRepair (
                        UserId int NOT NULL PRIMARY KEY REFERENCES dbo.Users(Id),
                        FailedAttempts int NOT NULL DEFAULT 0,
                        LockedUntilUtc datetime2 NULL
                    );
                    INSERT dbo.AccountSecurity_KeyRepair
                        (UserId,FailedAttempts,LockedUntilUtc)
                    SELECT UserId,FailedAttempts,LockedUntilUtc
                    FROM dbo.AccountSecurity WITH (TABLOCKX,HOLDLOCK);
                    DROP TABLE dbo.AccountSecurity;
                    EXEC sys.sp_rename N'dbo.AccountSecurity_KeyRepair', N'AccountSecurity';
                END;
                COMMIT TRANSACTION;
            END TRY
            BEGIN CATCH
                IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
                THROW;
            END CATCH;
        END;
        IF OBJECT_ID('dbo.AccountSecurity', 'U') IS NULL
        CREATE TABLE dbo.AccountSecurity (
            UserId int NOT NULL PRIMARY KEY REFERENCES dbo.Users(Id),
            FailedAttempts int NOT NULL DEFAULT 0,
            LockedUntilUtc datetime2 NULL
        );
        """);
}
