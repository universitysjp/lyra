namespace Lyra.Catalog.Api.Data;

using Lyra.Catalog.Api.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Track> Tracks => Set<Track>();

    public DbSet<UserTrack> UserTracks => Set<UserTrack>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.Entity<Track>(entity =>
        {
            entity.ToTable("tracks");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Artist).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Album).HasMaxLength(512);
            entity.Property(e => e.DurationSeconds).IsRequired();
            entity.Property(e => e.TrackFingerprint).IsRequired().HasMaxLength(64);
            entity.Property(e => e.IsEnriched).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.PrimaryMood).HasMaxLength(128);
            entity.Property(e => e.EnergyLevel);

            entity.HasIndex(e => e.TrackFingerprint).IsUnique();
            entity.HasIndex(e => e.Title)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");
            entity.HasIndex(e => e.Artist)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");
        });

        modelBuilder.Entity<UserTrack>(entity =>
        {
            entity.ToTable("user_tracks");
            entity.HasKey(e => new { e.UserId, e.TrackId });

            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.TrackId).IsRequired();
            entity.Property(e => e.LocalTrackId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ImportedAt).IsRequired();

            entity.HasIndex(e => new { e.UserId, e.ImportedAt });
            entity.HasIndex(e => new { e.UserId, e.LocalTrackId }).IsUnique();

            entity.HasOne(e => e.Track)
                .WithMany()
                .HasForeignKey(e => e.TrackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
