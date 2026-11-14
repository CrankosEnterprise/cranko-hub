using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Radares fixos do estado de São Paulo para o aviso do Modo Carro (Android/Plataforma/RadarCarroService.cs), de três fontes:
/// <list type="bullet">
/// <item><b>DER-SP</b>: rodovias estaduais, inclusive as de concessão. É o <c>radares.xml</c> que a página "Pesquisa de
/// Radares" do DER carrega, com coordenadas, velocidade regulamentada e sentido.</item>
/// <item><b>CET-SP</b>: capital. É o CSV do repositório oficial da CET no GitHub (<c>cetsp-dados/dados-fiscalizacao-eletronica</c>).</item>
/// <item><b>Pardal</b> (<c>murkiriel/pardal</c>, de terceiros, ODbL, atualizado toda semana): só o que nenhuma das outras
/// fontes tem, ou seja, os radares <b>municipais do cadastro do Inmetro</b> (Campinas, Piracicaba, Limeira...), com a posição
/// tirada do endereço, e os das <b>rodovias federais</b> (ANTT, DNIT, PRF).</item>
/// <item><b>OpenStreetMap</b> (<c>highway=speed_camera</c>, pelo Overpass): o resto do estado.</item>
/// </list>
/// Mais a lista de <b>Piracicaba</b> que vem dentro do app (<see cref="RadaresPiracicaba"/>).
/// Dentro da cidade de São Paulo vale só a CET, e dentro de Piracicaba só a lista da prefeitura (<see cref="Municipios"/>):
/// o OSM e o Inmetro podem ter radares já desligados ou fora do lugar. O DER vale em todo lugar (rodovias).
/// Fora dela, o radar do OSM que repete um oficial sai (ver <see cref="Juntar"/>).
/// Cada fonte tem a própria lista e data, e todas ficam no aparelho (radares-sp.json). Atualiza no máximo 1x por semana;
/// se uma fonte falhar, fica a lista anterior dela.
/// </summary>
public sealed partial class RadaresSp
{
    private const string UrlDer = "https://dersp.der.sp.gov.br/Upload/XML/radares.xml";
    private const string UrlPardal = "https://raw.githubusercontent.com/murkiriel/pardal/main/estados/SP/radares.csv";
    private const string UrlCet = "https://raw.githubusercontent.com/cetsp-dados/dados-fiscalizacao-eletronica/main/Base_Locais_Fiscalizados.csv";

    private static readonly string[] ServidoresOsm =
    [
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
        "https://overpass.private.coffee/api/interpreter",
        "https://maps.mail.ru/osm/tools/overpass/api/interpreter",
    ];

    private const string ConsultaOsm =
        "[out:json][timeout:90];area[\"ISO3166-2\"=\"BR-SP\"][admin_level=4]->.sp;node[\"highway\"=\"speed_camera\"](area.sp);out body;";

    private static readonly TimeSpan Validade = TimeSpan.FromDays(7);

    /// <summary>Até onde procura um radar à frente (a maior distância de aviso da engrenagem).</summary>
    public const double Alcance = 2000;

    /// <summary>Um radar a menos disso de um oficial é o mesmo radar.</summary>
    private const double Duplicado = 60;

    /// <summary>Nas rodovias (DER), o mesmo radar às vezes está marcado até ~250 m longe no OSM: com a mesma velocidade, é repetido.</summary>
    private const double DuplicadoRodovia = 250;

    // Cliente próprio: a consulta do Overpass demora mais que o timeout do HttpClient do app, e ele recusa sem User-Agent
    private static readonly HttpClient Http = CriarHttp();

