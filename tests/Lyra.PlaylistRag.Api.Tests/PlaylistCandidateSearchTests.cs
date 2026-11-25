namespace Lyra.PlaylistRag.Api.Tests;

using Lyra.PlaylistRag.Api.Configuration;
using Lyra.PlaylistRag.Api.Data;
using Lyra.PlaylistRag.Api.Models;
using Lyra.PlaylistRag.Api.Models.Playlists;
using Lyra.PlaylistRag.Api.Services.Playlists;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;
using Xunit;

public class PlaylistCandidateSearchTests : IClassFixture<VectorPostgresTestFixture>
{
    private readonly VectorPostgresTestFixture _fixture;

    public PlaylistCandidateSearchTests(VectorPostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SearchAsync_IsStrictlyScopedToTheCallerAndOrderedByDistance()
    {
        await ResetAsync();
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var near = Guid.NewGuid();
        var far = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        AddTrack(db, user, near, "local-near", "Near", "Artist A", VectorOf(1, 0), now);
        AddTrack(db, user, far, "local-far", "Far", "Artist B", VectorOf(0, 1), now);
        AddTrack(db, other, foreign, "local-foreign", "Foreign", "Artist C", VectorOf(1, 0), now);
        await db.SaveChangesAsync();

        var search = CreateSearch(db);
        var result = await search.SearchAsync(
            user,
            Plan(),
            VectorOf(1, 0).ToArray(),
            10,
            CancellationToken.None);

        Assert.True(result.HasIndexedTracks);
        Assert.Equal([near, far], result.Candidates.Select(candidate => candidate.TrackId));
        Assert.Equal(
            ["local-near", "local-far"],
            result.Candidates.Select(candidate => candidate.LocalTrackId));
        Assert.DoesNotContain(result.Candidates, candidate => candidate.TrackId == foreign);
    }

    [Fact]
    public async Task SearchAsync_AppliesHardEnergyArtistAndTitleFilters()
    {
        await ResetAsync();
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var match = Guid.NewGuid();
        var wrongMood = Guid.NewGuid();
        var wrongEnergy = Guid.NewGuid();
        AddTrack(db, user, match, "local-match", "Rain Song", "Night Artist", VectorOf(1, 0), now,
            mood: "calm", themes: ["Rain", "night"], energy: 3);
        AddTrack(db, user, wrongMood, "local-mood", "Loud Song", "Night Artist", VectorOf(1, 0), now,
            mood: "energetic", themes: ["rain"], energy: 8);
        AddTrack(db, user, wrongEnergy, "local-energy", "Quiet Song", "Night Artist", VectorOf(1, 0), now,
            mood: "calm", themes: ["rain"], energy: 9);
        await db.SaveChangesAsync();

        var search = CreateSearch(db);
        var result = await search.SearchAsync(
            user,
            Plan(
                moods: ["calm"],
                themes: ["rain"],
                minEnergy: 2,
                maxEnergy: 5,
                artist: "night",
                title: "rain"),
            VectorOf(1, 0).ToArray(),
            10,
            CancellationToken.None);

        Assert.True(result.HasIndexedTracks);
        Assert.Single(result.Candidates);
        Assert.Equal(match, result.Candidates[0].TrackId);
    }

    [Fact]
    public async Task SearchAsync_SoftDescriptorsDoNotEraseHighEnergyResults()
    {
        await ResetAsync();
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var romantic = Guid.NewGuid();
        var energetic = Guid.NewGuid();
        AddTrack(db, user, romantic, "local-romantic", "Shape of You", "Ed Sheeran", VectorOf(0.8f, 0.2f), now,
            mood: "Flirtatious, upbeat romantic dance-pop", themes: ["romance", "dancing"], energy: 7);
        AddTrack(db, user, energetic, "local-energetic", "Believer", "Imagine Dragons", VectorOf(0f, 1f), now,
            mood: "Defiant and resilient", themes: ["resilience", "inner strength"], energy: 8);
        await db.SaveChangesAsync();

        var result = await CreateSearch(db).SearchAsync(
            user,
            Plan(moods: ["romantic"], themes: ["love"], minEnergy: 7),
            VectorOf(1, 0).ToArray(),
            10,
            CancellationToken.None);

        Assert.True(result.HasIndexedTracks);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal(romantic, result.Candidates[0].TrackId);
        Assert.Contains(result.Candidates, candidate => candidate.TrackId == energetic);
    }

    [Fact]
    public async Task SearchAsync_CapsTracksPerArtistButKeepsOtherArtists()
    {
        await ResetAsync();
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        for (var index = 0; index < 4; index++)
        {
            AddTrack(
                db,
                user,
                Guid.NewGuid(),
                $"local-same-{index}",
                $"Same {index}",
                "Same Artist",
                VectorOf(1, index * 0.01f),
                now);
        }

        AddTrack(
            db,
            user,
            Guid.NewGuid(),
            "local-other",
            "Other",
            "Other Artist",
            VectorOf(0.9f, 0.1f),
            now);
        await db.SaveChangesAsync();

        var result = await CreateSearch(db).SearchAsync(
            user,
            Plan(),
            VectorOf(1, 0).ToArray(),
            10,
            CancellationToken.None);

        Assert.Equal(3, result.Candidates.Count);
        Assert.Equal(2, result.Candidates.Count(candidate => candidate.Artist == "Same Artist"));
        Assert.Contains(result.Candidates, candidate => candidate.Artist == "Other Artist");
    }

    [Fact]
    public async Task SearchAsync_DistinguishesAnUnindexedLibraryFromAnEmptyFilterResult()
    {
        await ResetAsync();
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        AddTrack(db, user, Guid.NewGuid(), "local-1", "Song", "Artist", VectorOf(1, 0), now);
        await db.SaveChangesAsync();

        var search = CreateSearch(db);
        var unindexed = await search.SearchAsync(
            Guid.NewGuid(),
            Plan(),
            VectorOf(1, 0).ToArray(),
            10,
            CancellationToken.None);

        var filtered = await search.SearchAsync(
            user,
            Plan(artist: "Not In This Library"),
            VectorOf(1, 0).ToArray(),
            10,
            CancellationToken.None);

        Assert.False(unindexed.HasIndexedTracks);
        Assert.Empty(unindexed.Candidates);
        Assert.True(filtered.HasIndexedTracks);
        Assert.Empty(filtered.Candidates);
    }

    private async Task ResetAsync() =>
        await VectorTestDatabase.ResetAsync(_fixture.ConnectionString);

    private VectorDbContext CreateDb() =>
        new(VectorTestDatabase.Options(_fixture.ConnectionString));

    private static PgvectorPlaylistCandidateSearch CreateSearch(VectorDbContext db) =>
        new(
            db,
            new FakeTrackEmbeddingGenerator(),
            Options.Create(new PlaylistOptions()));

    private static PlaylistQueryPlan Plan(
        IReadOnlyList<string>? moods = null,
        IReadOnlyList<string>? themes = null,
        int? minEnergy = null,
        int? maxEnergy = null,
        string? artist = null,
        string? title = null) =>
        new(
            "calm evening",
            moods ?? [],
            themes ?? [],
            minEnergy,
            maxEnergy,
            artist,
            title,
            "Evening",
            "Soft songs for the evening.");

    private static void AddTrack(
        VectorDbContext db,
        Guid userId,
        Guid trackId,
        string localTrackId,
        string title,
        string artist,
        Vector embedding,
        DateTimeOffset now,
        string mood = "calm",
        string[]? themes = null,
        int energy = 3)
    {
        db.UserTracks.Add(new UserTrack
        {
            UserId = userId,
            TrackId = trackId,
            LocalTrackId = localTrackId,
            Title = title,
            Artist = artist,
            Album = "Album",
            DurationSeconds = 180,
            ImportedAt = now
        });

        db.TrackEmbeddings.Add(new TrackEmbedding
        {
            TrackId = trackId,
            Title = title,
            Artist = artist,
            Album = "Album",
            DurationSeconds = 180,
            PrimaryMood = mood,
            Themes = themes ?? ["evening"],
            EnergyLevel = energy,
            SemanticSummary = "A calm evening track.",
            Embedding = embedding,
            EmbeddingModel = new FakeTrackEmbeddingGenerator().Model,
            EmbeddingDimensions = TrackEmbedding.Dimensions,
            EnrichedAt = now,
            UpdatedAt = now
        });
    }

    private static Vector VectorOf(float first, float second)
    {
        var values = new float[TrackEmbedding.Dimensions];
        values[0] = first;
        values[1] = second;
        return new Vector(values);
    }
}
