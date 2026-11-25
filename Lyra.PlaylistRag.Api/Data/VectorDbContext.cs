namespace Lyra.PlaylistRag.Api.Data;

using Lyra.PlaylistRag.Api.Models;
using Microsoft.EntityFrameworkCore;

public sealed class VectorDbContext(DbContextOptions<VectorDbContext> options) : DbContext(options)
{
    public DbSet<UserTrack> UserTracks => Set<UserTrack>();

    public DbSet<TrackEmbedding> TrackEmbeddings => Set<TrackEmbedding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<UserTrack>(entity =>
        {
            entity.ToTable("user_tracks");
            entity.HasKey(e => new { e.UserId, e.TrackId });

            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.TrackId).IsRequired();
            entity.Property(e => e.LocalTrackId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Artist).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Album).HasMaxLength(512);
            entity.Property(e => e.DurationSeconds).IsRequired();
            entity.Property(e => e.ImportedAt).IsRequired();

            entity.HasIndex(e => new { e.UserId, e.ImportedAt });
            entity.HasIndex(e => new { e.UserId, e.LocalTrackId }).IsUnique();
        });

        modelBuilder.Entity<TrackEmbedding>(entity =>
        {
            entity.ToTable("track_embeddings");
            entity.HasKey(e => e.TrackId);

            entity.Property(e => e.Title).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Artist).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Album).HasMaxLength(512);
            entity.Property(e => e.DurationSeconds).IsRequired();
            entity.Property(e => e.PrimaryMood).HasMaxLength(128);
            entity.Property(e => e.Themes).IsRequired().HasColumnType("text[]");
            entity.Property(e => e.SemanticSummary).IsRequired();
            entity.Property(e => e.Embedding)
                .IsRequired()
                .HasColumnType($"vector({TrackEmbedding.Dimensions})");
            entity.Property(e => e.EmbeddingModel).IsRequired().HasMaxLength(200);
            entity.Property(e => e.EmbeddingDimensions).IsRequired();
            entity.Property(e => e.EnrichedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.Embedding)
                .HasDatabaseName("ix_track_embeddings_embedding_hnsw")
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops")
                .HasStorageParameter("m", 16)
                .HasStorageParameter("ef_construction", 64);

            entity.HasIndex(e => e.PrimaryMood);
            entity.HasIndex(e => e.EnergyLevel);
            entity.HasIndex(e => e.Themes).HasMethod("gin");
        });
    }
}