    private static HttpClient CriarHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(100) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(EntretenimentoBusca.UserAgent);
        return http;
    }

    private static string Arquivo => Path.Combine(ConfigLocal.Pasta, "radares-sp.json");

    private Arquivado? _dados;
    private List<RadarSp> _juntos = [];
    private Task? _baixando;
    private readonly object _trava = new();

    /// <summary>Quantos radares valem para o aviso (os oficiais + os do OSM que não repetem nenhum oficial).</summary>
    public int Total
    {
        get
        {
            lock (_trava)
            {
                Dados();
                return _juntos.Count;
            }
        }
    }

    /// <summary>Quantos radares de uma fonte entram no aviso (o OSM já sem os repetidos).</summary>
    public int Contagem(string fonte)
    {
        lock (_trava)
        {
            Dados();
            return _juntos.Count(r => r.Fonte == fonte);
        }
    }

    /// <summary>Quando a fonte foi baixada pela última vez (null = nunca).</summary>
    public DateTime? Atualizado(string fonte)
    {
        lock (_trava) return Dados().Fontes.TryGetValue(fonte, out var f) ? f.Atualizado : null;
    }

    /// <summary>Está baixando agora.</summary>
    public bool Baixando => _baixando is { IsCompleted: false };

    /// <summary>A lista mudou (terminou de baixar).</summary>
    public event Action? Mudou;

    private List<RadarSp> Lista
    {
        get
        {
            lock (_trava)
            {
                Dados();
                return _juntos;
            }
        }
    }

    /// <summary>Baixa as fontes com mais de 7 dias (ou todas, com <paramref name="forcar"/>). Roda em segundo plano.</summary>
    public Task Atualizar(bool forcar = false)
    {
        lock (_trava)
        {
            if (Baixando) return _baixando!;
            var dados = Dados();
            var vencidas = FontesRadar.Todas
                .Where(f => forcar || !dados.Fontes.TryGetValue(f, out var x) || x.Radares.Count == 0 || DateTime.Now - x.Atualizado > Validade)
                .ToList();
            if (vencidas.Count == 0) return Task.CompletedTask;
            _baixando = Task.Run(() => Baixar(vencidas));
        }
        Mudou?.Invoke();
        return _baixando;
    }

    /// <summary>
    /// O radar mais perto à frente do carro (até <see cref="Alcance"/>), dentro de um cone de 40° para cada lado do rumo do GPS.
    /// Quando o OSM informa o eixo, o radar tem de estar na mesma via. Quando o DER informa o sentido, descarta o da outra pista:
    /// o rumo a mais de 120° do sentido. A tolerância é larga porque o "Norte" do DER é o sentido nominal da rodovia, não a
    /// bússola naquele trecho.
    /// </summary>
    public RadarProximo? Proximo(double lat, double lon, double rumo)
    {
        RadarProximo? melhor = null;
        // Filtro rápido em graus (a longitude encolhe com a latitude)
        var dLat = Alcance / 111_000;
        var dLon = Alcance / (111_000 * Math.Cos(lat * Math.PI / 180));
        foreach (var r in Lista)
        {
            if (Math.Abs(r.Lat - lat) > dLat || Math.Abs(r.Lon - lon) > dLon) continue;
            var metros = Distancia(lat, lon, r.Lat, r.Lon);
            if (metros > Alcance || melhor is not null && metros >= melhor.Metros) continue;
            // Muito perto o rumo até o ponto oscila: aceita até passar por ele
            if (metros > 30 && DiferencaAngulo(rumo, Rumo(lat, lon, r.Lat, r.Lon)) > 40) continue;
            if (r.Direcao is { } d && DiferencaAngulo(rumo, d) > 50 && DiferencaAngulo(rumo, d + 180) > 50) continue;
            if (r.Sentido is { } s && DiferencaAngulo(rumo, s) > 120) continue;
            melhor = new RadarProximo(r, metros);
        }
        return melhor;
    }

    // ===== Baixar =====

    private async Task Baixar(List<string> fontes)
    {
        var baixadas = await Task.WhenAll(fontes.Select(async f => (Fonte: f, Radares: await BaixarFonte(f))));
        lock (_trava)
        {
            var dados = Dados();
            foreach (var (fonte, radares) in baixadas)
                if (radares is { Count: > 0 }) dados.Fontes[fonte] = new FonteArquivada(DateTime.Now, radares);
            Juntar();
            Salvar(dados);
        }
        Mudou?.Invoke();
    }

    private static async Task<List<RadarSp>?> BaixarFonte(string fonte)
    {
        try
        {
            return fonte switch
            {
                FontesRadar.Der => LerDer(await Http.GetStreamAsync(UrlDer)),
                FontesRadar.Cet => LerCet(await Http.GetStringAsync(UrlCet)),
                FontesRadar.Pardal => LerPardal(await Http.GetStringAsync(UrlPardal)),
                _ => await BaixarOsm(),
            };
        }
        catch
        {
            return null; // fora do ar ou demorou: fica a lista anterior
        }
    }

    /// <summary>
    /// DER: os <c>&lt;radares&gt;</c> em operação do tipo Controlador (radar) ou Redutor (lombada eletrônica). Os Portáteis
    /// (pontos onde o radar móvel pode ficar) e os não metrológicos (não medem a velocidade) ficam de fora.
    /// </summary>
    internal static List<RadarSp> LerDer(Stream xml)
    {
        var lista = new List<RadarSp>();
        using var leitor = XmlReader.Create(xml, new XmlReaderSettings { IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Ignore });
        while (leitor.ReadToFollowing("radares"))
        {
            var campos = new Dictionary<string, string>();
            using (var item = leitor.ReadSubtree())
            {
                item.Read();
                item.Read();
                // ReadElementContentAsString já avança para o próximo nó: só chama Read quando não leu um campo
                while (!item.EOF)
                {
                    if (item.NodeType == XmlNodeType.Element && item.Depth == 1)
                        campos[item.LocalName] = item.ReadElementContentAsString();
                    else
                        item.Read();
                }
            }
            string Campo(string nome) => campos.TryGetValue(nome, out var v) ? v.Trim() : "";

            var tipo = Campo("TipoRadar");
            if (tipo is not ("Controlador" or "Redutor") || Campo("trc_em_operacao") != "true") continue;
            if (Numero(Campo("Latitude")) is not { } lat || Numero(Campo("Longitude")) is not { } lon || lat == 0 || lon == 0) continue;
            double? sentido = Campo("Orientacao") switch
            {
                "Norte" => 0,
                "Leste" => 90,
                "Sul" => 180,
                "Oeste" => 270,
                _ => null, // "Norte e Sul": os dois sentidos
            };
            lista.Add(new RadarSp($"der:{Campo("RAD_CODIGO")}", lat, lon, PrimeiraVelocidade(Campo("rad_vel_regul")), null,
                FontesRadar.Der, tipo == "Redutor", sentido));
        }
        return lista;
    }

    /// <summary>CET: os locais ativos (sem data de desativação) que fiscalizam a velocidade (metrológico ou enquadramento "V").</summary>
    internal static List<RadarSp> LerCet(string csv)
    {
        var lista = new List<RadarSp>();
        var linhas = csv.TrimStart((char)0xFEFF).Split('\n');
        if (linhas.Length < 2) return lista;
        var cabecalho = LinhaCsv(linhas[0]).Select(c => c.Trim().ToUpperInvariant()).ToList();
        int Coluna(string nome) => cabecalho.FindIndex(c => c.StartsWith(nome, StringComparison.Ordinal));
        int cod = Coluna("CÓDIGO"), desat = Coluna("DESATIVA"), la = Coluna("LATITUDE"), lo = Coluna("LONGITUDE"),
            metro = Coluna("METROL"), vel = Coluna("VELOCIDADE"), enq = Coluna("ENQUADRAMENTO");
        if (la < 0 || lo < 0) return lista;

        foreach (var linha in linhas.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(linha)) continue;
            var c = LinhaCsv(linha);
            string Campo(int i) => i >= 0 && i < c.Count ? c[i].Trim() : "";
            if (Campo(desat).Length > 0) continue;
            var velocidade = Campo(metro).Equals("SIM", StringComparison.OrdinalIgnoreCase)
                || Campo(enq).Split(';').Any(e => e.Trim() == "V");
            if (!velocidade) continue;
            if (Numero(Campo(la)) is not { } lat || Numero(Campo(lo)) is not { } lon || lat == 0 || lon == 0) continue;
            lista.Add(new RadarSp($"cet:{Campo(cod)}:{lista.Count}", lat, lon, PrimeiraVelocidade(Campo(vel)), null, FontesRadar.Cet));
        }
        return lista;
    }

    /// <summary>
    /// Pardal: só os ativos que vêm do Inmetro, da ANTT, do DNIT ou da PRF e de nenhuma das fontes que já baixamos direto
    /// (DER-SP, CET-SP, OSM). Fica de fora o avanço de sinal (RED_LIGHT). O <c>direction_deg</c> é o rumo do trânsito,
    /// calculado pela geometria da via.
    /// </summary>
    internal static List<RadarSp> LerPardal(string csv)
    {
        var lista = new List<RadarSp>();
        var linhas = csv.TrimStart((char)0xFEFF).Split('\n');
        if (linhas.Length < 2) return lista;
        var cabecalho = LinhaCsv(linhas[0]).Select(c => c.Trim()).ToList();
        int la = cabecalho.IndexOf("lat"), lo = cabecalho.IndexOf("lng"), tipo = cabecalho.IndexOf("kind"), lim = cabecalho.IndexOf("limit_kmh"),
            fonte = cabecalho.IndexOf("source"), ativo = cabecalho.IndexOf("active"), dir = cabecalho.IndexOf("direction_deg");
        if (la < 0 || lo < 0 || fonte < 0) return lista;

        foreach (var linha in linhas.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(linha)) continue;
            var c = LinhaCsv(linha);
            string Campo(int i) => i >= 0 && i < c.Count ? c[i].Trim() : "";
            var origem = Campo(fonte);
            if (Campo(ativo) is not ("1" or "true" or "True") || Campo(tipo) == "RED_LIGHT") continue;
            if (origem.Contains("DER-SP") || origem.Contains("CET-SP") || origem.Contains("OSM")) continue;
            if (!(origem.Contains("INMETRO") || origem.Contains("ANTT") || origem.Contains("DNIT") || origem.Contains("PRF"))) continue;
            if (Numero(Campo(la)) is not { } lat || Numero(Campo(lo)) is not { } lon || lat == 0 || lon == 0) continue;
            lista.Add(new RadarSp($"pardal:{lista.Count}:{lat:0.00000},{lon:0.00000}", lat, lon, PrimeiraVelocidade(Campo(lim)), null,
                FontesRadar.Pardal, false, Numero(Campo(dir))));
        }
        return lista;
    }

    private static async Task<List<RadarSp>?> BaixarOsm()
    {
        foreach (var servidor in ServidoresOsm)
        {
            try
            {
                using var resp = await Http.PostAsync(servidor, new FormUrlEncodedContent([new("data", ConsultaOsm)]));
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
                        if (tags.TryGetProperty("maxspeed", out var ms)) kmh = PrimeiraVelocidade(ms.GetString());
                        // Só a direção em graus; "forward/backward" depende da linha da via, que não vem na consulta
                        foreach (var chave in (string[])["camera:direction", "direction"])
                            if (direcao is null && tags.TryGetProperty(chave, out var dir) && Numero(dir.GetString()) is { } graus)
                                direcao = ((graus % 360) + 360) % 360;
                    }
                    lista.Add(new RadarSp($"osm:{e.GetProperty("id").GetInt64()}", la.GetDouble(), lo.GetDouble(), kmh, direcao));
                }
                if (lista.Count > 0) return lista; // vazia = área ainda não pronta no servidor: tenta o próximo
            }
            catch { } // lotado, fora do ar ou demorou: tenta o próximo
        }
        return null;
    }

    /// <summary>
    /// Junta as fontes:
    /// <list type="bullet">
    /// <item>DER e CET entram inteiros.</item>
    /// <item>Piracicaba (lista da prefeitura) entra inteira.</item>
    /// <item>Pardal: só fora de São Paulo e de Piracicaba, e sem os que estão a menos de <see cref="Duplicado"/> m de um oficial.</item>
    /// <item>OSM: só fora de São Paulo e de Piracicaba, e sem os que estão a menos de <see cref="Duplicado"/> m de qualquer outro. Também sai o
    /// que estiver a até <see cref="DuplicadoRodovia"/> m de um do DER com a mesma velocidade (ou sem velocidade): é o mesmo
    /// radar marcado em outro ponto da rodovia.</item>
    /// </list>
    /// </summary>
    private void Juntar()
    {
        var dados = _dados!;
        List<RadarSp> De(string f) => dados.Fontes.TryGetValue(f, out var x) ? x.Radares : [];
        var der = De(FontesRadar.Der);
        var oficiais = der.Concat(De(FontesRadar.Cet)).Concat(RadaresPiracicaba.Lista()).ToList();
        var porOficiais = Grade(oficiais);
        var pardal = De(FontesRadar.Pardal).Where(p => !ListaPropria(p) && !Perto(porOficiais, p, Duplicado, null)).ToList();

        var porTodos = Grade([.. oficiais, .. pardal]);
        var porDer = Grade(der);
        var osm = De(FontesRadar.Osm).Where(o => !ListaPropria(o)
            && !Perto(porTodos, o, Duplicado, null)
            && !Perto(porDer, o, DuplicadoRodovia, r => o.Kmh is null || r.Kmh is null || o.Kmh == r.Kmh));
        _juntos = [.. oficiais, .. pardal, .. osm];
    }

    /// <summary>Cidade com lista oficial própria (CET, prefeitura): lá o OSM e o Inmetro não entram.</summary>
    private static bool ListaPropria(RadarSp r) => Municipios.SaoPaulo.Contem(r.Lat, r.Lon) || Municipios.Piracicaba.Contem(r.Lat, r.Lon);

    /// <summary>Grade de ~0,01° (~1 km) para não comparar cada radar com todos os outros.</summary>
    private static ILookup<(double, double), RadarSp> Grade(IEnumerable<RadarSp> radares) =>
        radares.ToLookup(r => (Math.Floor(r.Lat * 100), Math.Floor(r.Lon * 100)));

    /// <summary>Algum radar da grade a menos de <paramref name="metros"/> (e que passe no <paramref name="igual"/>, se houver).</summary>
    private static bool Perto(ILookup<(double, double), RadarSp> grade, RadarSp o, double metros, Func<RadarSp, bool>? igual)
    {
        var (gl, go) = (Math.Floor(o.Lat * 100), Math.Floor(o.Lon * 100));
        for (var i = -1; i <= 1; i++)
            for (var j = -1; j <= 1; j++)
                foreach (var r in grade[(gl + i, go + j)])
                    if (Distancia(o.Lat, o.Lon, r.Lat, r.Lon) < metros && (igual is null || igual(r))) return true;
        return false;
    }

    // ===== Texto =====

    [GeneratedRegex(@"\d+")]
    private static partial Regex Inteiro();

    /// <summary>"100/080 Km/h" → 100 (carros); "000" ou nada → null.</summary>
    private static int? PrimeiraVelocidade(string? texto) =>
        Inteiro().Match(texto ?? "") is { Success: true } m && int.TryParse(m.Value, out var v) && v is > 0 and < 200 ? v : null;

    private static double? Numero(string? texto) =>
        double.TryParse(texto?.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>Uma linha de CSV com vírgula e campos entre aspas.</summary>
    private static List<string> LinhaCsv(string linha)
    {
        var campos = new List<string>();
        var atual = new StringBuilder();
        var aspas = false;
        for (var i = 0; i < linha.Length; i++)
        {
            var ch = linha[i];
            if (ch == '"')
            {
                if (aspas && i + 1 < linha.Length && linha[i + 1] == '"') { atual.Append('"'); i++; }
                else aspas = !aspas;
            }
            else if (ch == ',' && !aspas) { campos.Add(atual.ToString()); atual.Clear(); }
            else if (ch != '\r') atual.Append(ch);
        }
        campos.Add(atual.ToString());
        return campos;
    }

    // ===== Arquivo =====

    private sealed record FonteArquivada(DateTime Atualizado, List<RadarSp> Radares);

    private sealed class Arquivado
    {
        public Dictionary<string, FonteArquivada> Fontes { get; set; } = [];
    }

    /// <summary>Lê o arquivo na primeira vez (chamar com a trava).</summary>
    private Arquivado Dados()
    {
        if (_dados is not null) return _dados;
        try
        {
            if (File.Exists(Arquivo) && JsonSerializer.Deserialize<Arquivado>(File.ReadAllText(Arquivo)) is { } a) _dados = a;
        }
        catch { } // formato antigo (só o OSM): baixa de novo
        _dados ??= new Arquivado();
        Juntar();
        return _dados;
    }

    private static void Salvar(Arquivado dados)
    {
        try
        {
            Directory.CreateDirectory(ConfigLocal.Pasta);
            File.WriteAllText(Arquivo, JsonSerializer.Serialize(dados));
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
