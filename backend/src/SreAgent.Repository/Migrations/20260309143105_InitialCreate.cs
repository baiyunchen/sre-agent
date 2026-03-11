using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SreAgent.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    alert_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    alert_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    alert_data = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    service_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    service_metadata = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    current_agent_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    current_step = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    execution_state = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    diagnosis = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    diagnosis_summary = table.Column<string>(type: "text", nullable: true),
                    confidence = table.Column<double>(type: "numeric(3,2)", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    agent_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    input = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    output = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    confidence = table.Column<double>(type: "numeric(3,2)", nullable: true),
                    finding = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_agent_runs_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_description = table.Column<string>(type: "text", nullable: true),
                    event_data = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    actor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    actor_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_logs_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "checkpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checkpoint_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    step_number = table.Column<int>(type: "integer", nullable: false),
                    system_message = table.Column<string>(type: "text", nullable: true),
                    message_ids = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    session_state = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    agent_state = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkpoints", x => x.id);
                    table.ForeignKey(
                        name: "FK_checkpoints_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "diagnostic_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    tool_invocation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    log_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    structured_fields = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diagnostic_data", x => x.id);
                    table.ForeignKey(
                        name: "FK_diagnostic_data_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "interventions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    data = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    intervened_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    intervened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interventions", x => x.id);
                    table.ForeignKey(
                        name: "FK_interventions_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    parts = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    metadata = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    estimated_tokens = table.Column<int>(type: "integer", nullable: false),
                    agent_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_messages_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tool_invocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tool_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parameters = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    result = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    approval_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    approved_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_invocations", x => x.id);
                    table.ForeignKey(
                        name: "FK_tool_invocations_agent_runs_agent_run_id",
                        column: x => x.agent_run_id,
                        principalTable: "agent_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_session_id",
                table: "agent_runs",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_session_id_occurred_at",
                table: "audit_logs",
                columns: new[] { "session_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_checkpoints_session_id_created_at",
                table: "checkpoints",
                columns: new[] { "session_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_diagnostic_data_expires_at",
                table: "diagnostic_data",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_diagnostic_data_session_id_log_timestamp",
                table: "diagnostic_data",
                columns: new[] { "session_id", "log_timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_diagnostic_data_session_id_severity",
                table: "diagnostic_data",
                columns: new[] { "session_id", "severity" });

            migrationBuilder.CreateIndex(
                name: "IX_diagnostic_data_session_id_source_type",
                table: "diagnostic_data",
                columns: new[] { "session_id", "source_type" });

            migrationBuilder.CreateIndex(
                name: "IX_interventions_session_id",
                table: "interventions",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_messages_session_id_created_at",
                table: "messages",
                columns: new[] { "session_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sessions_alert_id",
                table: "sessions",
                column: "alert_id");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_created_at",
                table: "sessions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_status",
                table: "sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_tool_invocations_agent_run_id",
                table: "tool_invocations",
                column: "agent_run_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "checkpoints");

            migrationBuilder.DropTable(
                name: "diagnostic_data");

            migrationBuilder.DropTable(
                name: "interventions");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "tool_invocations");

            migrationBuilder.DropTable(
                name: "agent_runs");

            migrationBuilder.DropTable(
                name: "sessions");
        }
    }
}
