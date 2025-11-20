using Microsoft.EntityFrameworkCore;
using SpinMonitor.Api.Models;

namespace SpinMonitor.Api.Data
{
    public class SpinMonitorDbContext : DbContext
    {
        public SpinMonitorDbContext(DbContextOptions<SpinMonitorDbContext> options)
            : base(options)
        {
        }

        public DbSet<Detection> Detections { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Detection>(entity =>
            {
                entity.ToTable("detections");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedOnAdd();

                entity.Property(e => e.Timestamp)
                    .HasColumnName("timestamp")
                    .IsRequired();

                entity.Property(e => e.Stream)
                    .HasColumnName("stream")
                    .HasMaxLength(255)
                    .IsRequired();

                entity.Property(e => e.StreamType)
                    .HasColumnName("stream_type")
                    .HasMaxLength(50);

                entity.Property(e => e.StreamNumber)
                    .HasColumnName("stream_number")
                    .HasMaxLength(50);

                entity.Property(e => e.Track)
                    .HasColumnName("track")
                    .HasMaxLength(500);

                entity.Property(e => e.DurationSeconds)
                    .HasColumnName("duration_seconds");

                entity.Property(e => e.Confidence)
                    .HasColumnName("confidence")
                    .HasPrecision(5, 3);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Indexes
                entity.HasIndex(e => e.Timestamp).HasDatabaseName("idx_timestamp");
                entity.HasIndex(e => e.Stream).HasDatabaseName("idx_stream");
                entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_created_at");
                entity.HasIndex(e => e.Confidence).HasDatabaseName("idx_confidence");
            });
        }
    }
}
