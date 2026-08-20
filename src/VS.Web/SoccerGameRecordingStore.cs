using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using VS.Core.Models;
using VS.Soccer;

namespace VS.Web;

public sealed record SoccerRecordingSummary(string MatchId, string AwayTeam, string HomeTeam, DateTimeOffset Kickoff, DateTimeOffset SavedAt, int Snapshots, bool IsRecording);

public sealed class SoccerGameRecordingStore
{
    private readonly string _root;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public SoccerGameRecordingStore(string dataDirectory) { _root=Path.Combine(dataDirectory,"GameRecordings");Directory.CreateDirectory(_root); }
    public async Task SaveAsync(SoccerMatchCenter center, bool snapshot, CancellationToken ct)
    {
        var folder=Folder(center.Match.MatchId);Directory.CreateDirectory(folder);
        var json=JsonSerializer.Serialize(center,JsonOptions);await File.WriteAllTextAsync(Path.Combine(folder,"full-game.json"),json,ct);
        if(snapshot){var snapshots=Path.Combine(folder,"snapshots");Directory.CreateDirectory(snapshots);await File.WriteAllTextAsync(Path.Combine(snapshots,$"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}.json"),json,ct);}
    }
    public async Task<SoccerMatchCenter?> GetAsync(string matchId,CancellationToken ct){var path=Path.Combine(Folder(matchId),"full-game.json");return File.Exists(path)?JsonSerializer.Deserialize<SoccerMatchCenter>(await File.ReadAllTextAsync(path,ct),JsonOptions):null;}
    public async Task<IReadOnlyList<SoccerRecordingSummary>> ListAsync(IReadOnlySet<string> active,CancellationToken ct)
    {
        var rows=new List<SoccerRecordingSummary>();foreach(var directory in Directory.EnumerateDirectories(_root)){var path=Path.Combine(directory,"full-game.json");if(!File.Exists(path))continue;try{var center=JsonSerializer.Deserialize<SoccerMatchCenter>(await File.ReadAllTextAsync(path,ct),JsonOptions);if(center is null)continue;rows.Add(new(center.Match.MatchId,center.Match.Away.Name,center.Match.Home.Name,center.Match.PlannedKickoff,File.GetLastWriteTimeUtc(path),Directory.Exists(Path.Combine(directory,"snapshots"))?Directory.EnumerateFiles(Path.Combine(directory,"snapshots"),"*.json").Count():0,active.Contains(center.Match.MatchId)));}catch{}}
        return rows.OrderByDescending(row=>row.Kickoff).ToList();
    }
    private string Folder(string matchId){var safe=new string(matchId.Where(character=>char.IsLetterOrDigit(character)||character is '-' or '_').ToArray());return Path.Combine(_root,safe);}
}

public sealed class SoccerGameRecorder(IServiceScopeFactory scopes,SoccerGameRecordingStore store)
{
    private readonly ConcurrentDictionary<string,CancellationTokenSource> _active=new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlySet<string> ActiveIds=>_active.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    public bool Start(string matchId,DateOnly date)
    {
        var cts=new CancellationTokenSource();if(!_active.TryAdd(matchId,cts)){cts.Dispose();return false;}
        _=Task.Run(async()=>{try{while(!cts.IsCancellationRequested){using var scope=scopes.CreateScope();var soccer=scope.ServiceProvider.GetRequiredService<ISoccerStatsClient>();var center=await soccer.GetMatchCenterAsync(matchId,date,cts.Token);await store.SaveAsync(center,true,cts.Token);if(center.Match.Status.Contains("final",StringComparison.OrdinalIgnoreCase))break;await Task.Delay(TimeSpan.FromSeconds(30),cts.Token);}}catch(OperationCanceledException){}catch{}finally{_active.TryRemove(matchId,out _);cts.Dispose();}},CancellationToken.None);return true;
    }
    public bool Stop(string matchId){if(!_active.TryRemove(matchId,out var cts))return false;cts.Cancel();return true;}
}

public sealed class SoccerReplayCoordinator(SoccerGameRecordingStore store)
{
    private readonly object _sync=new();private string? _matchId;private bool _playing;private double _baseMinute;private double _speed=1;private DateTimeOffset _changedAt=DateTimeOffset.UtcNow;
    public object Status(){lock(_sync)return new{matchId=_matchId,playing=_playing,minute=Minute(),speed=_speed};}
    public void Start(string matchId,double speed){lock(_sync){_matchId=matchId;_speed=Math.Clamp(speed,.25,60);_playing=true;_changedAt=DateTimeOffset.UtcNow;}}
    public void Pause(){lock(_sync){_baseMinute=Minute();_playing=false;_changedAt=DateTimeOffset.UtcNow;}}
    public void Reset(){lock(_sync){_baseMinute=0;_playing=false;_changedAt=DateTimeOffset.UtcNow;}}
    public async Task<SoccerMatchCenter?> CurrentAsync(string matchId,CancellationToken ct)
    {
        var full=await store.GetAsync(matchId,ct);if(full is null)return null;double minute;lock(_sync){if(!string.Equals(_matchId,matchId,StringComparison.OrdinalIgnoreCase)){_matchId=matchId;_baseMinute=0;_playing=false;_changedAt=DateTimeOffset.UtcNow;}minute=Minute();}
        var events=full.Events.Where(value=>EventMinute(value.Minute)<=minute).ToList();var awayGoals=events.Count(value=>value.SubType=="goals"&&value.TeamId==full.Match.Away.TeamId);var homeGoals=events.Count(value=>value.SubType=="goals"&&value.TeamId==full.Match.Home.TeamId);var maximum=Math.Max(90,full.Events.Select(value=>EventMinute(value.Minute)).DefaultIfEmpty(90).Max());var complete=minute>=maximum;
        var away=full.Match.Away with{Score=complete?full.Match.Away.Score:awayGoals};var home=full.Match.Home with{Score=complete?full.Match.Home.Score:homeGoals};var match=full.Match with{Away=away,Home=home,Status=complete?"Replay Final":"Replay",Minute=Math.Floor(Math.Min(minute,maximum)).ToString(System.Globalization.CultureInfo.InvariantCulture)};
        return full with{Match=match,Events=events.OrderByDescending(value=>EventMinute(value.Minute)).ToList(),UpdatedAt=DateTimeOffset.UtcNow};
    }
    private double Minute()=>_baseMinute+(_playing?(DateTimeOffset.UtcNow-_changedAt).TotalMinutes*_speed:0);
    private static int EventMinute(string value){var parts=Regex.Matches(value??"",@"\d+").Cast<Match>().Select(match=>int.Parse(match.Value)).ToArray();return parts.Length==0?0:parts.Sum();}
}
