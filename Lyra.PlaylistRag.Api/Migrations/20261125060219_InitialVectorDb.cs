using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Lyra.PlaylistRag.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialVectorDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "track_embeddings",
                columns: table => new
                {
                    TrackId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Artist = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Album = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    PrimaryMood = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Themes = table.Column<string[]>(type: "text[]", nullable: false),
                    EnergyLevel = table.Column<int>(type: "integer", nullable: true),
                    SemanticSummary = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1536)", nullable: false),
                    EmbeddingModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EmbeddingDimensions = table.Column<int>(type: "integer", nullable: false),
                    EnrichedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_track_embeddings", x => x.TrackId);
                });

            migrationBuilder.CreateTable(
                name: "user_tracks",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocalTrackId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Artist = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Album = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_tracks", x => new { x.UserId, x.TrackId });
                });

            migrationBuilder.CreateIndex(
                name: "ix_track_embeddings_embedding_hnsw",
                table: "track_embeddings",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" })
                .Annotation("Npgsql:StorageParameter:ef_construction", 64)
                .Annotation("Npgsql:StorageParameter:m", 16);

            migrationBuilder.CreateIndex(
                name: "IX_track_embeddings_EnergyLevel",
                table: "track_embeddings",
                column: "EnergyLevel");

            migrationBuilder.CreateIndex(
                name: "IX_track_embeddings_PrimaryMood",
                table: "track_embeddings",
                column: "PrimaryMood");

            migrationBuilder.CreateIndex(
                name: "IX_track_embeddings_Themes",
                table: "track_embeddings",
                column: "Themes")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_user_tracks_UserId_ImportedAt",
                table: "user_tracks",
                columns: new[] { "UserId", "ImportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_tracks_UserId_LocalTrackId",
                table: "user_tracks",
                columns: new[] { "UserId", "LocalTrackId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "track_embeddings");

            migrationBuilder.DropTable(
                name: "user_tracks");
        }
    }
}
