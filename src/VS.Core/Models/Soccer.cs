namespace VS.Core.Models;

public sealed record SoccerTeam(
    string TeamId,
    string Name,
    string ShortName,
    string Code,
    int Score,
    string Role);

public sealed record SoccerMatch(
    string MatchId,
    DateTimeOffset PlannedKickoff,
    string Status,
    string Minute,
    string Competition,
    string SeasonId,
    int MatchDay,
    string Stadium,
    string StadiumCity,
    SoccerTeam Away,
    SoccerTeam Home);

public sealed record SoccerMatchConditions(
    double? TemperatureC,
    double? HumidityPercent,
    double? AirPressureHpa,
    string Precipitation,
    string Roof,
    string Floodlights,
    string PitchCondition,
    int? Attendance,
    int? StadiumCapacity,
    bool? SoldOut,
    string StadiumAddress);

public sealed record SoccerPlayer(
    string PersonId,
    string FirstName,
    string LastName,
    string ShortName,
    string Position,
    int? ShirtNumber,
    bool IsStarter,
    bool IsOnField,
    bool IsCaptain);

public sealed record SoccerEvent(
    long EventId,
    string Type,
    string SubType,
    string Minute,
    string Period,
    DateTimeOffset? EventTime,
    string TeamId,
    string TeamName,
    string PlayerId,
    string PlayerName,
    string Description,
    double? X,
    double? Y,
    double? ExpectedGoals,
    string Result);

public sealed record SoccerTeamStatistics(
    string TeamId,
    string TeamName,
    string Role,
    int Goals,
    int Shots,
    int ShotsOnTarget,
    int Corners,
    int Fouls,
    int Offsides,
    int YellowCards,
    int RedCards,
    int Passes,
    double PassCompletion,
    double Possession,
    double ExpectedGoals,
    int Saves);

public sealed record SoccerSide(
    SoccerTeam Team,
    string Formation,
    IReadOnlyList<SoccerPlayer> Players);

public sealed record SoccerMatchCenter(
    SoccerMatch Match,
    SoccerSide Away,
    SoccerSide Home,
    IReadOnlyList<SoccerEvent> Events,
    IReadOnlyList<SoccerTeamStatistics> TeamStatistics,
    SoccerMatchConditions? Conditions,
    DateTimeOffset UpdatedAt);

public sealed record SoccerStanding(
    int Position,
    string TeamId,
    string TeamName,
    string TeamCode,
    int Played,
    int Won,
    int Drawn,
    int Lost,
    int GoalsFor,
    int GoalsAgainst,
    int GoalDifference,
    int Points);

public sealed record SoccerLeader(
    string Category,
    string PlayerId,
    string PlayerName,
    string TeamId,
    string TeamName,
    double Value);

public sealed record SoccerAlert(string MatchId, string Kind, string Text, string Minute);

public sealed record SoccerDailySummary(
    DateOnly Date,
    IReadOnlyList<SoccerLeader> Leaders,
    IReadOnlyList<SoccerAlert> Alerts,
    DateTimeOffset UpdatedAt);
