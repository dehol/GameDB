using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNormalizedTitleToGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedTitle",
                table: "Game",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImportJob",
                columns: table => new
                {
                    ImportJobId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CurrentPhase = table.Column<string>(type: "text", nullable: true),
                    SteamTotal = table.Column<int>(type: "integer", nullable: false),
                    SteamProcessed = table.Column<int>(type: "integer", nullable: false),
                    SteamImported = table.Column<int>(type: "integer", nullable: false),
                    GogTotal = table.Column<int>(type: "integer", nullable: false),
                    GogProcessed = table.Column<int>(type: "integer", nullable: false),
                    GogImported = table.Column<int>(type: "integer", nullable: false),
                    EgsTotal = table.Column<int>(type: "integer", nullable: false),
                    EgsProcessed = table.Column<int>(type: "integer", nullable: false),
                    EgsImported = table.Column<int>(type: "integer", nullable: false),
                    TotalGamesCreated = table.Column<int>(type: "integer", nullable: false),
                    TotalOffersCreated = table.Column<int>(type: "integer", nullable: false),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportJob", x => x.ImportJobId);
                });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "NormalizedTitle", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 18, 20, 56, 775, DateTimeKind.Utc).AddTicks(4153), null, new DateTime(2026, 4, 9, 18, 20, 56, 775, DateTimeKind.Utc).AddTicks(4154) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "NormalizedTitle", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 18, 20, 56, 775, DateTimeKind.Utc).AddTicks(4160), null, new DateTime(2026, 4, 9, 18, 20, 56, 775, DateTimeKind.Utc).AddTicks(4161) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "NormalizedTitle", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 18, 20, 56, 775, DateTimeKind.Utc).AddTicks(4166), null, new DateTime(2026, 4, 9, 18, 20, 56, 775, DateTimeKind.Utc).AddTicks(4167) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "NormalizedTitle", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 18, 20, 56, 775, DateTimeKind.Utc).AddTicks(4172), null, new DateTime(2026, 4, 9, 18, 20, 56, 775, DateTimeKind.Utc).AddTicks(4173) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "NormalizedTitle", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 18, 20, 56, 775, DateTimeKind.Utc).AddTicks(4177), null, new DateTime(2026, 4, 9, 18, 20, 56, 775, DateTimeKind.Utc).AddTicks(4178) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "NormalizedTitle", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 18, 20, 56, 775, DateTimeKind.Utc).AddTicks(4182), null, new DateTime(2026, 4, 9, 18, 20, 56, 775, DateTimeKind.Utc).AddTicks(4183) });

            migrationBuilder.UpdateData(
                table: "GameShop",
                keyColumn: "ShopId",
                keyValue: 1,
                column: "Name",
                value: "Steam");

            migrationBuilder.UpdateData(
                table: "GameShop",
                keyColumn: "ShopId",
                keyValue: 2,
                column: "Name",
                value: "GOG");

            migrationBuilder.InsertData(
                table: "GameShop",
                columns: new[] { "ShopId", "ApiBaseUrl", "BaseUrl", "Name" },
                values: new object[] { 3, "https://store.epicgames.com/graphql", "https://store.epicgames.com", "Epic Games Store" });

            migrationBuilder.CreateIndex(
                name: "IX_Game_NormalizedTitle",
                table: "Game",
                column: "NormalizedTitle");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportJob");

            migrationBuilder.DropIndex(
                name: "IX_Game_NormalizedTitle",
                table: "Game");

            migrationBuilder.DeleteData(
                table: "GameShop",
                keyColumn: "ShopId",
                keyValue: 3);

            migrationBuilder.DropColumn(
                name: "NormalizedTitle",
                table: "Game");

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 16, 36, 17, 411, DateTimeKind.Utc).AddTicks(6647), new DateTime(2026, 4, 9, 16, 36, 17, 411, DateTimeKind.Utc).AddTicks(6648) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 16, 36, 17, 411, DateTimeKind.Utc).AddTicks(6661), new DateTime(2026, 4, 9, 16, 36, 17, 411, DateTimeKind.Utc).AddTicks(6662) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 16, 36, 17, 411, DateTimeKind.Utc).AddTicks(6672), new DateTime(2026, 4, 9, 16, 36, 17, 411, DateTimeKind.Utc).AddTicks(6673) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 16, 36, 17, 411, DateTimeKind.Utc).AddTicks(6682), new DateTime(2026, 4, 9, 16, 36, 17, 411, DateTimeKind.Utc).AddTicks(6683) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 16, 36, 17, 411, DateTimeKind.Utc).AddTicks(6690), new DateTime(2026, 4, 9, 16, 36, 17, 411, DateTimeKind.Utc).AddTicks(6691) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 16, 36, 17, 411, DateTimeKind.Utc).AddTicks(6772), new DateTime(2026, 4, 9, 16, 36, 17, 411, DateTimeKind.Utc).AddTicks(6773) });

            migrationBuilder.UpdateData(
                table: "GameShop",
                keyColumn: "ShopId",
                keyValue: 1,
                column: "Name",
                value: "steam");

            migrationBuilder.UpdateData(
                table: "GameShop",
                keyColumn: "ShopId",
                keyValue: 2,
                column: "Name",
                value: "gog");
        }
    }
}
