using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations
{
    /// <inheritdoc />
    public partial class PresenceViaSseConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SseConnections",
                schema: "chat",
                columns: table => new
                {
                    ConnectionId = table.Column<string>(type: "text", nullable: false),
                    ServerId = table.Column<string>(type: "text", nullable: false),
                    LastSeen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SseConnections", x => x.ConnectionId);
                    table.ForeignKey(
                        name: "FK_SseConnections_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "chat",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SseConnectionGroups",
                schema: "chat",
                columns: table => new
                {
                    ConnectionId = table.Column<string>(type: "text", nullable: false),
                    GroupName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SseConnectionGroups", x => new { x.ConnectionId, x.GroupName });
                    table.ForeignKey(
                        name: "FK_SseConnectionGroups_SseConnections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalSchema: "chat",
                        principalTable: "SseConnections",
                        principalColumn: "ConnectionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SseConnections_OwnerId",
                schema: "chat",
                table: "SseConnections",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SseConnectionGroups",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "SseConnections",
                schema: "chat");
        }
    }
}
