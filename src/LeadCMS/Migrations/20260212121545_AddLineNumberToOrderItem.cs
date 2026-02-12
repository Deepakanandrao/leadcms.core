using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddLineNumberToOrderItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_order_item_order_id",
                table: "order_item");

            migrationBuilder.AddColumn<int>(
                name: "line_number",
                table: "order_item",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill: assign sequential line numbers within each order
            migrationBuilder.Sql(@"
                UPDATE order_item
                SET line_number = sub.rn
                FROM (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY order_id ORDER BY id) AS rn
                    FROM order_item
                ) sub
                WHERE order_item.id = sub.id;
            ");

            migrationBuilder.CreateIndex(
                name: "ix_order_item_order_id_line_number",
                table: "order_item",
                columns: new[] { "order_id", "line_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_order_item_order_id_line_number",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "line_number",
                table: "order_item");

            migrationBuilder.CreateIndex(
                name: "ix_order_item_order_id",
                table: "order_item",
                column: "order_id");
        }
    }
}
