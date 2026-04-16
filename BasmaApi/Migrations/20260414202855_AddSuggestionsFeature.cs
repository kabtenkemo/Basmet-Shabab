using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasmaApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSuggestionsFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Complaints_Members_AssignedToMemberId')
BEGIN
    ALTER TABLE [Complaints] DROP CONSTRAINT [FK_Complaints_Members_AssignedToMemberId];
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('Tasks', 'AudienceType') IS NULL
BEGIN
    ALTER TABLE [Tasks] ADD [AudienceType] nvarchar(20) NOT NULL CONSTRAINT [DF_Tasks_AudienceType] DEFAULT N'';
END

IF COL_LENGTH('Members', 'CommitteeId') IS NULL
BEGIN
    ALTER TABLE [Members] ADD [CommitteeId] uniqueidentifier NULL;
END

IF COL_LENGTH('Members', 'GovernorateId') IS NULL
BEGIN
    ALTER TABLE [Members] ADD [GovernorateId] uniqueidentifier NULL;
END

IF COL_LENGTH('Members', 'MustChangePassword') IS NULL
BEGIN
    ALTER TABLE [Members] ADD [MustChangePassword] bit NOT NULL CONSTRAINT [DF_Members_MustChangePassword] DEFAULT(0);
END

IF OBJECT_ID('Governorates', 'U') IS NULL
BEGIN
    CREATE TABLE [Governorates] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        CONSTRAINT [PK_Governorates] PRIMARY KEY ([Id])
    );
END

IF OBJECT_ID('NewsPosts', 'U') IS NULL
BEGIN
    CREATE TABLE [NewsPosts] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(250) NOT NULL,
        [Content] nvarchar(4000) NOT NULL,
        [CreatedByMemberId] uniqueidentifier NOT NULL,
        [AudienceType] nvarchar(20) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_NewsPosts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NewsPosts_Members_CreatedByMemberId] FOREIGN KEY ([CreatedByMemberId]) REFERENCES [Members] ([Id]) ON DELETE NO ACTION
    );
END

IF OBJECT_ID('Suggestions', 'U') IS NULL
BEGIN
    CREATE TABLE [Suggestions] (
        [Id] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedByMemberId] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Status] int NOT NULL,
        [AcceptanceCount] int NOT NULL,
        [RejectionCount] int NOT NULL,
        CONSTRAINT [PK_Suggestions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Suggestions_Members_CreatedByMemberId] FOREIGN KEY ([CreatedByMemberId]) REFERENCES [Members] ([Id]) ON DELETE CASCADE
    );
END

IF OBJECT_ID('TaskTargetMembers', 'U') IS NULL
BEGIN
    CREATE TABLE [TaskTargetMembers] (
        [Id] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NOT NULL,
        [MemberId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_TaskTargetMembers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskTargetMembers_Members_MemberId] FOREIGN KEY ([MemberId]) REFERENCES [Members] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TaskTargetMembers_Tasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [Tasks] ([Id]) ON DELETE CASCADE
    );
END

IF OBJECT_ID('TaskTargetRoles', 'U') IS NULL
BEGIN
    CREATE TABLE [TaskTargetRoles] (
        [Id] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NOT NULL,
        [Role] nvarchar(40) NOT NULL,
        CONSTRAINT [PK_TaskTargetRoles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskTargetRoles_Tasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [Tasks] ([Id]) ON DELETE CASCADE
    );
END

IF OBJECT_ID('Committees', 'U') IS NULL
BEGIN
    CREATE TABLE [Committees] (
        [Id] uniqueidentifier NOT NULL,
        [GovernorateId] uniqueidentifier NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Committees] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Committees_Governorates_GovernorateId] FOREIGN KEY ([GovernorateId]) REFERENCES [Governorates] ([Id]) ON DELETE CASCADE
    );
END

