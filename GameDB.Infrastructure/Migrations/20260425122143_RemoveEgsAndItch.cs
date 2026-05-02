using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEgsAndItch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StagingGame_IgdbId_SteamAppId_GogId_EgsId",
                table: "StagingGame");

            // Remove EGS data (ShopId=3) before removing the GameShop seed
            migrationBuilder.Sql(@"DELETE FROM ""GameOffer"" WHERE ""ShopId"" = 3");
            migrationBuilder.Sql(@"DELETE FROM ""UserShopProfile"" WHERE ""ShopId"" = 3");

            migrationBuilder.DeleteData(
                table: "GameShop",
                keyColumn: "ShopId",
                keyValue: 3);

            migrationBuilder.DropColumn(
                name: "EgsDiscount",
                table: "StagingGame");

            migrationBuilder.DropColumn(
                name: "EgsId",
                table: "StagingGame");

            migrationBuilder.DropColumn(
                name: "EgsPrice",
                table: "StagingGame");

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 25, 12, 21, 42, 469, DateTimeKind.Utc).AddTicks(306), new DateTime(2026, 4, 25, 12, 21, 42, 469, DateTimeKind.Utc).AddTicks(307) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 25, 12, 21, 42, 469, DateTimeKind.Utc).AddTicks(313), new DateTime(2026, 4, 25, 12, 21, 42, 469, DateTimeKind.Utc).AddTicks(314) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 25, 12, 21, 42, 469, DateTimeKind.Utc).AddTicks(319), new DateTime(2026, 4, 25, 12, 21, 42, 469, DateTimeKind.Utc).AddTicks(320) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 25, 12, 21, 42, 469, DateTimeKind.Utc).AddTicks(324), new DateTime(2026, 4, 25, 12, 21, 42, 469, DateTimeKind.Utc).AddTicks(324) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 25, 12, 21, 42, 469, DateTimeKind.Utc).AddTicks(328), new DateTime(2026, 4, 25, 12, 21, 42, 469, DateTimeKind.Utc).AddTicks(329) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 25, 12, 21, 42, 469, DateTimeKind.Utc).AddTicks(332), new DateTime(2026, 4, 25, 12, 21, 42, 469, DateTimeKind.Utc).AddTicks(333) });

            migrationBuilder.CreateIndex(
                name: "IX_StagingGame_IgdbId_SteamAppId_GogId",
                table: "StagingGame",
                columns: new[] { "IgdbId", "SteamAppId", "GogId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StagingGame_IgdbId_SteamAppId_GogId",
                table: "StagingGame");

            migrationBuilder.AddColumn<short>(
                name: "EgsDiscount",
                table: "StagingGame",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EgsId",
                table: "StagingGame",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EgsPrice",
                table: "StagingGame",
                type: "numeric",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 25, 12, 7, 36, 45, DateTimeKind.Utc).AddTicks(5539), new DateTime(2026, 4, 25, 12, 7, 36, 45, DateTimeKind.Utc).AddTicks(5539) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 25, 12, 7, 36, 45, DateTimeKind.Utc).AddTicks(5545), new DateTime(2026, 4, 25, 12, 7, 36, 45, DateTimeKind.Utc).AddTicks(5546) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 25, 12, 7, 36, 45, DateTimeKind.Utc).AddTicks(5585), new DateTime(2026, 4, 25, 12, 7, 36, 45, DateTimeKind.Utc).AddTicks(5586) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 25, 12, 7, 36, 45, DateTimeKind.Utc).AddTicks(5590), new DateTime(2026, 4, 25, 12, 7, 36, 45, DateTimeKind.Utc).AddTicks(5591) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 25, 12, 7, 36, 45, DateTimeKind.Utc).AddTicks(5595), new DateTime(2026, 4, 25, 12, 7, 36, 45, DateTimeKind.Utc).AddTicks(5595) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 25, 12, 7, 36, 45, DateTimeKind.Utc).AddTicks(5599), new DateTime(2026, 4, 25, 12, 7, 36, 45, DateTimeKind.Utc).AddTicks(5599) });

            migrationBuilder.InsertData(
                table: "GameShop",
                columns: new[] { "ShopId", "ApiBaseUrl", "BaseUrl", "Name" },
                values: new object[] { 3, "https://store.epicgames.com/graphql", "https://store.epicgames.com", "Epic Games Store" });

            migrationBuilder.CreateIndex(
                name: "IX_StagingGame_IgdbId_SteamAppId_GogId_EgsId",
                table: "StagingGame",
                columns: new[] { "IgdbId", "SteamAppId", "GogId", "EgsId" });
        }
    }
}
