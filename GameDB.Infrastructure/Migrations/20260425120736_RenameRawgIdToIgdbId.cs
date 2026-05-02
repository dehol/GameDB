using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameRawgIdToIgdbId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RawgId",
                table: "Game",
                newName: "IgdbId");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IgdbId",
                table: "Game",
                newName: "RawgId");

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 21, 20, 32, 32, 5, DateTimeKind.Utc).AddTicks(3357), new DateTime(2026, 4, 21, 20, 32, 32, 5, DateTimeKind.Utc).AddTicks(3357) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 21, 20, 32, 32, 5, DateTimeKind.Utc).AddTicks(3362), new DateTime(2026, 4, 21, 20, 32, 32, 5, DateTimeKind.Utc).AddTicks(3363) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 21, 20, 32, 32, 5, DateTimeKind.Utc).AddTicks(3368), new DateTime(2026, 4, 21, 20, 32, 32, 5, DateTimeKind.Utc).AddTicks(3368) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 21, 20, 32, 32, 5, DateTimeKind.Utc).AddTicks(3373), new DateTime(2026, 4, 21, 20, 32, 32, 5, DateTimeKind.Utc).AddTicks(3373) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 21, 20, 32, 32, 5, DateTimeKind.Utc).AddTicks(3377), new DateTime(2026, 4, 21, 20, 32, 32, 5, DateTimeKind.Utc).AddTicks(3377) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 21, 20, 32, 32, 5, DateTimeKind.Utc).AddTicks(3381), new DateTime(2026, 4, 21, 20, 32, 32, 5, DateTimeKind.Utc).AddTicks(3381) });
        }
    }
}
