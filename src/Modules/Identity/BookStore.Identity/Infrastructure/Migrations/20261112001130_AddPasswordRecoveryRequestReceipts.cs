using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookStore.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordRecoveryRequestReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PasswordRecoveryRequestReceipts",
                schema: "identity",
                columns: table => new
                {
                    RequestEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordRecoveryRequestReceipts", x => x.RequestEventId);
                    table.ForeignKey(
                        name: "FK_PasswordRecoveryRequestReceipts_OutboxMessages_RequestEventId",
                        column: x => x.RequestEventId,
                        principalSchema: "identity",
                        principalTable: "OutboxMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasswordRecoveryRequestReceipts",
                schema: "identity");
        }
    }
}
