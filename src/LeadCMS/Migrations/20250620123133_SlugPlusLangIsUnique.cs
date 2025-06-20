using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class SlugPlusLangIsUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_content_slug",
                table: "content");

            migrationBuilder.CreateIndex(
                name: "ix_content_slug_language",
                table: "content",
                columns: new[] { "slug", "language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_content_slug_language",
                table: "content");

            migrationBuilder.CreateIndex(
                name: "ix_content_slug",
                table: "content",
                column: "slug",
                unique: true);
        }
    }
}
