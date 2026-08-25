using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Klyvesta.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class F2InitialPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ops");

            migrationBuilder.EnsureSchema(
                name: "notification");

            migrationBuilder.CreateTable(
                name: "idempotency_record",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    request_hash = table.Column<string>(type: "character(64)", nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_record", x => x.id);
                    table.CheckConstraint("ck_idempotency_record_expiry", "expires_at > created_at");
                    table.CheckConstraint("ck_idempotency_record_state", "state IN ('in_progress', 'completed', 'failed')");
                });

            migrationBuilder.CreateTable(
                name: "inbox_message",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    message_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    payload_hash = table.Column<string>(type: "character(64)", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_message", x => x.id);
                    table.CheckConstraint("ck_inbox_message_state", "state IN ('received', 'processing', 'processed', 'failed')");
                });

            migrationBuilder.CreateTable(
                name: "outbox",
                schema: "notification",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    headers_json = table.Column<string>(type: "jsonb", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox", x => x.id);
                    table.CheckConstraint("ck_outbox_attempt_count", "attempt_count >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_record_expires_at",
                schema: "ops",
                table: "idempotency_record",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ux_idempotency_record_scope_key",
                schema: "ops",
                table: "idempotency_record",
                columns: new[] { "scope", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inbox_message_state_received_at",
                schema: "ops",
                table: "inbox_message",
                columns: new[] { "state", "received_at" });

            migrationBuilder.CreateIndex(
                name: "ux_inbox_message_provider_message_id",
                schema: "ops",
                table: "inbox_message",
                columns: new[] { "provider", "message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending",
                schema: "notification",
                table: "outbox",
                columns: new[] { "next_attempt_at", "occurred_at" },
                filter: "published_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_record",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "inbox_message",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "outbox",
                schema: "notification");
        }
    }
}
