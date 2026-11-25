namespace Lyra.PlaylistRag.Api.Repositories;

using Lyra.PlaylistRag.Api.Data;
using Lyra.PlaylistRag.Api.Models;
using Microsoft.EntityFrameworkCore;

public sealed class TrackEmbeddingRepository(VectorDbContext db) : ITrackEmbeddingRepository
{
    public async Task UpsertAsync(TrackEmbedding embedding, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO track_embeddings
                ("TrackId", "Title", "Artist", "Album", "DurationSeconds", "PrimaryMood", "Themes",
                 "EnergyLevel", "SemanticSummary", "Embedding", "EmbeddingModel", "EmbeddingDimensions",
                 "EnrichedAt", "UpdatedAt")
            VALUES
                ({embedding.TrackId}, {embedding.Title}, {embedding.Artist}, {embedding.Album},
                 {embedding.DurationSeconds}, {embedding.PrimaryMood}, {embedding.Themes},
                 {embedding.EnergyLevel}, {embedding.SemanticSummary}, {embedding.Embedding},
                 {embedding.EmbeddingModel}, {embedding.EmbeddingDimensions}, {embedding.EnrichedAt},
                 {embedding.UpdatedAt})
            ON CONFLICT ("TrackId") DO UPDATE SET
                "Title" = EXCLUDED."Title",
                "Artist" = EXCLUDED."Artist",
                "Album" = EXCLUDED."Album",
                "DurationSeconds" = EXCLUDED."DurationSeconds",
                "PrimaryMood" = EXCLUDED."PrimaryMood",
                "Themes" = EXCLUDED."Themes",
                "EnergyLevel" = EXCLUDED."EnergyLevel",
                "SemanticSummary" = EXCLUDED."SemanticSummary",
                "Embedding" = EXCLUDED."Embedding",
                "EmbeddingModel" = EXCLUDED."EmbeddingModel",
                "EmbeddingDimensions" = EXCLUDED."EmbeddingDimensions",
                "EnrichedAt" = EXCLUDED."EnrichedAt",
                "UpdatedAt" = EXCLUDED."UpdatedAt";
            """, ct);
    }
}
