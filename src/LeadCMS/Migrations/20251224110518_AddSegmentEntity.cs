using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddSegmentEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "segment",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: false),
                    contact_count = table.Column<int>(type: "integer", nullable: false),
                    definition = table.Column<string>(type: "jsonb", nullable: true),
                    contact_ids = table.Column<int[]>(type: "integer[]", nullable: true),
                    tags = table.Column<string[]>(type: "text[]", nullable: true),
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
                    table.PrimaryKey("pk_segment", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_segment_name",
                table: "segment",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "segment");
        }
    }
}
