using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class RenameCommentToResultAndAddDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "comment",
                table: "task_execution_log",
                newName: "result");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "duration",
                table: "task_execution_log",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "duration",
                table: "task_execution_log");

            migrationBuilder.RenameColumn(
                name: "result",
                table: "task_execution_log",
                newName: "comment");
        }
    }
}