IF OBJECT_ID('NewsTargetMembers', 'U') IS NULL
BEGIN
    CREATE TABLE [NewsTargetMembers] (
        [Id] uniqueidentifier NOT NULL,
        [NewsPostId] uniqueidentifier NOT NULL,
        [MemberId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_NewsTargetMembers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NewsTargetMembers_Members_MemberId] FOREIGN KEY ([MemberId]) REFERENCES [Members] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_NewsTargetMembers_NewsPosts_NewsPostId] FOREIGN KEY ([NewsPostId]) REFERENCES [NewsPosts] ([Id]) ON DELETE CASCADE
    );
END

IF OBJECT_ID('NewsTargetRoles', 'U') IS NULL
BEGIN
    CREATE TABLE [NewsTargetRoles] (
        [Id] uniqueidentifier NOT NULL,
        [NewsPostId] uniqueidentifier NOT NULL,
        [Role] nvarchar(40) NOT NULL,
        CONSTRAINT [PK_NewsTargetRoles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NewsTargetRoles_NewsPosts_NewsPostId] FOREIGN KEY ([NewsPostId]) REFERENCES [NewsPosts] ([Id]) ON DELETE CASCADE
    );
END

IF OBJECT_ID('SuggestionVotes', 'U') IS NULL
BEGIN
    CREATE TABLE [SuggestionVotes] (
        [Id] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [SuggestionId] uniqueidentifier NOT NULL,
        [VotedByMemberId] uniqueidentifier NOT NULL,
        [IsAcceptance] bit NOT NULL,
        CONSTRAINT [PK_SuggestionVotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SuggestionVotes_Members_VotedByMemberId] FOREIGN KEY ([VotedByMemberId]) REFERENCES [Members] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SuggestionVotes_Suggestions_SuggestionId] FOREIGN KEY ([SuggestionId]) REFERENCES [Suggestions] ([Id]) ON DELETE NO ACTION
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Committees_GovernorateId_Name' AND object_id = OBJECT_ID('Committees'))
    CREATE UNIQUE INDEX [IX_Committees_GovernorateId_Name] ON [Committees] ([GovernorateId], [Name]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Governorates_Name' AND object_id = OBJECT_ID('Governorates'))
    CREATE UNIQUE INDEX [IX_Governorates_Name] ON [Governorates] ([Name]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NewsPosts_CreatedByMemberId' AND object_id = OBJECT_ID('NewsPosts'))
    CREATE INDEX [IX_NewsPosts_CreatedByMemberId] ON [NewsPosts] ([CreatedByMemberId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NewsTargetMembers_MemberId' AND object_id = OBJECT_ID('NewsTargetMembers'))
    CREATE INDEX [IX_NewsTargetMembers_MemberId] ON [NewsTargetMembers] ([MemberId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NewsTargetMembers_NewsPostId_MemberId' AND object_id = OBJECT_ID('NewsTargetMembers'))
    CREATE UNIQUE INDEX [IX_NewsTargetMembers_NewsPostId_MemberId] ON [NewsTargetMembers] ([NewsPostId], [MemberId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NewsTargetRoles_NewsPostId_Role' AND object_id = OBJECT_ID('NewsTargetRoles'))
    CREATE UNIQUE INDEX [IX_NewsTargetRoles_NewsPostId_Role] ON [NewsTargetRoles] ([NewsPostId], [Role]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Suggestions_CreatedByMemberId' AND object_id = OBJECT_ID('Suggestions'))
    CREATE INDEX [IX_Suggestions_CreatedByMemberId] ON [Suggestions] ([CreatedByMemberId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SuggestionVotes_SuggestionId' AND object_id = OBJECT_ID('SuggestionVotes'))
    CREATE INDEX [IX_SuggestionVotes_SuggestionId] ON [SuggestionVotes] ([SuggestionId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SuggestionVotes_VotedByMemberId' AND object_id = OBJECT_ID('SuggestionVotes'))
    CREATE INDEX [IX_SuggestionVotes_VotedByMemberId] ON [SuggestionVotes] ([VotedByMemberId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TaskTargetMembers_MemberId' AND object_id = OBJECT_ID('TaskTargetMembers'))
    CREATE INDEX [IX_TaskTargetMembers_MemberId] ON [TaskTargetMembers] ([MemberId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TaskTargetMembers_TaskId_MemberId' AND object_id = OBJECT_ID('TaskTargetMembers'))
    CREATE UNIQUE INDEX [IX_TaskTargetMembers_TaskId_MemberId] ON [TaskTargetMembers] ([TaskId], [MemberId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TaskTargetRoles_TaskId_Role' AND object_id = OBJECT_ID('TaskTargetRoles'))
    CREATE UNIQUE INDEX [IX_TaskTargetRoles_TaskId_Role] ON [TaskTargetRoles] ([TaskId], [Role]);

IF COL_LENGTH('Complaints', 'AssignedToMemberId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Complaints_Members_AssignedToMemberId')
BEGIN
    ALTER TABLE [Complaints]
    ADD CONSTRAINT [FK_Complaints_Members_AssignedToMemberId]
    FOREIGN KEY ([AssignedToMemberId]) REFERENCES [Members] ([Id]) ON DELETE NO ACTION;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Complaints_Members_AssignedToMemberId')
BEGIN
    ALTER TABLE [Complaints] DROP CONSTRAINT [FK_Complaints_Members_AssignedToMemberId];
END

IF OBJECT_ID('SuggestionVotes', 'U') IS NOT NULL DROP TABLE [SuggestionVotes];
IF OBJECT_ID('TaskTargetMembers', 'U') IS NOT NULL DROP TABLE [TaskTargetMembers];
IF OBJECT_ID('TaskTargetRoles', 'U') IS NOT NULL DROP TABLE [TaskTargetRoles];
IF OBJECT_ID('NewsTargetMembers', 'U') IS NOT NULL DROP TABLE [NewsTargetMembers];
IF OBJECT_ID('NewsTargetRoles', 'U') IS NOT NULL DROP TABLE [NewsTargetRoles];
IF OBJECT_ID('Committees', 'U') IS NOT NULL DROP TABLE [Committees];
IF OBJECT_ID('NewsPosts', 'U') IS NOT NULL DROP TABLE [NewsPosts];
IF OBJECT_ID('Suggestions', 'U') IS NOT NULL DROP TABLE [Suggestions];
IF OBJECT_ID('Governorates', 'U') IS NOT NULL DROP TABLE [Governorates];

IF COL_LENGTH('Tasks', 'AudienceType') IS NOT NULL AND OBJECT_ID('DF_Tasks_AudienceType', 'D') IS NOT NULL
    ALTER TABLE [Tasks] DROP CONSTRAINT [DF_Tasks_AudienceType];
IF COL_LENGTH('Tasks', 'AudienceType') IS NOT NULL
    ALTER TABLE [Tasks] DROP COLUMN [AudienceType];

IF COL_LENGTH('Members', 'CommitteeId') IS NOT NULL
    ALTER TABLE [Members] DROP COLUMN [CommitteeId];

IF COL_LENGTH('Members', 'GovernorateId') IS NOT NULL
    ALTER TABLE [Members] DROP COLUMN [GovernorateId];

IF COL_LENGTH('Members', 'MustChangePassword') IS NOT NULL AND OBJECT_ID('DF_Members_MustChangePassword', 'D') IS NOT NULL
    ALTER TABLE [Members] DROP CONSTRAINT [DF_Members_MustChangePassword];
IF COL_LENGTH('Members', 'MustChangePassword') IS NOT NULL
    ALTER TABLE [Members] DROP COLUMN [MustChangePassword];

IF COL_LENGTH('Complaints', 'AssignedToMemberId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Complaints_Members_AssignedToMemberId')
BEGIN
    ALTER TABLE [Complaints]
    ADD CONSTRAINT [FK_Complaints_Members_AssignedToMemberId]
    FOREIGN KEY ([AssignedToMemberId]) REFERENCES [Members] ([Id]);
END
");
        }
    }
}