using System.Text.Json;
using System.Collections.Concurrent;
using VS.Core.Models;

namespace VS.MLB;

public sealed class MlbStatsClient(HttpClient httpClient) : IMlbStatsClient
{
    public async Task<IReadOnlyList<ScoreboardGame>> GetScheduleAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/schedule?sportId=1&date={date:yyyy-MM-dd}&hydrate=linescore";
        using var doc = await GetJsonAsync(url, cancellationToken);

        var games = new List<ScoreboardGame>();
        if (!doc.RootElement.TryGetProperty("dates", out var dates))
            return games;

        foreach (var dateNode in dates.EnumerateArray())
        {
            if (!dateNode.TryGetProperty("games", out var gameNodes))
                continue;

            foreach (var game in gameNodes.EnumerateArray())
            {
                var teams = game.GetProperty("teams");
                var away = teams.GetProperty("away");
                var home = teams.GetProperty("home");
                var gamePk = game.GetProperty("gamePk").GetInt64();
                var hasLinescore = game.TryGetProperty("linescore", out var linescore);

                games.Add(new ScoreboardGame(
                    GamePk: gamePk,
                    GameDate: game.GetProperty("gameDate").GetDateTimeOffset(),
                    Status: GetString(game.GetProperty("status"), "abstractGameState"),
                    DetailedStatus: GetString(game.GetProperty("status"), "detailedState"),
                    Venue: game.TryGetProperty("venue", out var venue) ? GetString(venue, "name") : "",
                    SeriesGameNumber: GetNullableInt(game, "seriesGameNumber"),
                    GamesInSeries: GetNullableInt(game, "gamesInSeries"),
                    SeriesDescription: GetString(game, "seriesDescription"),
                    DayNight: GetString(game, "dayNight"),
                    ScheduledInnings: GetNullableInt(game, "scheduledInnings"),
                    CurrentInning: hasLinescore ? GetNullableInt(linescore, "currentInning") : null,
                    InningState: hasLinescore ? GetString(linescore, "inningState") : "",
                    InningOrdinal: hasLinescore ? GetString(linescore, "currentInningOrdinal") : "",
                    DoubleHeader: GetString(game, "doubleHeader"),
                    DisplayStart: "",
                    Away: ReadTeam(away),
                    Home: ReadTeam(home),
                    FeedUrl: $"https://statsapi.mlb.com/api/v1.1/game/{gamePk}/feed/live"
                ));
            }
        }

