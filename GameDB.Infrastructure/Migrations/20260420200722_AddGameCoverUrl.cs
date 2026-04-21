using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameCoverUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: only add if column doesn't exist
            migrationBuilder.Sql(
                @"DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Game' AND column_name = 'CoverUrl'
                    ) THEN
                        ALTER TABLE ""Game"" ADD ""CoverUrl"" text NULL;
                    END IF;
                END $$;");

            // Clear stale cover URLs from previous code that stored full Steam CDN URLs
            // New code uses "steam:{appId}" format or IGDB cover URLs
            migrationBuilder.Sql(
                @"UPDATE ""Game"" SET ""CoverUrl"" = NULL WHERE ""CoverUrl"" LIKE 'https://cdn.cloudflare.steamstatic.com/%' OR ""CoverUrl"" LIKE 'https://shared.cloudflare.steamstatic.com/%'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverUrl",
                table: "Game");
        }
    }
}
