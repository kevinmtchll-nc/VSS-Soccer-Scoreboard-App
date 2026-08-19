namespace VS.Data.Entities;

public sealed class SoccerMatchSnapshotEntity
{
    public string MatchId { get; set; } = "";
    public DateOnly MatchDate { get; set; }
    public DateTimeOffset PlannedKickoff { get; set; }
    public string Status { get; set; } = "";
    public string Competition { get; set; } = "";
    public string AwayTeam { get; set; } = "";
    public string HomeTeam { get; set; } = "";
    public int AwayScore { get; set; }
    public int HomeScore { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset CapturedAtUtc { get; set; }
}
