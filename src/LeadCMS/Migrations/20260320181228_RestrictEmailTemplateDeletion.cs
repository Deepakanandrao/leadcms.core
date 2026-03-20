using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class RestrictEmailTemplateDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sequence_step_email_template_email_template_id",
                table: "sequence_step");

            migrationBuilder.AddForeignKey(
                name: "fk_sequence_step_email_template_email_template_id",
                table: "sequence_step",
                column: "email_template_id",
                principalTable: "email_template",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sequence_step_email_template_email_template_id",
                table: "sequence_step");

            migrationBuilder.AddForeignKey(
                name: "fk_sequence_step_email_template_email_template_id",
                table: "sequence_step",
                column: "email_template_id",
                principalTable: "email_template",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
