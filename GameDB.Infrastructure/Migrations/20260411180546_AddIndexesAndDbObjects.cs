using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexesAndDbObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ========================
            // Schema Changes
            // ========================
            
            migrationBuilder.AddColumn<int>(
                name: "SteamAppId",
                table: "RawGameData",
                type: "integer",
                nullable: true);

            // Convert Status from text to integer enum
            // Using CASE to map string values to enum integers
            migrationBuilder.Sql(@"
                ALTER TABLE ""ImportJob"" 
                ALTER COLUMN ""Status"" TYPE integer 
                USING CASE 
                    WHEN ""Status"" = 'pending' THEN 0
                    WHEN ""Status"" = 'running' THEN 1
                    WHEN ""Status"" = 'completed' THEN 2
                    WHEN ""Status"" = 'failed' THEN 3
                    WHEN ""Status"" = 'cancelled' THEN 4
                    ELSE 0
                END;
            ");

            migrationBuilder.AddColumn<int>(
                name: "RawgId",
                table: "Game",
                type: "integer",
                nullable: true);

            // ========================
            // Indexes - Game Table
            // ========================
            
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_Game_Title ON ""Game"" (""Title"");
            ");
            
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_Game_NormalizedTitle 
                    ON ""Game"" (""NormalizedTitle"") 
                    WHERE ""NormalizedTitle"" IS NOT NULL;
            ");
            
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_Game_RawgId 
                    ON ""Game"" (""RawgId"") 
                    WHERE ""RawgId"" IS NOT NULL;
            ");
            
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_Game_Catalog 
                    ON ""Game"" (""UpdatedAt"" DESC, ""ReleaseDate"" DESC) 
                    WHERE ""NormalizedTitle"" IS NOT NULL;
            ");

            // ========================
            // Indexes - GameOffer Table
            // ========================
            
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_GameOffer_Sync 
                    ON ""GameOffer"" (""ShopId"", ""ExternalId"") 
                    INCLUDE (""CurrentPrice"", ""CurrentDiscount"", ""PriceSyncedAt"");
            ");
            
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_GameOffer_GamePrice 
                    ON ""GameOffer"" (""GameId"") 
                    INCLUDE (""CurrentPrice"", ""CurrentDiscount"", ""ShopId"");
            ");

            // ========================
            // Indexes - PriceHistory Table
            // ========================
            
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_PriceHistory_OfferDate 
                    ON ""PriceHistory"" (""GameOfferId"", ""RecordedAt"" DESC);
            ");
            
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_PriceHistory_Date 
                    ON ""PriceHistory"" (""RecordedAt"" DESC);
            ");

            // ========================
            // Indexes - GameGenre Table
            // ========================
            
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_GameGenre_Genre 
                    ON ""GameGenre"" (""GenreId"", ""GameId"");
            ");
            
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_GameGenre_Game 
                    ON ""GameGenre"" (""GameId"", ""GenreId"");
            ");

            // ========================
            // Indexes - Alert Table
            // ========================
            
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_Alert_User 
                    ON ""Alert"" (""UserId"", ""IsActive"");
            ");
            
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_Alert_ActiveGame
                    ON ""Alert"" (""GameId"")
                    WHERE ""IsActive"" = TRUE AND ""TriggeredAt"" IS NULL;
            ");

            // ========================
            // Indexes - Wishlist Table
            // ========================
            
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_Wishlist_User 
                    ON ""Wishlist"" (""UserId"", ""AddedAt"" DESC);
            ");

            // ========================
            // Indexes - UserLibrary Table
            // ========================
            
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_UserLibrary_User 
                    ON ""UserLibrary"" (""UserId"", ""AddedAt"" DESC);
            ");

            // ========================
            // Indexes - ImportJob Table
            // ========================
            
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_ImportJob_Status 
                    ON ""ImportJob"" (""Status"", ""StartedAt"" DESC);
            ");
            
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_ImportJob_History 
                    ON ""ImportJob"" (""StartedAt"" DESC);
            ");

            // ========================
            // Views
            // ========================

            // vw_user_alerts - User alerts with current best prices
            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW vw_user_alerts AS
                SELECT 
                    a.""AlertId"" AS alert_id,
                    a.""UserId"",
                    g.""GameId"",
                    g.""Title"" AS game_title,
                    a.""TargetPrice"" AS target_price,
                    a.""TargetDiscount"" AS target_discount,
                    MIN(o.""CurrentPrice"") AS best_current_price,
                    MAX(o.""CurrentDiscount"") AS best_current_discount,
                    CASE 
                        WHEN a.""TargetPrice"" IS NOT NULL AND MIN(o.""CurrentPrice"") IS NOT NULL
                        THEN ROUND(((MIN(o.""CurrentPrice"") - a.""TargetPrice"") / a.""TargetPrice"" * 100)::numeric, 1)
                        ELSE NULL
                    END AS price_gap_pct,
                    a.""IsActive"" AS is_active,
                    a.""TriggeredAt"" AS triggered_at,
                    a.""CreatedAt"" AS created_at
                FROM ""Alert"" a
                JOIN ""Game"" g ON a.""GameId"" = g.""GameId""
                LEFT JOIN ""GameOffer"" o ON g.""GameId"" = o.""GameId""
                GROUP BY a.""AlertId"", a.""UserId"", g.""GameId"", g.""Title"", 
                         a.""TargetPrice"", a.""TargetDiscount"", a.""IsActive"", 
                         a.""TriggeredAt"", a.""CreatedAt"";
            ");

            // vw_price_trends - Price trends for analytics
            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW vw_price_trends AS
                SELECT 
                    g.""GameId"",
                    g.""Title"",
                    o.""ShopId"",
                    s.""Name"" AS shop_name,
                    o.""CurrentPrice"",
                    o.""CurrentDiscount"",
                    (SELECT AVG(""Price"") FROM ""PriceHistory"" ph 
                     WHERE ph.""GameOfferId"" = o.""GameOfferId"" 
                     AND ph.""RecordedAt"" > NOW() - INTERVAL '7 days') AS avg_7d,
                    (SELECT AVG(""Price"") FROM ""PriceHistory"" ph 
                     WHERE ph.""GameOfferId"" = o.""GameOfferId"" 
                     AND ph.""RecordedAt"" > NOW() - INTERVAL '30 days') AS avg_30d,
                    (SELECT MIN(""Price"") FROM ""PriceHistory"" ph 
                     WHERE ph.""GameOfferId"" = o.""GameOfferId"") AS all_time_low,
                    (SELECT ""RecordedAt"" FROM ""PriceHistory"" ph 
                     WHERE ph.""GameOfferId"" = o.""GameOfferId"" 
                     ORDER BY ph.""RecordedAt"" DESC LIMIT 1) AS last_price_change
                FROM ""Game"" g
                JOIN ""GameOffer"" o ON g.""GameId"" = o.""GameId""
                JOIN ""GameShop"" s ON o.""ShopId"" = s.""ShopId"";
            ");

            // vw_import_statistics - Import job statistics
            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW vw_import_statistics AS
                SELECT 
                    DATE_TRUNC('day', ""StartedAt"") AS import_date,
                    COUNT(*) AS total_jobs,
                    COUNT(*) FILTER (WHERE ""Status"" = 2) AS successful_jobs,
                    COUNT(*) FILTER (WHERE ""Status"" = 3) AS failed_jobs,
                    AVG(""TotalGamesCreated"") AS avg_games_created,
                    SUM(""TotalGamesCreated"") AS total_games_created,
                    SUM(""TotalOffersCreated"") AS total_offers_created,
                    AVG(EXTRACT(EPOCH FROM (""CompletedAt"" - ""StartedAt""))) AS avg_duration_seconds,
                    SUM(""ErrorCount"") AS total_errors
                FROM ""ImportJob""
                GROUP BY DATE_TRUNC('day', ""StartedAt"")
                ORDER BY import_date DESC;
            ");

            // vw_popular_games - Popular games ranking
            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW vw_popular_games AS
                SELECT 
                    g.""GameId"",
                    g.""Title"",
                    g.""ReleaseDate"",
                    (SELECT COUNT(*) FROM ""Wishlist"" w WHERE w.""GameId"" = g.""GameId"") AS wishlist_count,
                    (SELECT COUNT(*) FROM ""UserLibrary"" ul WHERE ul.""GameId"" = g.""GameId"") AS library_count,
                    (SELECT COUNT(*) FROM ""Alert"" a WHERE a.""GameId"" = g.""GameId"" AND a.""IsActive"") AS active_alerts,
                    MIN(o.""CurrentPrice"") AS best_price,
                    MAX(o.""CurrentDiscount"") AS max_discount,
                    (
                        (SELECT COUNT(*) FROM ""Wishlist"" w WHERE w.""GameId"" = g.""GameId"") * 2 +
                        (SELECT COUNT(*) FROM ""UserLibrary"" ul WHERE ul.""GameId"" = g.""GameId"") * 3 +
                        (SELECT COUNT(*) FROM ""Alert"" a WHERE a.""GameId"" = g.""GameId"" AND a.""IsActive"")
                    ) AS popularity_score
                FROM ""Game"" g
                LEFT JOIN ""GameOffer"" o ON g.""GameId"" = o.""GameId""
                GROUP BY g.""GameId"", g.""Title"", g.""ReleaseDate""
                ORDER BY popularity_score DESC;
            ");

            // ========================
            // Functions
            // ========================

            // fn_get_game_deal_scores - Get deal scores for all offers of a game (fixes N+1)
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION fn_get_game_deal_scores(p_game_id INT)
                RETURNS TABLE (
                    game_offer_id INT,
                    shop_name TEXT,
                    current_price NUMERIC,
                    current_discount SMALLINT,
                    historical_low NUMERIC,
                    historical_avg NUMERIC,
                    deal_score SMALLINT,
                    is_historical_low BOOLEAN
                ) AS $$
                BEGIN
                    RETURN QUERY
                    SELECT 
                        o.""GameOfferId""::INT,
                        s.""Name""::TEXT,
                        o.""CurrentPrice""::NUMERIC,
                        o.""CurrentDiscount""::SMALLINT,
                        COALESCE(
                            (SELECT MIN(""Price"") FROM ""PriceHistory"" ph 
                             WHERE ph.""GameOfferId"" = o.""GameOfferId""), 
                            o.""CurrentPrice""
                        )::NUMERIC AS historical_low,
                        COALESCE(
                            (SELECT AVG(""Price"") FROM ""PriceHistory"" ph 
                             WHERE ph.""GameOfferId"" = o.""GameOfferId"" 
                             AND ph.""RecordedAt"" > NOW() - INTERVAL '180 days'),
                            o.""CurrentPrice""
                        )::NUMERIC AS historical_avg,
                        CASE 
                            WHEN o.""CurrentPrice"" = 0 THEN 0::SMALLINT
                            ELSE LEAST(100, GREATEST(0, 
                                ROUND(((1 - o.""CurrentPrice"" / NULLIF(
                                    (SELECT MAX(""Price"") FROM ""PriceHistory"" ph 
                                     WHERE ph.""GameOfferId"" = o.""GameOfferId"" 
                                     AND ph.""RecordedAt"" > NOW() - INTERVAL '180 days'),
                                    o.""CurrentPrice""
                                )) * 100))::SMALLINT
                            ))
                        END AS deal_score,
                        (o.""CurrentPrice"" <= (
                            SELECT MIN(""Price"") FROM ""PriceHistory"" ph 
                            WHERE ph.""GameOfferId"" = o.""GameOfferId""
                        ))::BOOLEAN AS is_historical_low
                    FROM ""GameOffer"" o
                    JOIN ""GameShop"" s ON o.""ShopId"" = s.""ShopId""
                    WHERE o.""GameId"" = p_game_id;
                END;
                $$ LANGUAGE plpgsql STABLE;
            ");

            // fn_toggle_wishlist - Atomic toggle operation
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION fn_toggle_wishlist(
                    p_user_id INT,
                    p_game_id INT
                ) RETURNS BOOLEAN AS $$
                DECLARE
                    v_exists BOOLEAN;
                BEGIN
                    SELECT EXISTS(
                        SELECT 1 FROM ""Wishlist"" 
                        WHERE ""UserId"" = p_user_id AND ""GameId"" = p_game_id
                    ) INTO v_exists;
                    
                    IF v_exists THEN
                        DELETE FROM ""Wishlist"" 
                        WHERE ""UserId"" = p_user_id AND ""GameId"" = p_game_id;
                        RETURN FALSE;
                    ELSE
                        INSERT INTO ""Wishlist"" (""UserId"", ""GameId"", ""AddedAt"")
                        VALUES (p_user_id, p_game_id, NOW());
                        RETURN TRUE;
                    END IF;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // ========================
            // Update seed data
            // ========================
            
            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "RawgId", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6210), null, new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6210) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "RawgId", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6215), null, new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6216) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "RawgId", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6220), null, new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6221) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "RawgId", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6226), null, new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6226) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "RawgId", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6230), null, new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6230) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "RawgId", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6274), null, new DateTime(2026, 4, 11, 18, 5, 45, 425, DateTimeKind.Utc).AddTicks(6274) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop functions
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS fn_get_game_deal_scores(INT);");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS fn_toggle_wishlist(INT, INT);");
            
            // Drop views
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS vw_user_alerts;");
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS vw_price_trends;");
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS vw_import_statistics;");
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS vw_popular_games;");
            
            // Drop indexes
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_Game_Title;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_Game_NormalizedTitle;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_Game_RawgId;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_Game_Catalog;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_GameOffer_Sync;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_GameOffer_GamePrice;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_PriceHistory_OfferDate;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_PriceHistory_Date;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_GameGenre_Genre;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_GameGenre_Game;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_Alert_User;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_Alert_ActiveGame;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_Wishlist_User;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_UserLibrary_User;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_ImportJob_Status;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_ImportJob_History;");

            // Schema changes
            migrationBuilder.DropColumn(
                name: "SteamAppId",
                table: "RawGameData");

            migrationBuilder.DropColumn(
                name: "RawgId",
                table: "Game");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ImportJob",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

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
        }
    }
}
