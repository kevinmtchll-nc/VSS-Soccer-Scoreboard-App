using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Http.Features;
using VS.Data;
using VS.Data.Services;
using VS.MLB;
using VS.Soccer;
using VS.Core.Models;
using VS.Web;

var builder = WebApplication.CreateBuilder(args);
var applicationVersion = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "Unknown";

var isExplicitWindowsService = args.Any(arg =>
    arg.Equals("--windows-service", StringComparison.OrdinalIgnoreCase));

if (isExplicitWindowsService)
{
    builder.Host.UseContentRoot(AppContext.BaseDirectory);
    builder.Services.AddSingleton<IHostLifetime, WindowsServiceLifetime>();
}
else
{
    builder.Host.UseWindowsService(options =>
    {
        options.ServiceName = "VITEC Soccer Scoreboard";
    });
}

var commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
var vsDataDir = Environment.GetEnvironmentVariable("VITEC_SCOREBOARD_DATA_DIR")
    ?? Path.Combine(commonData, "VITEC Soccer Scoreboard");
var vsLogDir = Path.Combine(vsDataDir, "Logs");
Directory.CreateDirectory(vsLogDir);

builder.Logging.AddProvider(new VS.Web.FileLoggerProvider(
    Path.Combine(vsLogDir, "VITEC-Scoreboard.log")));

var vsConfigDirectory = vsDataDir;
Directory.CreateDirectory(vsConfigDirectory);
var vsConfigPath = Path.Combine(vsConfigDirectory, "vssettings.json");

builder.Configuration.AddJsonFile(
    vsConfigPath,
    optional: true,
    reloadOnChange: true);

TimeZoneInfo ResolveDisplayTimeZone()
{
    var configured = builder.Configuration["VS:DisplayTimeZone"];
    if (string.IsNullOrWhiteSpace(configured) ||
        configured.Equals("SERVER_LOCAL", StringComparison.OrdinalIgnoreCase))
    {
        return TimeZoneInfo.Local;
    }

    try
    {
        return TimeZoneInfo.FindSystemTimeZoneById(configured);
    }
    catch
    {
        return TimeZoneInfo.Local;
    }
}

string FormatDisplayTime(DateTimeOffset value)
{
    var tz = ResolveDisplayTimeZone();
    var converted = TimeZoneInfo.ConvertTime(value, tz);
    return converted.ToString("h:mm tt");
}

string EffectiveDisplayTimeZoneId()
{
    return ResolveDisplayTimeZone().Id;
}


string? GetCurrentPostgresConnectionString()
{
    return builder.Configuration.GetConnectionString("VSPostgres");
}

NpgsqlConnectionStringBuilder ParseCurrentPostgres()
{
    var cs = GetCurrentPostgresConnectionString();
    if (string.IsNullOrWhiteSpace(cs))
    {
        return new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Database = "vitec_scoreboard",
            Username = "postgres"
        };
    }

    try
    {
        return new NpgsqlConnectionStringBuilder(cs);
    }
    catch
    {
        return new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Database = "vitec_scoreboard",
            Username = "postgres"
        };
    }
}

async Task<(bool ok, string message)> TestPostgresAsync(string connectionString, CancellationToken ct)
{
    try
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand("SELECT version();", connection);
        var version = Convert.ToString(await cmd.ExecuteScalarAsync(ct)) ?? "PostgreSQL";
        return (true, $"Connected successfully. {version}");
    }
    catch (Exception ex)
    {
        return (false, ex.Message);
    }
}

static string? PasswordValidationError(string value)
{
    if (value.Length is < 12 or > 128) return "Password must contain 12 through 128 characters.";
    if (value.Any(char.IsWhiteSpace)) return "Password cannot contain spaces.";
    if (!value.Any(char.IsUpper)) return "Password must contain at least one uppercase letter.";
    if (!value.Any(char.IsLower)) return "Password must contain at least one lowercase letter.";
    if (!value.Any(char.IsDigit)) return "Password must contain at least one number.";
    if (!value.Any(ch => "!@#$%^&*-_".Contains(ch))) return "Password must contain at least one special character: ! @ # $ % ^ & * - _.";
    return null;
}

static string PgIdentifier(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        throw new InvalidOperationException("A PostgreSQL identifier is required.");
    return $"\"{value.Replace("\"", "\"\"")}\"";
}
static string PgLiteral(string value) => $"'{value.Replace("'", "''")}'";
static bool IsLocalRequest(HttpContext context) => context.Connection.RemoteIpAddress is { } address && System.Net.IPAddress.IsLoopback(address);

HistoricalPitchStore? CreateDynamicStore()
{
    var cs = GetCurrentPostgresConnectionString();
    if (string.IsNullOrWhiteSpace(cs))
        return null;

    var options = new DbContextOptionsBuilder<VsDbContext>()
        .UseNpgsql(cs, npgsql =>
        {
            npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(3), null);
            npgsql.CommandTimeout(30);
        })
        .Options;

    var factory = new PooledDbContextFactory<VsDbContext>(options);
    return new HistoricalPitchStore(factory);
}

builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("VITEC_SOCCER_SCOREBOARD_LISTEN_URL")
    ?? builder.Configuration["VS:ListenUrl"]
    ?? "http://0.0.0.0:5100");

builder.Services.AddMemoryCache();
builder.Services.AddSingleton(new VideoOutputCoordinator(vsConfigDirectory));
builder.Services.AddSingleton(new WorkspaceTemplateStore(vsConfigDirectory));
builder.Services.AddSingleton(new SoccerWorkspaceStore(vsConfigDirectory));
var themeBackground = new ThemeBackgroundStore(vsConfigDirectory);
builder.Services.AddSingleton(themeBackground);
var adMedia = new AdMediaStore(vsConfigDirectory);
builder.Services.AddSingleton(adMedia);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 250L * 1024 * 1024);

builder.Services.AddHttpClient<IMlbStatsClient, MlbStatsClient>(client =>
{
    client.BaseAddress = new Uri("https://statsapi.mlb.com");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd($"VITEC-Scoreboard/{applicationVersion}");
});

builder.Services.Configure<SoccerStatsOptions>(builder.Configuration.GetSection(SoccerStatsOptions.SectionName));
builder.Services.Configure<SportradarImagesOptions>(builder.Configuration.GetSection(SportradarImagesOptions.SectionName));
builder.Services.AddHttpClient<ISoccerStatsClient, SoccerStatsClient>(client =>
{
    client.BaseAddress = new Uri("https://stats-api.mlssoccer.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd($"VITEC-Soccer-Scoreboard/{applicationVersion}");
});
builder.Services.AddHttpClient<SportradarLogoClient>(client =>
{
    client.BaseAddress = new Uri("https://api.sportradar.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd($"VITEC-Soccer-Scoreboard/{applicationVersion}");
});

var pgConnection = builder.Configuration.GetConnectionString("VSPostgres");

if (!string.IsNullOrWhiteSpace(pgConnection))
{
    builder.Services.AddPooledDbContextFactory<VsDbContext>(options =>
        options.UseNpgsql(pgConnection, npgsql =>
        {
            npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(3), null);
            npgsql.CommandTimeout(30);
        }));

    builder.Services.AddSingleton<HistoricalPitchStore>();
}

var app = builder.Build();

// The baseball implementation remains in the solution for preservation and
// comparison, but the soccer host must never expose baseball data or routes.
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments("/api/mlb") ||
        path.StartsWithSegments("/api/history") ||
        path.StartsWithSegments("/api/analytics/pitches") ||
        path.StartsWithSegments("/api/integrations/eztv"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { message = "This route is not available in VITEC Soccer Scoreboard." });
        return;
    }
    await next();
});

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("VS.Global");
        logger.LogError(ex, "Unhandled request failure for {Path}", context.Request.Path);

        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "VITEC Soccer Scoreboard request failed.",
                path = context.Request.Path.Value,
                message = ex.Message
            });
        }
    }
});

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        context.Context.Response.Headers.Pragma = "no-cache";
        context.Context.Response.Headers.Expires = "0";
    }
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(adMedia.DirectoryPath),
    RequestPath = "/media/ads"
});
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(themeBackground.DirectoryPath), RequestPath = "/media/themes" });

