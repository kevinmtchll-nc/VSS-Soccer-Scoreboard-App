namespace VS.Core.Models;

public sealed record TeamScore(
    int TeamId,
    string Name,
    string Abbreviation,
    int Score,
    int Wins,
    int Losses);

public sealed record ScoreboardGame(
    long GamePk,
    DateTimeOffset GameDate,
    string Status,
    string DetailedStatus,
    string Venue,
    int? SeriesGameNumber,
    int? GamesInSeries,
    string SeriesDescription,
    string DayNight,
    int? ScheduledInnings,
    int? CurrentInning,
    string InningState,
    string InningOrdinal,
    string DoubleHeader,
    string DisplayStart,
    TeamScore Away,
    TeamScore Home,
    string FeedUrl);
