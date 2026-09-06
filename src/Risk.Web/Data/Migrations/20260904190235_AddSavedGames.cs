using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Risk.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedGames",
                columns: table => new
                {
                    OwnerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlayersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PlayerCount = table.Column<int>(type: "int", nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SavedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedGames", x => x.OwnerId);
                    table.ForeignKey(
                        name: "FK_SavedGames_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedGames");
        }
    }
}
