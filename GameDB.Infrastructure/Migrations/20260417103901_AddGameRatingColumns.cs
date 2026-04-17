using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameRatingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Rating",
                table: "Game",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RatingCount",
                table: "Game",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "Rating", "RatingCount", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 10, 39, 0, 680, DateTimeKind.Utc).AddTicks(9023), null, null, new DateTime(2026, 4, 17, 10, 39, 0, 680, DateTimeKind.Utc).AddTicks(9024) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "Rating", "RatingCount", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 10, 39, 0, 680, DateTimeKind.Utc).AddTicks(9029), null, null, new DateTime(2026, 4, 17, 10, 39, 0, 680, DateTimeKind.Utc).AddTicks(9029) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "Rating", "RatingCount", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 10, 39, 0, 680, DateTimeKind.Utc).AddTicks(9034), null, null, new DateTime(2026, 4, 17, 10, 39, 0, 680, DateTimeKind.Utc).AddTicks(9034) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "Rating", "RatingCount", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 10, 39, 0, 680, DateTimeKind.Utc).AddTicks(9039), null, null, new DateTime(2026, 4, 17, 10, 39, 0, 680, DateTimeKind.Utc).AddTicks(9039) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "Rating", "RatingCount", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 10, 39, 0, 680, DateTimeKind.Utc).AddTicks(9043), null, null, new DateTime(2026, 4, 17, 10, 39, 0, 680, DateTimeKind.Utc).AddTicks(9043) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "Rating", "RatingCount", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 10, 39, 0, 680, DateTimeKind.Utc).AddTicks(9046), null, null, new DateTime(2026, 4, 17, 10, 39, 0, 680, DateTimeKind.Utc).AddTicks(9047) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "RatingCount",
                table: "Game");

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6210), new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6210) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6215), new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6216) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6220), new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6221) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6226), new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6226) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6230), new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6230) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6274), new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6274) });
        }
    }
}
