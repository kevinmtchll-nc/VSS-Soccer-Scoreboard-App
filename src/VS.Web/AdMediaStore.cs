namespace VS.Web;

public sealed class AdMediaStore
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".mp4", ".webm" };
    private readonly string _directory;

    public AdMediaStore(string dataDirectory)
    {
        _directory = Path.Combine(dataDirectory, "Advertising");
        Directory.CreateDirectory(_directory);
    }

    public string DirectoryPath => _directory;

    public object Status() => new { rail = Describe("rail"), banner = Describe("banner") };

    public async Task<object> SaveAsync(string slot, IFormFile file, CancellationToken cancellationToken)
    {
        slot = NormalizeSlot(slot);
        if (file.Length <= 0) throw new ArgumentException("Choose an advertising image or video to upload.");
        if (file.Length > 250L * 1024 * 1024) throw new ArgumentException("Advertising media must be 250 MB or smaller.");
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension)) throw new ArgumentException("Supported formats are PNG, JPG, WebP, GIF, MP4, and WebM.");
        Delete(slot);
        var target = Path.Combine(_directory, slot + extension.ToLowerInvariant());
        await using var stream = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await file.CopyToAsync(stream, cancellationToken);
        return Describe(slot)!;
    }

    public void Delete(string slot)
    {
        slot = NormalizeSlot(slot);
        foreach (var path in Directory.EnumerateFiles(_directory, slot + ".*")) File.Delete(path);
    }

    private object? Describe(string slot)
    {
        var path = Directory.EnumerateFiles(_directory, slot + ".*").FirstOrDefault();
        if (path is null) return null;
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return new
        {
            slot,
            fileName = Path.GetFileName(path),
            url = "/media/ads/" + Uri.EscapeDataString(Path.GetFileName(path)),
            mediaType = extension is ".mp4" or ".webm" ? "video" : "image"
        };
    }

    private static string NormalizeSlot(string slot) => slot.ToLowerInvariant() switch
    {
        "rail" => "rail",
        "banner" => "banner",
        _ => throw new ArgumentException("Advertising slot must be rail or banner.")
    };
}
