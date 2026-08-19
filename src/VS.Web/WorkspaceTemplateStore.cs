using System.Text.Json;

namespace VS.Web;

public sealed record WorkspaceTile(string Id, double X, double Y, double Width, double Height, int Z, bool Visible = true);
public sealed record WorkspaceTemplate(string Id, string Name, IReadOnlyList<WorkspaceTile> Tiles);

public sealed class WorkspaceTemplateStore
{
    private readonly string _path;
    private readonly object _gate = new();

    public WorkspaceTemplateStore(string dataDirectory) => _path = Path.Combine(dataDirectory, "workspace-templates.json");

    public IReadOnlyList<WorkspaceTemplate> List()
    {
        lock (_gate) return Load();
    }

    public WorkspaceTemplate? Get(string id)
    {
        lock (_gate) return Load().FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public WorkspaceTemplate Save(WorkspaceTemplate value)
    {
        var name = (value.Name ?? "").Trim();
        if (name.Length is < 1 or > 80) throw new ArgumentException("Template name must be between 1 and 80 characters.");
        var id = string.IsNullOrWhiteSpace(value.Id) ? Slug(name) : Slug(value.Id);
        var required = new[] { "live", "recent", "linescore", "awaybox", "homebox", "scoring" };
        var supplied = value.Tiles?.ToDictionary(tile => tile.Id, StringComparer.OrdinalIgnoreCase) ?? [];
        if (supplied.TryGetValue("matchup", out var oldMatchup))
        {
            var oldLive = supplied.GetValueOrDefault("live") ?? Defaults().Tiles[0];
            var x = Math.Min(oldLive.X, oldMatchup.X); var y = Math.Min(oldLive.Y, oldMatchup.Y);
            supplied["live"] = oldLive with { X=x, Y=y, Width=Math.Max(oldLive.X+oldLive.Width,oldMatchup.X+oldMatchup.Width)-x, Height=Math.Max(oldLive.Y+oldLive.Height,oldMatchup.Y+oldMatchup.Height)-y };
        }
        if (supplied.TryGetValue("boxscore", out var legacyBox))
        {
            supplied.TryAdd("awaybox", legacyBox with { Id = "awaybox", Width = legacyBox.Width / 2 });
            supplied.TryAdd("homebox", legacyBox with { Id = "homebox", X = legacyBox.X + legacyBox.Width / 2, Width = legacyBox.Width / 2 });
        }
        var tiles = required.Select((tileId, index) =>
        {
            var tile = supplied.GetValueOrDefault(tileId) ?? Defaults().Tiles[index];
            return tile with
            {
                Id = tileId,
                X = Clamp(tile.X, 0, 95), Y = Clamp(tile.Y, 0, 95),
                Width = Clamp(tile.Width, 5, 100), Height = Clamp(tile.Height, 5, 100),
                Z = Math.Clamp(tile.Z, 1, 100)
            };
        }).ToList();
        tiles = tiles.Select(tile => tile with { Width = Math.Min(tile.Width, 100 - tile.X), Height = Math.Min(tile.Height, 100 - tile.Y) }).ToList();
        var saved = new WorkspaceTemplate(id, name, tiles);
        lock (_gate)
        {
            var all = Load().Where(item => !item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).ToList();
            all.Add(saved);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
        }
        return saved;
    }

    public bool Delete(string id)
    {
        lock (_gate)
        {
            var all = Load(); var updated = all.Where(item => !item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).ToList();
            if (updated.Count == all.Count) return false;
            File.WriteAllText(_path, JsonSerializer.Serialize(updated, new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
    }

    public static WorkspaceTemplate Defaults() => new("default", "Default Workspace", [
        new("live", 20.5, 13, 58, 57, 4), new("recent", 54, 71, 46, 29, 8), new("linescore", 20.5, 0, 58, 12, 3),
        new("awaybox", 0, 0, 20, 70, 2), new("homebox", 79, 0, 21, 70, 5), new("scoring", 0, 71, 53.5, 29, 6)
    ]);

    private List<WorkspaceTemplate> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<WorkspaceTemplate>>(File.ReadAllText(_path)) ?? [] : []; }
        catch { return []; }
    }
    private static string Slug(string value) => string.Concat(value.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')).Trim('-');
    private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
}
