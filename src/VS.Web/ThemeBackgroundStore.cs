namespace VS.Web;

public sealed class ThemeBackgroundStore
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };
    private readonly string _directory;
    public ThemeBackgroundStore(string dataDirectory) { _directory = Path.Combine(dataDirectory, "Themes"); Directory.CreateDirectory(_directory); }
    public string DirectoryPath => _directory;
    public object Status() => Describe() ?? new { };
    public async Task<object> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0 || file.Length > 50L * 1024 * 1024) throw new ArgumentException("Background image must be between 1 byte and 50 MB.");
        var extension = Path.GetExtension(file.FileName); if (!Allowed.Contains(extension)) throw new ArgumentException("Background must be PNG, JPG, or WebP.");
        Delete(); var target = Path.Combine(_directory, "gamecenter-background" + extension.ToLowerInvariant());
        await using var stream = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.Read); await file.CopyToAsync(stream, cancellationToken);
        return Describe()!;
    }
    public void Delete() { foreach (var path in Directory.EnumerateFiles(_directory, "gamecenter-background.*")) File.Delete(path); }
    private object? Describe()
    {
        var path = Directory.EnumerateFiles(_directory, "gamecenter-background.*").FirstOrDefault();
        return path is null ? null : new { fileName = Path.GetFileName(path), url = "/media/themes/" + Uri.EscapeDataString(Path.GetFileName(path)) };
    }
}
