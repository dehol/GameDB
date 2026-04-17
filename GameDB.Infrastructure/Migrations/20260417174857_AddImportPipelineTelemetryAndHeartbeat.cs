using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImportPipelineTelemetryAndHeartbeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EgsFailed",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EgsSkipped",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EgsUpdated",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GogFailed",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GogSkipped",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GogUpdated",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedAt",
                table: "ImportJob",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SteamFailed",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SteamSkipped",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalGamesFailed",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalGamesSkipped",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalGamesUpdated",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalOffersFailed",
                table: "ImportJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalOffersSkipped",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EgsFailed",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "EgsSkipped",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "EgsUpdated",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "GogFailed",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "GogSkipped",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "GogUpdated",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "LastUpdatedAt",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "SteamFailed",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "SteamSkipped",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "TotalGamesFailed",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "TotalGamesSkipped",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "TotalGamesUpdated",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "TotalOffersFailed",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "TotalOffersSkipped",
                table: "ImportJob");

            migrationBuilder.DropColumn(
                name: "TotalOffersUpdated",
                table: "ImportJob");
        }
    }
}