app.MapGet("/api/health", () => Results.Ok(new
{
    app = "VITEC Soccer Scoreboard",
    abbreviation = "VS",
    version = applicationVersion,
    status = "ok",
    postgresConfigured = !string.IsNullOrWhiteSpace(pgConnection),
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/video/status", (VideoOutputCoordinator video) => Results.Ok(video.Snapshot()));

app.MapGet("/api/video/worker/command", (VideoOutputCoordinator video) => Results.Ok(video.Command()));

app.MapPost("/api/video/settings", async (HttpRequest request, VideoOutputCoordinator video) =>
{
    try
    {
        var settings = await request.ReadFromJsonAsync<VideoOutputSettings>();
        if (settings is null) return Results.BadRequest(new { message = "Video output settings are required." });
        video.Save(settings);
        return Results.Ok(new { message = "Video output settings saved.", status = video.Snapshot() });
    }
    catch (Exception ex) { return Results.BadRequest(new { message = ex.Message }); }
});

app.MapPost("/api/video/start", (VideoOutputCoordinator video) =>
{
    video.SetDesiredRunning(true);
    return Results.Ok(new { message = "Video output start requested.", status = video.Snapshot() });
});

app.MapPost("/api/video/stop", (VideoOutputCoordinator video) =>
{
    video.SetDesiredRunning(false);
    return Results.Ok(new { message = "Video output stop requested.", status = video.Snapshot() });
});

app.MapPost("/api/video/worker/status", async (HttpRequest request, VideoOutputCoordinator video) =>
{
    var status = await request.ReadFromJsonAsync<VideoWorkerStatus>();
    if (status is null) return Results.BadRequest();
    video.Report(status);
    return Results.Ok();
});

app.MapGet("/api/advertising/status", (AdMediaStore ads) => Results.Ok(ads.Status()));

app.MapPost("/api/advertising/{slot}", async (string slot, HttpRequest request, AdMediaStore ads, CancellationToken cancellationToken) =>
{
    try
    {
        if (!request.HasFormContentType) return Results.BadRequest(new { message = "Advertising upload must use multipart form data." });
        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("media");
        if (file is null) return Results.BadRequest(new { message = "Choose a media file to upload." });
        return Results.Ok(new { message = "Advertising media uploaded.", media = await ads.SaveAsync(slot, file, cancellationToken) });
    }
    catch (Exception ex) { return Results.BadRequest(new { message = ex.Message }); }
}).DisableAntiforgery();

app.MapDelete("/api/advertising/{slot}", (string slot, AdMediaStore ads) =>
{
    try { ads.Delete(slot); return Results.Ok(new { message = "Advertising media removed." }); }
    catch (Exception ex) { return Results.BadRequest(new { message = ex.Message }); }
});

app.MapGet("/api/workspace/templates", (WorkspaceTemplateStore templates) =>
    Results.Ok(new[] { WorkspaceTemplateStore.Defaults() }.Concat(templates.List())));
app.MapGet("/api/workspace/templates/{id}", (string id, WorkspaceTemplateStore templates) =>
    id.Equals("default", StringComparison.OrdinalIgnoreCase)
        ? Results.Ok(WorkspaceTemplateStore.Defaults())
        : templates.Get(id) is { } template ? Results.Ok(template) : Results.NotFound());
app.MapPost("/api/workspace/templates", async (HttpRequest request, WorkspaceTemplateStore templates) =>
{
    try
    {
        var template = await request.ReadFromJsonAsync<WorkspaceTemplate>();
        return template is null ? Results.BadRequest(new { message = "Template data is required." }) : Results.Ok(templates.Save(template));
    }
    catch (Exception ex) { return Results.BadRequest(new { message = ex.Message }); }
});
app.MapDelete("/api/workspace/templates/{id}", (string id, WorkspaceTemplateStore templates) =>
    id.Equals("default", StringComparison.OrdinalIgnoreCase)
        ? Results.BadRequest(new { message = "The built-in template cannot be deleted." })
        : templates.Delete(id) ? Results.Ok(new { message = "Template deleted." }) : Results.NotFound());
app.MapGet("/api/theme/background", (ThemeBackgroundStore backgrounds) => Results.Ok(backgrounds.Status()));
app.MapPost("/api/theme/background", async (HttpRequest request, ThemeBackgroundStore backgrounds, CancellationToken cancellationToken) =>
{
    try { var form = await request.ReadFormAsync(cancellationToken); var file = form.Files.GetFile("background"); return file is null ? Results.BadRequest(new { message = "Choose a background image." }) : Results.Ok(await backgrounds.SaveAsync(file, cancellationToken)); }
    catch (Exception ex) { return Results.BadRequest(new { message = ex.Message }); }
}).DisableAntiforgery();
app.MapDelete("/api/theme/background", (ThemeBackgroundStore backgrounds) => { backgrounds.Delete(); return Results.Ok(); });


app.MapGet("/api/settings/postgres", () =>
{
    var csb = ParseCurrentPostgres();

    return Results.Ok(new
    {
        host = csb.Host,
        port = csb.Port,
        database = csb.Database,
        username = csb.Username,
        hasPassword = !string.IsNullOrWhiteSpace(csb.Password),
        configured = !string.IsNullOrWhiteSpace(GetCurrentPostgresConnectionString())
    });
});

app.MapPost("/api/settings/postgres/test", async (
    HttpRequest request,
    CancellationToken ct) =>
{
    using var body = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
    var root = body.RootElement;

    var current = ParseCurrentPostgres();
    var host = root.TryGetProperty("host", out var h) ? h.GetString() : current.Host;
    var database = root.TryGetProperty("database", out var d) ? d.GetString() : current.Database;
    var username = root.TryGetProperty("username", out var u) ? u.GetString() : current.Username;
    var password = root.TryGetProperty("password", out var pw) ? pw.GetString() : null;
    var port = root.TryGetProperty("port", out var po) && po.TryGetInt32(out var portValue)
        ? portValue
        : current.Port;

    if (string.IsNullOrWhiteSpace(password))
        password = current.Password;

    var csb = new NpgsqlConnectionStringBuilder
    {
        Host = string.IsNullOrWhiteSpace(host) ? "localhost" : host,
        Port = port <= 0 ? 5432 : port,
        Database = string.IsNullOrWhiteSpace(database) ? "vitec_scoreboard" : database,
        Username = string.IsNullOrWhiteSpace(username) ? "postgres" : username,
        Password = password ?? "",
        Timeout = 10,
        CommandTimeout = 15,
        Pooling = true
    };

    var result = await TestPostgresAsync(csb.ConnectionString, ct);

    return result.ok
        ? Results.Ok(new { connected = true, message = result.message })
        : Results.BadRequest(new { connected = false, message = result.message });
});

app.MapPost("/api/settings/postgres", async (
    HttpRequest request,
    CancellationToken ct) =>
{
    using var body = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
    var rootElement = body.RootElement;

    var current = ParseCurrentPostgres();

    var host = rootElement.TryGetProperty("host", out var h) ? h.GetString() : current.Host;
    var database = rootElement.TryGetProperty("database", out var d) ? d.GetString() : current.Database;
    var username = rootElement.TryGetProperty("username", out var u) ? u.GetString() : current.Username;
    var password = rootElement.TryGetProperty("password", out var pw) ? pw.GetString() : null;
    var port = rootElement.TryGetProperty("port", out var po) && po.TryGetInt32(out var portValue)
        ? portValue
        : current.Port;

    if (string.IsNullOrWhiteSpace(password))
        password = current.Password;

    if (string.IsNullOrWhiteSpace(host) ||
        string.IsNullOrWhiteSpace(database) ||
        string.IsNullOrWhiteSpace(username))
    {
        return Results.BadRequest(new { message = "Host, database, and username are required." });
    }

    var csb = new NpgsqlConnectionStringBuilder
    {
        Host = host,
        Port = port <= 0 ? 5432 : port,
        Database = database,
        Username = username,
        Password = password ?? "",
        Timeout = 10,
        CommandTimeout = 30,
        Pooling = true
    };

    // Save only after syntax is valid. A failed connection can still be saved deliberately
    // after the user tests, but we return the connection test result in the response.
    JsonObject jsonRoot;
    try
    {
        jsonRoot = File.Exists(vsConfigPath)
            ? (JsonNode.Parse(await File.ReadAllTextAsync(vsConfigPath, ct)) as JsonObject ?? new JsonObject())
            : new JsonObject();
    }
    catch
    {
        jsonRoot = new JsonObject();
    }

    var connectionStrings = jsonRoot["ConnectionStrings"] as JsonObject;
    if (connectionStrings is null)
    {
        connectionStrings = new JsonObject();
        jsonRoot["ConnectionStrings"] = connectionStrings;
    }

    connectionStrings["VSPostgres"] = csb.ConnectionString;

    await File.WriteAllTextAsync(
        vsConfigPath,
        jsonRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
        ct);

    // Configuration reload is file-watcher based. Test the exact saved connection immediately.
    var test = await TestPostgresAsync(csb.ConnectionString, ct);

    return Results.Ok(new
    {
        saved = true,
        connected = test.ok,
        message = test.ok
            ? "PostgreSQL settings saved and connection test succeeded."
            : $"PostgreSQL settings saved, but the connection test failed: {test.message}"
    });
});

app.MapPost("/api/settings/postgres/password", async (HttpContext context, HttpRequest request, CancellationToken ct) =>
{
    if (!IsLocalRequest(context)) return Results.StatusCode(StatusCodes.Status403Forbidden);
    using var body = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
    var currentPassword = body.RootElement.TryGetProperty("currentPassword", out var current) ? current.GetString() ?? "" : "";
    var newPassword = body.RootElement.TryGetProperty("newPassword", out var next) ? next.GetString() ?? "" : "";
    var validation = PasswordValidationError(newPassword);
    if (validation is not null) return Results.BadRequest(new { message = validation });
    var csb = ParseCurrentPostgres(); csb.Password = currentPassword;
    try
    {
        await using var connection = new NpgsqlConnection(csb.ConnectionString); await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand($"ALTER ROLE {PgIdentifier(csb.Username)} PASSWORD {PgLiteral(newPassword)}", connection); await command.ExecuteNonQueryAsync(ct);
        csb.Password = newPassword;
        var root = File.Exists(vsConfigPath) ? JsonNode.Parse(await File.ReadAllTextAsync(vsConfigPath, ct)) as JsonObject ?? new JsonObject() : new JsonObject();
        var strings = root["ConnectionStrings"] as JsonObject ?? new JsonObject(); root["ConnectionStrings"] = strings; strings["VSPostgres"] = csb.ConnectionString;
        await File.WriteAllTextAsync(vsConfigPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct);
        return Results.Ok(new { message = "PostgreSQL password changed and VITEC settings updated." });
    }
    catch (Exception ex) { return Results.BadRequest(new { message = ex.Message }); }
});

app.MapGet("/api/settings/postgres/users", async (HttpContext context, CancellationToken ct) =>
{
    if (!IsLocalRequest(context)) return Results.StatusCode(StatusCodes.Status403Forbidden);
    try
    {
        var csb = ParseCurrentPostgres(); await using var connection = new NpgsqlConnection(csb.ConnectionString); await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("SELECT rolname, rolcanlogin FROM pg_roles WHERE rolname !~ '^pg_' ORDER BY rolname", connection);
        await using var reader = await command.ExecuteReaderAsync(ct); var users = new List<object>();
        while (await reader.ReadAsync(ct)) users.Add(new { username = reader.GetString(0), canLogin = reader.GetBoolean(1), isApplicationUser = reader.GetString(0).Equals(csb.Username, StringComparison.OrdinalIgnoreCase) });
        return Results.Ok(new { users });
    }
    catch (Exception ex) { return Results.BadRequest(new { message = ex.Message }); }
});

app.MapPost("/api/settings/postgres/users", async (HttpContext context, HttpRequest request, CancellationToken ct) =>
{
    if (!IsLocalRequest(context)) return Results.StatusCode(StatusCodes.Status403Forbidden);
    using var body = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct); var root = body.RootElement;
    var username = root.TryGetProperty("username", out var u) ? u.GetString()?.Trim() ?? "" : ""; var password = root.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";
    if (!System.Text.RegularExpressions.Regex.IsMatch(username, "^[A-Za-z][A-Za-z0-9_-]{2,62}$")) return Results.BadRequest(new { message = "Username must begin with a letter and contain 3 through 63 letters, numbers, underscores, or hyphens." });
    var validation = PasswordValidationError(password); if (validation is not null) return Results.BadRequest(new { message = validation });
    try
    {
        var csb = ParseCurrentPostgres(); await using var connection = new NpgsqlConnection(csb.ConnectionString); await connection.OpenAsync(ct);
        var statements = new[] { $"CREATE ROLE {PgIdentifier(username)} LOGIN PASSWORD {PgLiteral(password)}", $"GRANT CONNECT ON DATABASE {PgIdentifier(csb.Database)} TO {PgIdentifier(username)}", $"GRANT USAGE ON SCHEMA public TO {PgIdentifier(username)}", $"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {PgIdentifier(username)}", $"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {PgIdentifier(username)}" };
        foreach (var sql in statements) { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(ct); }
        return Results.Ok(new { message = $"PostgreSQL user {username} created." });
    }
    catch (Exception ex) { return Results.BadRequest(new { message = ex.Message }); }
});

app.MapMethods("/api/settings/postgres/users/{username}", ["PATCH"], async (string username, HttpContext context, HttpRequest request, CancellationToken ct) =>
{
    if (!IsLocalRequest(context)) return Results.StatusCode(StatusCodes.Status403Forbidden); var csb = ParseCurrentPostgres();
    using var body = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct); var root = body.RootElement;
    try
    {
        await using var connection = new NpgsqlConnection(csb.ConnectionString); await connection.OpenAsync(ct);
        if (root.TryGetProperty("password", out var passwordElement) && !string.IsNullOrEmpty(passwordElement.GetString())) { var password=passwordElement.GetString()!;var validation=PasswordValidationError(password);if(validation is not null)return Results.BadRequest(new{message=validation});await using var passwordCommand=new NpgsqlCommand($"ALTER ROLE {PgIdentifier(username)} PASSWORD {PgLiteral(password)}",connection);await passwordCommand.ExecuteNonQueryAsync(ct); }
        if (root.TryGetProperty("canLogin", out var loginElement) && loginElement.ValueKind is JsonValueKind.True or JsonValueKind.False) { if(username.Equals(csb.Username,StringComparison.OrdinalIgnoreCase)&&!loginElement.GetBoolean())return Results.BadRequest(new{message="The active VITEC application account cannot be disabled."});await using var loginCommand=new NpgsqlCommand($"ALTER ROLE {PgIdentifier(username)} {(loginElement.GetBoolean()?"LOGIN":"NOLOGIN")}",connection);await loginCommand.ExecuteNonQueryAsync(ct); }
        return Results.Ok(new { message = $"PostgreSQL user {username} updated." });
    }
    catch (Exception ex) { return Results.BadRequest(new { message = ex.Message }); }
});

app.MapDelete("/api/settings/postgres/users/{username}", async (string username, HttpContext context, CancellationToken ct) =>
{
    if (!IsLocalRequest(context)) return Results.StatusCode(StatusCodes.Status403Forbidden); var csb=ParseCurrentPostgres();
    if(username.Equals(csb.Username,StringComparison.OrdinalIgnoreCase)||username.Equals("postgres",StringComparison.OrdinalIgnoreCase))return Results.BadRequest(new{message="The active VITEC application account and PostgreSQL administrator cannot be removed here."});
    try{await using var connection=new NpgsqlConnection(csb.ConnectionString);await connection.OpenAsync(ct);await using var command=new NpgsqlCommand($"DROP ROLE {PgIdentifier(username)}",connection);await command.ExecuteNonQueryAsync(ct);return Results.Ok(new{message=$"PostgreSQL user {username} removed."});}catch(Exception ex){return Results.BadRequest(new{message=ex.Message});}
});

app.MapGet("/api/db/status", async (
    CancellationToken ct) =>
{
    var connectionString = GetCurrentPostgresConnectionString();
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.Ok(new
        {
            configured = false,
            canConnect = false,
            games = 0L,
            pitches = 0L,
            latestGameDate = (DateTimeOffset?)null,
            message = "PostgreSQL is not configured."
        });
    }

    var test = await TestPostgresAsync(connectionString, ct);
    if (!test.ok)
    {
        return Results.Ok(new
        {
            configured = true,
            canConnect = false,
            games = 0L,
            pitches = 0L,
            latestGameDate = (DateTimeOffset?)null,
            message = test.message
        });
    }

    try
    {
        var store = CreateDynamicStore();
        if (store is null)
            throw new InvalidOperationException("PostgreSQL store could not be created.");

        return Results.Ok(await store.GetStatusAsync(ct));
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            configured = true,
            canConnect = true,
            games = 0L,
            pitches = 0L,
            latestGameDate = (DateTimeOffset?)null,
            message = $"PostgreSQL connected, but VS schema/status is not ready: {ex.Message}"
        });
    }
});

