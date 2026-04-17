using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasmaApi.Migrations
{
    /// <inheritdoc />
    public partial class AddImportantContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportantContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PositionTitle = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportantContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportantContacts_Members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportantContacts_CreatedByMemberId",
                table: "ImportantContacts",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportantContacts_Domain",
                table: "ImportantContacts",
                column: "Domain");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportantContacts");
        }
    }
}
