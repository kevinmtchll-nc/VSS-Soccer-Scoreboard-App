using CefSharp;
using CefSharp.DevTools.Page;
using CefSharp.OffScreen;
using System.Drawing;

namespace VS.VideoOutput;

internal sealed class EmbeddedRenderer : IAsyncDisposable
{
    private static readonly object InitializationLock = new();
    private static bool initialized;
    private readonly ChromiumWebBrowser browser;

    private EmbeddedRenderer(ChromiumWebBrowser browser) => this.browser = browser;

    public static async Task<EmbeddedRenderer> CreateAsync(string url, int width, int height)
    {
        EnsureInitialized();
        var browser = new ChromiumWebBrowser(url)
        {
            Size = new Size(width, height),
            DeviceScaleFactor = 1
        };
        try
        {
            var loaded = await browser.WaitForInitialLoadAsync().WaitAsync(TimeSpan.FromSeconds(30));
            if (!loaded.Success)
                throw new InvalidOperationException($"The embedded renderer could not load the local output ({loaded.ErrorCode}).");
            await browser.ResizeAsync(width, height, 1);
            await Task.Delay(750);
            return new EmbeddedRenderer(browser);
        }
        catch
        {
            browser.Dispose();
            throw;
        }
    }

    private static void EnsureInitialized()
    {
        lock (InitializationLock)
        {
            if (initialized) return;
            CefSharpSettings.SubprocessExitIfParentProcessClosed = true;
            var settings = new CefSettings
            {
                WindowlessRenderingEnabled = true,
                MultiThreadedMessageLoop = true,
                LogSeverity = LogSeverity.Disable,
                CachePath = Path.Combine(
                    Path.GetTempPath(), "VITECScoreboard.VideoOutput",
                    $"Cef-{Environment.ProcessId}-{Guid.NewGuid():N}")
            };
            settings.CefCommandLineArgs["disable-extensions"] = "1";
            settings.CefCommandLineArgs["disable-sync"] = "1";
            settings.CefCommandLineArgs["disable-background-networking"] = "1";
            // Off-screen output dimensions must be physical video pixels and
            // must not inherit the signed-in user's Windows display scaling.
            settings.CefCommandLineArgs["force-device-scale-factor"] = "1";
            if (!Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null))
                throw new InvalidOperationException("The embedded off-screen renderer could not be initialized.");
            initialized = true;
        }
    }

    public Task<byte[]> CaptureJpegAsync(CancellationToken cancellationToken) =>
        browser.CaptureScreenshotAsync(CaptureScreenshotFormat.Jpeg, 88)
            .WaitAsync(cancellationToken);

    public ValueTask DisposeAsync()
    {
        browser.Dispose();
        return ValueTask.CompletedTask;
    }
}