app.MapPost("/api/db/initialize", async (
    CancellationToken ct) =>
{
    var store = CreateDynamicStore();
    if (store is null)
        return Results.BadRequest(new { message = "PostgreSQL is not configured." });

    try
    {
        await store.EnsureCreatedAsync(ct);
        return Results.Ok(new { message = "VS PostgreSQL schema initialized." });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});


app.MapGet("/api/settings/display", () =>
{
    var configured = builder.Configuration["VS:DisplayTimeZone"];
    var effective = ResolveDisplayTimeZone();

    var zones = TimeZoneInfo.GetSystemTimeZones()
        .Select(z => new
        {
            id = z.Id,
            name = z.DisplayName,
            baseUtcOffset = z.BaseUtcOffset.ToString()
        })
        .OrderBy(z => z.name)
        .ToList();

    return Results.Ok(new
    {
        configuredTimeZoneId = string.IsNullOrWhiteSpace(configured) ? "SERVER_LOCAL" : configured,
        effectiveTimeZoneId = effective.Id,
        effectiveTimeZoneName = effective.DisplayName,
        serverLocalTimeZoneId = TimeZoneInfo.Local.Id,
        serverLocalTimeZoneName = TimeZoneInfo.Local.DisplayName,
        serverLocalNow = DateTimeOffset.Now,
        zones
    });
});

app.MapGet("/api/settings/network", () =>
{
    var listenUrl = builder.Configuration["VS:ListenUrl"] ?? "http://0.0.0.0:5000";
    var port = Uri.TryCreate(listenUrl.Replace("0.0.0.0", "localhost"), UriKind.Absolute, out var uri)
        ? uri.Port
        : 5000;
    return Results.Ok(new { port, listenUrl, restartRequired = false });
});

app.MapPost("/api/settings/network", async (HttpRequest request) =>
{
    using var body = await JsonDocument.ParseAsync(request.Body);
    if (!body.RootElement.TryGetProperty("port", out var portNode) ||
        !portNode.TryGetInt32(out var port) || port is < 1024 or > 65535)
        return Results.BadRequest(new { message = "The listening port must be between 1024 and 65535." });

    var currentUrl = builder.Configuration["VS:ListenUrl"] ?? "http://0.0.0.0:5000";
    var currentPort = Uri.TryCreate(currentUrl.Replace("0.0.0.0", "localhost"), UriKind.Absolute, out var currentUri)
        ? currentUri.Port
        : 5000;
    if (port != currentPort && IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(endpoint => endpoint.Port == port))
        return Results.BadRequest(new { message = $"TCP port {port} is already in use. Choose another port." });

    JsonObject root;
    try
    {
        root = File.Exists(vsConfigPath)
            ? (JsonNode.Parse(await File.ReadAllTextAsync(vsConfigPath)) as JsonObject ?? new JsonObject())
            : new JsonObject();
    }
    catch { root = new JsonObject(); }

    var vsNode = root["VS"] as JsonObject ?? new JsonObject();
    root["VS"] = vsNode;
    vsNode["ListenUrl"] = $"http://0.0.0.0:{port}";
    await File.WriteAllTextAsync(vsConfigPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

    try
    {
        var removeFirewall = new ProcessStartInfo("netsh.exe") { UseShellExecute = false, CreateNoWindow = true };
        removeFirewall.ArgumentList.Add("advfirewall"); removeFirewall.ArgumentList.Add("firewall"); removeFirewall.ArgumentList.Add("delete"); removeFirewall.ArgumentList.Add("rule");
        removeFirewall.ArgumentList.Add("name=VITEC Scoreboard Configured Port");
        using (var removeProcess = Process.Start(removeFirewall))
            if (removeProcess is not null) await removeProcess.WaitForExitAsync();

        var firewall = new ProcessStartInfo("netsh.exe") { UseShellExecute = false, CreateNoWindow = true };
        firewall.ArgumentList.Add("advfirewall"); firewall.ArgumentList.Add("firewall"); firewall.ArgumentList.Add("add"); firewall.ArgumentList.Add("rule");
        firewall.ArgumentList.Add("name=VITEC Scoreboard Configured Port"); firewall.ArgumentList.Add("dir=in"); firewall.ArgumentList.Add("action=allow");
        firewall.ArgumentList.Add("protocol=TCP"); firewall.ArgumentList.Add($"localport={port}"); firewall.ArgumentList.Add("profile=any");
        using var process = Process.Start(firewall);
        if (process is not null) await process.WaitForExitAsync();
    }
    catch { /* The installer rule still permits the default port; report restart instructions below. */ }

    return Results.Ok(new
    {
        message = $"Listening port {port} saved. Restart the VITEC Scoreboard service to apply it.",
        port,
        listenUrl = $"http://0.0.0.0:{port}",
        restartRequired = port != currentPort
    });
});

app.MapPost("/api/settings/display", async (HttpRequest request) =>
{
    using var body = await JsonDocument.ParseAsync(request.Body);

    var requested = body.RootElement.TryGetProperty("timeZoneId", out var tzNode)
        ? tzNode.GetString()
        : null;

    requested = string.IsNullOrWhiteSpace(requested) ? "SERVER_LOCAL" : requested.Trim();

    if (!requested.Equals("SERVER_LOCAL", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(requested);
        }
        catch
        {
            return Results.BadRequest(new { message = $"Unknown time zone: {requested}" });
        }
    }

    JsonObject root;
    try
    {
        root = File.Exists(vsConfigPath)
            ? (JsonNode.Parse(await File.ReadAllTextAsync(vsConfigPath)) as JsonObject ?? new JsonObject())
            : new JsonObject();
    }
    catch
    {
        root = new JsonObject();
    }

    var vsNode = root["VS"] as JsonObject;
    if (vsNode is null)
    {
        vsNode = new JsonObject();
        root["VS"] = vsNode;
    }

    vsNode["DisplayTimeZone"] = requested;

    var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(vsConfigPath, json);

    return Results.Ok(new
    {
        message = "Display time zone saved.",
        configuredTimeZoneId = requested,
        effectiveTimeZoneId = requested.Equals("SERVER_LOCAL", StringComparison.OrdinalIgnoreCase)
            ? TimeZoneInfo.Local.Id
            : requested
    });
});


app.MapGet("/api/mlb/standings", async (
    int? season,
    IMlbStatsClient mlb,
    IMemoryCache cache,
    CancellationToken ct) =>
{
    var targetSeason = season ?? DateTime.Now.Year;
    var key = $"mlb:standings:{targetSeason}";

    if (!cache.TryGetValue(key, out IReadOnlyList<VS.Core.Models.StandingsDivision>? standings))
    {
        standings = await mlb.GetStandingsAsync(targetSeason, ct);
        cache.Set(key, standings, TimeSpan.FromSeconds(60));
    }

    standings ??= Array.Empty<VS.Core.Models.StandingsDivision>();

    return Results.Ok(new
    {
        season = targetSeason,
        updatedAt = DateTimeOffset.UtcNow,
        divisions = standings
    });
});

app.MapGet("/api/mlb/games", async (
    string? date,
    IMlbStatsClient mlb,
    IMemoryCache cache,
    CancellationToken ct) =>
{
    var targetDate = DateOnly.TryParse(date, out var parsed)
        ? parsed
        : DateOnly.FromDateTime(DateTime.Now);

    var key = $"mlb:schedule:{targetDate:yyyy-MM-dd}";
    if (!cache.TryGetValue(key, out IReadOnlyList<VS.Core.Models.ScoreboardGame>? games))
    {
        games = await mlb.GetScheduleAsync(targetDate, ct);
        cache.Set(key, games, TimeSpan.FromSeconds(15));
    }

    games ??= Array.Empty<VS.Core.Models.ScoreboardGame>();

    var displayGames = games
        .Select(game => game with
        {
            DisplayStart = FormatDisplayTime(game.GameDate)
        })
        .ToList();

    return Results.Ok(displayGames);
});

app.MapGet("/api/mlb/daily-dashboard", async (string? date, IMlbStatsClient mlb, IMemoryCache cache, CancellationToken ct) =>
{
    var targetDate = DateOnly.TryParse(date, out var parsed) ? parsed : DateOnly.FromDateTime(DateTime.Now);
    var key = $"mlb:daily-dashboard:{targetDate:yyyy-MM-dd}";
    if (cache.TryGetValue(key, out object? cached) && cached is not null) return Results.Ok(cached);
    var games = await mlb.GetScheduleAsync(targetDate, ct);
    var summaryTasks = games.Where(game => !game.Status.Equals("Preview", StringComparison.OrdinalIgnoreCase))
        .Select(async game => { try { return await mlb.GetGameSummaryAsync(game.GamePk, ct); } catch { return null; } });
    var summaries = (await Task.WhenAll(summaryTasks)).Where(summary => summary is not null).Cast<GameSummary>().ToList();
    static int Number(string? value) => int.TryParse(value, out var number) ? number : 0;
    static double GameEra(string innings, string earnedRuns)
    {
        var parts = innings.Split('.'); if (!int.TryParse(parts[0], out var whole)) return 99;
        var outs = whole * 3 + (parts.Length > 1 && int.TryParse(parts[1], out var partial) ? partial : 0);
        return outs > 0 ? Number(earnedRuns) * 27d / outs : 99;
    }
    static IEnumerable<(string Name, int Value)> HighlightPlayers(string value)
    {
        foreach (var entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var withoutSeason = entry.Split('(')[0].Trim(); var split = withoutSeason.LastIndexOf(' ');
            if (split > 0 && int.TryParse(withoutSeason[(split + 1)..], out var count)) yield return (withoutSeason[..split], count);
            else if (withoutSeason.Length > 0) yield return (withoutSeason, 1);
        }
    }
    var batters = summaries.SelectMany(game => new[] { game.BoxScore.Away, game.BoxScore.Home }.SelectMany(team => team.Batting.Select(player => new { player.Name, Team = team.TeamName, HR = Number(player.HomeRuns), D2 = Number(player.Doubles), D3 = Number(player.Triples), H = Number(player.Hits), SB = Number(player.StolenBases), CS=Number(player.CaughtStealing) })))
        .GroupBy(player=>new {player.Name,player.Team}).Select(group=>new {group.Key.Name,group.Key.Team,HR=group.Sum(player=>player.HR),D2=group.Sum(player=>player.D2),D3=group.Sum(player=>player.D3),H=group.Sum(player=>player.H),SB=group.Sum(player=>player.SB),CS=group.Sum(player=>player.CS)}).ToList();
    var pitchers = summaries.SelectMany(game => new[] { game.BoxScore.Away, game.BoxScore.Home }.SelectMany(team => team.Pitching.Select(player => new { player.Name, Team = team.TeamName, SO = Number(player.Strikeouts), IP = player.InningsPitched, GameERA = GameEra(player.InningsPitched, player.EarnedRuns) }))).ToList();
    var fielders = summaries.SelectMany(game => new[] { game.BoxScore.Away, game.BoxScore.Home }.SelectMany(team => team.Highlights.Where(item => item.Section == "Fielding" && (item.Label == "OFA" || item.Label == "DP")).SelectMany(item => HighlightPlayers(item.Value).Select(player => new { player.Name, Team = team.TeamName, Category = item.Label, player.Value })))).ToList();
    var offense = batters.OrderByDescending(player=>player.HR).Where(player=>player.HR>0).Take(3).Select(player=>new {category="Home Runs",player.Name,player.Team,value=player.HR})
        .Concat(batters.OrderByDescending(player=>player.D3).Where(player=>player.D3>0).Take(3).Select(player=>new {category="Triples",player.Name,player.Team,value=player.D3}))
        .Concat(batters.OrderByDescending(player=>player.D2).Where(player=>player.D2>0).Take(3).Select(player=>new {category="Doubles",player.Name,player.Team,value=player.D2}))
        .Concat(batters.OrderByDescending(player=>player.H).Where(player=>player.H>0).Take(3).Select(player=>new {category="Hits",player.Name,player.Team,value=player.H})).ToList();
    var pitchingLeaders=pitchers.OrderByDescending(player=>player.SO).Where(player=>player.SO>0).Take(3).Select(player=>new { category="Strikeouts", player.Name, player.Team, value=player.SO, detail=$"{player.SO}" })
        .Concat(pitchers.OrderByDescending(player=>double.TryParse(player.IP,out var ip)?ip:0).Take(3).Select(player=>new {category="Innings Pitched",player.Name,player.Team,value=0,detail=player.IP})).ToList();
    var running=batters.OrderByDescending(player=>player.SB).Where(player=>player.SB>0).Take(5).Select(player=>new { category="Stolen Bases",player.Name,player.Team,value=player.SB })
        .Concat(batters.OrderByDescending(player=>player.CS).Where(player=>player.CS>0).Take(5).Select(player=>new {category="Caught Stealing",player.Name,player.Team,value=player.CS})).ToList();
    var allDefense=fielders.GroupBy(player=>new {player.Name,player.Team,player.Category}).Select(group=>new {category=group.Key.Category,group.Key.Name,group.Key.Team,value=group.Sum(player=>player.Value)}).ToList();
    var defense=allDefense.Where(player=>player.category=="OFA"&&player.value>0).OrderByDescending(player=>player.value).Take(5).Select(player=>new {category="Outfield Assists",player.Name,player.Team,player.value}).ToList();
    var alerts = summaries.SelectMany(game =>
    {
        var result = new List<object>();
        var final = (game.Status + " " + game.DetailedStatus).Contains("Final", StringComparison.OrdinalIgnoreCase);
        if (final) result.Add(new { gamePk = game.GamePk, kind = "final", text = $"FINAL: {game.AwayTeam} {game.AwayScore}, {game.HomeTeam} {game.HomeScore}" });
        var homers = game.ScoringPlays.Where(play => play.Event.Contains("Home Run", StringComparison.OrdinalIgnoreCase) || play.Description.Contains("homers", StringComparison.OrdinalIgnoreCase));
        result.AddRange(homers.Select(play => (object)new { gamePk = game.GamePk, kind = "home-run", text = $"HOME RUN: {play.Batter} — {game.AwayTeam} {play.AwayScore}, {game.HomeTeam} {play.HomeScore}" }));
        var special = game.LastPlay.Contains("no-hitter", StringComparison.OrdinalIgnoreCase) || game.LastPlay.Contains("perfect game", StringComparison.OrdinalIgnoreCase);
        if (special) result.Add(new { gamePk = game.GamePk, kind = "special", text = game.LastPlay });
        return result;
    }).ToList();
    var payload = new
    {
        offense, pitching = pitchingLeaders, running, defense, alerts, updatedAt = DateTimeOffset.UtcNow
    };
    cache.Set(key, payload, TimeSpan.FromSeconds(15));
    return Results.Ok(payload);
});

app.MapGet("/api/integrations/eztv/feed", async (
    DateOnly? date,
    long? gamePk,
    string? schema,
    IMlbStatsClient mlb,
    CancellationToken ct) =>
{
    var targetDate = date ?? DateOnly.FromDateTime(DateTime.Now);
    var games = await mlb.GetScheduleAsync(targetDate, ct);
    GameSummary? selectedGame = null;
    if (gamePk.HasValue)
        selectedGame = await mlb.GetGameSummaryAsync(gamePk.Value, ct);

    var selections = (schema ?? "all").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
    object? selectedPayload = selectedGame;
    if (selectedGame is not null && !selections.Contains("all"))
    {
        static Dictionary<string, object?> TeamData(TeamBoxScore team, string side, HashSet<string> selected)
        {
            var value = new Dictionary<string, object?> { ["teamId"] = team.TeamId, ["teamName"] = team.TeamName };
            if (selected.Contains($"{side}-offense")) value["offense"] = team.Batting;
            if (selected.Contains($"{side}-pitching")) value["pitching"] = team.Pitching;
            if (selected.Contains($"{side}-defense")) value["defense"] = team.Highlights.Where(item => item.Section.Equals("Fielding", StringComparison.OrdinalIgnoreCase));
            if (selected.Contains($"{side}-baserunning")) value["baserunning"] = team.Highlights.Where(item => item.Section.Equals("Baserunning", StringComparison.OrdinalIgnoreCase));
            return value;
        }
        selectedPayload = new Dictionary<string, object?>
        {
            ["gamePk"] = selectedGame.GamePk, ["gameDate"] = selectedGame.GameDate, ["status"] = selectedGame.DetailedStatus,
            ["venue"] = selectedGame.Venue, ["lineScore"] = selectedGame.LineScore, ["scoringPlays"] = selectedGame.ScoringPlays,
            ["away"] = TeamData(selectedGame.BoxScore.Away, "away", selections), ["home"] = TeamData(selectedGame.BoxScore.Home, "home", selections)
        };
    }

    return Results.Ok(new
    {
        source = "VITEC Scoreboard",
        version = applicationVersion,
        generatedAt = DateTimeOffset.UtcNow,
        date = targetDate.ToString("yyyy-MM-dd"),
        games,
        selectedGame = selectedPayload,
        schema = selections
    });
});

app.MapGet("/api/integrations/eztv/feed.xml", async (
    DateOnly? date,
    long? gamePk,
    string? schema,
    IMlbStatsClient mlb,
    CancellationToken ct) =>
{
    var targetDate = date ?? DateOnly.FromDateTime(DateTime.Now);
    var games = await mlb.GetScheduleAsync(targetDate, ct);
    GameSummary? selectedGame = null;
    if (gamePk.HasValue)
        selectedGame = await mlb.GetGameSummaryAsync(gamePk.Value, ct);
    var selections = (schema ?? "all").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);

    static XElement TeamElement(string name, TeamScore team) =>
        new(name,
            new XElement("id", team.TeamId),
            new XElement("name", team.Name),
            new XElement("score", team.Score),
            new XElement("wins", team.Wins),
            new XElement("losses", team.Losses));

    static XElement BoxTeamElement(string name, TeamBoxScore team, string side, HashSet<string> selected)
    {
        var element = new XElement(name, new XAttribute("id", team.TeamId), new XElement("name", team.TeamName));
        if (selected.Contains("all") || selected.Contains($"{side}-offense")) element.Add(new XElement("offense", team.Batting.Select(player => new XElement("player", new XAttribute("id", player.PlayerId), new XElement("name", player.Name), new XElement("position", player.Position), new XElement("atBats", player.AtBats), new XElement("runs", player.Runs), new XElement("hits", player.Hits), new XElement("rbi", player.Rbi), new XElement("homeRuns", player.HomeRuns), new XElement("average", player.Average)))));
        if (selected.Contains("all") || selected.Contains($"{side}-pitching")) element.Add(new XElement("pitching", team.Pitching.Select(player => new XElement("player", new XAttribute("id", player.PlayerId), new XElement("name", player.Name), new XElement("role", player.Role), new XElement("inningsPitched", player.InningsPitched), new XElement("strikeouts", player.Strikeouts), new XElement("era", player.Era), new XElement("pitchCount", player.PitchCount)))));
        if (selected.Contains("all") || selected.Contains($"{side}-defense")) element.Add(new XElement("defense", team.Highlights.Where(item => item.Section.Equals("Fielding", StringComparison.OrdinalIgnoreCase)).Select(item => new XElement("stat", new XAttribute("name", item.Label), item.Value))));
        if (selected.Contains("all") || selected.Contains($"{side}-baserunning")) element.Add(new XElement("baserunning", team.Highlights.Where(item => item.Section.Equals("Baserunning", StringComparison.OrdinalIgnoreCase)).Select(item => new XElement("stat", new XAttribute("name", item.Label), item.Value))));
        return element;
    }

    var document = new XDocument(
        new XElement("vitecScoreboard",
            new XAttribute("version", applicationVersion),
            new XElement("generatedAt", DateTimeOffset.UtcNow.ToString("O")),
            new XElement("date", targetDate.ToString("yyyy-MM-dd")),
            new XElement("games", games.Select(game =>
                new XElement("game",
                    new XAttribute("gamePk", game.GamePk),
                    new XElement("status", game.Status),
                    new XElement("detailedStatus", game.DetailedStatus),
                    new XElement("inning", game.CurrentInning),
                    new XElement("inningState", game.InningState),
                    new XElement("inningOrdinal", game.InningOrdinal),
                    new XElement("venue", game.Venue),
                    TeamElement("away", game.Away),
                    TeamElement("home", game.Home)))),
            selectedGame is null
                ? null
                : new XElement("selectedGame",
                    new XAttribute("gamePk", selectedGame.GamePk),
                    new XElement("awayTeam", selectedGame.AwayTeam),
                    new XElement("homeTeam", selectedGame.HomeTeam),
                    new XElement("awayScore", selectedGame.AwayScore),
                    new XElement("homeScore", selectedGame.HomeScore),
                    new XElement("status", selectedGame.DetailedStatus),
                    new XElement("inning", selectedGame.Inning),
                    new XElement("inningState", selectedGame.InningState),
                    new XElement("lastPlay", selectedGame.LastPlay),
                    BoxTeamElement("away", selectedGame.BoxScore.Away, "away", selections),
                    BoxTeamElement("home", selectedGame.BoxScore.Home, "home", selections))));

    return Results.Text(document.ToString(), "application/xml");
});

app.MapGet("/api/mlb/games/{gamePk:long}/pitches", async (
    long gamePk,
    IMlbStatsClient mlb,
    IMemoryCache cache,
    ILoggerFactory loggerFactory,
    CancellationToken ct) =>
{
    try
    {
        var key = $"mlb:pitches:{gamePk}";
        if (!cache.TryGetValue(key, out IReadOnlyList<VS.Core.Models.Pitch>? pitches))
        {
            pitches = await mlb.GetPitchesAsync(gamePk, ct);
            cache.Set(key, pitches, TimeSpan.FromSeconds(10));
        }

        return Results.Ok(pitches);
    }
    catch (Exception ex)
    {
        loggerFactory.CreateLogger("VS.Pitches")
            .LogError(ex, "Failed to load pitches for gamePk {GamePk}", gamePk);

        return Results.Problem(
            title: "Unable to load pitch analytics",
            detail: ex.Message,
            statusCode: StatusCodes.Status502BadGateway);
    }
});


app.MapGet("/api/mlb/games/{gamePk:long}/summary", async (
    long gamePk,
    IMlbStatsClient mlb,
    IMemoryCache cache,
    ILoggerFactory loggerFactory,
    CancellationToken ct) =>
{
    try
    {
        var key = $"mlb:summary:{gamePk}";
        if (!cache.TryGetValue(key, out VS.Core.Models.GameSummary? summary))
        {
            summary = await mlb.GetGameSummaryAsync(gamePk, ct);
            cache.Set(key, summary, TimeSpan.FromSeconds(3));
        }

        var displaySummary = summary is null
            ? null
            : summary with { ScheduledStart = FormatDisplayTime(summary.GameDate) };

        return Results.Ok(displaySummary);
    }
    catch (Exception ex)
    {
        loggerFactory.CreateLogger("VS.GameSummary")
            .LogError(ex, "Failed to load live summary for gamePk {GamePk}", gamePk);

        return Results.Problem(
            title: "Unable to load live game summary",
            detail: ex.Message,
            statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/api/mlb/games/{gamePk:long}/gamecenter", async (
    long gamePk,
    IMlbStatsClient mlb,
    IMemoryCache cache,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var store = services.GetService<HistoricalPitchStore>();
    var key = $"mlb:gamecenter:{gamePk}";
    if (!cache.TryGetValue(key, out VS.Core.Models.GameCenter? gameCenter))
    {
        gameCenter = await mlb.GetGameCenterAsync(gamePk, ct);
        cache.Set(key, gameCenter, TimeSpan.FromSeconds(5));
    }

    // Best-effort incremental ingestion. A database outage must not break GameCenter.
    if (store is not null && gameCenter is not null)
    {
        try
        {
            await store.IngestAsync(gameCenter, ct);
        }
        catch
        {
            // Database status endpoint exposes connection/ingestion problems.
        }
    }

    return Results.Ok(gameCenter);
});

app.MapPost("/api/history/import/{gamePk:long}", async (
    long gamePk,
    IMlbStatsClient mlb,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var store = services.GetService<HistoricalPitchStore>();
    if (store is null)
        return Results.BadRequest(new { message = "PostgreSQL is not configured." });

    var game = await mlb.GetGameCenterAsync(gamePk, ct);
    var result = await store.IngestAsync(game, ct);
    return Results.Ok(result);
});

app.MapPost("/api/history/import-date", async (
    string? date,
    IMlbStatsClient mlb,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var store = services.GetService<HistoricalPitchStore>();
    if (store is null)
        return Results.BadRequest(new { message = "PostgreSQL is not configured." });

    var targetDate = DateOnly.TryParse(date, out var parsed)
        ? parsed
        : DateOnly.FromDateTime(DateTime.Now);

    var schedule = await mlb.GetScheduleAsync(targetDate, ct);
    var results = new List<object>();

    foreach (var scheduled in schedule)
    {
        try
        {
            var game = await mlb.GetGameCenterAsync(scheduled.GamePk, ct);
            var ingest = await store.IngestAsync(game, ct);
            results.Add(new
            {
                gamePk = scheduled.GamePk,
                game = $"{scheduled.Away.Name} at {scheduled.Home.Name}",
                ingest.Seen,
                ingest.Inserted,
                ingest.IsFinal,
                ingest.Result
            });
        }
        catch (Exception ex)
        {
            results.Add(new
            {
                gamePk = scheduled.GamePk,
                game = $"{scheduled.Away.Name} at {scheduled.Home.Name}",
                result = "ERROR",
                message = ex.Message
            });
        }
    }

    return Results.Ok(new { date = targetDate, games = results });
});

app.MapGet("/api/history/export", async (string? date, string? format, CancellationToken ct) =>
{
    var connectionString = GetCurrentPostgresConnectionString();
    if (string.IsNullOrWhiteSpace(connectionString)) return Results.BadRequest(new { message = "PostgreSQL is not configured." });
    var targetDate = DateOnly.TryParse(date, out var parsed) ? parsed : DateOnly.FromDateTime(DateTime.Now);
    var start = new DateTimeOffset(targetDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); var end = start.AddDays(1);
    var options = new DbContextOptionsBuilder<VsDbContext>().UseNpgsql(connectionString).Options;
    await using var db = new VsDbContext(options);
    var games = await db.Games.AsNoTracking().Where(game => game.GameDate >= start && game.GameDate < end).OrderBy(game => game.GameDate).Select(game => new { game.GamePk, game.GameDate, game.AwayTeamId, game.AwayTeam, game.HomeTeamId, game.HomeTeam, game.AwayScore, game.HomeScore, game.Status, game.DetailedStatus, game.Venue, game.IsFinal }).ToListAsync(ct);
    var gamePks = games.Select(game => game.GamePk).ToArray();
    var pitches = await db.Pitches.AsNoTracking().Where(pitch => gamePks.Contains(pitch.GamePk)).OrderBy(pitch => pitch.GamePk).ThenBy(pitch => pitch.AtBatIndex).ThenBy(pitch => pitch.PitchNumber).Select(pitch => new { pitch.GamePk, pitch.PlayId, pitch.AtBatIndex, pitch.PitchNumber, pitch.PitchCode, pitch.PitchType, pitch.Result, pitch.StartSpeedMph, pitch.EndSpeedMph, pitch.PlateX, pitch.PlateZ, pitch.StrikeZoneTop, pitch.StrikeZoneBottom, pitch.SpinRate, pitch.HorizontalBreak, pitch.VerticalBreak, pitch.Extension, pitch.Zone, pitch.BatterId, pitch.Batter, pitch.PitcherId, pitch.Pitcher, pitch.BatSide, pitch.PitchHand }).ToListAsync(ct);
    if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
    {
        static string Csv(object? value) { var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? ""; return $"\"{text.Replace("\"", "\"\"")}\""; }
        var lines = new List<string> { "GamePk,GameDate,AwayTeam,HomeTeam,AwayScore,HomeScore,Venue,PlayId,AtBatIndex,PitchNumber,PitchCode,PitchType,Result,StartSpeedMph,PlateX,PlateZ,Batter,Pitcher,BatSide,PitchHand" };
        var gameLookup = games.ToDictionary(game => game.GamePk);
        foreach (var pitch in pitches) { var game = gameLookup[pitch.GamePk]; lines.Add(string.Join(',', new object?[] { game.GamePk, game.GameDate, game.AwayTeam, game.HomeTeam, game.AwayScore, game.HomeScore, game.Venue, pitch.PlayId, pitch.AtBatIndex, pitch.PitchNumber, pitch.PitchCode, pitch.PitchType, pitch.Result, pitch.StartSpeedMph, pitch.PlateX, pitch.PlateZ, pitch.Batter, pitch.Pitcher, pitch.BatSide, pitch.PitchHand }.Select(Csv))); }
        return Results.File(System.Text.Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines)), "text/csv", $"vitec-scoreboard-{targetDate:yyyy-MM-dd}.csv");
    }
    var bytes = JsonSerializer.SerializeToUtf8Bytes(new { source = "VITEC Scoreboard", exportedAt = DateTimeOffset.UtcNow, date = targetDate, games, pitches }, new JsonSerializerOptions { WriteIndented = true });
    return Results.File(bytes, "application/json", $"vitec-scoreboard-{targetDate:yyyy-MM-dd}.json");
});

