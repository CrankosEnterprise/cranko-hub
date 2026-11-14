using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Radares fixos do estado de São Paulo para o aviso do Modo Carro (Android/Plataforma/RadarCarroService.cs).
/// Vêm do OpenStreetMap (<c>highway=speed_camera</c>) pelo Overpass, numa consulta só do estado inteiro (capital e rodovias,
/// cerca de 3 mil radares), e ficam guardados no aparelho (radares-sp.json): atualiza no máximo 1x por semana e, sem
/// internet ou com o Overpass lotado, usa o que já baixou.
/// A CET (capital) e o DER (rodovias) não publicam as coordenadas dos radares, por isso a fonte é o OSM.
/// </summary>
public sealed partial class RadaresSp
{
    private static readonly string[] Servidores =
    [
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
        "https://overpass.private.coffee/api/interpreter",
        "https://maps.mail.ru/osm/tools/overpass/api/interpreter",
    ];

    private const string Consulta =
        "[out:json][timeout:90];area[\"ISO3166-2\"=\"BR-SP\"][admin_level=4]->.sp;node[\"highway\"=\"speed_camera\"](area.sp);out body;";

    private static readonly TimeSpan Validade = TimeSpan.FromDays(7);

    /// <summary>Até onde procura um radar à frente.</summary>
    public const double Alcance = 1500;

    // Cliente próprio: a consulta do estado inteiro demora mais que o timeout do HttpClient do app, e o Overpass recusa sem User-Agent
    private static readonly HttpClient Http = CriarHttp();

