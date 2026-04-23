using System;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BasmaApi.Data;

public static class DatabaseSchemaEnsurer
{
    private static readonly int[] SchemaErrorNumbers = { 207, 208 };
    private static readonly int[] UniqueConstraintErrorNumbers = { 2601, 2627 };

    public static bool IsSchemaMismatch(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is not SqlException sqlException)
            {
                continue;
            }

            foreach (SqlError error in sqlException.Errors)
            {
                if (SchemaErrorNumbers.Contains(error.Number))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool IsUniqueConstraintViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is not SqlException sqlException)
            {
                continue;
            }

            foreach (SqlError error in sqlException.Errors)
            {
                if (UniqueConstraintErrorNumbers.Contains(error.Number))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static void EnsureReferenceDataSchema(AppDbContext dbContext)
    {
        dbContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID('dbo.Governorates', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Governorates (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Governorates PRIMARY KEY,
        Name nvarchar(120) NOT NULL,
        IsVisibleInJoinForm bit NOT NULL CONSTRAINT DF_Governorates_IsVisibleInJoinForm DEFAULT 1
    );
END;

IF OBJECT_ID('dbo.Committees', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Committees (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Committees PRIMARY KEY,
        GovernorateId uniqueidentifier NOT NULL,
        Name nvarchar(120) NOT NULL,
        IsStudentClub bit NOT NULL CONSTRAINT DF_Committees_IsStudentClub DEFAULT 0,
        IsVisibleInJoinForm bit NOT NULL CONSTRAINT DF_Committees_IsVisibleInJoinForm DEFAULT 1,
        CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_Committees_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Committees_Governorates_GovernorateId FOREIGN KEY (GovernorateId) REFERENCES dbo.Governorates (Id) ON DELETE CASCADE
    );
END;

IF COL_LENGTH('dbo.Committees', 'IsStudentClub') IS NULL
BEGIN
    ALTER TABLE dbo.Committees ADD IsStudentClub bit NOT NULL CONSTRAINT DF_Committees_IsStudentClub DEFAULT 0;
END;

IF COL_LENGTH('dbo.Governorates', 'IsVisibleInJoinForm') IS NULL
BEGIN
    ALTER TABLE dbo.Governorates ADD IsVisibleInJoinForm bit NOT NULL CONSTRAINT DF_Governorates_IsVisibleInJoinForm DEFAULT 1;
END;

IF COL_LENGTH('dbo.Committees', 'IsVisibleInJoinForm') IS NULL
BEGIN
    ALTER TABLE dbo.Committees ADD IsVisibleInJoinForm bit NOT NULL CONSTRAINT DF_Committees_IsVisibleInJoinForm DEFAULT 1;
END;

IF COL_LENGTH('dbo.Committees', 'CreatedAtUtc') IS NULL
BEGIN
    ALTER TABLE dbo.Committees ADD CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_Committees_CreatedAtUtc DEFAULT SYSUTCDATETIME();
END;

IF COL_LENGTH('dbo.Members', 'GovernorateId') IS NULL ALTER TABLE dbo.Members ADD GovernorateId uniqueidentifier NULL;
IF COL_LENGTH('dbo.Members', 'CommitteeId') IS NULL ALTER TABLE dbo.Members ADD CommitteeId uniqueidentifier NULL;
IF COL_LENGTH('dbo.Members', 'GovernorName') IS NULL ALTER TABLE dbo.Members ADD GovernorName nvarchar(120) NULL;
IF COL_LENGTH('dbo.Members', 'CommitteeName') IS NULL ALTER TABLE dbo.Members ADD CommitteeName nvarchar(120) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Governorates_Name' AND object_id = OBJECT_ID('dbo.Governorates'))
BEGIN
    CREATE UNIQUE INDEX IX_Governorates_Name ON dbo.Governorates (Name);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Committees_GovernorateId_Name' AND object_id = OBJECT_ID('dbo.Committees'))
BEGIN
    CREATE UNIQUE INDEX IX_Committees_GovernorateId_Name ON dbo.Committees (GovernorateId, Name);
END;
");

        dbContext.Database.ExecuteSqlRaw(@"
UPDATE dbo.Committees
SET IsStudentClub = 1
WHERE IsStudentClub = 0
    AND (
            LOWER(Name) LIKE '%club%'
            OR Name LIKE N'%نادي%'
            OR Name LIKE N'%النوادي%'
            OR Name LIKE N'%طلابي%'
    );
");
    }

    public static void EnsureJoinRequestsSchema(AppDbContext dbContext)
    {
        dbContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID('dbo.TeamJoinRequests', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TeamJoinRequests (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_TeamJoinRequests PRIMARY KEY,
        FullName nvarchar(150) NOT NULL,
        Email nvarchar(250) NOT NULL,
        PhoneNumber nvarchar(30) NOT NULL,
        NationalId nvarchar(14) NULL,
        BirthDate date NULL,
        GovernorateId uniqueidentifier NOT NULL,
        CommitteeId uniqueidentifier NULL,
        Motivation nvarchar(3000) NOT NULL,
        Experience nvarchar(3000) NULL,
        Status nvarchar(30) NOT NULL CONSTRAINT DF_TeamJoinRequests_Status DEFAULT 'Pending',
        AdminNotes nvarchar(2000) NULL,
        AssignedToMemberId uniqueidentifier NULL,
        ReviewedByMemberId uniqueidentifier NULL,
        CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_TeamJoinRequests_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        ReviewedAtUtc datetime2 NULL,
        CONSTRAINT FK_TeamJoinRequests_Governorates_GovernorateId FOREIGN KEY (GovernorateId) REFERENCES dbo.Governorates (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_TeamJoinRequests_Committees_CommitteeId FOREIGN KEY (CommitteeId) REFERENCES dbo.Committees (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_TeamJoinRequests_Members_AssignedToMemberId FOREIGN KEY (AssignedToMemberId) REFERENCES dbo.Members (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_TeamJoinRequests_Members_ReviewedByMemberId FOREIGN KEY (ReviewedByMemberId) REFERENCES dbo.Members (Id) ON DELETE NO ACTION
    );
END;

IF COL_LENGTH('dbo.TeamJoinRequests', 'CommitteeId') IS NULL
BEGIN
    ALTER TABLE dbo.TeamJoinRequests ADD CommitteeId uniqueidentifier NULL;
END;

IF COL_LENGTH('dbo.TeamJoinRequests', 'NationalId') IS NULL
BEGIN
    ALTER TABLE dbo.TeamJoinRequests ADD NationalId nvarchar(14) NULL;
END;

IF COL_LENGTH('dbo.TeamJoinRequests', 'BirthDate') IS NULL
BEGIN
    ALTER TABLE dbo.TeamJoinRequests ADD BirthDate date NULL;
END;

IF COL_LENGTH('dbo.TeamJoinRequests', 'Experience') IS NULL
BEGIN
    ALTER TABLE dbo.TeamJoinRequests ADD Experience nvarchar(3000) NULL;
END;

IF COL_LENGTH('dbo.TeamJoinRequests', 'Status') IS NULL
BEGIN
    ALTER TABLE dbo.TeamJoinRequests ADD Status nvarchar(30) NOT NULL CONSTRAINT DF_TeamJoinRequests_Status DEFAULT 'Pending';
END;

IF COL_LENGTH('dbo.TeamJoinRequests', 'AdminNotes') IS NULL
BEGIN
    ALTER TABLE dbo.TeamJoinRequests ADD AdminNotes nvarchar(2000) NULL;
END;

IF COL_LENGTH('dbo.TeamJoinRequests', 'AssignedToMemberId') IS NULL
BEGIN
    ALTER TABLE dbo.TeamJoinRequests ADD AssignedToMemberId uniqueidentifier NULL;
END;

IF COL_LENGTH('dbo.TeamJoinRequests', 'ReviewedByMemberId') IS NULL
BEGIN
    ALTER TABLE dbo.TeamJoinRequests ADD ReviewedByMemberId uniqueidentifier NULL;
END;

IF COL_LENGTH('dbo.TeamJoinRequests', 'CreatedAtUtc') IS NULL
BEGIN
    ALTER TABLE dbo.TeamJoinRequests ADD CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_TeamJoinRequests_CreatedAtUtc DEFAULT SYSUTCDATETIME();
END;

IF COL_LENGTH('dbo.TeamJoinRequests', 'ReviewedAtUtc') IS NULL
BEGIN
    ALTER TABLE dbo.TeamJoinRequests ADD ReviewedAtUtc datetime2 NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TeamJoinRequests_Status' AND object_id = OBJECT_ID('dbo.TeamJoinRequests'))
BEGIN
    CREATE INDEX IX_TeamJoinRequests_Status ON dbo.TeamJoinRequests (Status);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TeamJoinRequests_CreatedAtUtc' AND object_id = OBJECT_ID('dbo.TeamJoinRequests'))
BEGIN
    CREATE INDEX IX_TeamJoinRequests_CreatedAtUtc ON dbo.TeamJoinRequests (CreatedAtUtc DESC);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TeamJoinRequests_GovernorateId' AND object_id = OBJECT_ID('dbo.TeamJoinRequests'))
BEGIN
    CREATE INDEX IX_TeamJoinRequests_GovernorateId ON dbo.TeamJoinRequests (GovernorateId);
END;
");
    }
}
