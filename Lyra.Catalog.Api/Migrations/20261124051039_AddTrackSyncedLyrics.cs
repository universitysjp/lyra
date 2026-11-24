using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lyra.Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackSyncedLyrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SyncedLyrics",
                table: "tracks",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SyncedLyrics",
                table: "tracks");
        }
    }
}
