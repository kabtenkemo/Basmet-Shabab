using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasmaApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexesForMemberAndComplaint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Members', 'CreatedAtUtc') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Members_CreatedAtUtc' AND object_id = OBJECT_ID('Members'))
BEGIN
    CREATE INDEX [IX_Members_CreatedAtUtc] ON [Members] ([CreatedAtUtc]);
END

IF COL_LENGTH('Members', 'Role') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Members_Role' AND object_id = OBJECT_ID('Members'))
BEGIN
    CREATE INDEX [IX_Members_Role] ON [Members] ([Role]);
END

IF COL_LENGTH('Complaints', 'CreatedAtUtc') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Complaints_CreatedAtUtc' AND object_id = OBJECT_ID('Complaints'))
BEGIN
    CREATE INDEX [IX_Complaints_CreatedAtUtc] ON [Complaints] ([CreatedAtUtc] DESC);
END

IF COL_LENGTH('Complaints', 'Priority') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Complaints_Priority' AND object_id = OBJECT_ID('Complaints'))
BEGIN
    CREATE INDEX [IX_Complaints_Priority] ON [Complaints] ([Priority]);
END

IF COL_LENGTH('Complaints', 'Status') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Complaints_Status' AND object_id = OBJECT_ID('Complaints'))
BEGIN
    CREATE INDEX [IX_Complaints_Status] ON [Complaints] ([Status]);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Members_CreatedAtUtc' AND object_id = OBJECT_ID('Members'))
BEGIN
    DROP INDEX [IX_Members_CreatedAtUtc] ON [Members];
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Members_Role' AND object_id = OBJECT_ID('Members'))
BEGIN
    DROP INDEX [IX_Members_Role] ON [Members];
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Complaints_CreatedAtUtc' AND object_id = OBJECT_ID('Complaints'))
BEGIN
    DROP INDEX [IX_Complaints_CreatedAtUtc] ON [Complaints];
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Complaints_Priority' AND object_id = OBJECT_ID('Complaints'))
BEGIN
    DROP INDEX [IX_Complaints_Priority] ON [Complaints];
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Complaints_Status' AND object_id = OBJECT_ID('Complaints'))
BEGIN
    DROP INDEX [IX_Complaints_Status] ON [Complaints];
END
");
        }
    }
}
