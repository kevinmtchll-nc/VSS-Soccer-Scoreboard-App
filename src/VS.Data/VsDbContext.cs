using Microsoft.EntityFrameworkCore;
using VS.Data.Entities;

namespace VS.Data;

public sealed class VsDbContext(DbContextOptions<VsDbContext> options) : DbContext(options)
{
    public DbSet<GameEntity> Games => Set<GameEntity>();
    public DbSet<PitchEntity> Pitches => Set<PitchEntity>();
    public DbSet<IngestionLogEntity> IngestionLogs => Set<IngestionLogEntity>();
    public DbSet<SoccerMatchSnapshotEntity> SoccerMatchSnapshots => Set<SoccerMatchSnapshotEntity>();

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

        modelBuilder.Entity<SoccerMatchSnapshotEntity>(b =>
        {
            b.ToTable("soccer_match_snapshots");
            b.HasKey(x => x.MatchId);
            b.Property(x => x.MatchId).HasColumnName("match_id").HasMaxLength(80);
            b.Property(x => x.MatchDate).HasColumnName("match_date");
            b.Property(x => x.PlannedKickoff).HasColumnName("planned_kickoff");
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(40);
            b.Property(x => x.Competition).HasColumnName("competition").HasMaxLength(120);
            b.Property(x => x.AwayTeam).HasColumnName("away_team").HasMaxLength(120);
            b.Property(x => x.HomeTeam).HasColumnName("home_team").HasMaxLength(120);
            b.Property(x => x.AwayScore).HasColumnName("away_score");
            b.Property(x => x.HomeScore).HasColumnName("home_score");
            b.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb");
            b.Property(x => x.CapturedAtUtc).HasColumnName("captured_at_utc");
            b.HasIndex(x => x.MatchDate);
            b.HasIndex(x => x.PlannedKickoff);
        });
    }
}
