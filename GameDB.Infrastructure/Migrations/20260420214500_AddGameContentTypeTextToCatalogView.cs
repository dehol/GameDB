using Microsoft.EntityFrameworkCore.Migrations;

namespace GameDB.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddGameContentTypeTextToCatalogView : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ContentType",
            table: "Game",
            type: "text",
            nullable: true);

        migrationBuilder.Sql(@"
            UPDATE ""Game""
            SET ""ContentType"" = CASE
                WHEN COALESCE(""IsDlc"", false) THEN 'dlc_addon'
                ELSE 'main_game'
            END
            WHERE ""ContentType"" IS NULL;
        ");

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
            ),
            steam_covers AS (
                SELECT go.""GameId"",
                       'steam:' || go.""ExternalId"" AS steam_cover
                FROM ""GameOffer"" go
                WHERE go.""ShopId"" = 1
                  AND go.""ExternalId"" IS NOT NULL
                  AND go.""ExternalId"" ~ '^\d+$'
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
                COALESCE(g.""IsDlc"", false) AS is_dlc,
                COALESCE(g.""ContentType"", 'main_game') AS content_type,
                COALESCE(g.""CoverUrl"", sc.steam_cover) AS cover_source
            FROM ""Game"" g
            LEFT JOIN ""Developer"" d ON d.""DeveloperId"" = g.""DeveloperId""
            LEFT JOIN ""Publisher"" p ON p.""PublisherId"" = g.""PublisherId""
            LEFT JOIN genres_agg ga ON ga.""GameId"" = g.""GameId""
            LEFT JOIN offers_agg oa ON oa.""GameId"" = g.""GameId""
            LEFT JOIN steam_covers sc ON sc.""GameId"" = g.""GameId"";
        ");
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
            ),
            steam_covers AS (
                SELECT go.""GameId"",
                       'steam:' || go.""ExternalId"" AS steam_cover
                FROM ""GameOffer"" go
                WHERE go.""ShopId"" = 1
                  AND go.""ExternalId"" IS NOT NULL
                  AND go.""ExternalId"" ~ '^\d+$'
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
                COALESCE(g.""IsDlc"", false) AS is_dlc,
                COALESCE(g.""CoverUrl"", sc.steam_cover) AS cover_source
            FROM ""Game"" g
            LEFT JOIN ""Developer"" d ON d.""DeveloperId"" = g.""DeveloperId""
            LEFT JOIN ""Publisher"" p ON p.""PublisherId"" = g.""PublisherId""
            LEFT JOIN genres_agg ga ON ga.""GameId"" = g.""GameId""
            LEFT JOIN offers_agg oa ON oa.""GameId"" = g.""GameId""
            LEFT JOIN steam_covers sc ON sc.""GameId"" = g.""GameId"";
        ");

        migrationBuilder.DropColumn(
            name: "ContentType",
            table: "Game");
    }
}
