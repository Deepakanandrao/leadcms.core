using System.Collections.Generic;
using LeadCMS.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class MakeContactEmailAndDomainOptional_AddPhoneRawAndPendingUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert empty-string emails and phones to NULL before altering columns
            migrationBuilder.Sql("UPDATE contact SET email = NULL WHERE email = '';");
            migrationBuilder.Sql("UPDATE contact SET phone = NULL WHERE phone = '';");

            migrationBuilder.DropIndex(
                name: "ix_contact_email",
                table: "contact");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "contact",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "domain_id",
                table: "contact",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<List<PendingContactUpdate>>(
                name: "pending_updates",
                table: "contact",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_raw",
                table: "contact",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_contact_email",
                table: "contact",
                column: "email",
                unique: true,
                filter: "\"email\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_contact_phone",
                table: "contact",
                column: "phone",
                filter: "\"phone\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_contact_phone_raw",
                table: "contact",
                column: "phone_raw",
                filter: "\"phone_raw\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_contact_email",
                table: "contact");

            migrationBuilder.DropIndex(
                name: "ix_contact_phone",
                table: "contact");

            migrationBuilder.DropIndex(
                name: "ix_contact_phone_raw",
                table: "contact");

            migrationBuilder.DropColumn(
                name: "pending_updates",
                table: "contact");

            migrationBuilder.DropColumn(
                name: "phone_raw",
                table: "contact");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "contact",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "domain_id",
                table: "contact",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_contact_email",
                table: "contact",
                column: "email",
                unique: true);
        }
    }
}
