-- Optional deployment script. Local startup applies the same additive schema.
-- Run in the application's configured database; do not change existing role defaults.
IF OBJECT_ID(N'dbo.UserPermissionOverride', N'U') IS NULL
CREATE TABLE dbo.UserPermissionOverride (
    UserPermissionOverrideId INT IDENTITY PRIMARY KEY,
    UserId INT NOT NULL REFERENCES dbo.Users(Id) ON DELETE CASCADE,
    PermissionId INT NOT NULL REFERENCES dbo.Permission(PermissionId),
    IsAllowed BIT NOT NULL,
    CONSTRAINT UX_UserPermissionOverride UNIQUE(UserId, PermissionId)
);
GO
