using VS.Core.Models;

namespace VS.Soccer;

public interface ISoccerStatsClient
{
    Task<IReadOnlyList<SoccerMatch>> GetScheduleAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<SoccerMatchCenter> GetMatchCenterAsync(string matchId, DateOnly? scheduledDate = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SoccerStanding>> GetStandingsAsync(CancellationToken cancellationToken = default);
    Task<SoccerDailySummary> GetDailySummaryAsync(DateOnly date, CancellationToken cancellationToken = default);
}
