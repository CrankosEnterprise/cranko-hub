using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Radares fixos do OpenStreetMap (<c>highway=speed_camera</c>) para o Modo Carro, pelo Overpass.
/// O Overpass público às vezes fica lotado: tenta quatro servidores e guarda no aparelho (radares.json) tudo o que já
/// baixou, para continuar mostrando os radares conhecidos quando nenhum responder.
/// </summary>
public sealed partial class RadaresOsm(HttpClient http)
{
    private static readonly string[] Servidores =
    [
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
        "https://overpass.private.coffee/api/interpreter",
        "https://maps.mail.ru/osm/tools/overpass/api/interpreter",
    ];

    private const double PedacoMetros = 40_000; // a rota é dividida em caixas de ~40 km (uma caixa só ficaria enorme)
    private const double Folga = 0.004;         // ~400 m em volta da linha

    private static string Arquivo => Path.Combine(ConfigLocal.Pasta, "radares.json");

    private Dictionary<long, RadarRota>? _cache;

    /// <summary>Radares nas caixas em volta das rotas: os do Overpass (se algum servidor responder) + os já guardados no aparelho.</summary>
    public async Task<List<RadarRota>> Perto(IEnumerable<RotaAtiva> rotas)
    {
        var caixas = rotas.SelectMany(Caixas).ToList();
        var cache = Carregar();
        if (caixas.Count == 0) return [];

        var novos = await Consultar(caixas);
        if (novos is not null)
        {
            foreach (var r in novos) cache[r.Id] = r;
            Salvar(cache);
        }
        return cache.Values.Where(r => caixas.Any(c => c.Contem(r.Latitude, r.Longitude))).ToList();
    }

    private async Task<List<RadarRota>?> Consultar(List<Caixa> caixas)
    {
        var partes = string.Concat(caixas.Select(c => $"node[\"highway\"=\"speed_camera\"]({Num(c.S)},{Num(c.O)},{Num(c.N)},{Num(c.L)});"));
        var consulta = $"[out:json][timeout:20];({partes});out body;";
        foreach (var servidor in Servidores)
        {
            try
            {
                using var tempo = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                using var resp = await http.PostAsync(servidor, new FormUrlEncodedContent([new("data", consulta)]), tempo.Token);
                if (!resp.IsSuccessStatusCode) continue;
                using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(tempo.Token));
                var lista = new List<RadarRota>();
                foreach (var e in json.RootElement.GetProperty("elements").EnumerateArray())
                {
                    int? kmh = null;
                    if (e.TryGetProperty("tags", out var tags) && tags.TryGetProperty("maxspeed", out var ms)
                        && Numero().Match(ms.GetString() ?? "") is { Success: true } m && int.TryParse(m.Value, out var v) && v is > 0 and < 200)
                        kmh = v;
                    lista.Add(new RadarRota(e.GetProperty("id").GetInt64(), e.GetProperty("lat").GetDouble(), e.GetProperty("lon").GetDouble(), kmh));
                }
                return lista;
            }
            catch { } // lotado, fora do ar ou demorou: tenta o próximo
        }
        return null;
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex Numero();

    private Dictionary<long, RadarRota> Carregar()
    {
        if (_cache is not null) return _cache;
        try
        {
            if (File.Exists(Arquivo) && JsonSerializer.Deserialize<List<RadarRota>>(File.ReadAllText(Arquivo)) is { } lista)
                return _cache = lista.ToDictionary(r => r.Id);
        }
        catch { }
        return _cache = [];
    }

    private static void Salvar(Dictionary<long, RadarRota> cache)
    {
        try
        {
            Directory.CreateDirectory(ConfigLocal.Pasta);
            File.WriteAllText(Arquivo, JsonSerializer.Serialize(cache.Values.ToList()));
        }
        catch { }
    }

    private readonly record struct Caixa(double S, double O, double N, double L)
    {
        public bool Contem(double lat, double lng) => lat >= S && lat <= N && lng >= O && lng <= L;
    }

    /// <summary>Divide a linha da rota em pedaços de ~40 km e devolve a caixa (com folga) de cada um.</summary>
    private static IEnumerable<Caixa> Caixas(RotaAtiva rota)
    {
        var c = rota.Coordenadas;
        if (c.Length == 0) yield break;
        double s = c[0][1], n = c[0][1], o = c[0][0], l = c[0][0], andado = 0;
        for (var i = 1; i < c.Length; i++)
        {
            andado += RotaService.Distancia(c[i - 1][1], c[i - 1][0], c[i][1], c[i][0]);
            s = Math.Min(s, c[i][1]); n = Math.Max(n, c[i][1]);
            o = Math.Min(o, c[i][0]); l = Math.Max(l, c[i][0]);
            if (andado >= PedacoMetros || i == c.Length - 1)
            {
                yield return new Caixa(s - Folga, o - Folga, n + Folga, l + Folga);
                (s, n, o, l, andado) = (c[i][1], c[i][1], c[i][0], c[i][0], 0);
            }
        }
        if (c.Length == 1) yield return new Caixa(s - Folga, o - Folga, n + Folga, l + Folga);
    }

    private static string Num(double v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
}
