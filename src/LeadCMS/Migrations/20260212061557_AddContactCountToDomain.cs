using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddContactCountToDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "contact_count",
                table: "domain",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                UPDATE domain
                SET contact_count = COALESCE((
                    SELECT COUNT(*) FROM contact WHERE contact.domain_id = domain.id
                ), 0);
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION update_domain_contact_count()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW.domain_id IS NOT NULL THEN
                            UPDATE domain SET contact_count = contact_count + 1 WHERE id = NEW.domain_id;
                        END IF;
                    ELSIF TG_OP = 'UPDATE' THEN
                        IF OLD.domain_id IS DISTINCT FROM NEW.domain_id THEN
                            IF OLD.domain_id IS NOT NULL THEN
                                UPDATE domain SET contact_count = contact_count - 1 WHERE id = OLD.domain_id;
                            END IF;
                            IF NEW.domain_id IS NOT NULL THEN
                                UPDATE domain SET contact_count = contact_count + 1 WHERE id = NEW.domain_id;
                            END IF;
                        END IF;
                    ELSIF TG_OP = 'DELETE' THEN
                        IF OLD.domain_id IS NOT NULL THEN
                            UPDATE domain SET contact_count = contact_count - 1 WHERE id = OLD.domain_id;
                        END IF;
                    END IF;
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trigger_update_domain_contact_count
                AFTER INSERT OR UPDATE OR DELETE ON contact
                FOR EACH ROW
                EXECUTE FUNCTION update_domain_contact_count();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trigger_update_domain_contact_count ON contact;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS update_domain_contact_count();");

            migrationBuilder.DropColumn(
                name: "contact_count",
                table: "domain");
        }
    }
}
