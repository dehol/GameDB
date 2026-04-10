using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeSmutokAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Make smutokdanilo56@gmail.com an admin
            migrationBuilder.Sql(@"
                UPDATE ""User"" 
                SET ""RoleId"" = 3 
                WHERE LOWER(""Email"") = 'smutokdanilo56@gmail.com';
            ");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 15, 59, 51, 704, DateTimeKind.Utc).AddTicks(249), new DateTime(2026, 4, 9, 15, 59, 51, 704, DateTimeKind.Utc).AddTicks(250) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 15, 59, 51, 704, DateTimeKind.Utc).AddTicks(255), new DateTime(2026, 4, 9, 15, 59, 51, 704, DateTimeKind.Utc).AddTicks(256) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 15, 59, 51, 704, DateTimeKind.Utc).AddTicks(261), new DateTime(2026, 4, 9, 15, 59, 51, 704, DateTimeKind.Utc).AddTicks(262) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 15, 59, 51, 704, DateTimeKind.Utc).AddTicks(266), new DateTime(2026, 4, 9, 15, 59, 51, 704, DateTimeKind.Utc).AddTicks(266) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 15, 59, 51, 704, DateTimeKind.Utc).AddTicks(270), new DateTime(2026, 4, 9, 15, 59, 51, 704, DateTimeKind.Utc).AddTicks(270) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 15, 59, 51, 704, DateTimeKind.Utc).AddTicks(274), new DateTime(2026, 4, 9, 15, 59, 51, 704, DateTimeKind.Utc).AddTicks(274) });
        }
    }
}
