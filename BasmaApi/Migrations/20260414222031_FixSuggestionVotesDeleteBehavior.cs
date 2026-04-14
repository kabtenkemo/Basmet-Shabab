using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasmaApi.Migrations
{
    /// <inheritdoc />
    public partial class FixSuggestionVotesDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SuggestionVotes_Suggestions_SuggestionId",
                table: "SuggestionVotes");

            migrationBuilder.AddForeignKey(
                name: "FK_SuggestionVotes_Suggestions_SuggestionId",
                table: "SuggestionVotes",
                column: "SuggestionId",
                principalTable: "Suggestions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SuggestionVotes_Suggestions_SuggestionId",
                table: "SuggestionVotes");

            migrationBuilder.AddForeignKey(
                name: "FK_SuggestionVotes_Suggestions_SuggestionId",
                table: "SuggestionVotes",
                column: "SuggestionId",
                principalTable: "Suggestions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
