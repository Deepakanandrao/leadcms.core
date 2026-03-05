using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Converts all existing Contact.Timezone values from JavaScript
    /// <c>Date.getTimezoneOffset()</c> convention (sign inverted, e.g. −120 for UTC+2)
    /// to the standard UTC offset convention (e.g. +120 for UTC+2).
    /// </remarks>
    public partial class MigrateContactTimezoneToUtcFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE contact SET timezone = -timezone WHERE timezone IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE contact SET timezone = -timezone WHERE timezone IS NOT NULL;");
        }
    }
}
