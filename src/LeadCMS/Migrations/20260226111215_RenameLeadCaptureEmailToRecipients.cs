using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class RenameLeadCaptureEmailToRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE setting SET key = 'LeadCapture.Email.Recipients' WHERE key = 'LeadCapture.Email.To'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE setting SET key = 'LeadCapture.Email.To' WHERE key = 'LeadCapture.Email.Recipients'");
        }
    }
}
