using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCatalogViewWithCoverUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP VIEW IF EXISTS vw_game_catalog;
                
                CREATE OR REPLACE VIEW vw_game_catalog AS
                WITH genres_agg AS (
                    SELECT gg.""GameId"",
                           STRING_AGG(DISTINCT gn.""Name"", ', ' ORDER BY gn.""Name"") AS genres
                    FROM ""GameGenre"" gg
                    JOIN ""Genre"" gn ON gn.""GenreId"" = gg.""GenreId""
                    GROUP BY gg.""GameId""
                ),
                offers_agg AS (
                    SELECT go.""GameId"",
                           MIN(go.""CurrentPrice"") AS min_price,
                           MAX(go.""CurrentDiscount"") AS max_discount,
                           COUNT(DISTINCT go.""ShopId"") AS available_in_shops
                    FROM ""GameOffer"" go
                    GROUP BY go.""GameId""
                )
                SELECT
                    g.""GameId"",
                    g.""Title"",
                    g.""Description"",
                    g.""ReleaseDate"",
                    d.""Name"" AS developer_name,
                    p.""Name"" AS publisher_name,
                    ga.genres,
                    oa.min_price,
                    oa.max_discount,
                    oa.available_in_shops,
                    g.""CreatedAt"",
                    g.""UpdatedAt"",
                    g.""Rating"" AS rating,
                    g.""RatingCount"" AS rating_count,
                    g.""CoverUrl"" AS cover_url,
                    COALESCE(g.""IsDlc"", false) AS is_dlc
                FROM ""Game"" g
                LEFT JOIN ""Developer"" d ON d.""DeveloperId"" = g.""DeveloperId""
                LEFT JOIN ""Publisher"" p ON p.""PublisherId"" = g.""PublisherId""
                LEFT JOIN genres_agg ga ON ga.""GameId"" = g.""GameId""
                LEFT JOIN offers_agg oa ON oa.""GameId"" = g.""GameId"";
            ");

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 19, 20, 33, 43, 229, DateTimeKind.Utc).AddTicks(1083), new DateTime(2026, 4, 19, 20, 33, 43, 229, DateTimeKind.Utc).AddTicks(1083) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 19, 20, 33, 43, 229, DateTimeKind.Utc).AddTicks(1089), new DateTime(2026, 4, 19, 20, 33, 43, 229, DateTimeKind.Utc).AddTicks(1089) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 19, 20, 33, 43, 229, DateTimeKind.Utc).AddTicks(1094), new DateTime(2026, 4, 19, 20, 33, 43, 229, DateTimeKind.Utc).AddTicks(1095) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 19, 20, 33, 43, 229, DateTimeKind.Utc).AddTicks(1099), new DateTime(2026, 4, 19, 20, 33, 43, 229, DateTimeKind.Utc).AddTicks(1100) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 19, 20, 33, 43, 229, DateTimeKind.Utc).AddTicks(1103), new DateTime(2026, 4, 19, 20, 33, 43, 229, DateTimeKind.Utc).AddTicks(1104) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 19, 20, 33, 43, 229, DateTimeKind.Utc).AddTicks(1107), new DateTime(2026, 4, 19, 20, 33, 43, 229, DateTimeKind.Utc).AddTicks(1108) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP VIEW IF EXISTS vw_game_catalog;
                
                CREATE OR REPLACE VIEW vw_game_catalog AS
                WITH genres_agg AS (
                    SELECT gg.""GameId"",
                           STRING_AGG(DISTINCT gn.""Name"", ', ' ORDER BY gn.""Name"") AS genres
                    FROM ""GameGenre"" gg
                    JOIN ""Genre"" gn ON gn.""GenreId"" = gg.""GenreId""
                    GROUP BY gg.""GameId""
                ),
                offers_agg AS (
                    SELECT go.""GameId"",
                           MIN(go.""CurrentPrice"") AS min_price,
                           MAX(go.""CurrentDiscount"") AS max_discount,
                           COUNT(DISTINCT go.""ShopId"") AS available_in_shops
                    FROM ""GameOffer"" go
                    GROUP BY go.""GameId""
                )
                SELECT
                    g.""GameId"",
                    g.""Title"",
                    g.""Description"",
                    g.""ReleaseDate"",
                    d.""Name"" AS developer_name,
                    p.""Name"" AS publisher_name,
                    ga.genres,
                    oa.min_price,
                    oa.max_discount,
                    oa.available_in_shops,
                    g.""CreatedAt"",
                    g.""UpdatedAt"",
                    g.""Rating"" AS rating,
                    g.""RatingCount"" AS rating_count,
                    COALESCE(g.""IsDlc"", false) AS is_dlc
                FROM ""Game"" g
                LEFT JOIN ""Developer"" d ON d.""DeveloperId"" = g.""DeveloperId""
                LEFT JOIN ""Publisher"" p ON p.""PublisherId"" = g.""PublisherId""
                LEFT JOIN genres_agg ga ON ga.""GameId"" = g.""GameId""
                LEFT JOIN offers_agg oa ON oa.""GameId"" = g.""GameId"";
            ");

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6579), new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6579) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6587), new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6588) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6593), new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6593) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6598), new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6599) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6603), new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6603) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6607), new DateTime(2026, 4, 19, 20, 32, 36, 542, DateTimeKind.Utc).AddTicks(6608) });
        }
    }
}
