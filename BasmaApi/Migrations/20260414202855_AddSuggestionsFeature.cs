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
            migrationBuilder.DropForeignKey(
                name: "FK_Complaints_Members_AssignedToMemberId",
                table: "Complaints");

            migrationBuilder.AddColumn<string>(
                name: "AudienceType",
                table: "Tasks",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CommitteeId",
                table: "Members",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GovernorateId",
                table: "Members",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "Members",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Governorates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Governorates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AudienceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsPosts_Members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Suggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AcceptanceCount = table.Column<int>(type: "int", nullable: false),
                    RejectionCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Suggestions_Members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskTargetMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskTargetMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskTargetMembers_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskTargetMembers_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskTargetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskTargetRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskTargetRoles_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Committees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GovernorateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Committees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Committees_Governorates_GovernorateId",
                        column: x => x.GovernorateId,
                        principalTable: "Governorates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NewsTargetMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewsPostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsTargetMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsTargetMembers_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NewsTargetMembers_NewsPosts_NewsPostId",
                        column: x => x.NewsPostId,
                        principalTable: "NewsPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NewsTargetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewsPostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsTargetRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsTargetRoles_NewsPosts_NewsPostId",
                        column: x => x.NewsPostId,
                        principalTable: "NewsPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SuggestionVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SuggestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VotedByMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsAcceptance = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuggestionVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SuggestionVotes_Members_VotedByMemberId",
                        column: x => x.VotedByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SuggestionVotes_Suggestions_SuggestionId",
                        column: x => x.SuggestionId,
                        principalTable: "Suggestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Committees_GovernorateId_Name",
                table: "Committees",
                columns: new[] { "GovernorateId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Governorates_Name",
                table: "Governorates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsPosts_CreatedByMemberId",
                table: "NewsPosts",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsTargetMembers_MemberId",
                table: "NewsTargetMembers",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsTargetMembers_NewsPostId_MemberId",
                table: "NewsTargetMembers",
                columns: new[] { "NewsPostId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsTargetRoles_NewsPostId_Role",
                table: "NewsTargetRoles",
                columns: new[] { "NewsPostId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suggestions_CreatedByMemberId",
                table: "Suggestions",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_SuggestionVotes_SuggestionId",
                table: "SuggestionVotes",
                column: "SuggestionId");

            migrationBuilder.CreateIndex(
                name: "IX_SuggestionVotes_VotedByMemberId",
                table: "SuggestionVotes",
                column: "VotedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTargetMembers_MemberId",
                table: "TaskTargetMembers",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTargetMembers_TaskId_MemberId",
                table: "TaskTargetMembers",
                columns: new[] { "TaskId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskTargetRoles_TaskId_Role",
                table: "TaskTargetRoles",
                columns: new[] { "TaskId", "Role" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Complaints_Members_AssignedToMemberId",
                table: "Complaints",
                column: "AssignedToMemberId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Complaints_Members_AssignedToMemberId",
                table: "Complaints");

            migrationBuilder.DropTable(
                name: "Committees");

            migrationBuilder.DropTable(
                name: "NewsTargetMembers");

            migrationBuilder.DropTable(
                name: "NewsTargetRoles");

            migrationBuilder.DropTable(
                name: "SuggestionVotes");

            migrationBuilder.DropTable(
                name: "TaskTargetMembers");

            migrationBuilder.DropTable(
                name: "TaskTargetRoles");

            migrationBuilder.DropTable(
                name: "Governorates");

            migrationBuilder.DropTable(
                name: "NewsPosts");

            migrationBuilder.DropTable(
                name: "Suggestions");

            migrationBuilder.DropColumn(
                name: "AudienceType",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "CommitteeId",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "GovernorateId",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "Members");

            migrationBuilder.AddForeignKey(
                name: "FK_Complaints_Members_AssignedToMemberId",
                table: "Complaints",
                column: "AssignedToMemberId",
                principalTable: "Members",
                principalColumn: "Id");
        }
    }
}
