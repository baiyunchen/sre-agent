using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SreAgent.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionTokenUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompletionTokens",
                table: "sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PromptTokens",
                table: "sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalTokens",
                table: "sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletionTokens",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "PromptTokens",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "TotalTokens",
                table: "sessions");
        }
    }
}
