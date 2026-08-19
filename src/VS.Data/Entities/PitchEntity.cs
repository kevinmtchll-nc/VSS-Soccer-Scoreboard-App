namespace VS.Data.Entities;

public sealed class PitchEntity
{
    public long Id { get; set; }

    public long GamePk { get; set; }
    public GameEntity Game { get; set; } = null!;

    public string PlayId { get; set; } = "";
    public int AtBatIndex { get; set; }
    public int PitchNumber { get; set; }

    public string PitchCode { get; set; } = "";
    public string PitchType { get; set; } = "";
    public string Result { get; set; } = "";

    public double? StartSpeedMph { get; set; }
    public double? EndSpeedMph { get; set; }
    public double? PlateX { get; set; }
    public double? PlateZ { get; set; }
    public double? StrikeZoneTop { get; set; }
    public double? StrikeZoneBottom { get; set; }
    public double? SpinRate { get; set; }
    public double? HorizontalBreak { get; set; }
    public double? VerticalBreak { get; set; }
    public double? Extension { get; set; }
    public int? Zone { get; set; }

    public int? BatterId { get; set; }
    public string Batter { get; set; } = "";
    public int? PitcherId { get; set; }
    public string Pitcher { get; set; } = "";
    public string BatSide { get; set; } = "";
    public string PitchHand { get; set; } = "";
}