app.MapGet("/api/analytics/pitches", async (
    string? from,
    string? to,
    string? pitcher,
    string? batter,
    string? pitchType,
    int? limit,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var store = services.GetService<HistoricalPitchStore>();
    if (store is null)
        return Results.BadRequest(new { message = "PostgreSQL is not configured." });

    DateTimeOffset? fromDate = DateTimeOffset.TryParse(from, out var parsedFrom) ? parsedFrom : null;
    DateTimeOffset? toDate = DateTimeOffset.TryParse(to, out var parsedTo) ? parsedTo : null;

    var pitches = await store.QueryPitchesAsync(
        fromDate,
        toDate,
        pitcher,
        batter,
        pitchType,
        limit ?? 20000,
        ct);

    return Results.Ok(pitches);
});


app.MapGet("/api/setup/status", async (
    IServiceProvider services,
    CancellationToken ct) =>
{
    var store = services.GetService<HistoricalPitchStore>();
    object dbStatus;
    if (store is null)
    {
        dbStatus = new
        {
            configured = false,
            canConnect = false,
            games = 0,
            pitches = 0,
            latestGameDate = (DateTimeOffset?)null,
            message = "PostgreSQL is not configured."
        };
    }
    else
    {
        dbStatus = await store.GetStatusAsync(ct);
    }

    return Results.Ok(new
    {
        version = applicationVersion,
        service = "VITEC Soccer Scoreboard",
        listenUrl = builder.Configuration["VS:ListenUrl"] ?? "http://0.0.0.0:5100",
        postgresConfigured = !string.IsNullOrWhiteSpace(GetCurrentPostgresConnectionString()),
        database = dbStatus,
        settingsFile = vsConfigPath,
        displayTimeZoneId = builder.Configuration["VS:DisplayTimeZone"] ?? "SERVER_LOCAL",
        effectiveDisplayTimeZoneId = EffectiveDisplayTimeZoneId(),
        effectiveDisplayTimeZoneName = ResolveDisplayTimeZone().DisplayName
    });
});

