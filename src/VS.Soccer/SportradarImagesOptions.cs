namespace VS.Soccer;

public sealed class SportradarImagesOptions
{
    public const string SectionName = "SportradarImages";
    public string ApiKey { get; set; } = "";
    public string AccessLevel { get; set; } = "p";
    public string Provider { get; set; } = "ap";
    public string League { get; set; } = "mls";
    public int ManifestYear { get; set; } = DateTime.UtcNow.Year;
}
