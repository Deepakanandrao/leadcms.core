using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddContentTypeSlugAffixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "slug_postfix",
                table: "content_type",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "slug_prefix",
                table: "content_type",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "slug_postfix",
                table: "content_type");

            migrationBuilder.DropColumn(
                name: "slug_prefix",
                table: "content_type");
        }
    }
}