app.MapGet("/api/soccer/matches", async (string? date, ISoccerStatsClient soccer, CancellationToken ct) =>
{
    var targetDate = DateOnly.TryParse(date, out var parsed) ? parsed : DateOnly.FromDateTime(DateTime.Today);
    return Results.Ok(await soccer.GetScheduleAsync(targetDate, ct));
});

app.MapGet("/api/soccer/matches/{matchId}/matchcenter", async (string matchId, ISoccerStatsClient soccer, CancellationToken ct) =>
    Results.Ok(await soccer.GetMatchCenterAsync(matchId, ct)));

app.MapGet("/api/soccer/team-logo", async (string name, string? code, SportradarLogoClient logos, HttpContext context, CancellationToken ct) =>
{
    try
    {
        var logo = await logos.GetTeamLogoAsync(name, code, ct);
        if (logo is null) return Results.NotFound();
        context.Response.Headers.CacheControl = "public,max-age=43200";
        context.Response.Headers["X-Image-Copyright"] = logo.Copyright;
        return Results.File(logo.Bytes, logo.ContentType);
    }
    catch (HttpRequestException) { return Results.NotFound(); }
});

app.MapGet("/api/soccer/team-logo/status", (SportradarLogoClient logos) => Results.Ok(new { provider = "Sportradar Images API v3", configured = logos.IsConfigured }));

