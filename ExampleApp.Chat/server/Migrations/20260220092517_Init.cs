using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "chat");

            migrationBuilder.CreateTable(
                name: "Rooms",
                schema: "chat",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "chat",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Nickname = table.Column<string>(type: "text", nullable: false),
                    Salt = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                schema: "chat",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoomId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "chat",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "chat",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "UserRooms",
                schema: "chat",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoomId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRooms", x => new { x.UserId, x.RoomId });
                    table.ForeignKey(
                        name: "FK_UserRooms_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "chat",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRooms_Users_UserId",
                        column: x => x.UserId,
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
                name: "IX_Messages_RoomId",
                schema: "chat",
                table: "Messages",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_UserId",
                schema: "chat",
                table: "Messages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SseConnections_OwnerId",
                schema: "chat",
                table: "SseConnections",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRooms_RoomId",
                schema: "chat",
                table: "UserRooms",
                column: "RoomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Messages",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "SseConnectionGroups",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "UserRooms",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "SseConnections",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "Rooms",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "chat");
        }
    }
}
