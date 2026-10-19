USE [AiMisErpCopilotDb];
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Users', N'PasswordHash') IS NULL
    ALTER TABLE dbo.Users ADD PasswordHash NVARCHAR(500) NULL;
GO

UPDATE dbo.Users SET PasswordHash = N'QWRtaW4tU2FsdC0xNg==.2W6YzDE6e77ReBdSYfVbUctlBOOSo+8QximRuupJrw8=' WHERE UserName = N'admin' AND PasswordHash IS NULL;

DECLARE @AdministratorRoleId UNIQUEIDENTIFIER = 'a0000000-0000-0000-0000-000000000001';
DECLARE @MisAnalystRoleId UNIQUEIDENTIFIER = 'a0000000-0000-0000-0000-000000000002';
DECLARE @BranchUserRoleId UNIQUEIDENTIFIER = 'a0000000-0000-0000-0000-000000000003';

MERGE dbo.Roles AS target
USING (VALUES
    (@AdministratorRoleId, N'Administrator', N'Full application administration.'),
    (@MisAnalystRoleId, N'MIS Analyst', N'Access to approved MIS analytics.'),
    (@BranchUserRoleId, N'Branch User', N'Access to assigned branch data only.')
) AS source (Id, Name, Description)
ON target.Name = source.Name
WHEN MATCHED THEN UPDATE SET Description = source.Description
WHEN NOT MATCHED THEN INSERT (Id, Name, Description) VALUES (source.Id, source.Name, source.Description);

DECLARE @AdminUserId UNIQUEIDENTIFIER = 'b0000000-0000-0000-0000-000000000001';
DECLARE @MisUserId UNIQUEIDENTIFIER = 'b0000000-0000-0000-0000-000000000002';
DECLARE @BranchUserId UNIQUEIDENTIFIER = 'b0000000-0000-0000-0000-000000000003';
DECLARE @DemoUserId UNIQUEIDENTIFIER = 'b0000000-0000-0000-0000-000000000004';

MERGE dbo.Users AS target
USING (VALUES
    (@AdminUserId, N'admin', N'Demo Administrator', N'admin@example.local', N'QWRtaW4tU2FsdC0xNg==.2W6YzDE6e77ReBdSYfVbUctlBOOSo+8QximRuupJrw8='),
    (@MisUserId, N'mis.analyst', N'Demo MIS Analyst', N'mis.analyst@example.local', N'TUlTLVNhbHQtMTYtQQ==.NdnQMXMU1Uacc+26LhkPbwLhOYbuWMSih16YQffVXOc='),
    (@BranchUserId, N'branch.0212', N'Demo Branch User', N'branch.0212@example.local', N'QnJhbmNoLVNhbHQtMTYtQQ==.Uq213RRdEEUsADKSds7p4J5kdOXAgA4xbkQKSF0ZK+I='),
    (@DemoUserId, N'demo', N'Demo User', N'demo@example.local', N'RGVtby1Vc2VyLVNhbHQtMTY=.5ChPCIZKosp30rw8RrkgvUKs91lCgzw18iXzHIIb/Sg=')
) AS source (Id, UserName, DisplayName, Email, PasswordHash)
ON target.UserName = source.UserName
WHEN MATCHED THEN UPDATE SET DisplayName = source.DisplayName, Email = source.Email, PasswordHash = source.PasswordHash, IsActive = 1
WHEN NOT MATCHED THEN INSERT (Id, UserName, DisplayName, Email, PasswordHash) VALUES (source.Id, source.UserName, source.DisplayName, source.Email, source.PasswordHash);

MERGE dbo.UserRoles AS target
USING (VALUES
    (@AdminUserId, @AdministratorRoleId),
    (@MisUserId, @MisAnalystRoleId),
    (@BranchUserId, @BranchUserRoleId),
    (@DemoUserId, @AdministratorRoleId)
) AS source (UserId, RoleId)
ON target.UserId = source.UserId AND target.RoleId = source.RoleId
WHEN NOT MATCHED THEN INSERT (UserId, RoleId) VALUES (source.UserId, source.RoleId);

MERGE dbo.UserBranchAccess AS target
USING (VALUES (@BranchUserId, N'0212')) AS source (UserId, BranchCode)
ON target.UserId = source.UserId AND target.BranchCode = source.BranchCode
WHEN NOT MATCHED THEN INSERT (UserId, BranchCode) VALUES (source.UserId, source.BranchCode);

COMMIT TRANSACTION;
GO

SELECT u.UserName, u.DisplayName, r.Name AS RoleName, uba.BranchCode
FROM dbo.Users AS u
LEFT JOIN dbo.UserRoles AS ur ON ur.UserId = u.Id
LEFT JOIN dbo.Roles AS r ON r.Id = ur.RoleId
LEFT JOIN dbo.UserBranchAccess AS uba ON uba.UserId = u.Id
WHERE u.UserName IN (N'admin', N'mis.analyst', N'branch.0212', N'demo')
ORDER BY u.UserName;
GO
