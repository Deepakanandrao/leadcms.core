using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "translation_key",
                table: "email_template",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "translation_key",
                table: "email_group",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "translation_key",
                table: "content",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "translation_key",
                table: "contact",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "translation_key",
                table: "comment",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "translation_key",
                table: "email_template");

            migrationBuilder.DropColumn(
                name: "translation_key",
                table: "email_group");

            migrationBuilder.DropColumn(
                name: "translation_key",
                table: "content");

            migrationBuilder.DropColumn(
                name: "translation_key",
                table: "contact");

            migrationBuilder.DropColumn(
                name: "translation_key",
                table: "comment");
        }
    }
}
