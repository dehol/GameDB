using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImportJobLogsAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SkippedCount",
                table: "WishlistImport",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EgsOffersUpdated",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GogOffersUpdated",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SteamOffersUpdated",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalOffersUpdated",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    AuditLogId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: true),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    IPAddress = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.AuditLogId);
                });

            migrationBuilder.CreateTable(
                name: "ImportJobLog",
                columns: table => new
                {
                    ImportJobLogId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ImportJobId = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Phase = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Data = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportJobLog", x => x.ImportJobLogId);
                    table.ForeignKey(
                        name: "FK_ImportJobLog_ImportJob_ImportJobId",
                        column: x => x.ImportJobId,
                        principalTable: "ImportJob",
                        principalColumn: "ImportJobId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 12, 14, 38, 17, 465, DateTimeKind.Utc).AddTicks(9135), new DateTime(2026, 5, 12, 14, 38, 17, 465, DateTimeKind.Utc).AddTicks(9136) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 12, 14, 38, 17, 465, DateTimeKind.Utc).AddTicks(9142), new DateTime(2026, 5, 12, 14, 38, 17, 465, DateTimeKind.Utc).AddTicks(9143) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 12, 14, 38, 17, 465, DateTimeKind.Utc).AddTicks(9146), new DateTime(2026, 5, 12, 14, 38, 17, 465, DateTimeKind.Utc).AddTicks(9147) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 12, 14, 38, 17, 465, DateTimeKind.Utc).AddTicks(9150), new DateTime(2026, 5, 12, 14, 38, 17, 465, DateTimeKind.Utc).AddTicks(9150) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 12, 14, 38, 17, 465, DateTimeKind.Utc).AddTicks(9153), new DateTime(2026, 5, 12, 14, 38, 17, 465, DateTimeKind.Utc).AddTicks(9154) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 12, 14, 38, 17, 465, DateTimeKind.Utc).AddTicks(9157), new DateTime(2026, 5, 12, 14, 38, 17, 465, DateTimeKind.Utc).AddTicks(9157) });

            migrationBuilder.CreateIndex(
                name: "IX_WishlistImport_UserId_StartedAt",
                table: "WishlistImport",
                columns: new[] { "UserId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Timestamp_ActionType",
                table: "AuditLog",
                columns: new[] { "Timestamp", "ActionType" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_UserId",
                table: "AuditLog",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobLog_ImportJobId_Timestamp",
                table: "ImportJobLog",
                columns: new[] { "ImportJobId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "ImportJobLog");

            migrationBuilder.DropIndex(
                name: "IX_WishlistImport_UserId_StartedAt",
                table: "WishlistImport");

            migrationBuilder.DropColumn(
                name: "SkippedCount",
                table: "WishlistImport");

            migrationBuilder.DropColumn(
                name: "EgsOffersUpdated",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "GogOffersUpdated",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "SteamOffersUpdated",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "TotalOffersUpdated",
                table: "ImportJob");

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 22, 15, 10, 0, 33, DateTimeKind.Utc).AddTicks(7756), new DateTime(2026, 4, 22, 15, 10, 0, 33, DateTimeKind.Utc).AddTicks(7757) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 22, 15, 10, 0, 33, DateTimeKind.Utc).AddTicks(7762), new DateTime(2026, 4, 22, 15, 10, 0, 33, DateTimeKind.Utc).AddTicks(7763) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 22, 15, 10, 0, 33, DateTimeKind.Utc).AddTicks(7766), new DateTime(2026, 4, 22, 15, 10, 0, 33, DateTimeKind.Utc).AddTicks(7766) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 22, 15, 10, 0, 33, DateTimeKind.Utc).AddTicks(7770), new DateTime(2026, 4, 22, 15, 10, 0, 33, DateTimeKind.Utc).AddTicks(7770) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 22, 15, 10, 0, 33, DateTimeKind.Utc).AddTicks(7773), new DateTime(2026, 4, 22, 15, 10, 0, 33, DateTimeKind.Utc).AddTicks(7774) });

            migrationBuilder.UpdateData(
                table: "Game",
                keyColumn: "GameId",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 4, 22, 15, 10, 0, 33, DateTimeKind.Utc).AddTicks(7777), new DateTime(2026, 4, 22, 15, 10, 0, 33, DateTimeKind.Utc).AddTicks(7777) });
        }
    }
}
