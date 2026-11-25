namespace Lyra.PlaylistRag.Api.Tests;

using Lyra.PlaylistRag.Api.Data;
using Lyra.PlaylistRag.Api.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Xunit;

public class VectorDataLayerTests : IClassFixture<VectorPostgresTestFixture>
{
    private readonly VectorPostgresTestFixture _fixture;

    public VectorDataLayerTests(VectorPostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Migration_AppliesVectorExtensionAndHnswIndex()
    {
        await VectorTestDatabase.ResetAsync(_fixture.ConnectionString);
        await using var db = new VectorDbContext(
            VectorTestDatabase.Options(_fixture.ConnectionString));

        var extension = await db.Database
            .SqlQueryRaw<string>(
                """SELECT extname AS "Value" FROM pg_extension WHERE extname = 'vector'""")
            .SingleAsync();

        Assert.Equal("vector", extension);

        var indexDefinitions = await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT indexdef AS "Value"
                FROM pg_indexes
                WHERE schemaname = 'public' AND tablename = 'track_embeddings'
                """)
            .ToListAsync();

        Assert.Contains(
            indexDefinitions,
            definition => definition.Contains("hnsw", StringComparison.OrdinalIgnoreCase)
                && definition.Contains("vector_cosine_ops", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Vector_RoundTripsAndFindsNearestNeighbourForTheOwner()
    {
        await VectorTestDatabase.ResetAsync(_fixture.ConnectionString);
        await using var db = new VectorDbContext(
            VectorTestDatabase.Options(_fixture.ConnectionString));

        var userId = Guid.NewGuid();
        var firstTrackId = Guid.NewGuid();
        var secondTrackId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        db.UserTracks.AddRange(
            new UserTrack
            {
                UserId = userId,
                TrackId = firstTrackId,
                LocalTrackId = "local-first",
                Title = "First",
                Artist = "Artist",
                DurationSeconds = 180,
                ImportedAt = now
            },
            new UserTrack
            {
                UserId = userId,
                TrackId = secondTrackId,
                LocalTrackId = "local-second",
                Title = "Second",
                Artist = "Artist",
                DurationSeconds = 190,
                ImportedAt = now
            });

        db.TrackEmbeddings.AddRange(
            Track(firstTrackId, "First", "Artist", VectorOf(1, 0), now),
            Track(secondTrackId, "Second", "Artist", VectorOf(0, 1), now));

        await db.SaveChangesAsync();

        var queryVector = VectorOf(1, 0);
        var nearest = await db.TrackEmbeddings
            .OrderBy(embedding => embedding.Embedding.CosineDistance(queryVector))
            .FirstAsync();

        Assert.Equal(firstTrackId, nearest.TrackId);

        var owned = await db.UserTracks
            .Where(link => link.UserId == userId)
            .Join(
                db.TrackEmbeddings,
                link => link.TrackId,
                embedding => embedding.TrackId,
                (link, embedding) => new { link.LocalTrackId, embedding.Title })
            .ToListAsync();

        Assert.Equal(2, owned.Count);
        Assert.Contains(owned, row => row.LocalTrackId == "local-first" && row.Title == "First");
    }

    private static TrackEmbedding Track(
        Guid trackId,
        string title,
        string artist,
        Vector embedding,
        DateTimeOffset now) =>
        new()
        {
            TrackId = trackId,
            Title = title,
            Artist = artist,
            Album = "Album",
            DurationSeconds = 180,
            PrimaryMood = "calm",
            Themes = ["night"],
            EnergyLevel = 3,
            SemanticSummary = "A calm track.",
            Embedding = embedding,
            EmbeddingModel = "test-model",
            EmbeddingDimensions = TrackEmbedding.Dimensions,
            EnrichedAt = now,
            UpdatedAt = now
        };

    private static Vector VectorOf(float first, float second)
    {
        var values = new float[TrackEmbedding.Dimensions];
        values[0] = first;
        values[1] = second;
        return new Vector(values);
    }
}
