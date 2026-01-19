using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class FixContactFullNameSpacing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "full_name",
                table: "contact",
                type: "text",
                nullable: true,
                computedColumnSql: "TRIM(COALESCE(\"first_name\", '') || CASE WHEN COALESCE(\"middle_name\", '') != '' THEN ' ' || \"middle_name\" ELSE '' END || CASE WHEN COALESCE(\"last_name\", '') != '' THEN ' ' || \"last_name\" ELSE '' END)",
                stored: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComputedColumnSql: "TRIM(COALESCE(\"first_name\", '') || ' ' || COALESCE(\"middle_name\", '') || ' ' || COALESCE(\"last_name\", ''))",
                oldStored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "full_name",
                table: "contact",
                type: "text",
                nullable: true,
                computedColumnSql: "TRIM(COALESCE(\"first_name\", '') || ' ' || COALESCE(\"middle_name\", '') || ' ' || COALESCE(\"last_name\", ''))",
                stored: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComputedColumnSql: "TRIM(COALESCE(\"first_name\", '') || CASE WHEN COALESCE(\"middle_name\", '') != '' THEN ' ' || \"middle_name\" ELSE '' END || CASE WHEN COALESCE(\"last_name\", '') != '' THEN ' ' || \"last_name\" ELSE '' END)",
                oldStored: true);
        }
    }
}
