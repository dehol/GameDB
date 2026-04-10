using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStagingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RawGameData",
                columns: table => new
                {
                    RawGameDataId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Processed = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingAttempts = table.Column<int>(type: "integer", nullable: false),
                    ProcessingError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawGameData", x => x.RawGameDataId);
                });

            migrationBuilder.CreateTable(
                name: "StagingGame",
                columns: table => new
                {
                    StagingGameId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NormalizedTitle = table.Column<string>(type: "text", nullable: false),
                    IgdbId = table.Column<string>(type: "text", nullable: true),
                    SteamAppId = table.Column<string>(type: "text", nullable: true),
                    GogId = table.Column<string>(type: "text", nullable: true),
                    EgsId = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ReleaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Developer = table.Column<string>(type: "text", nullable: true),
                    Publisher = table.Column<string>(type: "text", nullable: true),
                    GenresJson = table.Column<string>(type: "text", nullable: true),
                    Genres = table.Column<List<string>>(type: "text[]", nullable: false),
                    SteamPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    SteamDiscount = table.Column<short>(type: "smallint", nullable: true),
                    GogPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    GogDiscount = table.Column<short>(type: "smallint", nullable: true),
                    EgsPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    EgsDiscount = table.Column<short>(type: "smallint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    GameId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StagingGame", x => x.StagingGameId);
                    table.ForeignKey(
                        name: "FK_StagingGame_Game_GameId",
                        column: x => x.GameId,
                        principalTable: "Game",
                        principalColumn: "GameId");
                });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 10, 11, 18, 0, 740, DateTimeKind.Utc).AddTicks(3792), new DateTime(2026, 4, 10, 11, 18, 0, 740, DateTimeKind.Utc).AddTicks(3793) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 10, 11, 18, 0, 740, DateTimeKind.Utc).AddTicks(3798), new DateTime(2026, 4, 10, 11, 18, 0, 740, DateTimeKind.Utc).AddTicks(3799) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 10, 11, 18, 0, 740, DateTimeKind.Utc).AddTicks(3808), new DateTime(2026, 4, 10, 11, 18, 0, 740, DateTimeKind.Utc).AddTicks(3809) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 10, 11, 18, 0, 740, DateTimeKind.Utc).AddTicks(3814), new DateTime(2026, 4, 10, 11, 18, 0, 740, DateTimeKind.Utc).AddTicks(3815) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 10, 11, 18, 0, 740, DateTimeKind.Utc).AddTicks(3819), new DateTime(2026, 4, 10, 11, 18, 0, 740, DateTimeKind.Utc).AddTicks(3819) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 10, 11, 18, 0, 740, DateTimeKind.Utc).AddTicks(3824), new DateTime(2026, 4, 10, 11, 18, 0, 740, DateTimeKind.Utc).AddTicks(3824) });

            migrationBuilder.CreateIndex(
                name: "IX_RawGameData_FetchedAt",
                table: "RawGameData",
                column: "FetchedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RawGameData_Processed",
                table: "RawGameData",
                column: "Processed");

            migrationBuilder.CreateIndex(
                name: "IX_RawGameData_Source_ExternalId",
                table: "RawGameData",
                columns: new[] { "Source", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StagingGame_GameId",
                table: "StagingGame",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_StagingGame_IgdbId_SteamAppId_GogId_EgsId",
                table: "StagingGame",
                columns: new[] { "IgdbId", "SteamAppId", "GogId", "EgsId" });

            migrationBuilder.CreateIndex(
                name: "IX_StagingGame_IsProcessed",
                table: "StagingGame",
                column: "IsProcessed");

            migrationBuilder.CreateIndex(
                name: "IX_StagingGame_NormalizedTitle",
                table: "StagingGame",
                column: "NormalizedTitle");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawGameData");

            migrationBuilder.DropTable(
                name: "StagingGame");

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 20, 19, 34, 664, DateTimeKind.Utc).AddTicks(8246), new DateTime(2026, 4, 9, 20, 19, 34, 664, DateTimeKind.Utc).AddTicks(8247) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 20, 19, 34, 664, DateTimeKind.Utc).AddTicks(8252), new DateTime(2026, 4, 9, 20, 19, 34, 664, DateTimeKind.Utc).AddTicks(8253) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 20, 19, 34, 664, DateTimeKind.Utc).AddTicks(8257), new DateTime(2026, 4, 9, 20, 19, 34, 664, DateTimeKind.Utc).AddTicks(8258) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 20, 19, 34, 664, DateTimeKind.Utc).AddTicks(8262), new DateTime(2026, 4, 9, 20, 19, 34, 664, DateTimeKind.Utc).AddTicks(8263) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 20, 19, 34, 664, DateTimeKind.Utc).AddTicks(8267), new DateTime(2026, 4, 9, 20, 19, 34, 664, DateTimeKind.Utc).AddTicks(8267) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 20, 19, 34, 664, DateTimeKind.Utc).AddTicks(8271), new DateTime(2026, 4, 9, 20, 19, 34, 664, DateTimeKind.Utc).AddTicks(8271) });
        }
    }
}
