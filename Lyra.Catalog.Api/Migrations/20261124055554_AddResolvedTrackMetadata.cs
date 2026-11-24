using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lyra.Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddResolvedTrackMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResolvedAlbum",
                table: "tracks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedArtist",
                table: "tracks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ResolvedDurationSeconds",
                table: "tracks",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ResolvedInstrumental",
                table: "tracks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedTitle",
                table: "tracks",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResolvedAlbum",
                table: "tracks");

            migrationBuilder.DropColumn(
                name: "ResolvedArtist",
                table: "tracks");

            migrationBuilder.DropColumn(
                name: "ResolvedDurationSeconds",
                table: "tracks");

            migrationBuilder.DropColumn(
                name: "ResolvedInstrumental",
                table: "tracks");

            migrationBuilder.DropColumn(
                name: "ResolvedTitle",
                table: "tracks");
        }
    }
}
