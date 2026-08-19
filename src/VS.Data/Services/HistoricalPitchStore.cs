using Microsoft.EntityFrameworkCore;
using VS.Core.Models;
using VS.Data.Entities;

namespace VS.Data.Services;

public sealed record IngestResult(
    long GamePk,
    int Seen,
    int Inserted,
    bool IsFinal,
    string Result,
    string Message);

public sealed record DatabaseStatus(
    bool Configured,
    bool CanConnect,
    long Games,
    long Pitches,
    DateTimeOffset? LatestGameDate,
    string Message);

public sealed class HistoricalPitchStore(IDbContextFactory<VsDbContext> factory)
{
    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
    }

    public async Task<DatabaseStatus> GetStatusAsync(CancellationToken ct = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var canConnect = await db.Database.CanConnectAsync(ct);
            if (!canConnect)
                return new DatabaseStatus(true, false, 0, 0, null, "PostgreSQL connection failed.");

            var games = await db.Games.LongCountAsync(ct);
            var pitches = await db.Pitches.LongCountAsync(ct);
            var latest = await db.Games
                .OrderByDescending(x => x.GameDate)
                .Select(x => (DateTimeOffset?)x.GameDate)
                .FirstOrDefaultAsync(ct);

            return new DatabaseStatus(true, true, games, pitches, latest, "PostgreSQL connected.");
        }
        catch (Exception ex)
        {
            return new DatabaseStatus(true, false, 0, 0, null, ex.Message);
        }
    }

    public async Task<IngestResult> IngestAsync(GameCenter game, CancellationToken ct = default)
    {
        var started = DateTimeOffset.UtcNow;
        await using var db = await factory.CreateDbContextAsync(ct);

        await db.Database.EnsureCreatedAsync(ct);

        var entity = await db.Games.FirstOrDefaultAsync(x => x.GamePk == game.GamePk, ct);
        if (entity is null)
        {
            entity = new GameEntity { GamePk = game.GamePk };
            db.Games.Add(entity);
        }

        entity.GameDate = game.GameDate;
        entity.AwayTeamId = game.AwayTeamId;
        entity.AwayTeam = game.AwayTeam;
        entity.HomeTeamId = game.HomeTeamId;
        entity.HomeTeam = game.HomeTeam;
        entity.AwayScore = game.AwayScore;
        entity.HomeScore = game.HomeScore;
        entity.Status = game.Status;
        entity.DetailedStatus = game.DetailedStatus;
        entity.Venue = game.Venue;
        entity.IsFinal = IsFinal(game);
        entity.LastIngestedAtUtc = DateTimeOffset.UtcNow;

        var playIds = game.Pitches
            .Select(x => x.PlayId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();

        var existing = playIds.Length == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await db.Pitches
                .Where(x => x.GamePk == game.GamePk && playIds.Contains(x.PlayId))
                .Select(x => x.PlayId)
                .ToListAsync(ct))
              .ToHashSet(StringComparer.Ordinal);

        var inserted = 0;

        foreach (var p in game.Pitches)
        {
            if (string.IsNullOrWhiteSpace(p.PlayId) || existing.Contains(p.PlayId))
                continue;

            db.Pitches.Add(new PitchEntity
            {
                GamePk = game.GamePk,
                PlayId = p.PlayId,
                AtBatIndex = p.AtBatIndex,
                PitchNumber = p.PitchNumber,
                PitchCode = p.PitchCode,
                PitchType = p.PitchType,
                Result = p.Result,
                StartSpeedMph = p.StartSpeedMph,
                EndSpeedMph = p.EndSpeedMph,
                PlateX = p.PlateX,
                PlateZ = p.PlateZ,
                StrikeZoneTop = p.StrikeZoneTop,
                StrikeZoneBottom = p.StrikeZoneBottom,
                SpinRate = p.SpinRate,
                HorizontalBreak = p.HorizontalBreak,
                VerticalBreak = p.VerticalBreak,
                Extension = p.Extension,
                Zone = p.Zone,
                BatterId = p.BatterId,
                Batter = p.Batter,
                PitcherId = p.PitcherId,
                Pitcher = p.Pitcher,
                BatSide = p.BatSide,
                PitchHand = p.PitchHand
            });

            existing.Add(p.PlayId);
            inserted++;
        }

        await db.SaveChangesAsync(ct);

        db.IngestionLogs.Add(new IngestionLogEntity
        {
            GamePk = game.GamePk,
            StartedAtUtc = started,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            PitchCountSeen = game.Pitches.Count,
            PitchCountInserted = inserted,
            Result = "OK",
            Message = entity.IsFinal ? "Final game ingested." : "Live game snapshot ingested."
        });
        await db.SaveChangesAsync(ct);

        return new IngestResult(
            game.GamePk,
            game.Pitches.Count,
            inserted,
            entity.IsFinal,
            "OK",
            inserted == 0 ? "No new pitches." : $"Inserted {inserted} new pitches.");
    }

    public async Task<IReadOnlyList<Pitch>> QueryPitchesAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? pitcher,
        string? batter,
        string? pitchType,
        int limit = 20000,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 50000);

        await using var db = await factory.CreateDbContextAsync(ct);

        var q =
            from p in db.Pitches.AsNoTracking()
            join g in db.Games.AsNoTracking() on p.GamePk equals g.GamePk
            select new { p, g.GameDate };

        if (from.HasValue) q = q.Where(x => x.GameDate >= from.Value);
        if (to.HasValue) q = q.Where(x => x.GameDate <= to.Value);
        if (!string.IsNullOrWhiteSpace(pitcher)) q = q.Where(x => x.p.Pitcher == pitcher);
        if (!string.IsNullOrWhiteSpace(batter)) q = q.Where(x => x.p.Batter == batter);
        if (!string.IsNullOrWhiteSpace(pitchType)) q = q.Where(x => x.p.PitchCode == pitchType);

        return await q
            .OrderByDescending(x => x.GameDate)
            .ThenByDescending(x => x.p.Id)
            .Take(limit)
            .Select(x => new Pitch(
                x.p.PlayId,
                x.p.AtBatIndex,
                x.p.PitchNumber,
                x.p.PitchCode,
                x.p.PitchType,
                x.p.Result,
                x.p.StartSpeedMph,
                x.p.EndSpeedMph,
                x.p.PlateX,
                x.p.PlateZ,
                x.p.StrikeZoneTop,
                x.p.StrikeZoneBottom,
                x.p.SpinRate,
                x.p.HorizontalBreak,
                x.p.VerticalBreak,
                x.p.Extension,
                x.p.Zone,
                x.p.BatterId,
                x.p.Batter,
                x.p.PitcherId,
                x.p.Pitcher,
                x.p.BatSide,
                x.p.PitchHand))
            .ToListAsync(ct);
    }

    private static bool IsFinal(GameCenter game) =>
        game.Status.Equals("Final", StringComparison.OrdinalIgnoreCase) ||
        game.DetailedStatus.Contains("Final", StringComparison.OrdinalIgnoreCase);
}
