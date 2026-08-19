namespace VS.Core.Models;

public sealed record StandingsDivision(
    int DivisionId,
    string DivisionName,
    int LeagueId,
    string LeagueName,
    IReadOnlyList<StandingsTeam> Teams);

public sealed record StandingsTeam(
    int TeamId,
    string TeamName,
    int Wins,
    int Losses,
    string Pct,
    string GamesBack,
    string WildCardGamesBack,
    string DivisionRank,
    string LeagueRank,
    string WildCardRank,
    string Streak,
    string LastTen,
    string HomeRecord,
    string AwayRecord,
    int RunsScored,
    int RunsAllowed,
    int RunDifferential,
    string ClinchIndicator,
    bool DivisionLeader,
    bool WildCardLeader);
