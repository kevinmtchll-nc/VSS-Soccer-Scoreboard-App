using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

var serverOverride = Environment.GetEnvironmentVariable("VITEC_SCOREBOARD_SERVER");
var mutexName = string.IsNullOrWhiteSpace(serverOverride)
    ? @"Local\VITECSoccerScoreboard.VideoOutput"
    : @"Local\VITECSoccerScoreboard.VideoOutput.Test";
using var instanceMutex = new Mutex(true, mutexName, out var ownsInstance);
if (!ownsInstance) return;

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
Process? ffmpeg = null;
VS.VideoOutput.EmbeddedRenderer? renderer = null;
CancellationTokenSource? captureCancellation = null;
Task? captureTask = null;
long activeRevision = 0;
DateTime nextStartAttemptUtc = DateTime.MinValue;
string message = "Video helper is ready.";
// The helper runs in the signed-in user's session. Keep Chromium's disposable
// profile in that session's writable temporary directory; an elevated install
// can otherwise leave LocalAppData folders that the normal user cannot reuse.

while (true)
{
    try
    {
        var server = ResolveServer();
        var command = await http.GetFromJsonAsync<WorkerCommand>($"{server}/api/video/worker/command");
        if (command is not null)
        {
            if (captureTask is { IsFaulted: true })
            {
                message = $"Embedded capture stopped: {captureTask.Exception?.GetBaseException().Message}";
                StopProcesses();
                nextStartAttemptUtc = DateTime.UtcNow.AddSeconds(15);
            }
            var encoderExited = ffmpeg is { HasExited: true };
            if (!command.DesiredRunning)
            {
                StopProcesses();
                message = "Video output is stopped.";
                activeRevision = command.Revision;
            }
            // If FFmpeg exits, leave the browser pump alive long enough to hit
            // the closed input pipe and report FFmpeg's actual stderr. An
            // immediate restart here would kill Edge first and mask the root
            // cause as a renderer disconnect.
            else if ((command.Revision != activeRevision || ffmpeg is null) && DateTime.UtcNow >= nextStartAttemptUtc)
            {
                StopProcesses();
                activeRevision = command.Revision;
                (renderer, ffmpeg, message) = await StartOutput(command.Settings, server);
                nextStartAttemptUtc = DateTime.MinValue;
            }

            var outputUrl = BuildOutputUrl(command.Settings);
            encoderExited = ffmpeg is { HasExited: true };
            var reportedMessage = encoderExited && !message.StartsWith("Browser capture stopped:", StringComparison.Ordinal)
                ? $"FFmpeg stopped unexpectedly (exit code {ffmpeg?.ExitCode}). Check the FFmpeg path and destination."
                : message;
            await http.PostAsJsonAsync($"{server}/api/video/worker/status", new WorkerStatus(
                Running: ffmpeg is { HasExited: false },
                Message: reportedMessage,
                FfmpegProcessId: ffmpeg is { HasExited: false } ? ffmpeg.Id : null,
                OutputUrl: outputUrl));
        }
    }
    catch (Exception ex)
    {
        // A temporary status/API failure must not tear down an otherwise
        // healthy multicast or SRT encoder. Keep the renderer running and
        // retry control-plane communication on the next polling interval.
        message = $"Video control connection temporarily unavailable: {ex.Message}";
        nextStartAttemptUtc = DateTime.UtcNow.AddSeconds(15);
    }

    await Task.Delay(2000);
}

async Task<(VS.VideoOutput.EmbeddedRenderer renderer, Process ffmpegProcess, string status)> StartOutput(VideoSettings settings, string server)
{
    if (!File.Exists(settings.FfmpegPath)) throw new FileNotFoundException("FFmpeg was not found at the configured path.", settings.FfmpegPath);
    var sceneUrl = $"{server}/output.html?scene={Uri.EscapeDataString(settings.Scene)}" +
                   (settings.Scene != "scoreboard" && !string.IsNullOrWhiteSpace(settings.MatchId) ? $"&matchId={Uri.EscapeDataString(settings.MatchId)}" : "") +
                   (settings.Scene == "game-workspace" && !string.IsNullOrWhiteSpace(settings.TemplateId) ? $"&template={Uri.EscapeDataString(settings.TemplateId)}" : "");
    var renderWidth = settings.Width;
    var renderHeight = settings.Height;
    var embeddedRenderer = await VS.VideoOutput.EmbeddedRenderer.CreateAsync(sceneUrl, renderWidth, renderHeight);
    try
    {
        var ffmpegInfo = new ProcessStartInfo(settings.FfmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true
        };
        foreach (var argument in BuildArguments(settings)) ffmpegInfo.ArgumentList.Add(argument);
        var ffmpegProcess = Process.Start(ffmpegInfo) ?? throw new InvalidOperationException("Unable to start FFmpeg.");
        captureCancellation = new CancellationTokenSource();
        captureTask = PumpBrowserFramesAsync(embeddedRenderer, ffmpegProcess, settings, captureCancellation.Token);
        return (embeddedRenderer, ffmpegProcess, $"Streaming {settings.Scene} to {BuildOutputUrl(settings)}");
    }
    catch
    {
        await embeddedRenderer.DisposeAsync();
        throw;
    }
}

