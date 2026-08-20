namespace VS.Web;

public sealed record TeamLogoFile(byte[] Bytes, string ContentType, string Source);

public sealed class MlsTeamLogoClient(HttpClient httpClient, SoccerTeamLogoStore logoStore)
{
    private const string Root = "https://images.mlssoccer.com/image/upload/t_club_logo_medium/";
    private readonly string _cacheDirectory = CreateCacheDirectory(logoStore.DirectoryPath);
    private static readonly IReadOnlyDictionary<string, string> PublicIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ATL"] = "assets/mnp/Club_Logo-Atlanta_ugeyc3_hw47tg.png",
        ["ATX"] = "assets/logos/mls-clubs/Club_Logo-Austin_pa9xtu.png",
        ["CLT"] = "assets/logos/mls-clubs/Club_Logo-Charlotte_p7sznf.png",
        ["CHI"] = "assets/logos/mls-clubs/Club_Logo-Chicago_jm2yev.png",
        ["CIN"] = "assets/logos/mls-clubs/Club_Logo-Cincinnati_jwgkps.png",
        ["COL"] = "assets/logos/mls-clubs/Club_Logo-Colorado_n5kpss.png",
        ["CLB"] = "assets/logos/mls-clubs/Club_Logo-Columbus_light_z3eq8l.png",
        ["DC"] = "assets/logos/mls-clubs/Club_Logo-D.C_t03ekm.png",
        ["DAL"] = "assets/logos/mls-clubs/Club_Logo-Dallas_sysmtj.png",
        ["HOU"] = "assets/logos/mls-clubs/Club_Logo-Houston_oifm77.png",
        ["SKC"] = "assets/logos/mls-clubs/Club_Logo-Kansas_City_cnhd75.png",
        ["LA"] = "assets/logos/mls-clubs/Club_Logo-LA_Galaxy_fg0wjp.png",
        ["LAG"] = "assets/logos/mls-clubs/Club_Logo-LA_Galaxy_fg0wjp.png",
        ["LAFC"] = "assets/logos/mls-clubs/Club_Logo-LAFC_djrhru.png",
        ["MIA"] = "assets/logos/mls-clubs/Club_Logo-Miami_tyqe64.png",
        ["MIN"] = "assets/logos/mls-clubs/Club_Logo-Minnesota_ftweor.png",
        ["MTL"] = "assets/logos/mls-clubs/Club_Logo-Montreal_beeqnh.png",
        ["NSH"] = "assets/logos/mls-clubs/Club_Logo-Nashville_rb9vwu.png",
        ["NE"] = "assets/NE_Logo_PRI_FC_RGB_480x480_fdx2us.png",
        ["RBNY"] = "assets/logos/mls-clubs/RBNY_Logo_v7jpkq.png",
        ["NYC"] = "assets/logos/mls-clubs/Club_Logo-New_York_City_xu6vax.png",
        ["ORL"] = "assets/logos/mls-clubs/Club_Logo-Orlando_ryyn7a.png",
        ["PHI"] = "assets/logos/mls-clubs/Club_Logo-Philadelphia_im7pqg.png",
        ["POR"] = "assets/logos/mls-clubs/Club_Logo-Portland_qihpaz.png",
        ["RSL"] = "assets/logos/mls-clubs/Club_Logo-Salt_Lake_City_hpvde5.png",
        ["SD"] = "assets/logos/mls-clubs/Club_Logo-San_Diego_nwpyul.png",
        ["SDFC"] = "assets/logos/mls-clubs/Club_Logo-San_Diego_nwpyul.png",
        ["SJ"] = "assets/logos/mls-clubs/Club_Logo-San_Jose_opzlmo.png",
        ["SEA"] = "assets/logos/mls-clubs/Club_Logo-Seattle_e6jk2x.png",
        ["STL"] = "assets/logos/mls-clubs/Club_Logo-Saint_Louis_guz12c.png",
        ["TOR"] = "assets/logos/mls-clubs/Club_Logo-Toronto_vz6hao.png",
        ["VAN"] = "assets/logos/mls-clubs/Club_Logo-Vancouver_ao9phl.png"
    };

    private static readonly IReadOnlyDictionary<string, string> TeamCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Atlanta United"]="ATL",["Austin FC"]="ATX",["Charlotte FC"]="CLT",["Chicago Fire FC"]="CHI",["FC Cincinnati"]="CIN",["Colorado Rapids"]="COL",["Columbus Crew"]="CLB",["D.C. United"]="DC",["FC Dallas"]="DAL",["Houston Dynamo FC"]="HOU",["Sporting Kansas City"]="SKC",["LA Galaxy"]="LA",["Los Angeles Football Club"]="LAFC",["Inter Miami CF"]="MIA",["Minnesota United FC"]="MIN",["CF Montréal"]="MTL",["Nashville SC"]="NSH",["New England Revolution"]="NE",["New York Red Bulls"]="RBNY",["New York City Football Club"]="NYC",["Orlando City SC"]="ORL",["Philadelphia Union"]="PHI",["Portland Timbers"]="POR",["Real Salt Lake"]="RSL",["San Diego FC"]="SD",["San Jose Earthquakes"]="SJ",["Seattle Sounders FC"]="SEA",["St. Louis CITY SC"]="STL",["Toronto FC"]="TOR",["Vancouver Whitecaps FC"]="VAN"
    };

    public async Task<TeamLogoFile?> GetAsync(string? name, string? code, CancellationToken ct)
    {
        var resolvedCode = string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name) && TeamCodes.TryGetValue(name.Trim(), out var mapped) ? mapped : code?.Trim();
        if (string.IsNullOrWhiteSpace(resolvedCode) || !PublicIds.TryGetValue(resolvedCode, out var publicId)) return null;
        var cachedPath = Path.Combine(_cacheDirectory, resolvedCode.ToUpperInvariant() + ".png");
        if (File.Exists(cachedPath)) return new TeamLogoFile(await File.ReadAllBytesAsync(cachedPath, ct), "image/png", "Local MLS cache");
        using var response = await httpClient.GetAsync(Root + publicId, ct);
        if (!response.IsSuccessStatusCode) return null;
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length == 0) return null;
        await File.WriteAllBytesAsync(cachedPath, bytes, ct);
        return new TeamLogoFile(bytes, response.Content.Headers.ContentType?.MediaType ?? "image/png", "MLSsoccer.com - cached locally");
    }

    private static string CreateCacheDirectory(string logoDirectory) { var path=Path.Combine(logoDirectory,"MlsCache");Directory.CreateDirectory(path);return path; }
}
