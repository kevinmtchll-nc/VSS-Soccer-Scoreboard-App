namespace VS.Web;

public sealed record TeamLogoFile(byte[] Bytes, string ContentType, string Source);

public sealed class MlsTeamLogoClient(HttpClient httpClient)
{
    private const string Root = "https://images.mlssoccer.com/image/upload/t_club_logo_medium/";
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

    public async Task<TeamLogoFile?> GetAsync(string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || !PublicIds.TryGetValue(code.Trim(), out var publicId)) return null;
        using var response = await httpClient.GetAsync(Root + publicId, ct);
        if (!response.IsSuccessStatusCode) return null;
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length == 0) return null;
        return new TeamLogoFile(bytes, response.Content.Headers.ContentType?.MediaType ?? "image/png", "MLSsoccer.com");
    }
}
