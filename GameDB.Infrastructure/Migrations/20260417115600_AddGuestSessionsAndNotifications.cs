using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestSessionsAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGuest",
                table: "User",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "User",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "User",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.Sql(@"
                ALTER TABLE ""User""
                ADD CONSTRAINT chk_user_guest_identity
                CHECK (""IsGuest"" = FALSE OR (""Email"" IS NULL AND ""PasswordHash"" IS NULL));
            ");

            migrationBuilder.CreateTable(
                name: "GuestSession",
                columns: table => new
                {
                    GuestSessionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DeviceHash = table.Column<string>(type: "text", nullable: false),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestSession", x => x.GuestSessionId);
                    table.ForeignKey(
                        name: "FK_GuestSession_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notification",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_Notification_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS IX_User_IsGuest ON ""User"" (""IsGuest"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS IX_Wishlist_User_AddedAt ON ""Wishlist"" (""UserId"", ""AddedAt"" DESC);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS IX_Alert_User_Active_Triggered ON ""Alert"" (""UserId"", ""IsActive"", ""TriggeredAt"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS IX_Notification_User_Read_CreatedAt ON ""Notification"" (""UserId"", ""IsRead"", ""CreatedAt"" DESC);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS IX_GuestSession_User_LastSeen ON ""GuestSession"" (""UserId"", ""LastSeen"" DESC);");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_GuestSession_DeviceHash_Active ON ""GuestSession"" (""DeviceHash"") WHERE ""RevokedAt"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_GuestSession_DeviceHash_Active;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_GuestSession_User_LastSeen;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_Notification_User_Read_CreatedAt;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_Alert_User_Active_Triggered;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_Wishlist_User_AddedAt;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_User_IsGuest;");

            migrationBuilder.DropTable(
                name: "GuestSession");

            migrationBuilder.DropTable(
                name: "Notification");

            migrationBuilder.Sql(@"ALTER TABLE ""User"" DROP CONSTRAINT IF EXISTS chk_user_guest_identity;");
            migrationBuilder.Sql(@"UPDATE ""User"" SET ""Email"" = CONCAT('guest_', ""UserId"", '@local.invalid') WHERE ""Email"" IS NULL;");
            migrationBuilder.Sql(@"UPDATE ""User"" SET ""PasswordHash"" = '' WHERE ""PasswordHash"" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "User",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "User",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "IsGuest",
                table: "User");
        }
    }
}
