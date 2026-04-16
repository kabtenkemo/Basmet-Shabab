using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasmaApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMemberScopeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Members', 'CommitteeName') IS NOT NULL
    ALTER TABLE [dbo].[Members] DROP COLUMN [CommitteeName];
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Members', 'GovernorName') IS NOT NULL
    ALTER TABLE [dbo].[Members] DROP COLUMN [GovernorName];
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Members', 'GovernorName') IS NULL
    ALTER TABLE [dbo].[Members] ADD [GovernorName] nvarchar(120) NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Members', 'CommitteeName') IS NULL
    ALTER TABLE [dbo].[Members] ADD [CommitteeName] nvarchar(120) NULL;
");
        }
    }
}
