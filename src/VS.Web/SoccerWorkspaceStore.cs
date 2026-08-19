using System.Text.Json;

namespace VS.Web;

public sealed record SoccerWorkspaceTile(string Id, int Order, bool Visible = true);
public sealed record SoccerWorkspace(string Id, string Name, IReadOnlyList<SoccerWorkspaceTile> Tiles);

public sealed class SoccerWorkspaceStore
{
    private static readonly string[] TileIds = ["away", "timeline", "home", "stats"];
    private readonly string _path;
    private readonly object _gate = new();
    public SoccerWorkspaceStore(string dataDirectory) => _path = Path.Combine(dataDirectory, "soccer-workspaces.json");
    public static SoccerWorkspace Default() => new("default", "Default MatchCenter", TileIds.Select((id, i) => new SoccerWorkspaceTile(id, i)).ToList());
    public IReadOnlyList<SoccerWorkspace> List() { lock (_gate) return Load(); }
    public SoccerWorkspace? Get(string id) { lock (_gate) return Load().FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)); }
    public SoccerWorkspace Save(SoccerWorkspace value)
    {
        var name = (value.Name ?? "").Trim(); if (name.Length is < 1 or > 80) throw new ArgumentException("Workspace name must be between 1 and 80 characters.");
        var id = Slug(string.IsNullOrWhiteSpace(value.Id) ? name : value.Id);
        var supplied = (value.Tiles ?? []).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var tiles = TileIds.Select((tileId, index) => supplied.TryGetValue(tileId, out var tile) ? tile with { Id = tileId, Order = Math.Clamp(tile.Order, 0, TileIds.Length - 1) } : new SoccerWorkspaceTile(tileId, index)).OrderBy(x => x.Order).Select((x, i) => x with { Order = i }).ToList();
        var saved = new SoccerWorkspace(id, name, tiles);
        lock (_gate) { var all = Load().Where(x => !x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).Append(saved).ToList(); Directory.CreateDirectory(Path.GetDirectoryName(_path)!); File.WriteAllText(_path, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true })); }
        return saved;
    }
    public bool Delete(string id) { lock (_gate) { var all = Load(); var next = all.Where(x => !x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).ToList(); if (all.Count == next.Count) return false; File.WriteAllText(_path, JsonSerializer.Serialize(next, new JsonSerializerOptions { WriteIndented = true })); return true; } }
    private List<SoccerWorkspace> Load() { try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<SoccerWorkspace>>(File.ReadAllText(_path)) ?? [] : []; } catch { return []; } }
    private static string Slug(string value) => string.Concat(value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
}
