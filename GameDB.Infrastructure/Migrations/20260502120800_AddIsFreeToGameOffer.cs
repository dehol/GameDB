using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsFreeToGameOffer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add IsFree column to GameOffer
            migrationBuilder.AddColumn<bool>(
                name: "IsFree",
                table: "GameOffer",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // 2. Mark existing free games (CurrentPrice=0 AND already synced)
            migrationBuilder.Sql(@"
                UPDATE ""GameOffer"" SET ""IsFree"" = true
                WHERE ""CurrentPrice"" = 0 AND ""PriceSyncedAt"" IS NOT NULL;
            ");

            // 3. Fix trigger: write NEW price instead of OLD, skip if price=0 and not free
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION fn_auto_price_history()
                    RETURNS TRIGGER
                    LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF (OLD.""CurrentPrice"" IS DISTINCT FROM NEW.""CurrentPrice""
                        OR OLD.""CurrentDiscount"" IS DISTINCT FROM NEW.""CurrentDiscount"") THEN
                        -- Only record if the NEW price is meaningful (non-zero or explicitly free)
                        IF (NEW.""CurrentPrice"" > 0 OR NEW.""IsFree"" = true) THEN
                            INSERT INTO ""PriceHistory"" (""GameOfferId"", ""RecordedAt"", ""Price"", ""DiscountPercent"", ""Currency"")
                            VALUES (NEW.""GameOfferId"", NOW(), NEW.""CurrentPrice"", NEW.""CurrentDiscount"", NEW.""Currency"");
                        END IF;
                        NEW.""PriceSyncedAt"" = NOW();
                    END IF;
                    RETURN NEW;
                END;
                $$;
            ");

            // 4. Create dedup procedure for PriceHistory
            migrationBuilder.Sql(@"
                CREATE OR REPLACE PROCEDURE pr_dedup_price_history()
                    LANGUAGE plpgsql
                AS $$
                BEGIN
                    -- Delete consecutive duplicate price entries (same price + discount in a row)
                    DELETE FROM ""PriceHistory""
                    WHERE ""PriceHistoryId"" IN (
                        SELECT ph.""PriceHistoryId""
                        FROM (
                            SELECT ph.*,
                                   LAG(ph.""Price"") OVER (PARTITION BY ph.""GameOfferId"" ORDER BY ph.""RecordedAt"") AS prev_price,
                                   LAG(ph.""DiscountPercent"") OVER (PARTITION BY ph.""GameOfferId"" ORDER BY ph.""RecordedAt"") AS prev_discount
                            FROM ""PriceHistory"" ph
                        ) ph
                        WHERE ph.""Price"" = ph.prev_price
                          AND ph.""DiscountPercent"" = ph.prev_discount
                    );

                    -- Delete PriceHistory entries with Price = 0 (noise from initial import)
                    DELETE FROM ""PriceHistory"" WHERE ""Price"" = 0;
                END;
                $$;
            ");

            // 5. Clean up existing bad data
            migrationBuilder.Sql(@"CALL pr_dedup_price_history();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP PROCEDURE IF EXISTS pr_dedup_price_history();");

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

            migrationBuilder.DropColumn(
                name: "IsFree",
                table: "GameOffer");
        }
    }
}
