using System.Text.Json;

namespace VS.Web;

public sealed record VideoOutputSettings(
    string FfmpegPath = @"C:\ffmpeg\bin\ffmpeg.exe",
    string Protocol = "udp",
    string Destination = "239.10.10.10",
    int Port = 5004,
    string Scene = "gamecenter-standard",
    string? TemplateId = null,
    long? GamePk = null,
    int Width = 1920,
    int Height = 1080,
    int FrameRate = 30,
    int VideoBitrateKbps = 6000,
    int SrtLatencyMs = 120);

public sealed record VideoWorkerStatus(
    bool Connected = false,
    bool Running = false,
    string Message = "The video helper is not connected.",
    DateTimeOffset? LastSeenUtc = null,
    int? FfmpegProcessId = null,
    string? OutputUrl = null);

public sealed class VideoOutputCoordinator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _sync = new();
    private readonly string _path;
    private VideoOutputSettings _settings;
    private VideoWorkerStatus _worker = new();
    private bool _desiredRunning;
    private long _revision = 1;

    public VideoOutputCoordinator(string dataDirectory)
    {
        _path = Path.Combine(dataDirectory, "video-output.json");
        _settings = Load() ?? new VideoOutputSettings();
    }

    public object Snapshot()
    {
        lock (_sync)
        {
            var connected = _worker.LastSeenUtc is { } seen && DateTimeOffset.UtcNow - seen < TimeSpan.FromSeconds(10);
            return new
            {
                settings = _settings,
                desiredRunning = _desiredRunning,
                revision = _revision,
                worker = _worker with { Connected = connected }
            };
        }
    }

    public object Command()
    {
        lock (_sync)
            return new { settings = _settings, desiredRunning = _desiredRunning, revision = _revision };
    }

    public void Save(VideoOutputSettings settings)
    {
        Validate(settings);
        lock (_sync)
        {
            _settings = settings;
            _revision++;
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions));
        }
    }

    public void SetDesiredRunning(bool running)
    {
        lock (_sync)
        {
            _desiredRunning = running;
            _revision++;
            _worker = _worker with
            {
                Running = running && _worker.Running,
                Message = running ? "Starting the dedicated GameCenter video output..." : "Stopping video output...",
                FfmpegProcessId = running ? _worker.FfmpegProcessId : null
            };
        }
    }

    public void Report(VideoWorkerStatus status)
    {
        lock (_sync)
            _worker = status with { Connected = true, LastSeenUtc = DateTimeOffset.UtcNow };
    }

    private VideoOutputSettings? Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<VideoOutputSettings>(File.ReadAllText(_path))
                : null;
        }
        catch { return null; }
    }

    private static void Validate(VideoOutputSettings value)
    {
        if (value.Protocol is not ("udp" or "srt")) throw new ArgumentException("Protocol must be UDP multicast or SRT.");
        if (string.IsNullOrWhiteSpace(value.Destination)) throw new ArgumentException("A destination address is required.");
        if (value.Port is < 1 or > 65535) throw new ArgumentException("Port must be between 1 and 65535.");
        if (value.Width is < 640 or > 3840 || value.Height is < 360 or > 2160) throw new ArgumentException("Resolution must be between 640x360 and 3840x2160.");
        if (value.FrameRate is < 10 or > 60) throw new ArgumentException("Frame rate must be between 10 and 60.");
        if (value.VideoBitrateKbps is < 500 or > 50000) throw new ArgumentException("Video bitrate must be between 500 and 50,000 Kbps.");
        if (value.Protocol == "udp" && (!System.Net.IPAddress.TryParse(value.Destination, out var ip) || ip.GetAddressBytes()[0] is < 224 or > 239))
            throw new ArgumentException("UDP destination must be an IPv4 multicast address from 224.0.0.0 through 239.255.255.255.");
    }
}