app.MapGet("/api/soccer/standings", async (ISoccerStatsClient soccer, IMemoryCache cache, CancellationToken ct) =>
{
    const string key = "soccer:standings";
    if (!cache.TryGetValue(key, out IReadOnlyList<SoccerStanding>? standings))
    {
        standings = await soccer.GetStandingsAsync(ct);
        cache.Set(key, standings, TimeSpan.FromMinutes(5));
    }
    return Results.Ok(standings);
});

app.MapGet("/api/soccer/daily-summary", async (string? date, ISoccerStatsClient soccer, IMemoryCache cache, CancellationToken ct) =>
{
    var target = DateOnly.TryParse(date, out var parsed) ? parsed : DateOnly.FromDateTime(DateTime.Today);
    var key = $"soccer:daily:{target:yyyy-MM-dd}";
    if (!cache.TryGetValue(key, out SoccerDailySummary? summary)) { summary = await soccer.GetDailySummaryAsync(target, ct); cache.Set(key, summary, TimeSpan.FromSeconds(30)); }
    return Results.Ok(summary);
});

app.MapGet("/api/soccer/workspaces", (SoccerWorkspaceStore store) => Results.Ok(new[] { SoccerWorkspaceStore.Default() }.Concat(store.List())));
app.MapPost("/api/soccer/workspaces", async (HttpRequest request, SoccerWorkspaceStore store) =>
{
    try { var value = await request.ReadFromJsonAsync<SoccerWorkspace>(); return value is null ? Results.BadRequest(new { message = "Workspace data is required." }) : Results.Ok(store.Save(value)); }
    catch (Exception ex) { return Results.BadRequest(new { message = ex.Message }); }
});
app.MapDelete("/api/soccer/workspaces/{id}", (string id, SoccerWorkspaceStore store) => id.Equals("default", StringComparison.OrdinalIgnoreCase) ? Results.BadRequest(new { message = "The default workspace cannot be deleted." }) : store.Delete(id) ? Results.Ok() : Results.NotFound());

