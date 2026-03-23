using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddSequenceIdToEmailLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "sequence_id",
                table: "email_log",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_log_sequence_id",
                table: "email_log",
                column: "sequence_id");

            migrationBuilder.AddForeignKey(
                name: "fk_email_log_sequence_sequence_id",
                table: "email_log",
                column: "sequence_id",
                principalTable: "sequence",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_email_log_sequence_sequence_id",
                table: "email_log");

            migrationBuilder.DropIndex(
                name: "ix_email_log_sequence_id",
                table: "email_log");

            migrationBuilder.DropColumn(
                name: "sequence_id",
                table: "email_log");
        }
    }
}
