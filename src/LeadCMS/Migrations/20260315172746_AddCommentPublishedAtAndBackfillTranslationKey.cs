using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentPublishedAtAndBackfillTranslationKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "published_at",
                table: "comment",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill published_at for existing rows
            migrationBuilder.Sql("UPDATE comment SET published_at = COALESCE(updated_at, created_at);");

            // Backfill translation_key for existing rows that have a null value
            migrationBuilder.Sql(@"
                UPDATE comment
                SET translation_key = 'comment_' || commentable_type || '_' || commentable_id
                    || '_' || encode(substring(sha256(convert_to(to_char(created_at AT TIME ZONE 'UTC', 'YYYY-MM-DD""T""HH24:MI:SS.US0000""Z""'), 'UTF8')) from 1 for 4), 'hex')
                    || '_' || encode(substring(sha256(convert_to(COALESCE(body, ''), 'UTF8')) from 1 for 4), 'hex')
                WHERE translation_key IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "published_at",
                table: "comment");
        }
    }
}
