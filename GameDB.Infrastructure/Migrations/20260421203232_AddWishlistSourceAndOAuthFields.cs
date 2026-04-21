using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWishlistSourceAndOAuthFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add OAuth token columns to UserShopProfile
            migrationBuilder.AddColumn<string>(
                name: "AccessToken",
                table: "UserShopProfile",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefreshToken",
                table: "UserShopProfile",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TokenExpiresAt",
                table: "UserShopProfile",
                type: "timestamp with time zone",
                nullable: true);

            // Step 2: Create WishlistSource table (before migrating data)
            migrationBuilder.CreateTable(
                name: "WishlistSource",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    GameId = table.Column<int>(type: "integer", nullable: false),
                    ShopId = table.Column<int>(type: "integer", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishlistSource", x => new { x.UserId, x.GameId, x.ShopId });
                    table.ForeignKey(
                        name: "FK_WishlistSource_GameShop_ShopId",
                        column: x => x.ShopId,
                        principalTable: "GameShop",
                        principalColumn: "ShopId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WishlistSource_Game_GameId",
                        column: x => x.GameId,
                        principalTable: "Game",
                        principalColumn: "GameId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WishlistSource_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WishlistSource_Wishlist_UserId_GameId",
                        columns: x => new { x.UserId, x.GameId },
                        principalTable: "Wishlist",
                        principalColumns: new[] { "UserId", "GameId" },
                        onDelete: ReferentialAction.Cascade);
                });

            // Step 3: Migrate existing SourceShopId data to WishlistSource
            migrationBuilder.Sql(@"
                INSERT INTO ""WishlistSource"" (""UserId"", ""GameId"", ""ShopId"", ""ImportedAt"")
                SELECT ""UserId"", ""GameId"", ""SourceShopId"", ""AddedAt""
                FROM ""Wishlist""
                WHERE ""SourceShopId"" IS NOT NULL
                ON CONFLICT (""UserId"", ""GameId"", ""ShopId"") DO NOTHING
            ");

            // Step 4: Drop SourceShopId from Wishlist (data already migrated)
            migrationBuilder.DropForeignKey(
                name: "FK_Wishlist_GameShop_SourceShopId",
                table: "Wishlist");

            migrationBuilder.DropIndex(
                name: "IX_Wishlist_SourceShopId",
                table: "Wishlist");

            migrationBuilder.DropColumn(
                name: "SourceShopId",
                table: "Wishlist");

            // Step 5: Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_WishlistSource_GameId",
                table: "WishlistSource",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_WishlistSource_ShopId",
                table: "WishlistSource",
                column: "ShopId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WishlistSource");

            migrationBuilder.DropColumn(
                name: "AccessToken",
                table: "UserShopProfile");

            migrationBuilder.DropColumn(
                name: "RefreshToken",
                table: "UserShopProfile");

            migrationBuilder.DropColumn(
                name: "TokenExpiresAt",
                table: "UserShopProfile");

            migrationBuilder.AddColumn<int>(
                name: "SourceShopId",
                table: "Wishlist",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Wishlist_SourceShopId",
                table: "Wishlist",
                column: "SourceShopId");

            migrationBuilder.AddForeignKey(
                name: "FK_Wishlist_GameShop_SourceShopId",
                table: "Wishlist",
                column: "SourceShopId",
                principalTable: "GameShop",
                principalColumn: "ShopId",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
