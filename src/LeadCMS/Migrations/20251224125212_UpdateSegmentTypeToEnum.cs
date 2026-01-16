using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSegmentTypeToEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert existing text values to enum integers
            // "dynamic" or "Dynamic" -> 0
            // "static" or "Static" -> 1
            migrationBuilder.Sql(@"
                ALTER TABLE segment
                ALTER COLUMN type TYPE integer
                USING (CASE
                    WHEN LOWER(type) = 'dynamic' THEN 0
                    WHEN LOWER(type) = 'static' THEN 1
                    ELSE 0
                END);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Convert enum integers back to text values
            migrationBuilder.Sql(@"
                ALTER TABLE segment
                ALTER COLUMN type TYPE text
                USING (CASE
                    WHEN type = 0 THEN 'Dynamic'
                    WHEN type = 1 THEN 'Static'
                    ELSE 'Dynamic'
                END);
            ");
        }
    }
}
