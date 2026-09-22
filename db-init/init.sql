-- Creates the application database and a SQL login/user matching the
-- credentials from .env (DB_NAME, DB_USER, DB_PASSWORD), since the base
-- mssql-server image only provisions the "sa" login by default.

IF DB_ID(N'$(DB_NAME)') IS NULL
BEGIN
    CREATE DATABASE [$(DB_NAME)];
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(DB_USER)')
BEGIN
    CREATE LOGIN [$(DB_USER)] WITH PASSWORD = N'$(DB_PASSWORD)', CHECK_POLICY = OFF;
END
GO

USE [$(DB_NAME)];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(DB_USER)')
BEGIN
    CREATE USER [$(DB_USER)] FOR LOGIN [$(DB_USER)];
    ALTER ROLE db_owner ADD MEMBER [$(DB_USER)];
END
GO
