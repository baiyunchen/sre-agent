using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SreAgent.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionAlertSourceSeverity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "alert_severity",
                table: "sessions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "alert_source",
                table: "sessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "alert_severity",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "alert_source",
                table: "sessions");
        }
    }
}
