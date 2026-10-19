IF OBJECT_ID(N'dbo.ExcelImports', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExcelImports (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ExcelImports PRIMARY KEY,
        OwnerUserId UNIQUEIDENTIFIER NOT NULL,
        FileName NVARCHAR(260) NOT NULL,
        WorksheetName NVARCHAR(128) NOT NULL,
        ColumnsJson NVARCHAR(MAX) NOT NULL,
        [RowCount] INT NOT NULL,
        ImportedAt DATETIME2(3) NOT NULL CONSTRAINT DF_ExcelImports_ImportedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_ExcelImports_Users FOREIGN KEY (OwnerUserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE,
        CONSTRAINT CK_ExcelImports_RowCount CHECK ([RowCount] > 0)
    );

    CREATE INDEX IX_ExcelImports_OwnerUserId_ImportedAt
        ON dbo.ExcelImports (OwnerUserId, ImportedAt DESC);
END;
GO

IF OBJECT_ID(N'dbo.ExcelImportRows', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExcelImportRows (
        ImportId UNIQUEIDENTIFIER NOT NULL,
        RowNumber INT NOT NULL,
        RowDataJson NVARCHAR(MAX) NOT NULL,
        CONSTRAINT PK_ExcelImportRows PRIMARY KEY (ImportId, RowNumber),
        CONSTRAINT FK_ExcelImportRows_ExcelImports FOREIGN KEY (ImportId)
            REFERENCES dbo.ExcelImports(Id) ON DELETE CASCADE,
        CONSTRAINT CK_ExcelImportRows_RowNumber CHECK (RowNumber > 0)
    );
END;
GO
