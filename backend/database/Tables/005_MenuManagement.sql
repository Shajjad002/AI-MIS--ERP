IF OBJECT_ID(N'dbo.MenuRoleDefinitions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MenuRoleDefinitions (
        RoleName NVARCHAR(100) NOT NULL PRIMARY KEY,
        IsSystemRole BIT NOT NULL CONSTRAINT DF_MenuRoleDefinitions_IsSystemRole DEFAULT 0,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_MenuRoleDefinitions_CreatedAtUtc DEFAULT SYSUTCDATETIME()
    );
END;
GO

IF OBJECT_ID(N'dbo.MenuItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MenuItems (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MenuItems PRIMARY KEY,
        Label NVARCHAR(100) NOT NULL,
        Section NVARCHAR(100) NOT NULL,
        PageKey NVARCHAR(30) NOT NULL,
        Icon NVARCHAR(30) NOT NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_MenuItems_SortOrder DEFAULT 0,
        IsEnabled BIT NOT NULL CONSTRAINT DF_MenuItems_IsEnabled DEFAULT 1,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_MenuItems_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_MenuItems_PageKey CHECK (PageKey IN (N'home', N'visualization', N'documents', N'users'))
    );
END;
GO

IF OBJECT_ID(N'dbo.MenuItemRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MenuItemRoles (
        MenuItemId UNIQUEIDENTIFIER NOT NULL,
        RoleName NVARCHAR(100) NOT NULL,
        CONSTRAINT PK_MenuItemRoles PRIMARY KEY (MenuItemId, RoleName),
        CONSTRAINT FK_MenuItemRoles_MenuItems FOREIGN KEY (MenuItemId) REFERENCES dbo.MenuItems(Id) ON DELETE CASCADE,
        CONSTRAINT FK_MenuItemRoles_MenuRoleDefinitions FOREIGN KEY (RoleName) REFERENCES dbo.MenuRoleDefinitions(RoleName)
    );
END;
GO

IF OBJECT_ID(N'dbo.UserMenuRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserMenuRoles (
        UserId UNIQUEIDENTIFIER NOT NULL,
        RoleName NVARCHAR(100) NOT NULL,
        CONSTRAINT PK_UserMenuRoles PRIMARY KEY (UserId, RoleName),
        CONSTRAINT FK_UserMenuRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE,
        CONSTRAINT FK_UserMenuRoles_MenuRoleDefinitions FOREIGN KEY (RoleName) REFERENCES dbo.MenuRoleDefinitions(RoleName)
    );
END;
GO

INSERT INTO dbo.MenuRoleDefinitions (RoleName, IsSystemRole)
SELECT role.Name, 1
FROM (VALUES (N'Administrator'), (N'MIS Analyst'), (N'Branch User')) role(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.MenuRoleDefinitions existing WHERE existing.RoleName = role.Name);
GO

DECLARE @AllUsers TABLE (RoleName NVARCHAR(100) NOT NULL);
INSERT INTO @AllUsers (RoleName) VALUES (N'Administrator'), (N'MIS Analyst'), (N'Branch User');

IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE Label = N'Overview' AND Section = N'Workspace')
BEGIN
    DECLARE @OverviewId UNIQUEIDENTIFIER = 'b1000000-0000-0000-0000-000000000001';
    INSERT INTO dbo.MenuItems (Id, Label, Section, PageKey, Icon, SortOrder)
    VALUES (@OverviewId, N'Overview', N'Workspace', N'home', N'overview', 10);
    INSERT INTO dbo.MenuItemRoles (MenuItemId, RoleName)
    SELECT @OverviewId, RoleName FROM @AllUsers;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE Label = N'Reports' AND Section = N'Analytics')
BEGIN
    DECLARE @ReportsId UNIQUEIDENTIFIER = 'b1000000-0000-0000-0000-000000000002';
    INSERT INTO dbo.MenuItems (Id, Label, Section, PageKey, Icon, SortOrder)
    VALUES (@ReportsId, N'Reports', N'Analytics', N'visualization', N'report', 10);
    INSERT INTO dbo.MenuItemRoles (MenuItemId, RoleName)
    SELECT @ReportsId, RoleName FROM @AllUsers;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE Label = N'Sources' AND Section = N'Data Management')
BEGIN
    DECLARE @SourcesId UNIQUEIDENTIFIER = 'b1000000-0000-0000-0000-000000000003';
    INSERT INTO dbo.MenuItems (Id, Label, Section, PageKey, Icon, SortOrder)
    VALUES (@SourcesId, N'Sources', N'Data Management', N'documents', N'source', 10);
    INSERT INTO dbo.MenuItemRoles (MenuItemId, RoleName)
    VALUES (@SourcesId, N'Administrator'), (@SourcesId, N'MIS Analyst');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE Label = N'User access' AND Section = N'Configuration')
BEGIN
    DECLARE @UserAccessId UNIQUEIDENTIFIER = 'b1000000-0000-0000-0000-000000000004';
    INSERT INTO dbo.MenuItems (Id, Label, Section, PageKey, Icon, SortOrder)
    VALUES (@UserAccessId, N'User access', N'Configuration', N'users', N'users', 10);
    INSERT INTO dbo.MenuItemRoles (MenuItemId, RoleName)
    VALUES (@UserAccessId, N'Administrator');
END;
GO
