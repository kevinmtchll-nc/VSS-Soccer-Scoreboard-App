namespace VS.Web;

public sealed class SoccerTeamLogoStore
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".webp"] = "image/webp", [".svg"] = "image/svg+xml"
    };
    public string DirectoryPath { get; }

    public SoccerTeamLogoStore(string dataDirectory)
    {
        DirectoryPath = Path.Combine(dataDirectory, "TeamLogos");
        Directory.CreateDirectory(DirectoryPath);
    }

    public async Task<TeamLogoFile?> ReadAsync(string? code, CancellationToken ct)
    {
        var safeCode = SafeCode(code);
        if (safeCode.Length == 0) return null;
        foreach (var pair in ContentTypes)
        {
            var path = Path.Combine(DirectoryPath, safeCode + pair.Key);
            if (File.Exists(path)) return new TeamLogoFile(await File.ReadAllBytesAsync(path, ct), pair.Value, "Local override");
        }
        return null;
    }

    public async Task<object> SaveAsync(string? code, IFormFile file, CancellationToken ct)
    {
        var safeCode = SafeCode(code);
        if (safeCode.Length == 0) throw new InvalidOperationException("A valid MLS team code is required.");
        if (file.Length is <= 0 or > 5 * 1024 * 1024) throw new InvalidOperationException("The team image must be between 1 byte and 5 MB.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!ContentTypes.ContainsKey(extension)) throw new InvalidOperationException("Use a PNG, JPG, WebP, or SVG team image.");
        foreach (var existingExtension in ContentTypes.Keys) File.Delete(Path.Combine(DirectoryPath, safeCode + existingExtension));
        var destination = Path.Combine(DirectoryPath, safeCode + extension);
        await using var output = File.Create(destination);
        await file.CopyToAsync(output, ct);
        return new { code = safeCode, file = Path.GetFileName(destination), bytes = file.Length };
    }

    private static string SafeCode(string? value) => new((value ?? "").Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).Take(8).ToArray());
}
