namespace VS.Core.Models;

public sealed record BaseState(
    bool First,
    bool Second,
    bool Third);

public sealed record CurrentMatchup(
    string Batter,
    int? BatterId,
    string BatSide,
    string BatterPosition,
    string BatterJerseyNumber,
    string BatterHeight,
    int? BatterWeight,
    string BatterAverage,
    string BatterHomeRuns,
    string BatterRbi,
    string Pitcher,
    int? PitcherId,
    string PitchHand,
    string PitcherPosition,
    string PitcherJerseyNumber,
    string PitcherHeight,
    int? PitcherWeight,
    string PitcherWins,
    string PitcherLosses,
    string PitcherEra,
    string PitcherStrikeouts);

public sealed record GameCenter(
    long GamePk,
    DateTimeOffset GameDate,
    int AwayTeamId,
    string AwayTeam,
    int HomeTeamId,
    string HomeTeam,
    int AwayScore,
    int HomeScore,
    string Status,
    string DetailedStatus,
    string Venue,
    int? Inning,
    string InningState,
    int Balls,
    int Strikes,
    int Outs,
    BaseState Bases,
    CurrentMatchup? Matchup,
    string LastPlay,
    IReadOnlyList<Pitch> Pitches,
    DateTimeOffset UpdatedAt);
