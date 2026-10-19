IF COL_LENGTH(N'dbo.Users', N'ProfileImage') IS NULL
    ALTER TABLE dbo.Users ADD ProfileImage VARBINARY(MAX) NULL;

IF COL_LENGTH(N'dbo.Users', N'ProfileImageContentType') IS NULL
    ALTER TABLE dbo.Users ADD ProfileImageContentType NVARCHAR(50) NULL;
