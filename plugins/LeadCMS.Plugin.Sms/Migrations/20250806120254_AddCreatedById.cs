using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Plugin.Sms.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedById : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "created_by_id",
                table: "sms_log",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "sms_log");
        }
    }
}
