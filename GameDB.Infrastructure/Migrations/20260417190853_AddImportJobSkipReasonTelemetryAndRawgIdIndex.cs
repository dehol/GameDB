using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImportJobSkipReasonTelemetryAndRawgIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EligibleForImport",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IgdbCollected",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RequestedIgdbGameIds",
                table: "ImportJob",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequestedLimit",
                table: "ImportJob",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequestedOverwriteExisting",
                table: "ImportJob",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SkippedAlreadyImported",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SkippedDuplicateTitles",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SkippedInvalidStoreIds",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SkippedNoStoreOffers",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WarningMessage",
                table: "ImportJob",
                type: "text",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_Game_RawgId",
                table: "Game",
                column: "RawgId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Game_RawgId",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "EligibleForImport",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "IgdbCollected",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "RequestedIgdbGameIds",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "RequestedLimit",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "RequestedOverwriteExisting",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "SkippedAlreadyImported",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "SkippedDuplicateTitles",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "SkippedInvalidStoreIds",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "SkippedNoStoreOffers",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "WarningMessage",
                table: "ImportJob");

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 17, 48, 56, 682, DateTimeKind.Utc).AddTicks(9558), new DateTime(2026, 4, 17, 17, 48, 56, 682, DateTimeKind.Utc).AddTicks(9560) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 17, 48, 56, 682, DateTimeKind.Utc).AddTicks(9566), new DateTime(2026, 4, 17, 17, 48, 56, 682, DateTimeKind.Utc).AddTicks(9566) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 17, 48, 56, 682, DateTimeKind.Utc).AddTicks(9569), new DateTime(2026, 4, 17, 17, 48, 56, 682, DateTimeKind.Utc).AddTicks(9569) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 17, 48, 56, 682, DateTimeKind.Utc).AddTicks(9573), new DateTime(2026, 4, 17, 17, 48, 56, 682, DateTimeKind.Utc).AddTicks(9574) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 17, 48, 56, 682, DateTimeKind.Utc).AddTicks(9577), new DateTime(2026, 4, 17, 17, 48, 56, 682, DateTimeKind.Utc).AddTicks(9577) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 17, 17, 48, 56, 682, DateTimeKind.Utc).AddTicks(9580), new DateTime(2026, 4, 17, 17, 48, 56, 682, DateTimeKind.Utc).AddTicks(9580) });
        }
    }
}
