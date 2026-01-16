using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrichments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "enrichment_audit",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    field_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    enriched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enrichment_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "enrichment_provider_config",
                columns: table => new
                {
                    provider_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    configuration = table.Column<string>(type: "jsonb", nullable: true),
                    daily_quota = table.Column<int>(type: "integer", nullable: true),
                    monthly_quota = table.Column<int>(type: "integer", nullable: true),
                    hourly_quota = table.Column<int>(type: "integer", nullable: true),
                    min_call_interval_ms = table.Column<int>(type: "integer", nullable: true),
                    max_concurrency = table.Column<int>(type: "integer", nullable: true),
                    allow_parallel_calls = table.Column<bool>(type: "boolean", nullable: true),
                    last_config_change_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enrichment_provider_config", x => x.provider_key);
                });

            migrationBuilder.CreateTable(
                name: "enrichment_quota_usage",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    provider_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    window_type = table.Column<int>(type: "integer", nullable: false),
                    window_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usage_count = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enrichment_quota_usage", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "enrichment_work_item",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    provider_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    trigger = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    source = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enrichment_work_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_enrichment_work_item_enrichment_provider_config_provider_key",
                        column: x => x.provider_key,
                        principalTable: "enrichment_provider_config",
                        principalColumn: "provider_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "enrichment_provider_attempt",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    work_item_id = table.Column<int>(type: "integer", nullable: false),
                    provider_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_category = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: false),
                    response_payload = table.Column<string>(type: "jsonb", nullable: true),
                    request_payload = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enrichment_provider_attempt", x => x.id);
                    table.ForeignKey(
                        name: "fk_enrichment_provider_attempt_enrichment_work_item_work_item_",
                        column: x => x.work_item_id,
                        principalTable: "enrichment_work_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_enrichment_audit_entity_type_entity_id_field_name",
                table: "enrichment_audit",
                columns: new[] { "entity_type", "entity_id", "field_name" });

            migrationBuilder.CreateIndex(
                name: "ix_enrichment_provider_attempt_provider_key_entity_type_entity",
                table: "enrichment_provider_attempt",
                columns: new[] { "provider_key", "entity_type", "entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_enrichment_provider_attempt_work_item_id",
                table: "enrichment_provider_attempt",
                column: "work_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrichment_quota_usage_provider_key_window_type_window_start",
                table: "enrichment_quota_usage",
                columns: new[] { "provider_key", "window_type", "window_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_enrichment_work_item_provider_key",
                table: "enrichment_work_item",
                column: "provider_key");

            migrationBuilder.CreateIndex(
                name: "ix_enrichment_work_item_status",
                table: "enrichment_work_item",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "enrichment_audit");

            migrationBuilder.DropTable(
                name: "enrichment_provider_attempt");

            migrationBuilder.DropTable(
                name: "enrichment_quota_usage");

            migrationBuilder.DropTable(
                name: "enrichment_work_item");

            migrationBuilder.DropTable(
                name: "enrichment_provider_config");
        }
    }
}
