using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEmailLogChangeLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove all change log records for EmailLog since the entity
            // no longer opts into change tracking. These records are redundant
            // because email_log is append-only and already has created_at/updated_at.
            migrationBuilder.Sql("DELETE FROM change_log WHERE object_type = 'EmailLog';");

            // Clean up associated task log entries so background tasks
            // don't attempt to re-process stale EmailLog batches.
            migrationBuilder.Sql("DELETE FROM change_log_task_log WHERE task_name LIKE '%_EmailLog';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data deletion cannot be reversed.
        }
    }
}
