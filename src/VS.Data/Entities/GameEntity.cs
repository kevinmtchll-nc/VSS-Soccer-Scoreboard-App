namespace VS.Data.Entities;

public sealed class GameEntity
{
    public long GamePk { get; set; }
    public DateTimeOffset GameDate { get; set; }

    public int AwayTeamId { get; set; }
    public string AwayTeam { get; set; } = "";
    public int HomeTeamId { get; set; }
    public string HomeTeam { get; set; } = "";

    public int AwayScore { get; set; }
    public int HomeScore { get; set; }

    public string Status { get; set; } = "";
    public string DetailedStatus { get; set; } = "";
    public string Venue { get; set; } = "";

    public bool IsFinal { get; set; }
    public DateTimeOffset LastIngestedAtUtc { get; set; }

    public List<PitchEntity> Pitches { get; set; } = [];
}
