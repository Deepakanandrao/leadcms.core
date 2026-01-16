using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddCountColumnsAndTriggersToAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add columns
            migrationBuilder.AddColumn<int>(
                name: "contact_count",
                table: "account",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "deals_count",
                table: "account",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "domains_count",
                table: "account",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Populate existing counts
            migrationBuilder.Sql(@"
                UPDATE account
                SET contact_count = COALESCE((
                    SELECT COUNT(*) FROM contact WHERE contact.account_id = account.id
                ), 0);
            ");

            migrationBuilder.Sql(@"
                UPDATE account
                SET deals_count = COALESCE((
                    SELECT COUNT(*) FROM deal WHERE deal.account_id = account.id
                ), 0);
            ");

            migrationBuilder.Sql(@"
                UPDATE account
                SET domains_count = COALESCE((
                    SELECT COUNT(*) FROM domain WHERE domain.account_id = account.id
                ), 0);
            ");

            // Create function to update contact count
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION update_account_contact_count()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW.account_id IS NOT NULL THEN
                            UPDATE account SET contact_count = contact_count + 1 WHERE id = NEW.account_id;
                        END IF;
                    ELSIF TG_OP = 'UPDATE' THEN
                        IF OLD.account_id IS DISTINCT FROM NEW.account_id THEN
                            IF OLD.account_id IS NOT NULL THEN
                                UPDATE account SET contact_count = contact_count - 1 WHERE id = OLD.account_id;
                            END IF;
                            IF NEW.account_id IS NOT NULL THEN
                                UPDATE account SET contact_count = contact_count + 1 WHERE id = NEW.account_id;
                            END IF;
                        END IF;
                    ELSIF TG_OP = 'DELETE' THEN
                        IF OLD.account_id IS NOT NULL THEN
                            UPDATE account SET contact_count = contact_count - 1 WHERE id = OLD.account_id;
                        END IF;
                    END IF;
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trigger_update_account_contact_count
                AFTER INSERT OR UPDATE OR DELETE ON contact
                FOR EACH ROW
                EXECUTE FUNCTION update_account_contact_count();
            ");

            // Create function to update deals count
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION update_account_deals_count()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW.account_id IS NOT NULL THEN
                            UPDATE account SET deals_count = deals_count + 1 WHERE id = NEW.account_id;
                        END IF;
                    ELSIF TG_OP = 'UPDATE' THEN
                        IF OLD.account_id IS DISTINCT FROM NEW.account_id THEN
                            IF OLD.account_id IS NOT NULL THEN
                                UPDATE account SET deals_count = deals_count - 1 WHERE id = OLD.account_id;
                            END IF;
                            IF NEW.account_id IS NOT NULL THEN
                                UPDATE account SET deals_count = deals_count + 1 WHERE id = NEW.account_id;
                            END IF;
                        END IF;
                    ELSIF TG_OP = 'DELETE' THEN
                        IF OLD.account_id IS NOT NULL THEN
                            UPDATE account SET deals_count = deals_count - 1 WHERE id = OLD.account_id;
                        END IF;
                    END IF;
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trigger_update_account_deals_count
                AFTER INSERT OR UPDATE OR DELETE ON deal
                FOR EACH ROW
                EXECUTE FUNCTION update_account_deals_count();
            ");

            // Create function to update domains count
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION update_account_domains_count()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW.account_id IS NOT NULL THEN
                            UPDATE account SET domains_count = domains_count + 1 WHERE id = NEW.account_id;
                        END IF;
                    ELSIF TG_OP = 'UPDATE' THEN
                        IF OLD.account_id IS DISTINCT FROM NEW.account_id THEN
                            IF OLD.account_id IS NOT NULL THEN
                                UPDATE account SET domains_count = domains_count - 1 WHERE id = OLD.account_id;
                            END IF;
                            IF NEW.account_id IS NOT NULL THEN
                                UPDATE account SET domains_count = domains_count + 1 WHERE id = NEW.account_id;
                            END IF;
                        END IF;
                    ELSIF TG_OP = 'DELETE' THEN
                        IF OLD.account_id IS NOT NULL THEN
                            UPDATE account SET domains_count = domains_count - 1 WHERE id = OLD.account_id;
                        END IF;
                    END IF;
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trigger_update_account_domains_count
                AFTER INSERT OR UPDATE OR DELETE ON domain
                FOR EACH ROW
                EXECUTE FUNCTION update_account_domains_count();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop triggers
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trigger_update_account_contact_count ON contact;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trigger_update_account_deals_count ON deal;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trigger_update_account_domains_count ON domain;");

            // Drop functions
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS update_account_contact_count();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS update_account_deals_count();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS update_account_domains_count();");

            // Drop columns
            migrationBuilder.DropColumn(
                name: "contact_count",
                table: "account");

            migrationBuilder.DropColumn(
                name: "deals_count",
                table: "account");

            migrationBuilder.DropColumn(
                name: "domains_count",
                table: "account");
        }
    }
}
