using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddFullNameToContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "full_name",
                table: "contact",
                type: "text",
                nullable: true,
                computedColumnSql: "TRIM(COALESCE(\"first_name\", '') || ' ' || COALESCE(\"middle_name\", '') || ' ' || COALESCE(\"last_name\", ''))",
                stored: true);

            // Create index on full_name for better query performance
            migrationBuilder.CreateIndex(
                name: "ix_contact_full_name",
                table: "contact",
                column: "full_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_contact_full_name",
                table: "contact");

            migrationBuilder.DropColumn(
                name: "full_name",
                table: "contact");
        }
    }
}
