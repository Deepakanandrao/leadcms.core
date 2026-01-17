using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddContactOrderCountAndTotalRevenue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "deals_count",
                table: "contact",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_order_date",
                table: "contact",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "orders_count",
                table: "contact",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "total_revenue",
                table: "contact",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Backfill existing contacts
            migrationBuilder.Sql(@"
                UPDATE contact
                SET deals_count = COALESCE(deal_counts.count, 0)
                FROM (
                    SELECT contacts_id AS contact_id, COUNT(*) AS count
                    FROM contact_deal
                    GROUP BY contacts_id
                ) AS deal_counts
                WHERE contact.id = deal_counts.contact_id;
            ");

            migrationBuilder.Sql(@"
                UPDATE contact
                SET orders_count = COALESCE(order_stats.orders_count, 0),
                    last_order_date = order_stats.last_order_date,
                    total_revenue = COALESCE(order_stats.total_revenue, 0)
                FROM (
                    SELECT contact_id,
                           COUNT(*) AS orders_count,
                           MAX(created_at) AS last_order_date,
                           COALESCE(SUM(total)::numeric(18,2), 0) AS total_revenue
                    FROM ""order""
                    GROUP BY contact_id
                ) AS order_stats
                WHERE contact.id = order_stats.contact_id;
            ");

            // Helpers to keep contact stats in sync
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION refresh_contact_order_stats_for(p_contact_id integer)
                RETURNS void AS $$
                BEGIN
                    IF p_contact_id IS NULL THEN
                        RETURN;
                    END IF;

                    UPDATE contact
                    SET orders_count = COALESCE((SELECT COUNT(*) FROM ""order"" WHERE contact_id = p_contact_id), 0),
                        last_order_date = (SELECT MAX(created_at) FROM ""order"" WHERE contact_id = p_contact_id),
                        total_revenue = COALESCE((SELECT SUM(total)::numeric(18,2) FROM ""order"" WHERE contact_id = p_contact_id), 0)
                    WHERE id = p_contact_id;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION refresh_contact_deals_count_for(p_contact_id integer)
                RETURNS void AS $$
                BEGIN
                    IF p_contact_id IS NULL THEN
                        RETURN;
                    END IF;

                    UPDATE contact
                    SET deals_count = COALESCE((SELECT COUNT(*) FROM contact_deal WHERE contacts_id = p_contact_id), 0)
                    WHERE id = p_contact_id;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION apply_contact_order_stats_change()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        PERFORM refresh_contact_order_stats_for(NEW.contact_id);
                    ELSIF TG_OP = 'UPDATE' THEN
                        PERFORM refresh_contact_order_stats_for(OLD.contact_id);
                        IF NEW.contact_id IS DISTINCT FROM OLD.contact_id THEN
                            PERFORM refresh_contact_order_stats_for(NEW.contact_id);
                        END IF;
                    ELSIF TG_OP = 'DELETE' THEN
                        PERFORM refresh_contact_order_stats_for(OLD.contact_id);
                    END IF;
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trigger_update_contact_order_stats
                AFTER INSERT OR UPDATE OR DELETE ON ""order""
                FOR EACH ROW
                EXECUTE FUNCTION apply_contact_order_stats_change();
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION apply_contact_deals_count_change()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        PERFORM refresh_contact_deals_count_for(NEW.contacts_id);
                    ELSIF TG_OP = 'UPDATE' THEN
                        PERFORM refresh_contact_deals_count_for(OLD.contacts_id);
                        IF NEW.contacts_id IS DISTINCT FROM OLD.contacts_id THEN
                            PERFORM refresh_contact_deals_count_for(NEW.contacts_id);
                        END IF;
                    ELSIF TG_OP = 'DELETE' THEN
                        PERFORM refresh_contact_deals_count_for(OLD.contacts_id);
                    END IF;
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trigger_update_contact_deals_count
                AFTER INSERT OR UPDATE OR DELETE ON contact_deal
                FOR EACH ROW
                EXECUTE FUNCTION apply_contact_deals_count_change();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trigger_update_contact_order_stats ON ""order"";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trigger_update_contact_deals_count ON contact_deal;");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS apply_contact_order_stats_change();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS apply_contact_deals_count_change();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS refresh_contact_order_stats_for(integer);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS refresh_contact_deals_count_for(integer);");

            migrationBuilder.DropColumn(
                name: "deals_count",
                table: "contact");

            migrationBuilder.DropColumn(
                name: "last_order_date",
                table: "contact");

            migrationBuilder.DropColumn(
                name: "orders_count",
                table: "contact");

            migrationBuilder.DropColumn(
                name: "total_revenue",
                table: "contact");
        }
    }
}
