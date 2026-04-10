using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertsLibraryAndDbObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PriceHistory_GameOffer_OfferGameOfferId",
                table: "PriceHistory");

            migrationBuilder.DropCheckConstraint(
                name: "chk_source",
                table: "Wishlist");

            migrationBuilder.DropIndex(
                name: "IX_PriceHistory_ListingId_RecordedAt",
                table: "PriceHistory");

            migrationBuilder.DropIndex(
                name: "IX_PriceHistory_OfferGameOfferId",
                table: "PriceHistory");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Wishlist");

            migrationBuilder.DropColumn(
                name: "ListingId",
                table: "PriceHistory");

            migrationBuilder.RenameColumn(
                name: "OfferGameOfferId",
                table: "PriceHistory",
                newName: "GameOfferId");

            migrationBuilder.AddColumn<int>(
                name: "SourceShopId",
                table: "Wishlist",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Alert",
                columns: table => new
                {
                    AlertId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    GameId = table.Column<int>(type: "integer", nullable: false),
                    TargetPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    TargetDiscount = table.Column<short>(type: "smallint", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastNotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alert", x => x.AlertId);
                    table.CheckConstraint("chk_alert_condition", "\"TargetPrice\" IS NOT NULL OR \"TargetDiscount\" IS NOT NULL");
                    table.CheckConstraint("chk_alert_discount", "\"TargetDiscount\" IS NULL OR (\"TargetDiscount\" BETWEEN 1 AND 100)");
                    table.CheckConstraint("chk_alert_price", "\"TargetPrice\" IS NULL OR \"TargetPrice\" > 0");
                    table.ForeignKey(
                        name: "FK_Alert_Game_GameId",
                        column: x => x.GameId,
                        principalTable: "Game",
                        principalColumn: "GameId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Alert_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLibrary",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    GameId = table.Column<int>(type: "integer", nullable: false),
                    ShopId = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLibrary", x => new { x.UserId, x.GameId, x.ShopId });
                    table.ForeignKey(
                        name: "FK_UserLibrary_GameShop_ShopId",
                        column: x => x.ShopId,
                        principalTable: "GameShop",
                        principalColumn: "ShopId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserLibrary_Game_GameId",
                        column: x => x.GameId,
                        principalTable: "Game",
                        principalColumn: "GameId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserLibrary_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Wishlist_SourceShopId",
                table: "Wishlist",
                column: "SourceShopId");

            migrationBuilder.CreateIndex(
                name: "IX_User_Email",
                table: "User",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_Username",
                table: "User",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceHistory_GameOfferId_RecordedAt",
                table: "PriceHistory",
                columns: new[] { "GameOfferId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GameShop_Name",
                table: "GameShop",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Game_Title",
                table: "Game",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_Alert_GameId",
                table: "Alert",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Alert_UserId_GameId",
                table: "Alert",
                columns: new[] { "UserId", "GameId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLibrary_GameId",
                table: "UserLibrary",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibrary_ShopId",
                table: "UserLibrary",
                column: "ShopId");

            migrationBuilder.AddForeignKey(
                name: "FK_PriceHistory_GameOffer_GameOfferId",
                table: "PriceHistory",
                column: "GameOfferId",
                principalTable: "GameOffer",
                principalColumn: "GameOfferId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Wishlist_GameShop_SourceShopId",
                table: "Wishlist",
                column: "SourceShopId",
                principalTable: "GameShop",
                principalColumn: "ShopId",
                onDelete: ReferentialAction.SetNull);

            // ============================================================
            // Partial / sorted indexes (raw SQL  EF Core íå ï³äòðèìóº)
            // ============================================================
            migrationBuilder.Sql(@"
                CREATE INDEX ix_alert_user_active
                    ON ""Alert"" (""UserId"", ""IsActive"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX ix_alert_game_active
                    ON ""Alert"" (""GameId"")
                    WHERE ""IsActive"" = TRUE AND ""TriggeredAt"" IS NULL;
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX ix_user_library_user_added
                    ON ""UserLibrary"" (""UserId"", ""AddedAt"" DESC);
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX ix_wishlist_source_shop
                    ON ""Wishlist"" (""SourceShopId"")
                    WHERE ""SourceShopId"" IS NOT NULL;
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX ix_game_offer_synced_at
                    ON ""GameOffer"" (""PriceSyncedAt"");
            ");

            // ============================================================
            // Òðèãåðí³ ôóíêö³¿
            // ============================================================

            // 1. Àâòîìàòè÷íèé çàïèñ â PriceHistory ïðè çì³í³ ö³íè
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION fn_auto_price_history()
                    RETURNS TRIGGER
                    LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF (OLD.""CurrentPrice"" IS DISTINCT FROM NEW.""CurrentPrice""
                        OR OLD.""CurrentDiscount"" IS DISTINCT FROM NEW.""CurrentDiscount"") THEN
                        INSERT INTO ""PriceHistory"" (""GameOfferId"", ""RecordedAt"", ""Price"", ""DiscountPercent"", ""Currency"")
                        VALUES (OLD.""GameOfferId"", NOW(), OLD.""CurrentPrice"", OLD.""CurrentDiscount"", OLD.""Currency"");
                        NEW.""PriceSyncedAt"" = NOW();
                    END IF;
                    RETURN NEW;
                END;
                $$;
            ");
            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_auto_price_history
                    BEFORE UPDATE ON ""GameOffer""
                    FOR EACH ROW
                    EXECUTE FUNCTION fn_auto_price_history();
            ");

            // 2. Ïåðåâ³ðêà àëåðò³â ïðè çì³í³ ö³íè
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION fn_check_price_alerts()
                    RETURNS TRIGGER
                    LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF (NEW.""CurrentPrice"" = OLD.""CurrentPrice""
                        AND NEW.""CurrentDiscount"" = OLD.""CurrentDiscount"") THEN
                        RETURN NEW;
                    END IF;

                    UPDATE ""Alert""
                    SET ""TriggeredAt"" = NOW(),
                        ""IsActive"" = FALSE
                    WHERE ""GameId"" = NEW.""GameId""
                      AND ""IsActive"" = TRUE
                      AND ""TriggeredAt"" IS NULL
                      AND (
                          (""TargetPrice"" IS NOT NULL AND NEW.""CurrentPrice"" <= ""TargetPrice"")
                          OR
                          (""TargetDiscount"" IS NOT NULL AND NEW.""CurrentDiscount"" >= ""TargetDiscount"")
                      );

                    RETURN NEW;
                END;
                $$;
            ");
            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_check_price_alerts
                    AFTER UPDATE OF ""CurrentPrice"", ""CurrentDiscount"" ON ""GameOffer""
                    FOR EACH ROW
                    EXECUTE FUNCTION fn_check_price_alerts();
            ");

            // 3. Êîìá³íîâàíèé òðèãåð: âèäàëåííÿ ç wishlist + äåàêòèâàö³ÿ àëåðòó ïðè ïîêóïö³
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION fn_handle_library_insert()
                    RETURNS TRIGGER
                    LANGUAGE plpgsql
                AS $$
                BEGIN
                    DELETE FROM ""Wishlist""
                    WHERE ""UserId"" = NEW.""UserId""
                      AND ""GameId"" = NEW.""GameId"";

                    UPDATE ""Alert""
                    SET ""IsActive"" = FALSE
                    WHERE ""UserId"" = NEW.""UserId""
                      AND ""GameId"" = NEW.""GameId""
                      AND ""IsActive"" = TRUE;

                    RETURN NEW;
                END;
                $$;
            ");
            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_handle_library_insert
                    AFTER INSERT ON ""UserLibrary""
                    FOR EACH ROW
                    EXECUTE FUNCTION fn_handle_library_insert();
            ");

            // 4. Àâòîîíîâëåííÿ UpdatedAt ïðè çì³í³ ãðè
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION fn_update_game_timestamp()
                    RETURNS TRIGGER
                    LANGUAGE plpgsql
                AS $$
                BEGIN
                    NEW.""UpdatedAt"" = NOW();
                    RETURN NEW;
                END;
                $$;
            ");
            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_update_game_timestamp
                    BEFORE UPDATE ON ""Game""
                    FOR EACH ROW
                    EXECUTE FUNCTION fn_update_game_timestamp();
            ");

            // ============================================================
            // Çáåðåæåí³ ôóíêö³¿
            // ============================================================

            // fn_get_user_alerts  àëåðòè þçåðà ç ïîòî÷íèì ñòàòóñîì
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION fn_get_user_alerts(p_user_id INT)
                    RETURNS TABLE (
                        alert_id        INT,
                        game_title      TEXT,
                        target_price    NUMERIC,
                        target_discount SMALLINT,
                        best_current_price    NUMERIC,
                        best_current_discount SMALLINT,
                        price_gap_pct   NUMERIC,
                        is_active       BOOLEAN,
                        triggered_at    TIMESTAMPTZ,
                        created_at      TIMESTAMPTZ
                    )
                    LANGUAGE sql
                    STABLE
                AS $$
                    SELECT
                        a.""AlertId"",
                        g.""Title"",
                        a.""TargetPrice"",
                        a.""TargetDiscount"",
                        MIN(go.""CurrentPrice""),
                        MAX(go.""CurrentDiscount""),
                        CASE
                            WHEN a.""TargetPrice"" IS NOT NULL AND MIN(go.""CurrentPrice"") > 0
                            THEN ROUND(
                                (MIN(go.""CurrentPrice"") - a.""TargetPrice"") / a.""TargetPrice"" * 100, 1)
                            ELSE NULL
                        END,
                        a.""IsActive"",
                        a.""TriggeredAt"",
                        a.""CreatedAt""
                    FROM ""Alert"" a
                    JOIN ""Game"" g ON g.""GameId"" = a.""GameId""
                    LEFT JOIN ""GameOffer"" go ON go.""GameId"" = a.""GameId""
                    WHERE a.""UserId"" = p_user_id
                    GROUP BY a.""AlertId"", g.""Title"", a.""TargetPrice"", a.""TargetDiscount"",
                             a.""IsActive"", a.""TriggeredAt"", a.""CreatedAt""
                    ORDER BY a.""IsActive"" DESC, a.""CreatedAt"" DESC;
                $$;
            ");

            // fn_get_deal_score  îö³íêà ÿêîñò³ óãîäè 0-100
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION fn_get_deal_score(p_game_offer_id INT)
                    RETURNS TABLE (
                        listing_id       INT,
                        shop_name        TEXT,
                        current_price    NUMERIC,
                        current_discount SMALLINT,
                        historical_low   NUMERIC,
                        historical_avg   NUMERIC,
                        deal_score       SMALLINT,
                        is_historical_low BOOLEAN
                    )
                    LANGUAGE sql
                    STABLE
                AS $$
                    SELECT
                        go.""GameOfferId"",
                        gs.""Name"",
                        go.""CurrentPrice"",
                        go.""CurrentDiscount"",
                        COALESCE(MIN(ph.""Price""), go.""CurrentPrice""),
                        COALESCE(AVG(ph.""Price""), go.""CurrentPrice""),
                        CASE
                            WHEN COALESCE(MIN(ph.""Price""), go.""CurrentPrice"") =
                                 COALESCE(MAX(ph.""Price""), go.""CurrentPrice"")
                            THEN 0::SMALLINT
                            ELSE GREATEST(0, LEAST(100,
                                ROUND(
                                    (1 - (go.""CurrentPrice"" - MIN(ph.""Price"")) /
                                         NULLIF(MAX(ph.""Price"") - MIN(ph.""Price""), 0)
                                    ) * 100
                                )::INT
                            ))::SMALLINT
                        END,
                        go.""CurrentPrice"" <= COALESCE(MIN(ph.""Price""), go.""CurrentPrice"" + 1)
                    FROM ""GameOffer"" go
                    JOIN ""GameShop"" gs ON gs.""ShopId"" = go.""ShopId""
                    LEFT JOIN ""PriceHistory"" ph ON ph.""GameOfferId"" = go.""GameOfferId""
                    WHERE go.""GameOfferId"" = p_game_offer_id
                    GROUP BY go.""GameOfferId"", gs.""Name"", go.""CurrentPrice"", go.""CurrentDiscount"";
                $$;
            ");

            // ============================================================
            // Çáåðåæåí³ ïðîöåäóðè
            // ============================================================

            // pr_add_to_library  äîäàòè ãðó äî á³áë³îòåêè
            migrationBuilder.Sql(@"
                CREATE OR REPLACE PROCEDURE pr_add_to_library(
                    p_user_id   INT,
                    p_game_id   INT,
                    p_shop_id   INT
                )
                    LANGUAGE plpgsql
                AS $$
                DECLARE
                    v_game_title TEXT;
                    v_shop_name  TEXT;
                BEGIN
                    SELECT ""Title"" INTO v_game_title
                    FROM ""Game"" WHERE ""GameId"" = p_game_id;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Game with GameId=% not found', p_game_id;
                    END IF;

                    SELECT ""Name"" INTO v_shop_name
                    FROM ""GameShop"" WHERE ""ShopId"" = p_shop_id;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Shop with ShopId=% not found', p_shop_id;
                    END IF;

                    INSERT INTO ""UserLibrary"" (""UserId"", ""GameId"", ""ShopId"", ""AddedAt"")
                    VALUES (p_user_id, p_game_id, p_shop_id, NOW())
                    ON CONFLICT (""UserId"", ""GameId"", ""ShopId"") DO NOTHING;

                    IF FOUND THEN
                        RAISE NOTICE 'Game added to library';
                    ELSE
                        RAISE NOTICE 'Game already in library';
                    END IF;
                END;
                $$;
            ");

            // pr_set_alert  ñòâîðèòè àáî îíîâèòè àëåðò
            migrationBuilder.Sql(@"
                CREATE OR REPLACE PROCEDURE pr_set_alert(
                    p_user_id       INT,
                    p_game_id       INT,
                    p_target_price  NUMERIC DEFAULT NULL,
                    p_target_discount SMALLINT DEFAULT NULL
                )
                    LANGUAGE plpgsql
                AS $$
                DECLARE
                    v_in_library BOOLEAN;
                BEGIN
                    IF p_target_price IS NULL AND p_target_discount IS NULL THEN
                        RAISE EXCEPTION 'Must specify target_price or target_discount';
                    END IF;

                    SELECT EXISTS(
                        SELECT 1 FROM ""UserLibrary""
                        WHERE ""UserId"" = p_user_id AND ""GameId"" = p_game_id
                    ) INTO v_in_library;

                    IF v_in_library THEN
                        RAISE EXCEPTION 'Game is already in your library  alert not needed';
                    END IF;

                    INSERT INTO ""Alert""
                        (""UserId"", ""GameId"", ""TargetPrice"", ""TargetDiscount"", ""IsActive"", ""TriggeredAt"", ""CreatedAt"")
                    VALUES
                        (p_user_id, p_game_id, p_target_price, p_target_discount, TRUE, NULL, NOW())
                    ON CONFLICT (""UserId"", ""GameId"")
                    DO UPDATE SET
                        ""TargetPrice""    = EXCLUDED.""TargetPrice"",
                        ""TargetDiscount"" = EXCLUDED.""TargetDiscount"",
                        ""IsActive""       = TRUE,
                        ""TriggeredAt""    = NULL,
                        ""CreatedAt""      = NOW();
                END;
                $$;
            ");

            // ============================================================
            // Views
            // ============================================================

            // vw_game_catalog  êàòàëîã ³ãîð ç CTE äëÿ ïðàâèëüíèõ àãðåãàò³â
            migrationBuilder.Sql(@"
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
                    g.""UpdatedAt""
                FROM ""Game"" g
                LEFT JOIN ""Developer"" d ON d.""DeveloperId"" = g.""DeveloperId""
                LEFT JOIN ""Publisher"" p ON p.""PublisherId"" = g.""PublisherId""
                LEFT JOIN genres_agg ga ON ga.""GameId"" = g.""GameId""
                LEFT JOIN offers_agg oa ON oa.""GameId"" = g.""GameId"";
            ");

            // vw_user_library  á³áë³îòåêà þçåðà
            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW vw_user_library AS
                SELECT
                    ul.""UserId"",
                    ul.""GameId"",
                    g.""Title"" AS game_title,
                    g.""ReleaseDate"",
                    d.""Name"" AS developer_name,
                    gs.""Name"" AS shop_name,
                    gs.""BaseUrl"" AS shop_url,
                    go.""DownloadUrl"",
                    go.""CurrentPrice"" AS purchase_store_price,
                    ul.""AddedAt""
                FROM ""UserLibrary"" ul
                JOIN ""Game"" g ON g.""GameId"" = ul.""GameId""
                JOIN ""GameShop"" gs ON gs.""ShopId"" = ul.""ShopId""
                LEFT JOIN ""Developer"" d ON d.""DeveloperId"" = g.""DeveloperId""
                LEFT JOIN ""GameOffer"" go ON go.""GameId"" = ul.""GameId""
                                          AND go.""ShopId"" = ul.""ShopId"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop raw SQL objects in reverse dependency order
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_auto_price_history ON ""GameOffer"";");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_check_price_alerts ON ""GameOffer"";");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_handle_library_insert ON ""UserLibrary"";");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_update_game_timestamp ON ""Game"";");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS fn_auto_price_history();");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS fn_check_price_alerts();");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS fn_handle_library_insert();");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS fn_update_game_timestamp();");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS fn_get_user_alerts(INT);");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS fn_get_deal_score(INT);");
            migrationBuilder.Sql(@"DROP PROCEDURE IF EXISTS pr_add_to_library(INT, INT, INT);");
            migrationBuilder.Sql(@"DROP PROCEDURE IF EXISTS pr_set_alert(INT, INT, NUMERIC, SMALLINT);");
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS vw_game_catalog;");
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS vw_user_library;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_alert_user_active;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_alert_game_active;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_user_library_user_added;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_wishlist_source_shop;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_game_offer_synced_at;");

            migrationBuilder.DropForeignKey(
                name: "FK_PriceHistory_GameOffer_GameOfferId",
                table: "PriceHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_Wishlist_GameShop_SourceShopId",
                table: "Wishlist");

            migrationBuilder.DropTable(
                name: "Alert");

            migrationBuilder.DropTable(
                name: "UserLibrary");

            migrationBuilder.DropIndex(
                name: "IX_Wishlist_SourceShopId",
                table: "Wishlist");

            migrationBuilder.DropIndex(
                name: "IX_User_Email",
                table: "User");

            migrationBuilder.DropIndex(
                name: "IX_User_Username",
                table: "User");

            migrationBuilder.DropIndex(
                name: "IX_PriceHistory_GameOfferId_RecordedAt",
                table: "PriceHistory");

            migrationBuilder.DropIndex(
                name: "IX_GameShop_Name",
                table: "GameShop");

            migrationBuilder.DropIndex(
                name: "IX_Game_Title",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "SourceShopId",
                table: "Wishlist");

            migrationBuilder.RenameColumn(
                name: "GameOfferId",
                table: "PriceHistory",
                newName: "OfferGameOfferId");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Wishlist",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ListingId",
                table: "PriceHistory",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "chk_source",
                table: "Wishlist",
                sql: "\"Source\" IN ('manual', 'shop_import')");

            migrationBuilder.CreateIndex(
                name: "IX_PriceHistory_ListingId_RecordedAt",
                table: "PriceHistory",
                columns: new[] { "ListingId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceHistory_OfferGameOfferId",
                table: "PriceHistory",
                column: "OfferGameOfferId");

            migrationBuilder.AddForeignKey(
                name: "FK_PriceHistory_GameOffer_OfferGameOfferId",
                table: "PriceHistory",
                column: "OfferGameOfferId",
                principalTable: "GameOffer",
                principalColumn: "GameOfferId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
