using VS.Core.Models;

namespace VS.MLB;

public interface IMlbStatsClient
{
    Task<IReadOnlyList<ScoreboardGame>> GetScheduleAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Pitch>> GetPitchesAsync(long gamePk, CancellationToken cancellationToken = default);
    Task<GameSummary> GetGameSummaryAsync(long gamePk, CancellationToken cancellationToken = default);
    Task<GameCenter> GetGameCenterAsync(long gamePk, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StandingsDivision>> GetStandingsAsync(int season, CancellationToken cancellationToken = default);
}
