using Microsoft.EntityFrameworkCore;
using VS.Data.Entities;

namespace VS.Data;

public sealed class VsDbContext(DbContextOptions<VsDbContext> options) : DbContext(options)
{
    public DbSet<GameEntity> Games => Set<GameEntity>();
    public DbSet<PitchEntity> Pitches => Set<PitchEntity>();
    public DbSet<IngestionLogEntity> IngestionLogs => Set<IngestionLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameEntity>(b =>
        {
            b.ToTable("games");
            b.HasKey(x => x.GamePk);
            b.Property(x => x.AwayTeam).HasMaxLength(100);
            b.Property(x => x.HomeTeam).HasMaxLength(100);
            b.Property(x => x.Status).HasMaxLength(40);
            b.Property(x => x.DetailedStatus).HasMaxLength(80);
            b.Property(x => x.Venue).HasMaxLength(120);

            b.HasIndex(x => x.GameDate);
            b.HasIndex(x => new { x.AwayTeamId, x.GameDate });
            b.HasIndex(x => new { x.HomeTeamId, x.GameDate });
            b.HasIndex(x => x.IsFinal);
        });

        modelBuilder.Entity<PitchEntity>(b =>
        {
            b.ToTable("pitches");
            b.HasKey(x => x.Id);
            b.Property(x => x.PlayId).HasMaxLength(80);
            b.Property(x => x.PitchCode).HasMaxLength(8);
            b.Property(x => x.PitchType).HasMaxLength(60);
            b.Property(x => x.Result).HasMaxLength(100);
            b.Property(x => x.Batter).HasMaxLength(100);
            b.Property(x => x.Pitcher).HasMaxLength(100);
            b.Property(x => x.BatSide).HasMaxLength(2);
            b.Property(x => x.PitchHand).HasMaxLength(2);

            b.HasOne(x => x.Game)
                .WithMany(x => x.Pitches)
                .HasForeignKey(x => x.GamePk)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => new { x.GamePk, x.PlayId }).IsUnique();
            b.HasIndex(x => new { x.PitcherId, x.GamePk });
            b.HasIndex(x => new { x.BatterId, x.GamePk });
            b.HasIndex(x => new { x.PitchCode, x.GamePk });
            b.HasIndex(x => new { x.Pitcher, x.GamePk });
            b.HasIndex(x => new { x.Batter, x.GamePk });
        });

        modelBuilder.Entity<IngestionLogEntity>(b =>
        {
            b.ToTable("ingestion_log");
            b.HasKey(x => x.Id);
            b.Property(x => x.Result).HasMaxLength(30);
            b.Property(x => x.Message).HasMaxLength(500);
            b.HasIndex(x => new { x.GamePk, x.CompletedAtUtc });
        });
    }
}
