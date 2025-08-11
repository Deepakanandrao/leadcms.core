using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddTagsToMoreEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "tags",
                table: "order",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "tags",
                table: "domain",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "tags",
                table: "deal",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "tags",
                table: "contact",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "tags",
                table: "comment",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tags",
                table: "order");

            migrationBuilder.DropColumn(
                name: "tags",
                table: "domain");

            migrationBuilder.DropColumn(
                name: "tags",
                table: "deal");

            migrationBuilder.DropColumn(
                name: "tags",
                table: "contact");

            migrationBuilder.DropColumn(
                name: "tags",
                table: "comment");
        }
    }
}
