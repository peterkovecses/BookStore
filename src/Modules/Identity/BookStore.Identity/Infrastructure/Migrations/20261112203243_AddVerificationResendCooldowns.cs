using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookStore.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVerificationResendCooldowns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VerificationResendCooldowns",
                schema: "identity",
                columns: table => new
                {
                    EmailHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationResendCooldowns", x => x.EmailHash);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VerificationResendCooldowns_ExpiresAt",
                schema: "identity",
                table: "VerificationResendCooldowns",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VerificationResendCooldowns",
                schema: "identity");
        }
    }
}
