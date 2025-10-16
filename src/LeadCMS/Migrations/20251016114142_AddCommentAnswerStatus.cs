using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentAnswerStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "answer_status",
                table: "comment",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Data migration: Set correct answer status and comment status for existing comments

            // 1. Set internal user replies to external comments as Answer status (3) and Closed answer status (2)
            migrationBuilder.Sql(@"
                UPDATE comment AS child_comment
                SET status = 3, answer_status = 2
                WHERE LOWER(child_comment.author_email) IN (SELECT LOWER(email) FROM public.users)
                AND child_comment.parent_id IS NOT NULL
                AND EXISTS (
                    SELECT 1
                    FROM comment AS parent
                    WHERE parent.id = child_comment.parent_id
                    AND LOWER(parent.author_email) NOT IN (SELECT LOWER(email) FROM public.users)
                )");

            // 2. Set other internal user comments (top-level or replies to internal users) to Closed answer status (2)
            migrationBuilder.Sql(@"
                UPDATE comment
                SET answer_status = 2
                WHERE LOWER(author_email) IN (SELECT LOWER(email) FROM public.users)
                AND (status != 3 OR status IS NULL)"); // Don't override Answer status set above

            // 3. For external user comments that have replies from internal users
            // Mark them as both answered (answer_status = 1) and approved (status = 1)
            migrationBuilder.Sql(@"
                UPDATE comment AS parent_comment
                SET answer_status = 1, status = 1
                WHERE parent_comment.author_email NOT IN (SELECT LOWER(email) FROM public.users)
                AND EXISTS (
                    SELECT 1
                    FROM comment AS child
                    WHERE child.parent_id = parent_comment.id
                    AND child.author_email IN (SELECT LOWER(email) FROM public.users)
                )");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "answer_status",
                table: "comment");
        }
    }
}
