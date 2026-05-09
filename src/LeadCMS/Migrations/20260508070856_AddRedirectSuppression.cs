using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddRedirectSuppression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_auto_discovery_suppressed",
                table: "redirect",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_redirect_from_content_id",
                table: "redirect",
                column: "from_content_id",
                unique: true,
                filter: "\"source_type\" = 2");

            migrationBuilder.CreateIndex(
                name: "ix_redirect_from_language_from_slug",
                table: "redirect",
                columns: new[] { "from_language", "from_slug" },
                unique: true,
                filter: "\"source_type\" = 1 AND NOT \"is_auto_discovery_suppressed\"");

            migrationBuilder.CreateIndex(
                name: "ix_redirect_from_path",
                table: "redirect",
                column: "from_path",
                unique: true,
                filter: "\"source_type\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_redirect_from_content_id",
                table: "redirect");

            migrationBuilder.DropIndex(
                name: "ix_redirect_from_language_from_slug",
                table: "redirect");

            migrationBuilder.DropIndex(
                name: "ix_redirect_from_path",
                table: "redirect");

            migrationBuilder.DropColumn(
                name: "is_auto_discovery_suppressed",
                table: "redirect");
        }
    }
}