    private static HttpClient CriarHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(100) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(EntretenimentoBusca.UserAgent);
        return http;
    }

    private static string Arquivo => Path.Combine(ConfigLocal.Pasta, "radares-sp.json");

    private List<RadarSp>? _radares;
    private Task? _baixando;
    private readonly object _trava = new();

    /// <summary>Quantos radares estão guardados no aparelho.</summary>
    public int Total => Lista.Count;

    /// <summary>Quando a lista foi baixada pela última vez (null = nunca).</summary>
    public DateTime? Atualizado { get; private set; }

    /// <summary>Está baixando agora.</summary>
    public bool Baixando => _baixando is { IsCompleted: false };

    /// <summary>A lista mudou (terminou de baixar).</summary>
    public event Action? Mudou;

    private List<RadarSp> Lista
    {
        get
        {
            lock (_trava) return _radares ??= Carregar();
        }
    }

    /// <summary>Baixa a lista se ela tem mais de 7 dias (ou <paramref name="forcar"/>). Não espera: roda em segundo plano.</summary>
    public Task Atualizar(bool forcar = false)
    {
        _ = Lista; // lê o arquivo (e a data) antes de decidir
        lock (_trava)
        {
            if (Baixando) return _baixando!;
            if (!forcar && Atualizado is { } quando && DateTime.Now - quando < Validade && _radares is { Count: > 0 }) return Task.CompletedTask;
            _baixando = Task.Run(Baixar);
            Mudou?.Invoke();
            return _baixando;
        }
    }

    /// <summary>
    /// O radar mais perto à frente do carro (até <see cref="Alcance"/>): dentro de um cone de 40° para cada lado do rumo do GPS
    /// e, quando o OSM informa a direção, na mesma via do carro.
    /// </summary>
    public RadarProximo? Proximo(double lat, double lon, double rumo)
    {
        RadarProximo? melhor = null;
        // Filtro rápido: ~1,5 km em graus (a longitude encolhe com a latitude)
        var dLat = Alcance / 111_000;
        var dLon = Alcance / (111_000 * Math.Cos(lat * Math.PI / 180));
        foreach (var r in Lista)
        {
            if (Math.Abs(r.Lat - lat) > dLat || Math.Abs(r.Lon - lon) > dLon) continue;
            var metros = Distancia(lat, lon, r.Lat, r.Lon);
            if (metros > Alcance || melhor is not null && metros >= melhor.Metros) continue;
            // Muito perto o rumo até o ponto oscila: aceita até passar por ele
            if (metros > 30 && DiferencaAngulo(rumo, Rumo(lat, lon, r.Lat, r.Lon)) > 40) continue;
            // No OSM, a direção em graus ora é para onde a câmera olha, ora o sentido do trânsito: vale como o eixo da via
            // (descarta só o radar de uma rua que cruza a do carro)
            if (r.Direcao is { } d && DiferencaAngulo(rumo, d) > 50 && DiferencaAngulo(rumo, d + 180) > 50) continue;
            melhor = new RadarProximo(r, metros);
        }
        return melhor;
    }

    // ===== Baixar =====

    private async Task Baixar()
    {
        foreach (var servidor in Servidores)
        {
            try
            {
                using var resp = await Http.PostAsync(servidor, new FormUrlEncodedContent([new("data", Consulta)]));
                if (!resp.IsSuccessStatusCode) continue;
                using var json = JsonDocument.Parse(await resp.Content.ReadAsStreamAsync());
                var lista = new List<RadarSp>();
                foreach (var e in json.RootElement.GetProperty("elements").EnumerateArray())
                {
                    if (!e.TryGetProperty("lat", out var la) || !e.TryGetProperty("lon", out var lo)) continue;
                    int? kmh = null;
                    double? direcao = null;
                    if (e.TryGetProperty("tags", out var tags))
                    {
                        if (tags.TryGetProperty("maxspeed", out var ms) && Numero().Match(ms.GetString() ?? "") is { Success: true } m
                            && int.TryParse(m.Value, out var v) && v is > 0 and < 200)
                            kmh = v;
                        // Só a direção em graus; "forward/backward" depende da linha da via, que não vem na consulta
                        foreach (var chave in (string[])["camera:direction", "direction"])
                            if (direcao is null && tags.TryGetProperty(chave, out var dir)
                                && double.TryParse(dir.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var graus))
                                direcao = ((graus % 360) + 360) % 360;
                    }
                    lista.Add(new RadarSp(e.GetProperty("id").GetInt64(), la.GetDouble(), lo.GetDouble(), kmh, direcao));
                }
                if (lista.Count == 0) continue; // resposta vazia (área ainda não pronta no servidor): tenta o próximo
                lock (_trava)
                {
                    _radares = lista;
                    Atualizado = DateTime.Now;
                }
                Salvar(lista);
                break;
            }
            catch { } // lotado, fora do ar ou demorou: tenta o próximo
        }
        Mudou?.Invoke();
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex Numero();

    private sealed record Arquivado(DateTime Atualizado, List<RadarSp> Radares);

    private List<RadarSp> Carregar()
    {
        try
        {
            if (File.Exists(Arquivo) && JsonSerializer.Deserialize<Arquivado>(File.ReadAllText(Arquivo)) is { } a)
            {
                Atualizado = a.Atualizado;
                return a.Radares;
            }
        }
        catch { }
        return [];
    }

    private void Salvar(List<RadarSp> lista)
    {
        try
        {
            Directory.CreateDirectory(ConfigLocal.Pasta);
            File.WriteAllText(Arquivo, JsonSerializer.Serialize(new Arquivado(Atualizado ?? DateTime.Now, lista)));
        }
        catch { }
    }

    // ===== Geometria =====

    /// <summary>Distância em metros entre dois pontos (Haversine).</summary>
    public static double Distancia(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }

    /// <summary>Rumo de um ponto até o outro, em graus (0 = norte, 90 = leste).</summary>
    public static double Rumo(double lat1, double lon1, double lat2, double lon2)
    {
        var f1 = lat1 * Math.PI / 180;
        var f2 = lat2 * Math.PI / 180;
        var dl = (lon2 - lon1) * Math.PI / 180;
        var y = Math.Sin(dl) * Math.Cos(f2);
        var x = Math.Cos(f1) * Math.Sin(f2) - Math.Sin(f1) * Math.Cos(f2) * Math.Cos(dl);
        return (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
    }

    private static double DiferencaAngulo(double a, double b)
    {
        var d = Math.Abs(((a - b) % 360 + 360) % 360);
        return d > 180 ? 360 - d : d;
    }
}
