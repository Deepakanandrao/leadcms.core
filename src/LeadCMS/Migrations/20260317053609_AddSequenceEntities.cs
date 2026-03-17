using System;
using LeadCMS.Models;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddSequenceEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sequence",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    stop_on_reply = table.Column<bool>(type: "boolean", nullable: false),
                    use_contact_time_zone = table.Column<bool>(type: "boolean", nullable: false),
                    time_zone = table.Column<int>(type: "integer", nullable: false),
                    last_activated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_paused_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    active_enrollment_count = table.Column<int>(type: "integer", nullable: false),
                    completed_enrollment_count = table.Column<int>(type: "integer", nullable: false),
                    exited_enrollment_count = table.Column<int>(type: "integer", nullable: false),
                    sent_count = table.Column<int>(type: "integer", nullable: false),
                    failed_count = table.Column<int>(type: "integer", nullable: false),
                    enrollment = table.Column<SequenceEnrollmentConfig>(type: "jsonb", nullable: true),
                    utm_parameters = table.Column<Utms>(type: "jsonb", nullable: true),
                    source = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_ip = table.Column<string>(type: "text", nullable: true),
                    created_by_id = table.Column<string>(type: "text", nullable: true),
                    created_by_user_agent = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_ip = table.Column<string>(type: "text", nullable: true),
                    updated_by_id = table.Column<string>(type: "text", nullable: true),
                    updated_by_user_agent = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sequence", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sequence_enrollment",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sequence_id = table.Column<int>(type: "integer", nullable: false),
                    contact_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    last_completed_step_key = table.Column<string>(type: "text", nullable: true),
                    entered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    exited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    exit_reason = table.Column<int>(type: "integer", nullable: false),
                    enrollment_source = table.Column<int>(type: "integer", nullable: false),
                    enrollment_reason = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sequence_enrollment", x => x.id);
                    table.ForeignKey(
                        name: "fk_sequence_enrollment_contact_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contact",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sequence_enrollment_sequence_sequence_id",
                        column: x => x.sequence_id,
                        principalTable: "sequence",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sequence_step",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sequence_id = table.Column<int>(type: "integer", nullable: false),
                    email_template_id = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    step_key = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: true),
                    timing = table.Column<SequenceStepTiming>(type: "jsonb", nullable: false),
                    source = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sequence_step", x => x.id);
                    table.ForeignKey(
                        name: "fk_sequence_step_email_template_email_template_id",
                        column: x => x.email_template_id,
                        principalTable: "email_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sequence_step_sequence_sequence_id",
                        column: x => x.sequence_id,
                        principalTable: "sequence",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sequence_delivery",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sequence_id = table.Column<int>(type: "integer", nullable: false),
                    sequence_step_id = table.Column<int>(type: "integer", nullable: false),
                    contact_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    skip_reason = table.Column<string>(type: "text", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    email_log_id = table.Column<int>(type: "integer", nullable: true),
                    source = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sequence_delivery", x => x.id);
                    table.ForeignKey(
                        name: "fk_sequence_delivery_contact_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contact",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sequence_delivery_email_log_email_log_id",
                        column: x => x.email_log_id,
                        principalTable: "email_log",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_sequence_delivery_sequence_sequence_id",
                        column: x => x.sequence_id,
                        principalTable: "sequence",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sequence_delivery_sequence_step_sequence_step_id",
                        column: x => x.sequence_step_id,
                        principalTable: "sequence_step",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sequence_name",
                table: "sequence",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sequence_delivery_contact_id",
                table: "sequence_delivery",
                column: "contact_id");

            migrationBuilder.CreateIndex(
                name: "ix_sequence_delivery_email_log_id",
                table: "sequence_delivery",
                column: "email_log_id");

            migrationBuilder.CreateIndex(
                name: "ix_sequence_delivery_sequence_id_sequence_step_id_contact_id",
                table: "sequence_delivery",
                columns: new[] { "sequence_id", "sequence_step_id", "contact_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sequence_delivery_sequence_step_id",
                table: "sequence_delivery",
                column: "sequence_step_id");

            migrationBuilder.CreateIndex(
                name: "ix_sequence_delivery_status_scheduled_at",
                table: "sequence_delivery",
                columns: new[] { "status", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sequence_enrollment_contact_id",
                table: "sequence_enrollment",
                column: "contact_id");

            migrationBuilder.CreateIndex(
                name: "ix_sequence_enrollment_sequence_id_contact_id",
                table: "sequence_enrollment",
                columns: new[] { "sequence_id", "contact_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sequence_enrollment_sequence_id_status",
                table: "sequence_enrollment",
                columns: new[] { "sequence_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_sequence_step_email_template_id",
                table: "sequence_step",
                column: "email_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_sequence_step_sequence_id_position",
                table: "sequence_step",
                columns: new[] { "sequence_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sequence_step_sequence_id_step_key",
                table: "sequence_step",
                columns: new[] { "sequence_id", "step_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sequence_delivery");

            migrationBuilder.DropTable(
                name: "sequence_enrollment");

            migrationBuilder.DropTable(
                name: "sequence_step");

            migrationBuilder.DropTable(
                name: "sequence");
        }
    }
}