        return games;
    }



    private static string NormalizeDivisionName(int divisionId, string apiName)
    {
        return divisionId switch
        {
            200 => "AL West",
            201 => "AL East",
            202 => "AL Central",
            203 => "NL West",
            204 => "NL East",
            205 => "NL Central",
            _ => string.IsNullOrWhiteSpace(apiName) ? $"Division {divisionId}" : apiName
        };
    }

    public async Task<IReadOnlyList<StandingsDivision>> GetStandingsAsync(
        int season,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"/api/v1/standings?leagueId=103,104&season={season}&standingsTypes=regularSeason&hydrate=team(division,league)";
        using var doc = await GetJsonAsync(url, cancellationToken);

        var divisions = new List<StandingsDivision>();

        if (!doc.RootElement.TryGetProperty("records", out var records) ||
            records.ValueKind != JsonValueKind.Array)
            return divisions;

        foreach (var record in records.EnumerateArray())
        {
            var division = record.TryGetProperty("division", out var divisionNode)
                ? divisionNode
                : default;
            var league = record.TryGetProperty("league", out var leagueNode)
                ? leagueNode
                : default;

            var divisionId = GetInt(division, "id");
            var divisionName = NormalizeDivisionName(divisionId, GetString(division, "name"));
            var leagueId = GetInt(league, "id");
            var leagueName = GetString(league, "name");

            var teams = new List<StandingsTeam>();

            if (record.TryGetProperty("teamRecords", out var teamRecords) &&
                teamRecords.ValueKind == JsonValueKind.Array)
            {
                foreach (var tr in teamRecords.EnumerateArray())
                {
                    var team = tr.TryGetProperty("team", out var teamNode)
                        ? teamNode
                        : default;

                    var wins = GetInt(tr, "wins");
                    var losses = GetInt(tr, "losses");
                    var runsScored = GetInt(tr, "runsScored");
                    var runsAllowed = GetInt(tr, "runsAllowed");

                    string lastTen = "";
                    string home = "";
                    string away = "";

                    if (tr.TryGetProperty("records", out var recordGroups) &&
                        recordGroups.ValueKind == JsonValueKind.Object &&
                        recordGroups.TryGetProperty("splitRecords", out var splits) &&
                        splits.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var split in splits.EnumerateArray())
                        {
                            var type = GetString(split, "type");
                            var recordText = $"{GetInt(split, "wins")}-{GetInt(split, "losses")}";

                            if (type.Equals("lastTen", StringComparison.OrdinalIgnoreCase))
                                lastTen = recordText;
                            else if (type.Equals("home", StringComparison.OrdinalIgnoreCase))
                                home = recordText;
                            else if (type.Equals("away", StringComparison.OrdinalIgnoreCase))
                                away = recordText;
                        }
                    }

                    var streak = "";
                    if (tr.TryGetProperty("streak", out var streakNode))
                        streak = GetString(streakNode, "streakCode");

                    var clinch = GetString(tr, "clinchIndicator");
                    var divisionRank = GetString(tr, "divisionRank");
                    var wildCardRank = GetString(tr, "wildCardRank");

                    teams.Add(new StandingsTeam(
                        TeamId: GetInt(team, "id"),
                        TeamName: GetString(team, "name"),
                        Wins: wins,
                        Losses: losses,
                        Pct: GetString(tr, "winningPercentage"),
                        GamesBack: GetString(tr, "gamesBack"),
                        WildCardGamesBack: GetString(tr, "wildCardGamesBack"),
                        DivisionRank: divisionRank,
                        LeagueRank: GetString(tr, "leagueRank"),
                        WildCardRank: wildCardRank,
                        Streak: streak,
                        LastTen: lastTen,
                        HomeRecord: home,
                        AwayRecord: away,
                        RunsScored: runsScored,
                        RunsAllowed: runsAllowed,
                        RunDifferential: runsScored - runsAllowed,
                        ClinchIndicator: clinch,
                        DivisionLeader: divisionRank == "1",
                        WildCardLeader: wildCardRank == "1"
                    ));
                }
            }

            divisions.Add(new StandingsDivision(
                DivisionId: divisionId,
                DivisionName: divisionName,
                LeagueId: leagueId,
                LeagueName: leagueName,
                Teams: teams
            ));
        }

        return divisions
            .OrderBy(d => d.LeagueId)
            .ThenBy(d => d.DivisionId)
            .ToList();
    }

    public async Task<IReadOnlyList<Pitch>> GetPitchesAsync(
        long gamePk,
        CancellationToken cancellationToken = default)
    {
        using var doc = await GetJsonAsync($"/api/v1.1/game/{gamePk}/feed/live", cancellationToken);
        return ReadPitches(doc.RootElement);
    }


    public async Task<GameSummary> GetGameSummaryAsync(
        long gamePk,
        CancellationToken cancellationToken = default)
    {
        using var doc = await GetJsonAsync($"/api/v1.1/game/{gamePk}/feed/live", cancellationToken);
        var root = doc.RootElement;

        var gameData = root.GetProperty("gameData");
        var liveData = root.GetProperty("liveData");
        var teams = gameData.GetProperty("teams");
        var status = gameData.GetProperty("status");

        var awayTeam = GetString(teams.GetProperty("away"), "name");
        var homeTeam = GetString(teams.GetProperty("home"), "name");

        int awayScore = 0;
        int homeScore = 0;
        int? inning = null;
        string inningState = "";
        int balls = 0;
        int strikes = 0;
        int outs = 0;
        var bases = new BaseState(false, false, false);

        if (liveData.TryGetProperty("linescore", out var linescore))
        {
            inning = GetNullableInt(linescore, "currentInning");
            inningState = GetString(linescore, "inningState");
            balls = GetInt(linescore, "balls");
            strikes = GetInt(linescore, "strikes");
            outs = GetInt(linescore, "outs");

            if (linescore.TryGetProperty("teams", out var scoreTeams))
            {
                if (scoreTeams.TryGetProperty("away", out var awayScoreNode))
                    awayScore = GetInt(awayScoreNode, "runs");
                if (scoreTeams.TryGetProperty("home", out var homeScoreNode))
                    homeScore = GetInt(homeScoreNode, "runs");
            }

            if (linescore.TryGetProperty("offense", out var offense))
            {
                bases = new BaseState(
                    offense.TryGetProperty("first", out _),
                    offense.TryGetProperty("second", out _),
                    offense.TryGetProperty("third", out _)
                );
            }
        }

        CurrentMatchup? matchup = null;
        string lastPlay = "";
        JsonElement lastPlayElement = default;
        bool hasLastPlayElement = false;

        if (liveData.TryGetProperty("plays", out var plays))
        {
            JsonElement play = default;
            bool hasPlay = false;

            if (plays.TryGetProperty("currentPlay", out var currentPlay) &&
                currentPlay.ValueKind == JsonValueKind.Object &&
                currentPlay.TryGetProperty("matchup", out _))
            {
                play = currentPlay;
                hasPlay = true;
            }
            else if (plays.TryGetProperty("allPlays", out var allPlays) &&
                     allPlays.ValueKind == JsonValueKind.Array &&
                     allPlays.GetArrayLength() > 0)
            {
                play = allPlays[allPlays.GetArrayLength() - 1];
                hasPlay = true;
            }

            if (hasPlay)
            {
                if (play.TryGetProperty("matchup", out var m))
                {
                    var batter = m.TryGetProperty("batter", out var b) ? b : default;
                    var pitcher = m.TryGetProperty("pitcher", out var p) ? p : default;

                    matchup = EnrichMatchup(ReadMatchup(m), gameData, liveData);
                }

                lastPlayElement = play;
                hasLastPlayElement = true;

                if (play.TryGetProperty("result", out var result))
                    lastPlay = GetString(result, "description");
            }
        }

        var awayNode = teams.GetProperty("away");
        var homeNode = teams.GetProperty("home");
        var awayRecord = awayNode.TryGetProperty("record", out var awayRecordNode) ? awayRecordNode : default;
        var homeRecord = homeNode.TryGetProperty("record", out var homeRecordNode) ? homeRecordNode : default;
        var lastEvent = hasLastPlayElement ? ReadLiveEvent(lastPlayElement) : null;

        var gameInfo = gameData.TryGetProperty("game", out var gameInfoNode) ? gameInfoNode : default;
        var dateTimeInfo = gameData.TryGetProperty("datetime", out var dateTimeNode) ? dateTimeNode : default;
        var venueInfo = gameData.TryGetProperty("venue", out var venueNode2) ? venueNode2 : default;
        var venueLocation = venueInfo.ValueKind == JsonValueKind.Object && venueInfo.TryGetProperty("location", out var venueLocationNode) ? venueLocationNode : default;
        var venueCoordinates = venueLocation.ValueKind == JsonValueKind.Object && venueLocation.TryGetProperty("defaultCoordinates", out var coordinatesNode) ? coordinatesNode : default;
        var venueTimeZone = venueInfo.ValueKind == JsonValueKind.Object && venueInfo.TryGetProperty("timeZone", out var venueTimeZoneNode) ? venueTimeZoneNode : default;
        var weatherInfo = gameData.TryGetProperty("weather", out var weatherNode) ? weatherNode : default;
        var scheduleContext = await GetScheduleContextAsync(gamePk, cancellationToken);

        return new GameSummary(
            GamePk: gamePk,
            GameDate: gameData.TryGetProperty("datetime", out var dtNode) &&
                      dtNode.TryGetProperty("dateTime", out var dtValue) &&
                      dtValue.ValueKind == JsonValueKind.String &&
                      DateTimeOffset.TryParse(dtValue.GetString(), out var parsedGameDate)
                        ? parsedGameDate
                        : DateTimeOffset.UtcNow,
            AwayTeamId: GetInt(awayNode, "id"),
            AwayTeam: awayTeam,
            AwayAbbreviation: GetString(awayNode, "abbreviation"),
            AwayWins: GetNullableInt(awayRecord, "wins"),
            AwayLosses: GetNullableInt(awayRecord, "losses"),
            HomeTeamId: GetInt(homeNode, "id"),
            HomeTeam: homeTeam,
            HomeAbbreviation: GetString(homeNode, "abbreviation"),
            HomeWins: GetNullableInt(homeRecord, "wins"),
            HomeLosses: GetNullableInt(homeRecord, "losses"),
            AwayScore: awayScore,
            HomeScore: homeScore,
            Status: GetString(status, "abstractGameState"),
            DetailedStatus: GetString(status, "detailedState"),
            Venue: GetString(venueInfo, "name"),
            VenueId: GetNullableInt(venueInfo, "id"),
            VenueLatitude: GetNullableDouble(venueCoordinates, "latitude"),
            VenueLongitude: GetNullableDouble(venueCoordinates, "longitude"),
            VenueTimeZone: GetString(venueTimeZone, "id"),
            Inning: inning,
            InningState: inningState,
            Balls: balls,
            Strikes: strikes,
            Outs: outs,
            Bases: bases,
            Matchup: matchup,
            LastPlay: lastPlay,
            LastEvent: lastEvent,
            SeriesGameNumber: scheduleContext.SeriesGameNumber ?? GetNullableInt(gameInfo, "gameNumber"),
            GamesInSeries: scheduleContext.GamesInSeries,
            SeriesDescription: scheduleContext.SeriesDescription,
            DayNight: !string.IsNullOrWhiteSpace(scheduleContext.DayNight)
                ? scheduleContext.DayNight
                : GetString(dateTimeInfo, "dayNight"),
            ScheduledInnings: scheduleContext.ScheduledInnings,
            DoubleHeader: !string.IsNullOrWhiteSpace(scheduleContext.DoubleHeader)
                ? scheduleContext.DoubleHeader
                : GetString(gameInfo, "doubleHeader"),
            ScheduledStart: !string.IsNullOrWhiteSpace(scheduleContext.ScheduledStart)
                ? scheduleContext.ScheduledStart
                : GetString(dateTimeInfo, "time"),
            WeatherTempF: GetFlexibleInt(weatherInfo, "temp"),
            WeatherCondition: GetString(weatherInfo, "condition"),
            WeatherWind: GetString(weatherInfo, "wind"),
            BoxScore: ReadBoxScore(liveData, awayNode, homeNode),
            LineScore: ReadLineScore(liveData),
            ScoringPlays: ReadScoringPlays(liveData),
            UpdatedAt: DateTimeOffset.UtcNow
        );
    }

    private static GameLineScore ReadLineScore(JsonElement liveData)
    {
        var innings = new List<InningLineScore>();
        var awayRuns = 0; var awayHits = 0; var awayErrors = 0;
        var homeRuns = 0; var homeHits = 0; var homeErrors = 0;
        if (!liveData.TryGetProperty("linescore", out var lineScore))
            return new GameLineScore(innings, 0, 0, 0, 0, 0, 0);

        if (lineScore.TryGetProperty("innings", out var inningNodes) && inningNodes.ValueKind == JsonValueKind.Array)
        {
            foreach (var node in inningNodes.EnumerateArray())
            {
                var away = node.TryGetProperty("away", out var awayNode) ? awayNode : default;
                var home = node.TryGetProperty("home", out var homeNode) ? homeNode : default;
                innings.Add(new InningLineScore(
                    GetInt(node, "num"),
                    away.ValueKind == JsonValueKind.Object ? GetNullableInt(away, "runs") : null,
                    home.ValueKind == JsonValueKind.Object ? GetNullableInt(home, "runs") : null));
            }
        }
        if (lineScore.TryGetProperty("teams", out var teams))
        {
            if (teams.TryGetProperty("away", out var away))
            { awayRuns = GetInt(away, "runs"); awayHits = GetInt(away, "hits"); awayErrors = GetInt(away, "errors"); }
            if (teams.TryGetProperty("home", out var home))
            { homeRuns = GetInt(home, "runs"); homeHits = GetInt(home, "hits"); homeErrors = GetInt(home, "errors"); }
        }
        return new GameLineScore(innings, awayRuns, awayHits, awayErrors, homeRuns, homeHits, homeErrors);
    }

    private static IReadOnlyList<ScoringPlay> ReadScoringPlays(JsonElement liveData)
    {
        var output = new List<ScoringPlay>();
        if (!liveData.TryGetProperty("plays", out var plays) ||
            !plays.TryGetProperty("allPlays", out var allPlays) ||
            allPlays.ValueKind != JsonValueKind.Array)
            return output;

        HashSet<int>? scoringIndexes = null;
        if (plays.TryGetProperty("scoringPlays", out var scoringNodes) &&
            scoringNodes.ValueKind == JsonValueKind.Array)
        {
            scoringIndexes = scoringNodes.EnumerateArray()
                .Where(node => node.ValueKind == JsonValueKind.Number)
                .Select(node => node.GetInt32())
                .ToHashSet();
        }

        var index = 0;
        foreach (var play in allPlays.EnumerateArray())
        {
            var result = play.TryGetProperty("result", out var resultNode) ? resultNode : default;
            var isScoring = scoringIndexes?.Contains(index) == true ||
                (result.ValueKind == JsonValueKind.Object &&
                 result.TryGetProperty("isScoringPlay", out var scoringFlag) &&
                 scoringFlag.ValueKind == JsonValueKind.True);
            index++;
            if (!isScoring)
                continue;

            var about = play.TryGetProperty("about", out var aboutNode) ? aboutNode : default;
            var matchup = play.TryGetProperty("matchup", out var matchupNode) ? matchupNode : default;
            var batter = matchup.ValueKind == JsonValueKind.Object && matchup.TryGetProperty("batter", out var batterNode)
                ? GetString(batterNode, "fullName")
                : "";

            output.Add(new ScoringPlay(
                GetInt(about, "inning"),
                GetString(about, "halfInning"),
                batter,
                GetString(result, "event"),
                GetString(result, "description"),
                GetInt(result, "rbi"),
                GetInt(result, "awayScore"),
                GetInt(result, "homeScore")));
        }

        return output;
    }

    public async Task<GameCenter> GetGameCenterAsync(
        long gamePk,
        CancellationToken cancellationToken = default)
    {
        using var doc = await GetJsonAsync($"/api/v1.1/game/{gamePk}/feed/live", cancellationToken);
        var root = doc.RootElement;

        var gameData = root.GetProperty("gameData");
        var liveData = root.GetProperty("liveData");
        var teams = gameData.GetProperty("teams");
        var status = gameData.GetProperty("status");

        var awayTeam = GetString(teams.GetProperty("away"), "name");
        var homeTeam = GetString(teams.GetProperty("home"), "name");

        int awayScore = 0;
        int homeScore = 0;
        int? inning = null;
        string inningState = "";
        int balls = 0;
        int strikes = 0;
        int outs = 0;
        var bases = new BaseState(false, false, false);

        if (liveData.TryGetProperty("linescore", out var linescore))
        {
            inning = GetNullableInt(linescore, "currentInning");
            inningState = GetString(linescore, "inningState");
            balls = GetInt(linescore, "balls");
            strikes = GetInt(linescore, "strikes");
            outs = GetInt(linescore, "outs");

            if (linescore.TryGetProperty("teams", out var scoreTeams))
            {
                if (scoreTeams.TryGetProperty("away", out var awayScoreNode))
                    awayScore = GetInt(awayScoreNode, "runs");
                if (scoreTeams.TryGetProperty("home", out var homeScoreNode))
                    homeScore = GetInt(homeScoreNode, "runs");
            }

            if (linescore.TryGetProperty("offense", out var offense))
            {
                bases = new BaseState(
                    offense.TryGetProperty("first", out _),
                    offense.TryGetProperty("second", out _),
                    offense.TryGetProperty("third", out _)
                );
            }
        }

        // Final feeds should still expose scores even if linescore is incomplete.
        if (awayScore == 0 && homeScore == 0 &&
            liveData.TryGetProperty("boxscore", out var boxscore) &&
            boxscore.TryGetProperty("teams", out var boxTeams))
        {
            if (boxTeams.TryGetProperty("away", out var awayBox) &&
                awayBox.TryGetProperty("teamStats", out var awayStats) &&
                awayStats.TryGetProperty("batting", out var awayBatting))
                awayScore = GetInt(awayBatting, "runs");

            if (boxTeams.TryGetProperty("home", out var homeBox) &&
                homeBox.TryGetProperty("teamStats", out var homeStats) &&
                homeStats.TryGetProperty("batting", out var homeBatting))
                homeScore = GetInt(homeBatting, "runs");
        }

        CurrentMatchup? matchup = null;
        string lastPlay = "";

        if (liveData.TryGetProperty("plays", out var plays))
        {
            JsonElement play = default;
            bool hasPlay = false;

            if (plays.TryGetProperty("currentPlay", out var currentPlay) &&
                currentPlay.ValueKind == JsonValueKind.Object &&
                currentPlay.TryGetProperty("matchup", out _))
            {
                play = currentPlay;
                hasPlay = true;
            }
            else if (plays.TryGetProperty("allPlays", out var allPlays) &&
                     allPlays.ValueKind == JsonValueKind.Array &&
                     allPlays.GetArrayLength() > 0)
            {
                play = allPlays[allPlays.GetArrayLength() - 1];
                hasPlay = true;
            }

            if (hasPlay)
            {
                if (play.TryGetProperty("matchup", out var m))
                {
                    var batter = m.TryGetProperty("batter", out var b) ? b : default;
                    var pitcher = m.TryGetProperty("pitcher", out var p) ? p : default;

                    matchup = EnrichMatchup(ReadMatchup(m), gameData, liveData);
                }

                if (play.TryGetProperty("result", out var result))
                    lastPlay = GetString(result, "description");
            }
        }

        var pitches = ReadPitches(root);

        return new GameCenter(
            GamePk: gamePk,
            GameDate: gameData.TryGetProperty("datetime", out var dtNode) &&
                      dtNode.TryGetProperty("dateTime", out var dtValue) &&
                      dtValue.ValueKind == JsonValueKind.String &&
                      DateTimeOffset.TryParse(dtValue.GetString(), out var parsedGameDate)
                        ? parsedGameDate
                        : DateTimeOffset.UtcNow,
            AwayTeamId: GetInt(teams.GetProperty("away"), "id"),
            AwayTeam: awayTeam,
            HomeTeamId: GetInt(teams.GetProperty("home"), "id"),
            HomeTeam: homeTeam,
            AwayScore: awayScore,
            HomeScore: homeScore,
            Status: GetString(status, "abstractGameState"),
            DetailedStatus: GetString(status, "detailedState"),
            Venue: gameData.TryGetProperty("venue", out var venueNode) ? GetString(venueNode, "name") : "",
            Inning: inning,
            InningState: inningState,
            Balls: balls,
            Strikes: strikes,
            Outs: outs,
            Bases: bases,
            Matchup: matchup,
            LastPlay: lastPlay,
            Pitches: pitches,
            UpdatedAt: DateTimeOffset.UtcNow
        );
    }

    private static IReadOnlyList<Pitch> ReadPitches(JsonElement root)
    {
        var pitches = new List<Pitch>();

        if (!root.TryGetProperty("liveData", out var liveData) ||
            !liveData.TryGetProperty("plays", out var plays) ||
            !plays.TryGetProperty("allPlays", out var allPlays))
            return pitches;

        foreach (var atBat in allPlays.EnumerateArray())
        {
            if (!atBat.TryGetProperty("matchup", out var matchup))
                continue;

            var atBatIndex = atBat.TryGetProperty("about", out var aboutNode)
                ? GetInt(aboutNode, "atBatIndex")
                : 0;

            var batterNode = matchup.TryGetProperty("batter", out var b) ? b : default;
            var pitcherNode = matchup.TryGetProperty("pitcher", out var p) ? p : default;
            var batter = GetString(batterNode, "fullName");
            var pitcher = GetString(pitcherNode, "fullName");
            var batSide = matchup.TryGetProperty("batSide", out var bs) ? GetString(bs, "code") : "";
            var pitchHand = matchup.TryGetProperty("pitchHand", out var ph) ? GetString(ph, "code") : "";

            if (!atBat.TryGetProperty("playEvents", out var events))
                continue;

            foreach (var evt in events.EnumerateArray())
            {
                if (!evt.TryGetProperty("isPitch", out var isPitch) || !isPitch.GetBoolean())
                    continue;
                if (!evt.TryGetProperty("pitchData", out var pitchData))
                    continue;

                var details = evt.TryGetProperty("details", out var d) ? d : default;
                var coordinates = pitchData.TryGetProperty("coordinates", out var coords) ? coords : default;
                var breaks = pitchData.TryGetProperty("breaks", out var br) ? br : default;
                var type = details.ValueKind != JsonValueKind.Undefined && details.TryGetProperty("type", out var t) ? t : default;

                pitches.Add(new Pitch(
                    PlayId: GetString(evt, "playId"),
                    AtBatIndex: atBatIndex,
                    PitchNumber: GetInt(evt, "pitchNumber"),
                    PitchCode: GetString(type, "code"),
                    PitchType: GetString(type, "description"),
                    Result: GetString(details, "description"),
                    StartSpeedMph: GetNullableDouble(pitchData, "startSpeed"),
                    EndSpeedMph: GetNullableDouble(pitchData, "endSpeed"),
                    PlateX: GetNullableDouble(coordinates, "pX"),
                    PlateZ: GetNullableDouble(coordinates, "pZ"),
                    StrikeZoneTop: GetNullableDouble(pitchData, "strikeZoneTop"),
                    StrikeZoneBottom: GetNullableDouble(pitchData, "strikeZoneBottom"),
                    SpinRate: GetNullableDouble(breaks, "spinRate"),
                    HorizontalBreak: GetNullableDouble(breaks, "breakHorizontal"),
                    VerticalBreak: GetNullableDouble(breaks, "breakVertical"),
                    Extension: GetNullableDouble(pitchData, "extension"),
                    Zone: GetNullableInt(pitchData, "zone"),
                    BatterId: GetNullableInt(batterNode, "id"),
                    Batter: batter,
                    PitcherId: GetNullableInt(pitcherNode, "id"),
                    Pitcher: pitcher,
                    BatSide: batSide,
                    PitchHand: pitchHand
                ));
            }
        }

        return pitches;
    }




    private static readonly ConcurrentDictionary<long, (ScheduleContext Context, DateTimeOffset ExpiresAt)> ScheduleCache = new();

    private sealed record ScheduleContext(
        int? SeriesGameNumber,
        int? GamesInSeries,
        string SeriesDescription,
        string DayNight,
        int? ScheduledInnings,
        string DoubleHeader,
        string ScheduledStart);

    private async Task<ScheduleContext> GetScheduleContextAsync(
        long gamePk,
        CancellationToken cancellationToken)
    {
        if (ScheduleCache.TryGetValue(gamePk, out var cached) &&
            cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Context;
        }

        try
        {
            using var doc = await GetJsonAsync(
                $"/api/v1/schedule?sportId=1&gamePk={gamePk}",
                cancellationToken);

            if (doc.RootElement.TryGetProperty("dates", out var dates) &&
                dates.ValueKind == JsonValueKind.Array &&
                dates.GetArrayLength() > 0 &&
                dates[0].TryGetProperty("games", out var games) &&
                games.ValueKind == JsonValueKind.Array &&
                games.GetArrayLength() > 0)
            {
                var game = games[0];
                var start = game.TryGetProperty("gameDate", out var gd) &&
                            gd.ValueKind == JsonValueKind.String &&
                            DateTimeOffset.TryParse(gd.GetString(), out var parsed)
                    ? parsed.ToLocalTime().ToString("h:mm tt")
                    : "";

                var context = new ScheduleContext(
                    SeriesGameNumber: GetNullableInt(game, "seriesGameNumber"),
                    GamesInSeries: GetNullableInt(game, "gamesInSeries"),
                    SeriesDescription: GetString(game, "seriesDescription"),
                    DayNight: GetString(game, "dayNight"),
                    ScheduledInnings: GetNullableInt(game, "scheduledInnings"),
                    DoubleHeader: GetString(game, "doubleHeader"),
                    ScheduledStart: start);

                ScheduleCache[gamePk] = (context, DateTimeOffset.UtcNow.AddHours(6));
                return context;
            }
        }
        catch
        {
            // Schedule context is enhancement-only; never fail live GameCenter for it.
        }

        return new ScheduleContext(null, null, "", "", null, "", "");
    }

    private static JsonElement FindPlayer(JsonElement gameData, int? playerId)
    {
        if (!playerId.HasValue ||
            !gameData.TryGetProperty("players", out var players) ||
            players.ValueKind != JsonValueKind.Object)
            return default;

        var key = $"ID{playerId.Value}";
        return players.TryGetProperty(key, out var player) ? player : default;
    }

    private static JsonElement FindBoxscorePlayer(JsonElement liveData, int? playerId)
    {
        if (!playerId.HasValue ||
            !liveData.TryGetProperty("boxscore", out var boxscore) ||
            !boxscore.TryGetProperty("teams", out var teams))
            return default;

        var key = $"ID{playerId.Value}";
        foreach (var side in new[] { "away", "home" })
        {
            if (teams.TryGetProperty(side, out var team) &&
                team.TryGetProperty("players", out var players) &&
                players.TryGetProperty(key, out var player))
                return player;
        }

        return default;
    }

    private static GameBoxScore ReadBoxScore(JsonElement liveData, JsonElement awayTeam, JsonElement homeTeam)
    {
        JsonElement awayBox = default;
        JsonElement homeBox = default;
        if (liveData.TryGetProperty("boxscore", out var boxscore) &&
            boxscore.TryGetProperty("teams", out var boxTeams))
        {
            boxTeams.TryGetProperty("away", out awayBox);
            boxTeams.TryGetProperty("home", out homeBox);
        }

        return new GameBoxScore(
            ReadTeamBoxScore(awayBox, awayTeam),
            ReadTeamBoxScore(homeBox, homeTeam));
    }

    private static TeamBoxScore ReadTeamBoxScore(JsonElement boxTeam, JsonElement team)
    {
        var batting = new List<(int Order, BattingLine Line)>();
        var pitching = new List<(int PlayerId, PitchingLine Line)>();
        var officialBattingOrder = ReadPlayerOrder(boxTeam, "battingOrder");
        var officialPitchingOrder = ReadPlayerOrder(boxTeam, "pitchers");
        var doubles = new List<string>();
        var triples = new List<string>();
        var homeRuns = new List<string>();
        var runsBattedIn = new List<string>();
        var stolenBases = new List<string>();
        var caughtStealing = new List<string>();
        var errors = new List<string>();
        var doublePlays = new List<string>();
        var outfieldAssists = new List<string>();
        var wildPitches = new List<string>();
        var hitBatters = new List<string>();
        var balks = new List<string>();
        var pitchCounts = new List<string>();

        if (boxTeam.ValueKind == JsonValueKind.Object &&
            boxTeam.TryGetProperty("players", out var players) &&
            players.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in players.EnumerateObject())
            {
                var player = property.Value;
                var person = player.TryGetProperty("person", out var personNode) ? personNode : default;
                var position = player.TryGetProperty("position", out var positionNode) ? positionNode : default;
                var stats = player.TryGetProperty("stats", out var statsNode) ? statsNode : default;
                var battingStats = stats.ValueKind == JsonValueKind.Object && stats.TryGetProperty("batting", out var battingNode)
                    ? battingNode : default;
                var pitchingStats = stats.ValueKind == JsonValueKind.Object && stats.TryGetProperty("pitching", out var pitchingNode)
                    ? pitchingNode : default;
                var fieldingStats = stats.ValueKind == JsonValueKind.Object && stats.TryGetProperty("fielding", out var fieldingNode)
                    ? fieldingNode : default;
                var seasonBatting = ReadSeasonStats(player, "batting");
                var seasonPitching = ReadSeasonStats(player, "pitching");
                var seasonFielding = ReadSeasonStats(player, "fielding");
                var playerId = GetInt(person, "id");
                var name = GetString(person, "fullName");
                var battingOrderText = GetStatString(player, "battingOrder");

                if (!string.IsNullOrWhiteSpace(battingOrderText) || officialBattingOrder.ContainsKey(playerId))
                {
                    _ = int.TryParse(battingOrderText, out var battingOrder);
                    if (battingOrder <= 0 && officialBattingOrder.TryGetValue(playerId, out var lineupIndex))
                        battingOrder = (lineupIndex + 1) * 100;
                    batting.Add((battingOrder, new BattingLine(
                        playerId, name, GetString(position, "abbreviation"),
                        GetStatString(battingStats, "atBats"), GetStatString(battingStats, "runs"),
                        GetStatString(battingStats, "hits"), GetStatString(battingStats, "rbi"),
                        GetStatString(battingStats, "baseOnBalls"), GetStatString(battingStats, "strikeOuts"),
                        GetStatString(seasonBatting, "avg"),
                        GetStatString(battingStats, "homeRuns"), GetStatString(battingStats, "doubles"),
                        GetStatString(battingStats, "triples"), GetStatString(battingStats, "stolenBases"),
                        GetStatString(battingStats, "caughtStealing"))));

                    AddGameSeasonStat(doubles, name, battingStats, seasonBatting, "doubles");
                    AddGameSeasonStat(triples, name, battingStats, seasonBatting, "triples");
                    AddGameSeasonStat(homeRuns, name, battingStats, seasonBatting, "homeRuns");
                    AddGameSeasonStat(runsBattedIn, name, battingStats, seasonBatting, "rbi");
                    AddGameSeasonStat(stolenBases, name, battingStats, seasonBatting, "stolenBases");
                    AddGameSeasonStat(caughtStealing, name, battingStats, seasonBatting, "caughtStealing");
                }

                AddGameSeasonStat(errors, name, fieldingStats, seasonFielding, "errors");
                AddGameSeasonStat(doublePlays, name, fieldingStats, seasonFielding, "doublePlays");
                if (GetString(position, "abbreviation") is "LF" or "CF" or "RF")
                    AddGameSeasonStat(outfieldAssists, name, fieldingStats, seasonFielding, "assists");

                var inningsPitched = GetStatString(pitchingStats, "inningsPitched");
                var pitchesThrown = GetStatString(pitchingStats, "numberOfPitches");
                if ((!string.IsNullOrWhiteSpace(inningsPitched) && inningsPitched != "0.0") ||
                    (!string.IsNullOrWhiteSpace(pitchesThrown) && pitchesThrown != "0"))
                {
                    pitching.Add((playerId, new PitchingLine(
                        playerId, name, "", inningsPitched,
                        GetStatString(pitchingStats, "hits"), GetStatString(pitchingStats, "runs"),
                        GetStatString(pitchingStats, "earnedRuns"), GetStatString(pitchingStats, "baseOnBalls"),
                        GetStatString(pitchingStats, "strikeOuts"), GetStatString(seasonPitching, "era"),
                        pitchesThrown)));

                    if (!string.IsNullOrWhiteSpace(pitchesThrown) && pitchesThrown != "0")
                        pitchCounts.Add($"{AbbreviateName(name)} {pitchesThrown}");

                    AddGameSeasonStat(wildPitches, name, pitchingStats, seasonPitching, "wildPitches");
                    AddGameSeasonStat(hitBatters, name, pitchingStats, seasonPitching, "hitBatsmen");
                    AddGameSeasonStat(balks, name, pitchingStats, seasonPitching, "balks");
                }
            }
        }

        var highlights = new List<GameHighlight>();
        AddHighlight(highlights, "Batting", "2B", doubles);
        AddHighlight(highlights, "Batting", "3B", triples);
        AddHighlight(highlights, "Batting", "HR", homeRuns);
        AddHighlight(highlights, "Batting", "RBI", runsBattedIn);
        AddHighlight(highlights, "Baserunning", "SB", stolenBases);
        AddHighlight(highlights, "Baserunning", "CS", caughtStealing);
        AddHighlight(highlights, "Fielding", "DP", doublePlays);
        AddHighlight(highlights, "Fielding", "OFA", outfieldAssists);
        AddHighlight(highlights, "Fielding", "E", errors);
        AddHighlight(highlights, "Pitchers", "WP", wildPitches);
        AddHighlight(highlights, "Pitchers", "HBP", hitBatters);
        AddHighlight(highlights, "Pitchers", "BK", balks);
        AddHighlight(highlights, "Pitchers", "Pitches", pitchCounts);

        return new TeamBoxScore(
            GetInt(team, "id"),
            GetString(team, "name"),
            batting.OrderBy(item => item.Order).Select(item => item.Line).ToList(),
            pitching
                .OrderBy(item => officialPitchingOrder.TryGetValue(item.PlayerId, out var order) ? order : int.MaxValue)
                .Select((item, index) => item.Line with { Role = index == 0 ? "SP" : "RP" })
                .ToList(),
            highlights);
    }

    private static Dictionary<int, int> ReadPlayerOrder(JsonElement team, string propertyName)
    {
        var result = new Dictionary<int, int>();
        if (team.ValueKind != JsonValueKind.Object ||
            !team.TryGetProperty(propertyName, out var values) ||
            values.ValueKind != JsonValueKind.Array)
            return result;

        var index = 0;
        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var playerId) && !result.ContainsKey(playerId))
                result[playerId] = index++;
        }
        return result;
    }

    private static void AddGameSeasonStat(
        List<string> output,
        string playerName,
        JsonElement gameStats,
        JsonElement seasonStats,
        string property)
    {
        var gameValue = GetNullableInt(gameStats, property) ?? 0;
        if (gameValue <= 0 || string.IsNullOrWhiteSpace(playerName))
            return;

        var seasonValue = GetNullableInt(seasonStats, property);
        var today = gameValue > 1 ? $" {gameValue}" : "";
        var season = seasonValue.HasValue ? $" ({seasonValue.Value})" : "";
        output.Add($"{AbbreviateName(playerName)}{today}{season}");
    }

    private static string AbbreviateName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length < 2 ? fullName : $"{parts[0][0]}. {string.Join(' ', parts.Skip(1))}";
    }

    private static void AddHighlight(
        List<GameHighlight> output,
        string section,
        string label,
        List<string> values)
    {
        if (values.Count > 0)
            output.Add(new GameHighlight(section, label, string.Join(", ", values)));
    }

    private static JsonElement ReadSeasonStats(JsonElement player, string group)
    {
        if (player.ValueKind == JsonValueKind.Object &&
            player.TryGetProperty("seasonStats", out var seasonStats) &&
            seasonStats.TryGetProperty(group, out var stats))
            return stats;

        return default;
    }

    private static CurrentMatchup EnrichMatchup(CurrentMatchup matchup, JsonElement gameData, JsonElement liveData)
    {
        var batter = FindPlayer(gameData, matchup.BatterId);
        var pitcher = FindPlayer(gameData, matchup.PitcherId);
        var batterStats = ReadSeasonStats(FindBoxscorePlayer(liveData, matchup.BatterId), "batting");
        var pitcherStats = ReadSeasonStats(FindBoxscorePlayer(liveData, matchup.PitcherId), "pitching");

        string position(JsonElement p) =>
            p.ValueKind == JsonValueKind.Object &&
            p.TryGetProperty("primaryPosition", out var pos)
                ? GetString(pos, "abbreviation")
                : "";

        return matchup with
        {
            BatterPosition = position(batter),
            BatterJerseyNumber = GetString(batter, "primaryNumber"),
            BatterHeight = GetString(batter, "height"),
            BatterWeight = GetNullableInt(batter, "weight"),
            BatterAverage = GetStatString(batterStats, "avg"),
            BatterHomeRuns = GetStatString(batterStats, "homeRuns"),
            BatterRbi = GetStatString(batterStats, "rbi"),
            PitcherPosition = position(pitcher),
            PitcherJerseyNumber = GetString(pitcher, "primaryNumber"),
            PitcherHeight = GetString(pitcher, "height"),
            PitcherWeight = GetNullableInt(pitcher, "weight"),
            PitcherWins = GetStatString(pitcherStats, "wins"),
            PitcherLosses = GetStatString(pitcherStats, "losses"),
            PitcherEra = GetStatString(pitcherStats, "era"),
            PitcherStrikeouts = GetStatString(pitcherStats, "strikeOuts")
        };
    }

    private static CurrentMatchup ReadMatchup(JsonElement m)
    {
        var batter = m.TryGetProperty("batter", out var b) ? b : default;
        var pitcher = m.TryGetProperty("pitcher", out var p) ? p : default;

        return new CurrentMatchup(
            Batter: GetString(batter, "fullName"),
            BatterId: GetNullableInt(batter, "id"),
            BatSide: m.TryGetProperty("batSide", out var bs) ? GetString(bs, "code") : "",
            BatterPosition: "",
            BatterJerseyNumber: "",
            BatterHeight: "",
            BatterWeight: null,
            BatterAverage: "",
            BatterHomeRuns: "",
            BatterRbi: "",
            Pitcher: GetString(pitcher, "fullName"),
            PitcherId: GetNullableInt(pitcher, "id"),
            PitchHand: m.TryGetProperty("pitchHand", out var ph) ? GetString(ph, "code") : "",
            PitcherPosition: "",
            PitcherJerseyNumber: "",
            PitcherHeight: "",
            PitcherWeight: null,
            PitcherWins: "",
            PitcherLosses: "",
            PitcherEra: "",
            PitcherStrikeouts: ""
        );
    }

    private static LiveEvent? ReadLiveEvent(JsonElement play)
    {
        if (play.ValueKind != JsonValueKind.Object || !play.TryGetProperty("result", out var result))
            return null;

        var matchup = play.TryGetProperty("matchup", out var m) ? m : default;
        var batter = matchup.ValueKind == JsonValueKind.Object && matchup.TryGetProperty("batter", out var b) ? b : default;
        var pitcher = matchup.ValueKind == JsonValueKind.Object && matchup.TryGetProperty("pitcher", out var p) ? p : default;

        double? exitVelocity = null;
        double? launchAngle = null;
        double? distance = null;
        string trajectory = "";
        string hardness = "";

        if (play.TryGetProperty("playEvents", out var events) && events.ValueKind == JsonValueKind.Array)
        {
            foreach (var evt in events.EnumerateArray().Reverse())
            {
                if (!evt.TryGetProperty("hitData", out var hitData))
                    continue;

                exitVelocity = GetNullableDouble(hitData, "launchSpeed");
                launchAngle = GetNullableDouble(hitData, "launchAngle");
                distance = GetNullableDouble(hitData, "totalDistance");
                trajectory = GetString(hitData, "trajectory");
                hardness = GetString(hitData, "hardness");
                break;
            }
        }

        string startBase = "";
        string endBase = "";
        bool runnerScored = false;
        bool runnerRbi = false;
        int? outsOnPlay = null;

        if (play.TryGetProperty("runners", out var runners) && runners.ValueKind == JsonValueKind.Array && runners.GetArrayLength() > 0)
        {
            foreach (var runner in runners.EnumerateArray())
            {
                if (!runner.TryGetProperty("movement", out var movement))
                    continue;

                var sbase = GetString(movement, "start");
                var ebase = GetString(movement, "end");
                var scored = movement.TryGetProperty("isOut", out var isOutNode)
                    ? !isOutNode.GetBoolean() && string.Equals(ebase, "score", StringComparison.OrdinalIgnoreCase)
                    : string.Equals(ebase, "score", StringComparison.OrdinalIgnoreCase);

                if (string.IsNullOrWhiteSpace(startBase) && !string.IsNullOrWhiteSpace(sbase)) startBase = sbase;
                if (!string.IsNullOrWhiteSpace(ebase)) endBase = ebase;
                runnerScored = runnerScored || scored;

                if (runner.TryGetProperty("details", out var details))
                    runnerRbi = runnerRbi || (details.TryGetProperty("rbi", out var rbiNode) && rbiNode.ValueKind == JsonValueKind.True);
            }
        }

        if (play.TryGetProperty("count", out var countNode))
            outsOnPlay = GetNullableInt(countNode, "outs");

        return new LiveEvent(
            Event: GetString(result, "event"),
            EventType: GetString(result, "eventType"),
            Description: GetString(result, "description"),
            IsScoringPlay: result.TryGetProperty("isScoringPlay", out var isp) && isp.ValueKind == JsonValueKind.True,
            HasOut: result.TryGetProperty("hasOut", out var ho) && ho.ValueKind == JsonValueKind.True,
            HasReview: result.TryGetProperty("hasReview", out var hr) && hr.ValueKind == JsonValueKind.True,
            CaptivatingIndex: GetNullableInt(result, "captivatingIndex"),
            Batter: GetString(batter, "fullName"),
            BatterId: GetNullableInt(batter, "id"),
            Pitcher: GetString(pitcher, "fullName"),
            PitcherId: GetNullableInt(pitcher, "id"),
            Rbi: GetInt(result, "rbi"),
            ExitVelocity: exitVelocity,
            LaunchAngle: launchAngle,
            DistanceFeet: distance,
            Trajectory: trajectory,
            Hardness: hardness,
            StartBase: startBase,
            EndBase: endBase,
            RunnerScored: runnerScored,
            RunnerRbi: runnerRbi,
            OutsOnPlay: outsOnPlay
        );
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static TeamScore ReadTeam(JsonElement node)
    {
        var team = node.GetProperty("team");
        var record = node.TryGetProperty("leagueRecord", out var lr) ? lr : default;

        return new TeamScore(
            TeamId: GetInt(team, "id"),
            Name: GetString(team, "name"),
            Abbreviation: GetString(team, "abbreviation"),
            Score: GetInt(node, "score"),
            Wins: GetInt(record, "wins"),
            Losses: GetInt(record, "losses")
        );
    }

    private static string GetString(JsonElement e, string name) =>
        e.ValueKind is JsonValueKind.Object &&
        e.TryGetProperty(name, out var v) &&
        v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? ""
            : "";

    private static string GetStatString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var value))
            return "";

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.GetRawText(),
            _ => ""
        };
    }

    private static int GetInt(JsonElement e, string name) =>
        e.ValueKind is JsonValueKind.Object &&
        e.TryGetProperty(name, out var v) &&
        v.ValueKind == JsonValueKind.Number &&
        v.TryGetInt32(out var value)
            ? value
            : 0;


    private static int? GetFlexibleInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n))
            return n;

        if (value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), out var parsed))
            return parsed;

        return null;
    }

    private static int? GetNullableInt(JsonElement e, string name) =>
        e.ValueKind is JsonValueKind.Object &&
        e.TryGetProperty(name, out var v) &&
        v.ValueKind == JsonValueKind.Number &&
        v.TryGetInt32(out var value)
            ? value
            : null;

    private static double? GetNullableDouble(JsonElement e, string name) =>
        e.ValueKind is JsonValueKind.Object &&
        e.TryGetProperty(name, out var v) &&
        v.ValueKind == JsonValueKind.Number
            ? v.GetDouble()
            : null;
}
