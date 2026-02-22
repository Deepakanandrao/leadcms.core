using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "campaign_id",
                table: "order",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "campaign_id",
                table: "email_log",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "campaign_id",
                table: "deal",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "campaign",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    email_template_id = table.Column<int>(type: "integer", nullable: false),
                    segment_ids = table.Column<int[]>(type: "integer[]", nullable: false),
                    exclude_segment_ids = table.Column<int[]>(type: "integer[]", nullable: true),
                    scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    time_zone = table.Column<int>(type: "integer", nullable: true),
                    use_contact_time_zone = table.Column<bool>(type: "boolean", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false),
                    send_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    send_completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_recipients = table.Column<int>(type: "integer", nullable: false),
                    sent_count = table.Column<int>(type: "integer", nullable: false),
                    failed_count = table.Column<int>(type: "integer", nullable: false),
                    skipped_count = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_campaign", x => x.id);
                    table.ForeignKey(
                        name: "fk_campaign_email_template_email_template_id",
                        column: x => x.email_template_id,
                        principalTable: "email_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campaign_recipient",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    campaign_id = table.Column<int>(type: "integer", nullable: false),
                    contact_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    skip_reason = table.Column<int>(type: "integer", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campaign_recipient", x => x.id);
                    table.ForeignKey(
                        name: "fk_campaign_recipient_campaign_campaign_id",
                        column: x => x.campaign_id,
                        principalTable: "campaign",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_campaign_recipient_contact_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contact",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_order_campaign_id",
                table: "order",
                column: "campaign_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_log_campaign_id",
                table: "email_log",
                column: "campaign_id");

            migrationBuilder.CreateIndex(
                name: "ix_deal_campaign_id",
                table: "deal",
                column: "campaign_id");

            migrationBuilder.CreateIndex(
                name: "ix_campaign_email_template_id",
                table: "campaign",
                column: "email_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_campaign_name",
                table: "campaign",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_campaign_recipient_campaign_id_contact_id",
                table: "campaign_recipient",
                columns: new[] { "campaign_id", "contact_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_campaign_recipient_contact_id",
                table: "campaign_recipient",
                column: "contact_id");

            migrationBuilder.AddForeignKey(
                name: "fk_deal_campaign_campaign_id",
                table: "deal",
                column: "campaign_id",
                principalTable: "campaign",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_email_log_campaign_campaign_id",
                table: "email_log",
                column: "campaign_id",
                principalTable: "campaign",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_order_campaign_campaign_id",
                table: "order",
                column: "campaign_id",
                principalTable: "campaign",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_deal_campaign_campaign_id",
                table: "deal");

            migrationBuilder.DropForeignKey(
                name: "fk_email_log_campaign_campaign_id",
                table: "email_log");

            migrationBuilder.DropForeignKey(
                name: "fk_order_campaign_campaign_id",
                table: "order");

            migrationBuilder.DropTable(
                name: "campaign_recipient");

            migrationBuilder.DropTable(
                name: "campaign");

            migrationBuilder.DropIndex(
                name: "ix_order_campaign_id",
                table: "order");

            migrationBuilder.DropIndex(
                name: "ix_email_log_campaign_id",
                table: "email_log");

            migrationBuilder.DropIndex(
                name: "ix_deal_campaign_id",
                table: "deal");

            migrationBuilder.DropColumn(
                name: "campaign_id",
                table: "order");

            migrationBuilder.DropColumn(
                name: "campaign_id",
                table: "email_log");

            migrationBuilder.DropColumn(
                name: "campaign_id",
                table: "deal");
        }
    }
}
