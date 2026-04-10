using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixAlertProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Recreate pr_set_alert procedure with correct ON CONFLICT syntax
            migrationBuilder.Sql(@"
                DROP PROCEDURE IF EXISTS pr_set_alert(INT, INT, NUMERIC, SMALLINT);
            ");

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
                        RAISE EXCEPTION 'Game is already in your library — alert not needed';
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4500), new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4501) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4506), new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4507) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4512), new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4512) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4518), new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4519) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4522), new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4523) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4527), new DateTime(2026, 4, 9, 15, 31, 17, 78, DateTimeKind.Utc).AddTicks(4527) });
        }
    }
}
