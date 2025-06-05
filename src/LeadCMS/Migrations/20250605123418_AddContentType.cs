using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddContentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "content_type",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    uid = table.Column<string>(type: "text", nullable: false),
                    format = table.Column<int>(type: "integer", nullable: false),
                    supports_comments = table.Column<bool>(type: "boolean", nullable: false),
                    supports_cover_image = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_content_type", x => x.id);
                    table.UniqueConstraint("ak_content_type_uid", x => x.uid);
                });

            // Insert default ContentTypes before inserting from select
            migrationBuilder.Sql(@"
                INSERT INTO content_type (uid, format, supports_comments, supports_cover_image, created_at)
                VALUES
                    ('legal', 1, FALSE, FALSE, NOW()),
                    ('blog-post', 1, TRUE, TRUE, NOW()),
                    ('landing', 4, FALSE, FALSE, NOW()),
                    ('about-us', 1, FALSE, FALSE, NOW()),
                    ('pricing', 3, FALSE, FALSE, NOW()),
                    ('contact', 4, FALSE, FALSE, NOW())
                ON CONFLICT (uid) DO NOTHING;
            ");

            // Insert ContentTypes for all distinct Content.Type values before creating the FK
            migrationBuilder.Sql(@"
                INSERT INTO content_type (uid, format, supports_comments, supports_cover_image, created_at)
                SELECT DISTINCT type, 1, TRUE, TRUE, NOW()
                FROM content c
                WHERE NOT EXISTS (
                    SELECT 1 FROM content_type ct WHERE ct.uid = c.type
                );
            ");

            migrationBuilder.CreateIndex(
                name: "ix_content_type",
                table: "content",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "ix_content_type_uid",
                table: "content_type",
                column: "uid",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_content_content_type_type",
                table: "content",
                column: "type",
                principalTable: "content_type",
                principalColumn: "uid",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_content_content_type_type",
                table: "content");

            migrationBuilder.DropTable(
                name: "content_type");

            migrationBuilder.DropIndex(
                name: "ix_content_type",
                table: "content");
        }
    }
}