string ResolveServer()
{
    var overrideServer = Environment.GetEnvironmentVariable("VITEC_SCOREBOARD_SERVER");
    if (Uri.TryCreate(overrideServer, UriKind.Absolute, out var overrideUri))
        return overrideUri.GetLeftPart(UriPartial.Authority);
    try
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VITEC Soccer Scoreboard", "vssettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var configured = document.RootElement.GetProperty("VS").GetProperty("ListenUrl").GetString();
        if (Uri.TryCreate(configured?.Replace("0.0.0.0", "localhost"), UriKind.Absolute, out var uri))
            return uri.GetLeftPart(UriPartial.Authority);
    }
    catch { }
    return "http://localhost:5100";
}

IEnumerable<string> BuildArguments(VideoSettings s)
{
    yield return "-hide_banner"; yield return "-loglevel"; yield return "warning";
    yield return "-f"; yield return "image2pipe"; yield return "-vcodec"; yield return "mjpeg";
    yield return "-framerate"; yield return s.FrameRate.ToString(); yield return "-i"; yield return "pipe:0";
    yield return "-vf"; yield return $"scale={s.Width}:{s.Height}:force_original_aspect_ratio=decrease,pad={s.Width}:{s.Height}:(ow-iw)/2:(oh-ih)/2:black,format=yuv420p";
    yield return "-an"; yield return "-c:v"; yield return "libx264"; yield return "-preset"; yield return "veryfast"; yield return "-tune"; yield return "zerolatency";
    // Repeat SPS/PPS with every keyframe so multicast and SRT receivers can
    // begin decoding cleanly even when they join an already-running stream.
    yield return "-x264-params"; yield return "repeat-headers=1:aud=1";
    yield return "-b:v"; yield return $"{s.VideoBitrateKbps}k"; yield return "-maxrate"; yield return $"{s.VideoBitrateKbps}k"; yield return "-bufsize"; yield return $"{s.VideoBitrateKbps * 2}k";
    yield return "-g"; yield return (s.FrameRate * 2).ToString(); yield return "-f"; yield return "mpegts"; yield return BuildOutputUrl(s);
}

string BuildOutputUrl(VideoSettings s) => s.Protocol == "srt"
    ? $"srt://{s.Destination}:{s.Port}?mode=caller&latency={s.SrtLatencyMs * 1000}"
    : $"udp://{s.Destination}:{s.Port}?pkt_size=1316&ttl=16";

async Task PumpBrowserFramesAsync(VS.VideoOutput.EmbeddedRenderer embeddedRenderer, Process ffmpegProcess, VideoSettings settings, CancellationToken cancellationToken)
{
    var destination = ffmpegProcess.StandardInput.BaseStream;
    var errorTask = ffmpegProcess.StandardError.ReadToEndAsync(cancellationToken);

    // Capture at a stable maximum of 10 distinct page snapshots per second and
    // repeat each snapshot into FFmpeg so the configured transport remains a
    // standards-friendly 25/30 fps without unnecessary rendering overhead.
    var captureRate = Math.Min(settings.FrameRate, 10);
    var repeatsPerCapture = Math.Max(1, (int)Math.Ceiling((double)settings.FrameRate / captureRate));
    var captureInterval = TimeSpan.FromSeconds((double)repeatsPerCapture / settings.FrameRate);
    while (!cancellationToken.IsCancellationRequested)
    {
        var started = Stopwatch.GetTimestamp();
        var frame = await embeddedRenderer.CaptureJpegAsync(cancellationToken);
        try
        {
            for (var repeat = 0; repeat < repeatsPerCapture; repeat++)
                await destination.WriteAsync(frame, cancellationToken);
            await destination.FlushAsync(cancellationToken);
        }
        catch (Exception ex) when (ffmpegProcess.HasExited)
        {
            var ffmpegError = (await errorTask).Trim();
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(ffmpegError)
                    ? $"FFmpeg stopped while receiving browser frames (exit code {ffmpegProcess.ExitCode})."
                    : $"FFmpeg stopped while receiving browser frames: {ffmpegError}", ex);
        }

        var elapsed = Stopwatch.GetElapsedTime(started);
        var remaining = captureInterval - elapsed;
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining, cancellationToken);
    }
}

void StopProcesses()
{
    try { captureCancellation?.Cancel(); } catch { }
    captureCancellation?.Dispose(); captureCancellation = null; captureTask = null;
    Stop(ffmpeg); ffmpeg = null;
    if (renderer is not null)
    {
        try { renderer.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
        renderer = null;
    }
}

void Stop(Process? process)
{
    try { if (process is { HasExited: false }) process.Kill(entireProcessTree: true); }
    catch { }
    process?.Dispose();
}

sealed record WorkerCommand(VideoSettings Settings, bool DesiredRunning, long Revision);
sealed record WorkerStatus(bool Running, string Message, int? FfmpegProcessId, string OutputUrl);
sealed record VideoSettings(string FfmpegPath, string Protocol, string Destination, int Port, string Scene, string? TemplateId, string? MatchId, int Width, int Height, int FrameRate, int VideoBitrateKbps, int SrtLatencyMs);
