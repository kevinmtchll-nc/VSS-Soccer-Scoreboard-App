using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Options;

namespace VS.Soccer;

public sealed record SoccerLogo(string ContentType, byte[] Bytes, string Copyright);

public sealed class SportradarLogoClient(HttpClient http, IOptionsMonitor<SportradarImagesOptions> options)
{
    private readonly SemaphoreSlim _manifestGate = new(1, 1);
    private readonly ConcurrentDictionary<string, SoccerLogo> _images = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<LogoAsset> _assets = [];
    private DateTimeOffset _manifestExpires;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.CurrentValue.ApiKey);

    public async Task<SoccerLogo?> GetTeamLogoAsync(string teamName, string? teamCode, CancellationToken ct)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(teamName)) return null;
        var key = Normalize(teamName);
        if (_images.TryGetValue(key, out var cached)) return cached;
        await EnsureManifestAsync(ct);
        var code = Normalize(teamCode ?? "");
        var asset = _assets
            .Where(x => x.Type is "primary" or "global")
            .OrderByDescending(x => x.Type == "primary")
            .FirstOrDefault(x => NameMatches(key, Normalize(x.TeamName), code));
        if (asset is null) return null;

        using var request = CreateRequest(asset.Path);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length is 0 or > 5_000_000) throw new InvalidDataException("Sportradar returned an invalid logo file size.");
        var contentType = response.Content.Headers.ContentType?.MediaType ?? ContentTypeFromPath(asset.Path);
        var result = new SoccerLogo(contentType, bytes, asset.Copyright);
        _images[key] = result;
        return result;
    }

    private async Task EnsureManifestAsync(CancellationToken ct)
    {
        if (_assets.Count > 0 && _manifestExpires > DateTimeOffset.UtcNow) return;
        await _manifestGate.WaitAsync(ct);
        try
        {
            if (_assets.Count > 0 && _manifestExpires > DateTimeOffset.UtcNow) return;
            var o = options.CurrentValue;
            var level = o.AccessLevel.Equals("t", StringComparison.OrdinalIgnoreCase) ? "t" : "p";
            var year = level == "p" ? $"/{Math.Clamp(o.ManifestYear, 2013, DateTime.UtcNow.Year + 1)}" : "";
            using var request = CreateRequest($"soccer-images-{level}3/{Uri.EscapeDataString(o.Provider)}/{Uri.EscapeDataString(o.League)}/logos{year}/manifest.xml");
            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var document = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
            _assets = document.Descendants().Where(x => x.Name.LocalName == "asset").Select(ReadAsset).Where(x => x is not null).Cast<LogoAsset>().ToList();
            _manifestExpires = DateTimeOffset.UtcNow.AddHours(12);
            _images.Clear();
        }
        finally { _manifestGate.Release(); }
    }

    private HttpRequestMessage CreateRequest(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path.TrimStart('/'));
        request.Headers.TryAddWithoutValidation("x-api-key", options.CurrentValue.ApiKey.Trim());
        request.Headers.Accept.ParseAdd("image/*, application/xml;q=0.9");
        return request;
    }

    private static LogoAsset? ReadAsset(XElement asset)
    {
        var reference = asset.Descendants().FirstOrDefault(x => x.Name.LocalName == "ref" && ((string?)x.Attribute("type") is "organization" or "team"))
            ?? asset.Descendants().FirstOrDefault(x => x.Name.LocalName == "ref");
        var links = asset.Descendants().Where(x => x.Name.LocalName == "link")
            .Select(x => new { Path = (string?)x.Attribute("href") ?? "", Width = ParseInt((string?)x.Attribute("width")), Height = ParseInt((string?)x.Attribute("height")) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Path)).ToList();
        var link = links.Where(x => x.Path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).OrderBy(x => Math.Abs(Math.Max(x.Width, x.Height) - 500)).FirstOrDefault()
            ?? links.OrderBy(x => Math.Abs(Math.Max(x.Width, x.Height) - 500)).FirstOrDefault();
        var name = (string?)reference?.Attribute("name") ?? (string?)asset.Attribute("title") ?? "";
        return link is null || string.IsNullOrWhiteSpace(name) ? null : new LogoAsset(name, ((string?)asset.Attribute("type") ?? "").ToLowerInvariant(), link.Path.TrimStart('/'), (string?)asset.Attribute("copyright") ?? "Associated Press");
    }

    private static bool NameMatches(string requested, string candidate, string code) => requested == candidate || requested.Contains(candidate) || candidate.Contains(requested) || (!string.IsNullOrWhiteSpace(code) && candidate.Split(' ').Any(x => Normalize(x) == code));
    private static string Normalize(string value) { var normalized = value.Normalize(NormalizationForm.FormD); return new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c)).Select(char.ToLowerInvariant).ToArray()); }
    private static int ParseInt(string? value) => int.TryParse(value, out var result) ? result : 0;
    private static string ContentTypeFromPath(string path) => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp" : "image/jpeg";
    private sealed record LogoAsset(string TeamName, string Type, string Path, string Copyright);
}
