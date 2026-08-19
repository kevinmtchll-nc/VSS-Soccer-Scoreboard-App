namespace VS.Data.Entities;

public sealed class IngestionLogEntity
{
    public long Id { get; set; }
    public long GamePk { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public int PitchCountSeen { get; set; }
    public int PitchCountInserted { get; set; }
    public string Result { get; set; } = "";
    public string Message { get; set; } = "";
}
