using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class ExtendSettingsWithLanguageAndMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_setting_key_user_id",
                table: "setting");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "setting",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "language",
                table: "setting",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "required",
                table: "setting",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "setting",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_setting_key_user_id_language",
                table: "setting",
                columns: new[] { "key", "user_id", "language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_setting_key_user_id_language",
                table: "setting");

            migrationBuilder.DropColumn(
                name: "description",
                table: "setting");

            migrationBuilder.DropColumn(
                name: "language",
                table: "setting");

            migrationBuilder.DropColumn(
                name: "required",
                table: "setting");

            migrationBuilder.DropColumn(
                name: "type",
                table: "setting");

            migrationBuilder.CreateIndex(
                name: "ix_setting_key_user_id",
                table: "setting",
                columns: new[] { "key", "user_id" },
                unique: true);
        }
    }
}
