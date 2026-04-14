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
            migrationBuilder.CreateIndex(
                name: "IX_Members_CreatedAtUtc",
                table: "Members",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Members_Role",
                table: "Members",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_CreatedAtUtc",
                table: "Complaints",
                column: "CreatedAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_Priority",
                table: "Complaints",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_Status",
                table: "Complaints",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Members_CreatedAtUtc",
                table: "Members");

            migrationBuilder.DropIndex(
                name: "IX_Members_Role",
                table: "Members");

            migrationBuilder.DropIndex(
                name: "IX_Complaints_CreatedAtUtc",
                table: "Complaints");

            migrationBuilder.DropIndex(
                name: "IX_Complaints_Priority",
                table: "Complaints");

            migrationBuilder.DropIndex(
                name: "IX_Complaints_Status",
                table: "Complaints");
        }
    }
}