app.MapGet("/api/integrations/soccer/feed", async (string? date, string? matchId, ISoccerStatsClient soccer, CancellationToken ct) =>
{
    var target = DateOnly.TryParse(date, out var parsed) ? parsed : DateOnly.FromDateTime(DateTime.Today);
    var matches = await soccer.GetScheduleAsync(target, ct);
    var selected = string.IsNullOrWhiteSpace(matchId) ? null : await soccer.GetMatchCenterAsync(matchId, ct);
    return Results.Ok(new { source = "VITEC Soccer Scoreboard", schemaVersion = "1.0", generatedAt = DateTimeOffset.UtcNow, date = target, matches, selectedMatch = selected });
});

app.MapGet("/api/integrations/soccer/feed.xml", async (string? date, string? matchId, ISoccerStatsClient soccer, CancellationToken ct) =>
{
    var target = DateOnly.TryParse(date, out var parsed) ? parsed : DateOnly.FromDateTime(DateTime.Today);
    var matches = await soccer.GetScheduleAsync(target, ct);
    var selected = string.IsNullOrWhiteSpace(matchId) ? null : await soccer.GetMatchCenterAsync(matchId, ct);
    static XElement Team(string name, SoccerTeam team) => new(name, new XAttribute("id", team.TeamId), new XElement("name", team.Name), new XElement("code", team.Code), new XElement("score", team.Score));
    var xml = new XDocument(new XElement("vitecSoccerScoreboard", new XAttribute("schemaVersion", "1.0"), new XElement("generatedAt", DateTimeOffset.UtcNow.ToString("O")), new XElement("date", target.ToString("yyyy-MM-dd")),
        new XElement("matches", matches.Select(m => new XElement("match", new XAttribute("id", m.MatchId), new XElement("kickoff", m.PlannedKickoff.ToString("O")), new XElement("status", m.Status), new XElement("minute", m.Minute), new XElement("competition", m.Competition), Team("away", m.Away), Team("home", m.Home)))),
        selected is null ? null : new XElement("selectedMatch", new XAttribute("id", selected.Match.MatchId), new XElement("events", selected.Events.Select(e => new XElement("event", new XAttribute("id", e.EventId), new XAttribute("type", e.Type), new XElement("minute", e.Minute), new XElement("team", e.TeamName), new XElement("player", e.PlayerName), new XElement("description", e.Description), e.ExpectedGoals is null ? null : new XElement("expectedGoals", e.ExpectedGoals)))))));
    return Results.Text(xml.ToString(), "application/xml");
});

app.MapFallbackToFile("index.html");

app.Run();
