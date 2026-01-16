using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class ExtendAccountAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "account",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "profit",
                table: "account",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tin",
                table: "account",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address",
                table: "account");

            migrationBuilder.DropColumn(
                name: "profit",
                table: "account");

            migrationBuilder.DropColumn(
                name: "tin",
                table: "account");
        }
    }
}
