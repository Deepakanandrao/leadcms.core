using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaOriginal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "height",
                table: "media",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "original_data",
                table: "media",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_extension",
                table: "media",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "original_height",
                table: "media",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_mime_type",
                table: "media",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_name",
                table: "media",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "original_size",
                table: "media",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "original_width",
                table: "media",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "width",
                table: "media",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "height",
                table: "media");

            migrationBuilder.DropColumn(
                name: "original_data",
                table: "media");

            migrationBuilder.DropColumn(
                name: "original_extension",
                table: "media");

            migrationBuilder.DropColumn(
                name: "original_height",
                table: "media");

            migrationBuilder.DropColumn(
                name: "original_mime_type",
                table: "media");

            migrationBuilder.DropColumn(
                name: "original_name",
                table: "media");

            migrationBuilder.DropColumn(
                name: "original_size",
                table: "media");

            migrationBuilder.DropColumn(
                name: "original_width",
                table: "media");

            migrationBuilder.DropColumn(
                name: "width",
                table: "media");
        }
    }
}
