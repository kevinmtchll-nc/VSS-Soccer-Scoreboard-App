using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VS.Core.Models;

namespace VS.Soccer;

public sealed class SoccerStatsClient(HttpClient httpClient, IOptions<SoccerStatsOptions> options) : ISoccerStatsClient
{
    private readonly SoccerStatsOptions _options = options.Value;

    public async Task<IReadOnlyList<SoccerMatch>> GetScheduleAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var value = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var url = $"matches/seasons/{Uri.EscapeDataString(_options.SeasonId)}?match_date[gte]={value}&match_date[lte]={value}&competition_id={Uri.EscapeDataString(_options.CompetitionId)}&per_page=120&sort=planned_kickoff_time:asc,home_team_name:asc";
        using var doc = await GetJsonAsync(url, cancellationToken);
        if (!doc.RootElement.TryGetProperty("schedule", out var schedule) || schedule.ValueKind != JsonValueKind.Array)
            return [];

        return schedule.EnumerateArray().Select(ReadScheduleMatch).ToList();
    }

    public async Task<SoccerMatchCenter> GetMatchCenterAsync(string matchId, DateOnly? scheduledDate = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(matchId))
            throw new ArgumentException("A match ID is required.", nameof(matchId));

        var id = Uri.EscapeDataString(matchId.Trim());
        var matchTask = GetOptionalJsonTextAsync($"matches/{id}", cancellationToken);
        var eventsTask = GetOptionalJsonTextAsync($"matches/{id}/key_events?per_page=1000", cancellationToken);
        var statsTask = GetOptionalJsonTextAsync($"statistics/clubs/matches/{id}", cancellationToken);
        await Task.WhenAll(matchTask, eventsTask, statsTask);

        using var matchDoc = JsonDocument.Parse(await matchTask);
        using var eventsDoc = JsonDocument.Parse(await eventsTask);
        using var statsDoc = JsonDocument.Parse(await statsTask);

        if (!matchDoc.RootElement.TryGetProperty("match_information", out var info))
        {
            var scheduled = await FindScheduledMatchAsync(matchId.Trim(), scheduledDate, cancellationToken)
                ?? throw new KeyNotFoundException($"MLS match {matchId} was not found.");
            return new SoccerMatchCenter(
                scheduled,
                new SoccerSide(scheduled.Away, "", []),
                new SoccerSide(scheduled.Home, "", []),
                ReadEvents(eventsDoc.RootElement),
                ReadTeamStatistics(statsDoc.RootElement),
                null,
                DateTimeOffset.UtcNow);
        }
        var homeNode = matchDoc.RootElement.GetProperty("home");
        var awayNode = matchDoc.RootElement.GetProperty("away");
        var match = ReadDetailedMatch(info, homeNode, awayNode, matchDoc.RootElement);

        return new SoccerMatchCenter(
            match,
            ReadSide(awayNode, match.Away),
            ReadSide(homeNode, match.Home),
            ReadEvents(eventsDoc.RootElement),
            ReadTeamStatistics(statsDoc.RootElement),
            ReadConditions(matchDoc.RootElement),
            DateTimeOffset.UtcNow);
    }

    private async Task<SoccerMatch?> FindScheduledMatchAsync(string matchId, DateOnly? scheduledDate, CancellationToken cancellationToken)
    {
        var dates = scheduledDate.HasValue ? new[] { scheduledDate.Value } : Enumerable.Range(-2, 17).Select(offset => DateOnly.FromDateTime(DateTime.Today).AddDays(offset));
        foreach (var date in dates)
        {
            IReadOnlyList<SoccerMatch> matches;
            try { matches = await GetScheduleAsync(date, cancellationToken); }
            catch (HttpRequestException) { continue; }
            var match = matches.FirstOrDefault(value => value.MatchId.Equals(matchId, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        return null;
    }

    public async Task<IReadOnlyList<SoccerStanding>> GetStandingsAsync(CancellationToken cancellationToken = default)
    {
        // Derive a provider-neutral table from completed matches. This keeps the public
        // contract stable when a licensed provider replaces the initial MLS adapter.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var seasonStart = new DateOnly(today.Year, 1, 1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var through = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var baseUrl = $"matches/seasons/{Uri.EscapeDataString(_options.SeasonId)}?match_date[gte]={seasonStart}&match_date[lte]={through}&competition_id={Uri.EscapeDataString(_options.CompetitionId)}&per_page=120&sort=planned_kickoff_time:asc";
        var rows = new Dictionary<string, StandingAccumulator>(StringComparer.OrdinalIgnoreCase);
        string? nextPageToken = null;
        var seenTokens = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var url = nextPageToken is null ? baseUrl : $"{baseUrl}&next_page_token={Uri.EscapeDataString(nextPageToken)}";
            using var doc = await GetJsonAsync(url, cancellationToken);
            if (doc.RootElement.TryGetProperty("schedule", out var schedule) && schedule.ValueKind == JsonValueKind.Array)
            {
                foreach (var node in schedule.EnumerateArray())
                {
                    var match = ReadScheduleMatch(node);
                    if (!IsCompleted(match.Status)) continue;
                    Add(match.Away); Add(match.Home);
                    var away = rows[match.Away.TeamId]; var home = rows[match.Home.TeamId];
                    away.Played++; home.Played++; away.GoalsFor += match.Away.Score; away.GoalsAgainst += match.Home.Score;
                    home.GoalsFor += match.Home.Score; home.GoalsAgainst += match.Away.Score;
                    if (match.Away.Score > match.Home.Score) { away.Won++; home.Lost++; }
                    else if (match.Home.Score > match.Away.Score) { home.Won++; away.Lost++; }
                    else { away.Drawn++; home.Drawn++; }
                }
            }

            nextPageToken = S(doc.RootElement, "next_page_token");
            if (string.IsNullOrWhiteSpace(nextPageToken) || !seenTokens.Add(nextPageToken)) nextPageToken = null;
        } while (nextPageToken is not null);

        void Add(SoccerTeam team) => rows.TryAdd(team.TeamId, new StandingAccumulator(team));
        return rows.Values.OrderByDescending(x => x.Points).ThenByDescending(x => x.GoalDifference).ThenByDescending(x => x.GoalsFor).ThenBy(x => x.Team.Name)
            .Select((x, i) => new SoccerStanding(i + 1, x.Team.TeamId, x.Team.Name, x.Team.Code, x.Played, x.Won, x.Drawn, x.Lost, x.GoalsFor, x.GoalsAgainst, x.GoalDifference, x.Points)).ToList();
    }

    public async Task<SoccerDailySummary> GetDailySummaryAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var matches = await GetScheduleAsync(date, cancellationToken);
        var centers = new List<SoccerMatchCenter>();
        foreach (var match in matches)
        {
            try { centers.Add(await GetMatchCenterAsync(match.MatchId, date, cancellationToken)); }
            catch (HttpRequestException) { /* A pre-match feed may not expose details yet. */ }
        }
        var allLeaders = centers.SelectMany(center => center.Events.Select(e => (center, e)))
            .Where(x => x.e.Type == "shot_at_goals" || x.e.SubType == "goals")
            .GroupBy(x => new { x.e.PlayerId, x.e.PlayerName, x.e.TeamId, x.e.TeamName, Category = x.e.SubType == "goals" ? "Goals" : "Shots" })
            .Select(g => new SoccerLeader(g.Key.Category, g.Key.PlayerId, g.Key.PlayerName, g.Key.TeamId, g.Key.TeamName, g.Count()))
            .Concat(centers.SelectMany(c => c.TeamStatistics).Select(s => new SoccerLeader("Expected Goals", "", s.TeamName, s.TeamId, s.TeamName, s.ExpectedGoals)))
            .Where(x => !string.IsNullOrWhiteSpace(x.PlayerName) && x.Value > 0)
            .ToList();
        var leaders = allLeaders.GroupBy(x => x.Category)
            .SelectMany(group => group.OrderByDescending(x => x.Value).ThenBy(x => x.PlayerName).Take(5))
            .OrderBy(x => x.Category).ThenByDescending(x => x.Value).ToList();
        var alerts = centers.SelectMany(c => c.Events.Where(e => e.SubType == "goals" || e.Type == "cards")
            .Select(e => new SoccerAlert(c.Match.MatchId, e.SubType == "goals" ? "goal" : "card", $"{e.Description} — {e.TeamName}", e.Minute)))
            .Concat(centers.Where(c => IsCompleted(c.Match.Status)).Select(c => new SoccerAlert(c.Match.MatchId, "final", $"FINAL: {c.Match.Away.Name} {c.Match.Away.Score}, {c.Match.Home.Name} {c.Match.Home.Score}", c.Match.Minute)))
            .ToList();
        return new SoccerDailySummary(date, leaders, alerts, DateTimeOffset.UtcNow);
    }

    private static bool IsCompleted(string status) => status.Contains("final", StringComparison.OrdinalIgnoreCase) || status.Contains("full", StringComparison.OrdinalIgnoreCase);
    private sealed class StandingAccumulator(SoccerTeam team)
    {
        public SoccerTeam Team { get; } = team; public int Played; public int Won; public int Drawn; public int Lost; public int GoalsFor; public int GoalsAgainst;
        public int GoalDifference => GoalsFor - GoalsAgainst; public int Points => Won * 3 + Drawn;
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
        => JsonDocument.Parse(await GetJsonTextAsync(url, cancellationToken));

    private async Task<string> GetJsonTextAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private async Task<string> GetOptionalJsonTextAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.NoContent)
            return "{}";
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(value) ? "{}" : value;
    }

    private static SoccerMatch ReadScheduleMatch(JsonElement node)
    {
        var home = new SoccerTeam(S(node,"home_team_id"), S(node,"home_team_name"), S(node,"home_team_short_name"), S(node,"home_team_three_letter_code"), I(node,"home_team_goals"), "home");
        var away = new SoccerTeam(S(node,"away_team_id"), S(node,"away_team_name"), S(node,"away_team_short_name"), S(node,"away_team_three_letter_code"), I(node,"away_team_goals"), "away");
        return new SoccerMatch(S(node,"match_id"), D(node,"planned_kickoff_time"), S(node,"match_status"), S(node,"minute_of_play"), S(node,"competition_name"), S(node,"season_id"), I(node,"match_day"), S(node,"stadium_name"), S(node,"stadium_city"), away, home);
    }

    private static SoccerMatch ReadDetailedMatch(JsonElement info, JsonElement homeNode, JsonElement awayNode, JsonElement root)
    {
        var environment = root.TryGetProperty("environment", out var env) ? env : default;
        var home = ReadTeam(homeNode, I(info,"home_team_goals"), "home");
        var away = ReadTeam(awayNode, I(info,"away_team_goals"), "away");
        return new SoccerMatch(S(info,"match_id"), D(info,"planned_kickoff_time"), S(info,"match_status"), S(info,"minute_of_play"), S(info,"competition_name"), S(info,"season_id"), I(info,"match_day"), S(environment,"stadium_name"), S(environment,"city"), away, home);
    }

    private static SoccerMatchConditions? ReadConditions(JsonElement root)
    {
        if (!root.TryGetProperty("environment", out var env) || env.ValueKind != JsonValueKind.Object) return null;
        var address = string.Join(", ", new[] { S(env,"stadium_address"), S(env,"city"), S(env,"postal_code") }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));
        return new SoccerMatchConditions(NDouble(env,"temperature"),NDouble(env,"air_humidity"),NDouble(env,"air_pressure"),S(env,"precipitation"),S(env,"roof"),S(env,"floodlight"),S(env,"pitch_erosion"),NI(env,"number_of_spectators"),NI(env,"stadium_capacity"),env.TryGetProperty("sold_out",out var soldOut)?soldOut.ValueKind==JsonValueKind.True?true:soldOut.ValueKind==JsonValueKind.False?false:null:null,address);
    }

    private static SoccerTeam ReadTeam(JsonElement node, int score, string role)
        => new(S(node,"team_id"), S(node,"team_name"), S(node,"team_short_name"), S(node,"team_three_letter_code"), score, role);

    private static SoccerSide ReadSide(JsonElement node, SoccerTeam team)
    {
        var players = new List<SoccerPlayer>();
        if (node.TryGetProperty("players", out var list) && list.ValueKind == JsonValueKind.Array)
            foreach (var p in list.EnumerateArray())
                players.Add(new SoccerPlayer(S(p,"person_id"), S(p,"first_name"), S(p,"last_name"), S(p,"short_name"), S(p,"playing_position"), NI(p,"shirt_number"), B(p,"starting"), B(p,"is_on_field"), B(p,"team_leader")));
        return new SoccerSide(team, S(node,"initial_line_up"), players);
    }

    private static IReadOnlyList<SoccerEvent> ReadEvents(JsonElement root)
    {
        var result = new List<SoccerEvent>();
        if (!root.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array) return result;
        foreach (var wrapper in events.EnumerateArray())
        {
            var type = S(wrapper,"type"); var sub = S(wrapper,"sub_type");
            if (!wrapper.TryGetProperty("event", out var e)) continue;
            var playerName = JoinName(S(e,"player_first_name"), S(e,"player_last_name"));
            if (string.IsNullOrWhiteSpace(playerName)) playerName = JoinName(S(e,"player_in_first_name"), S(e,"player_in_last_name"));
            var description = DescribeEvent(type, sub, e, playerName);
            result.Add(new SoccerEvent(L(e,"event_id"), type, sub, S(e,"minute_of_play"), S(e,"game_section"), ND(e,"event_time"), S(e,"team_id"), S(e,"team_name"), S(e,"player_id"), playerName, description, NDouble(e,"position_x"), NDouble(e,"position_y"), NDouble(e,"xG"), S(e,"result")));
        }
        return result;
    }

    private static string DescribeEvent(string type, string subType, JsonElement e, string player)
    {
        if (subType == "goals") return $"Goal by {player}";
        if (type == "substitutions") return $"{JoinName(S(e,"player_in_first_name"),S(e,"player_in_last_name"))} replaces {JoinName(S(e,"player_out_first_name"),S(e,"player_out_last_name"))}";
        if (type == "cards") return $"Card shown to {player}";
        if (type == "shot_at_goals") return $"{S(e,"shot_result")} by {player}".Trim();
        return string.IsNullOrWhiteSpace(player) ? type.Replace('_',' ') : $"{type.Replace('_',' ')}: {player}";
    }

    private static IReadOnlyList<SoccerTeamStatistics> ReadTeamStatistics(JsonElement root)
    {
        var output = new List<SoccerTeamStatistics>();
        if (!root.TryGetProperty("match_statistics_list", out var lists) || lists.ValueKind != JsonValueKind.Array) return output;
        foreach (var item in lists.EnumerateArray())
        {
            if (!item.TryGetProperty("match_statistics", out var match) || !match.TryGetProperty("team_statistics", out var teams)) continue;
            foreach (var t in teams.EnumerateArray()) output.Add(new SoccerTeamStatistics(S(t,"team_id"),S(t,"team_name"),S(t,"team_role"),I(t,"goals"),I(t,"shots_at_goal_sum"),I(t,"shots_on_target"),I(t,"corner_kicks_sum"),I(t,"fouls_sum"),I(t,"offsides"),I(t,"cards_yellow"),I(t,"cards_red"),I(t,"passes_sum"),NDouble(t,"passes_conversion_rate") ?? 0,NDouble(t,"possession_ratio") ?? 0,NDouble(t,"xG") ?? 0,I(t,"goalkeeper_saves")));
        }
        return output;
    }

    private static string JoinName(string first, string last) => string.Join(' ', new[]{first,last}.Where(x=>!string.IsNullOrWhiteSpace(x)));
    private static string S(JsonElement n,string p) => n.ValueKind==JsonValueKind.Object && n.TryGetProperty(p,out var v) ? v.ValueKind==JsonValueKind.String ? v.GetString()??"" : v.ToString() : "";
    private static int I(JsonElement n,string p) => n.ValueKind==JsonValueKind.Object && n.TryGetProperty(p,out var v) && v.TryGetInt32(out var x) ? x : 0;
    private static long L(JsonElement n,string p) => n.ValueKind==JsonValueKind.Object && n.TryGetProperty(p,out var v) && v.TryGetInt64(out var x) ? x : 0;
    private static int? NI(JsonElement n,string p) => n.ValueKind==JsonValueKind.Object && n.TryGetProperty(p,out var v) && v.TryGetInt32(out var x) ? x : null;
    private static double? NDouble(JsonElement n,string p) => n.ValueKind==JsonValueKind.Object && n.TryGetProperty(p,out var v) && v.TryGetDouble(out var x) ? x : null;
    private static DateTimeOffset D(JsonElement n,string p) => ND(n,p) ?? DateTimeOffset.MinValue;
    private static DateTimeOffset? ND(JsonElement n,string p) => DateTimeOffset.TryParse(S(n,p), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var x) ? x : null;
    private static bool B(JsonElement n,string p) { if(n.ValueKind!=JsonValueKind.Object || !n.TryGetProperty(p,out var v)) return false; return v.ValueKind==JsonValueKind.True || (v.ValueKind==JsonValueKind.String && bool.TryParse(v.GetString(),out var x) && x); }
}
