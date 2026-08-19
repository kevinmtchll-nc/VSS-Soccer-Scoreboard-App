using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VS.Core.Models;
using VS.Data.Entities;

namespace VS.Data.Services;

public sealed class SoccerMatchHistoryStore(IDbContextFactory<VsDbContext> factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private bool _schemaReady;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);

    public async Task<SoccerMatchSnapshotEntity> CaptureAsync(SoccerMatchCenter matchCenter, CancellationToken ct = default)
    {
        await using var db = await OpenAsync(ct);
        var match = matchCenter.Match;
        var entity = await db.SoccerMatchSnapshots.FindAsync([match.MatchId], ct);
        if (entity is null)
        {
            entity = new SoccerMatchSnapshotEntity { MatchId = match.MatchId };
            db.SoccerMatchSnapshots.Add(entity);
        }

        entity.MatchDate = DateOnly.FromDateTime(match.PlannedKickoff.Date);
        entity.PlannedKickoff = match.PlannedKickoff;
        entity.Status = match.Status;
        entity.Competition = match.Competition;
        entity.AwayTeam = match.Away.Name;
        entity.HomeTeam = match.Home.Name;
        entity.AwayScore = match.Away.Score;
        entity.HomeScore = match.Home.Score;
        entity.PayloadJson = JsonSerializer.Serialize(matchCenter, JsonOptions);
        entity.CapturedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<IReadOnlyList<SoccerMatchSnapshotEntity>> ListAsync(DateOnly? date, int limit, CancellationToken ct = default)
    {
        await using var db = await OpenAsync(ct);
        var query = db.SoccerMatchSnapshots.AsNoTracking();
        if (date.HasValue) query = query.Where(row => row.MatchDate == date.Value);
        return await query.OrderByDescending(row => row.PlannedKickoff).Take(Math.Clamp(limit, 1, 500)).ToListAsync(ct);
    }

    public async Task<SoccerMatchSnapshotEntity?> GetAsync(string matchId, CancellationToken ct = default)
    {
        await using var db = await OpenAsync(ct);
        return await db.SoccerMatchSnapshots.AsNoTracking().SingleOrDefaultAsync(row => row.MatchId == matchId, ct);
    }

    private async Task<VsDbContext> OpenAsync(CancellationToken ct)
    {
        var db = await factory.CreateDbContextAsync(ct);
        try { await EnsureSchemaAsync(db, ct); }
        catch { await db.DisposeAsync(); throw; }
        return db;
    }

    private async Task EnsureSchemaAsync(VsDbContext db, CancellationToken ct)
    {
        if (_schemaReady) return;
        await _schemaLock.WaitAsync(ct);
        try
        {
            if (_schemaReady) return;
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS soccer_match_snapshots (
                    match_id varchar(80) PRIMARY KEY,
                    match_date date NOT NULL,
                    planned_kickoff timestamptz NOT NULL,
                    status varchar(40) NOT NULL,
                    competition varchar(120) NOT NULL,
                    away_team varchar(120) NOT NULL,
                    home_team varchar(120) NOT NULL,
                    away_score integer NOT NULL,
                    home_score integer NOT NULL,
                    payload_json jsonb NOT NULL,
                    captured_at_utc timestamptz NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_soccer_match_snapshots_match_date ON soccer_match_snapshots(match_date);
                CREATE INDEX IF NOT EXISTS ix_soccer_match_snapshots_kickoff ON soccer_match_snapshots(planned_kickoff);
                """, ct);
            _schemaReady = true;
        }
        finally { _schemaLock.Release(); }
    }
}
