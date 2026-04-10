using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedGamesAndGenres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Developer",
                columns: new[] { "DeveloperId", "Name" },
                values: new object[,]
                {
                    { 1, "Larian Studios" },
                    { 2, "CD Projekt Red" },
                    { 3, "FromSoftware" },
                    { 4, "Supergiant Games" },
                    { 5, "Valve" }
                });

            migrationBuilder.InsertData(
                table: "Genre",
                columns: new[] { "GenreId", "Name" },
                values: new object[,]
                {
                    { 1, "RPG" },
                    { 2, "Action" },
                    { 3, "Adventure" },
                    { 4, "Roguelike" },
                    { 5, "Open World" },
                    { 6, "FPS" },
                    { 7, "Indie" },
                    { 8, "Strategy" }
                });

            migrationBuilder.InsertData(
                table: "Publisher",
                columns: new[] { "PublisherId", "Name" },
                values: new object[,]
                {
                    { 1, "Larian Studios" },
                    { 2, "CD Projekt" },
                    { 3, "Bandai Namco" },
                    { 4, "Supergiant Games" },
                    { 5, "Valve" }
                });

            migrationBuilder.InsertData(
                table: "Game",
                columns: new[] { "GameId", "CreatedAt", "Description", "DeveloperId", "PublisherId", "ReleaseDate", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4500), "An epic RPG set in the D&D universe", 1, 1, new DateOnly(2023, 8, 3), "Baldur's Gate 3", new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4501) },
                    { 2, new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4506), "Open-world action RPG in a dystopian future", 2, 2, new DateOnly(2020, 12, 10), "Cyberpunk 2077", new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4507) },
                    { 3, new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4512), "Open-world action RPG by FromSoftware and George R.R. Martin", 3, 3, new DateOnly(2022, 2, 25), "Elden Ring", new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4512) },
                    { 4, new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4518), "Roguelike action sequel from Supergiant Games", 4, 4, new DateOnly(2024, 5, 6), "Hades II", new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4519) },
                    { 5, new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4522), "Competitive tactical FPS", 5, 5, new DateOnly(2023, 9, 27), "Counter-Strike 2", new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4523) },
                    { 6, new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4527), "Story-driven open world RPG", 2, 2, new DateOnly(2015, 5, 19), "The Witcher 3: Wild Hunt", new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4527) }
                });

            migrationBuilder.InsertData(
                table: "GameGenre",
                columns: new[] { "GameId", "GenreId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 1, 3 },
                    { 2, 1 },
                    { 2, 2 },
                    { 2, 5 },
                    { 3, 1 },
                    { 3, 2 },
                    { 3, 5 },
                    { 4, 2 },
                    { 4, 4 },
                    { 4, 7 },
                    { 5, 2 },
                    { 5, 6 },
                    { 6, 1 },
                    { 6, 3 },
                    { 6, 5 }
                });

            migrationBuilder.InsertData(
                table: "GameOffer",
                columns: new[] { "GameOfferId", "Currency", "CurrentDiscount", "CurrentPrice", "DownloadUrl", "ExternalId", "GameId", "PriceSyncedAt", "ShopId" },
                values: new object[,]
                {
                    { 1, "USD", (short)0, 59.99m, null, "1086940", 1, null, 1 },
                    { 2, "USD", (short)0, 59.99m, null, "1086940", 1, null, 2 },
                    { 3, "USD", (short)50, 29.99m, null, "1091500", 2, null, 1 },
                    { 4, "USD", (short)50, 29.99m, null, "1423049", 2, null, 2 },
                    { 5, "USD", (short)0, 59.99m, null, "1245620", 3, null, 1 },
                    { 6, "USD", (short)0, 29.99m, null, "1145350", 4, null, 1 },
                    { 7, "USD", (short)0, 0.00m, null, "730", 5, null, 1 },
                    { 8, "USD", (short)75, 9.99m, null, "292030", 6, null, 1 },
                    { 9, "USD", (short)75, 9.99m, null, "1495134320", 6, null, 2 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "GameGenre",
                keyColumns: new[] { "GameId", "GenreId" },
                keyValues: new object[] { 1, 1 });

            migrationBuilder.DeleteData(
                table: "GameGenre",
                keyColumns: new[] { "GameId", "GenreId" },
                keyValues: new object[] { 1, 3 });

            migrationBuilder.DeleteData(
                table: "GameGenre",
                keyColumns: new[] { "GameId", "GenreId" },
                keyValues: new object[] { 2, 1 });

            migrationBuilder.DeleteData(
                table: "GameGenre",
                keyColumns: new[] { "GameId", "GenreId" },
                keyValues: new object[] { 2, 2 });

            migrationBuilder.DeleteData(
                table: "GameGenre",
                keyColumns: new[] { "GameId", "GenreId" },
                keyValues: new object[] { 2, 5 });

            migrationBuilder.DeleteData(
                table: "GameGenre",
                keyColumns: new[] { "GameId", "GenreId" },
                keyValues: new object[] { 3, 1 });

            migrationBuilder.DeleteData(
                table: "GameGenre",
                keyColumns: new[] { "GameId", "GenreId" },
                keyValues: new object[] { 3, 2 });

            migrationBuilder.DeleteData(
                table: "GameGenre",
                keyColumns: new[] { "GameId", "GenreId" },
                keyValues: new object[] { 3, 5 });

            migrationBuilder.DeleteData(
                table: "GameGenre",
                keyColumns: new[] { "GameId", "GenreId" },
                keyValues: new object[] { 4, 2 });

            migrationBuilder.DeleteData(
                table: "GameGenre",
                keyColumns: new[] { "GameId", "GenreId" },
                keyValues: new object[] { 4, 4 });

            migrationBuilder.DeleteData(
                table: "GameGenre",
                keyColumns: new[] { "GameId", "GenreId" },
                keyValues: new object[] { 4, 7 });

            migrationBuilder.DeleteData(
                table: "GameGenre",
                keyColumns: new[] { "GameId", "GenreId" },
                keyValues: new object[] { 5, 2 });

            migrationBuilder.DeleteData(
                table: "GameGenre",
                keyColumns: new[] { "GameId", "GenreId" },
                keyValues: new object[] { 5, 6 });

            migrationBuilder.DeleteData(
                table: "GameGenre",
                keyColumns: new[] { "GameId", "GenreId" },
                keyValues: new object[] { 6, 1 });

            migrationBuilder.DeleteData(
                table: "GameGenre",
                keyColumns: new[] { "GameId", "GenreId" },
                keyValues: new object[] { 6, 3 });

            migrationBuilder.DeleteData(
                table: "GameGenre",
                keyColumns: new[] { "GameId", "GenreId" },
                keyValues: new object[] { 6, 5 });

            migrationBuilder.DeleteData(
                table: "GameOffer",
                keyColumn: "GameOfferId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "GameOffer",
                keyColumn: "GameOfferId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "GameOffer",
                keyColumn: "GameOfferId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "GameOffer",
                keyColumn: "GameOfferId",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "GameOffer",
                keyColumn: "GameOfferId",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "GameOffer",
                keyColumn: "GameOfferId",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "GameOffer",
                keyColumn: "GameOfferId",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "GameOffer",
                keyColumn: "GameOfferId",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "GameOffer",
                keyColumn: "GameOfferId",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Genre",
                keyColumn: "GenreId",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Genre",
                keyColumn: "GenreId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Genre",
                keyColumn: "GenreId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Genre",
                keyColumn: "GenreId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Genre",
                keyColumn: "GenreId",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Genre",
                keyColumn: "GenreId",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Genre",
                keyColumn: "GenreId",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Genre",
                keyColumn: "GenreId",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Developer",
                keyColumn: "DeveloperId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Developer",
                keyColumn: "DeveloperId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Developer",
                keyColumn: "DeveloperId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Developer",
                keyColumn: "DeveloperId",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Developer",
                keyColumn: "DeveloperId",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Publisher",
                keyColumn: "PublisherId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Publisher",
                keyColumn: "PublisherId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Publisher",
                keyColumn: "PublisherId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Publisher",
                keyColumn: "PublisherId",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Publisher",
                keyColumn: "PublisherId",
                keyValue: 5);
        }
    }
}
