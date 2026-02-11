using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class ContactDeleteBehaviorForContactReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_email_log_contact_contact_id",
                table: "email_log");

            migrationBuilder.DropForeignKey(
                name: "fk_unsubscribe_contact_contact_id",
                table: "unsubscribe");

            migrationBuilder.AddForeignKey(
                name: "fk_email_log_contact_contact_id",
                table: "email_log",
                column: "contact_id",
                principalTable: "contact",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_unsubscribe_contact_contact_id",
                table: "unsubscribe",
                column: "contact_id",
                principalTable: "contact",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_email_log_contact_contact_id",
                table: "email_log");

            migrationBuilder.DropForeignKey(
                name: "fk_unsubscribe_contact_contact_id",
                table: "unsubscribe");

            migrationBuilder.AddForeignKey(
                name: "fk_email_log_contact_contact_id",
                table: "email_log",
                column: "contact_id",
                principalTable: "contact",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_unsubscribe_contact_contact_id",
                table: "unsubscribe",
                column: "contact_id",
                principalTable: "contact",
                principalColumn: "id");
        }
    }
}
