using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingCoverUrlColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverUrl",
                table: "Game",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CoverUrl", "CreatedAt", "UpdatedAt" },
                values: new object[] { null, new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6579), new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6579) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CoverUrl", "CreatedAt", "UpdatedAt" },
                values: new object[] { null, new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6587), new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6588) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CoverUrl", "CreatedAt", "UpdatedAt" },
                values: new object[] { null, new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6593), new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6593) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CoverUrl", "CreatedAt", "UpdatedAt" },
                values: new object[] { null, new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6598), new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6599) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CoverUrl", "CreatedAt", "UpdatedAt" },
                values: new object[] { null, new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6603), new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6603) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CoverUrl", "CreatedAt", "UpdatedAt" },
                values: new object[] { null, new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6607), new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6608) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverUrl",
                table: "Game");

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 19, 8, 53, 69, DateTimeKind.Utc).AddTicks(3835), new DateTime(2026, 4, 17, 19, 8, 53, 69, DateTimeKind.Utc).AddTicks(3836) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 19, 8, 53, 69, DateTimeKind.Utc).AddTicks(3841), new DateTime(2026, 4, 17, 19, 8, 53, 69, DateTimeKind.Utc).AddTicks(3842) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 19, 8, 53, 69, DateTimeKind.Utc).AddTicks(3846), new DateTime(2026, 4, 17, 19, 8, 53, 69, DateTimeKind.Utc).AddTicks(3846) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 19, 8, 53, 69, DateTimeKind.Utc).AddTicks(3850), new DateTime(2026, 4, 17, 19, 8, 53, 69, DateTimeKind.Utc).AddTicks(3850) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 19, 8, 53, 69, DateTimeKind.Utc).AddTicks(3853), new DateTime(2026, 4, 17, 19, 8, 53, 69, DateTimeKind.Utc).AddTicks(3854) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 19, 8, 53, 69, DateTimeKind.Utc).AddTicks(3857), new DateTime(2026, 4, 17, 19, 8, 53, 69, DateTimeKind.Utc).AddTicks(3857) });
        }
    }
}
