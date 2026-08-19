namespace VS.Core.Models;

public sealed record BattingLine(
    int PlayerId, string Name, string Position, string AtBats, string Runs,
    string Hits, string Rbi, string Walks, string Strikeouts, string Average,
    string HomeRuns, string Doubles, string Triples, string StolenBases, string CaughtStealing);

public sealed record PitchingLine(
    int PlayerId, string Name, string Role, string InningsPitched, string Hits, string Runs,
    string EarnedRuns, string Walks, string Strikeouts, string Era,
    string PitchCount);

public sealed record GameHighlight(string Section, string Label, string Value);

public sealed record TeamBoxScore(
    int TeamId, string TeamName,
    IReadOnlyList<BattingLine> Batting,
    IReadOnlyList<PitchingLine> Pitching,
    IReadOnlyList<GameHighlight> Highlights);

public sealed record GameBoxScore(TeamBoxScore Away, TeamBoxScore Home);

public sealed record InningLineScore(
    int Inning, int? AwayRuns, int? HomeRuns);

public sealed record GameLineScore(
    IReadOnlyList<InningLineScore> Innings,
    int AwayRuns, int AwayHits, int AwayErrors,
    int HomeRuns, int HomeHits, int HomeErrors);

public sealed record ScoringPlay(
    int Inning,
    string HalfInning,
    string Batter,
    string Event,
    string Description,
    int RunsBattedIn,
    int AwayScore,
    int HomeScore);

public sealed record GameSummary(
    long GamePk,
    DateTimeOffset GameDate,
    int AwayTeamId,
    string AwayTeam,
    string AwayAbbreviation,
    int? AwayWins,
    int? AwayLosses,
    int HomeTeamId,
    string HomeTeam,
    string HomeAbbreviation,
    int? HomeWins,
    int? HomeLosses,
    int AwayScore,
    int HomeScore,
    string Status,
    string DetailedStatus,
    string Venue,
    int? VenueId,
    double? VenueLatitude,
    double? VenueLongitude,
    string VenueTimeZone,
    int? Inning,
    string InningState,
    int Balls,
    int Strikes,
    int Outs,
    BaseState Bases,
    CurrentMatchup? Matchup,
    string LastPlay,
    LiveEvent? LastEvent,
    int? SeriesGameNumber,
    int? GamesInSeries,
    string SeriesDescription,
    string DayNight,
    int? ScheduledInnings,
    string DoubleHeader,
    string ScheduledStart,
    int? WeatherTempF,
    string WeatherCondition,
    string WeatherWind,
    GameBoxScore BoxScore,
    GameLineScore LineScore,
    IReadOnlyList<ScoringPlay> ScoringPlays,
    DateTimeOffset UpdatedAt);
