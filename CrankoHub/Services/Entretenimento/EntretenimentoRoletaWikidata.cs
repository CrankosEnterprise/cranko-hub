using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Roleta sem chave do TMDB/RAWG: filmes, séries, animações e jogos saem de um catálogo da Wikidata (títulos com artigo
/// na Wikipedia em português e em inglês, com gêneros, duração, ano e "fama" = em quantas Wikipedias aparecem).
/// A consulta é pesada (10 a 30 s), então baixa a lista inteira de cada tipo uma vez, em segundo plano assim que a roleta
/// abre, guarda em disco por uma semana e filtra aqui. A capa vem da Wikipedia em inglês (que tem os pôsteres) e o
/// resultado aponta para o artigo em português, como a busca sem chave (fonte "wikipedia").
/// </summary>
public partial class EntretenimentoBusca
{
    /// <summary>Uma obra do catálogo. <see cref="Generos"/> e <see cref="Tipos"/> são ids da Wikidata ("Q157443").</summary>
    private sealed record ObraWikidata(string Id, string Pt, string En, int? Ano, double? Minutos, string[] Generos, string[] Tipos, int Fama);

    private static readonly TimeSpan ValidadeCatalogo = TimeSpan.FromDays(7);
    private static readonly Dictionary<string, Task<List<ObraWikidata>>> Catalogos = [];
    private static readonly object TravaCatalogos = new();

    // Tipos que marcam animação (filme ou série) e anime (que fica na categoria Animes, pelo AniList)
    private static readonly string[] IdsAnimacao = ["Q202866", "Q29168811", "Q581714", "Q117467246"];
    private static readonly string[] IdsAnime = ["Q63952888", "Q20650540"];
    private const string IdMinisserie = "Q1259759";

    // Popularidade mínima (número de Wikipedias) e os campos: título em pt e en, ano, duração, gêneros e tipos
    private const string CamposWikidata = """
        SELECT ?item ?pt ?en (MIN(YEAR(?d)) AS ?ano) (SAMPLE(?dur) AS ?min) (SAMPLE(?s) AS ?fama)
          (GROUP_CONCAT(DISTINCT STRAFTER(STR(?g), "entity/")) AS ?gen) (GROUP_CONCAT(DISTINCT STRAFTER(STR(?t), "entity/")) AS ?tipos)
        """;
    private const string ArtigosWikidata = """
          ?ap schema:about ?item; schema:isPartOf <https://pt.wikipedia.org/>; schema:name ?pt.
          ?ae schema:about ?item; schema:isPartOf <https://en.wikipedia.org/>; schema:name ?en.
          OPTIONAL { ?item wdt:P2047 ?dur. }
          OPTIONAL { ?item wdt:P136 ?g. }
        } GROUP BY ?item ?pt ?en
        """;

    private static readonly Dictionary<string, string> ConsultasWikidata = new()
    {
        ["filmes"] = CamposWikidata + """
             WHERE { VALUES ?t { wd:Q11424 wd:Q29168811 wd:Q202866 }
              ?item wdt:P31 ?t; wikibase:sitelinks ?s. FILTER(?s >= 30)
              OPTIONAL { ?item wdt:P577 ?d. }
            """ + ArtigosWikidata,
        ["series"] = CamposWikidata + """
             WHERE { VALUES ?t0 { wd:Q5398426 wd:Q581714 wd:Q1259759 wd:Q117467246 }
              ?item wdt:P31 ?t0; wikibase:sitelinks ?s. FILTER(?s >= 20)
              ?item wdt:P31 ?t.
              OPTIONAL { ?item wdt:P580|wdt:P577 ?d. }
            """ + ArtigosWikidata,
        ["jogos"] = CamposWikidata + """
             WHERE { VALUES ?t { wd:Q7889 }
              ?item wdt:P31 ?t; wikibase:sitelinks ?s. FILTER(?s >= 20)
              OPTIONAL { ?item wdt:P577 ?d. }
            """ + ArtigosWikidata,
    };

    private static string[] CatalogosDe(string categoria) => categoria switch
    {
        "filme" => ["filmes"],
        "serie" => ["series"],
        "animacao" => ["filmes", "series"],
        _ => ["jogos"],
    };

    /// <summary>A pergunta do tempo vale para a categoria (sem a chave do RAWG, a Wikidata não tem a duração dos jogos).</summary>
    public bool RoletaFiltraTempo(string categoria) => !(categoria == "jogo" && UsaWikidata("jogo"));

    /// <summary>Começa a baixar (ou ler do disco) os catálogos que a roleta vai usar, para estarem prontos ao girar.</summary>
    public void PreCarregarRoleta()
    {
        foreach (var nome in CategoriaEntretenimento.Todas.Where(c => UsaWikidata(c.Chave)).SelectMany(c => CatalogosDe(c.Chave)).Distinct())
            _ = Catalogo(nome);
    }

    private Task<List<ObraWikidata>> Catalogo(string nome)
    {
        lock (TravaCatalogos)
        {
            // Se falhou, tenta de novo na próxima vez
            if (Catalogos.TryGetValue(nome, out var t) && !t.IsFaulted && !t.IsCanceled) return t;
            return Catalogos[nome] = CarregarCatalogo(nome);
        }
    }

    private async Task<List<ObraWikidata>> CarregarCatalogo(string nome)
    {
        var arquivo = Path.Combine(ConfigLocal.Pasta, "cache", $"roleta-{nome}.json");
        List<ObraWikidata>? guardado = null;
        try
        {
            if (File.Exists(arquivo))
            {
                guardado = JsonSerializer.Deserialize<List<ObraWikidata>>(await File.ReadAllTextAsync(arquivo));
                if (guardado is { Count: > 0 } && DateTime.Now - File.GetLastWriteTime(arquivo) < ValidadeCatalogo) return guardado;
            }
        }
        catch { }

        try
        {
            var j = await Get("https://query.wikidata.org/sparql?format=json&query=" + Uri.EscapeDataString(ConsultasWikidata[nome]));
            string? V(JsonNode? b, string campo) => Texto(b?[campo]?["value"]);
            string[] Ids(JsonNode? b, string campo) => V(b, campo)?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
            var lista = (j["results"]?["bindings"]?.AsArray() ?? [])
                .Select(b => new ObraWikidata(
                    V(b, "item")?.Split('/')[^1] ?? "",
                    V(b, "pt") ?? "", V(b, "en") ?? "",
                    int.TryParse(V(b, "ano"), out var ano) ? ano : null,
                    double.TryParse(V(b, "min"), NumberStyles.Float, CultureInfo.InvariantCulture, out var min) ? min : null,
                    Ids(b, "gen"), Ids(b, "tipos"),
                    int.TryParse(V(b, "fama"), out var fama) ? fama : 0))
                .Where(o => o.Pt.Length > 0 && o.En.Length > 0 && !o.En.Contains('|') && !o.Pt.Contains('|'))
                .ToList();
            if (lista.Count == 0) throw new BuscaException("Catálogo vazio.");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
                await File.WriteAllTextAsync(arquivo, JsonSerializer.Serialize(lista));
            }
            catch { }
            return lista;
        }
        // Sem internet (ou a Wikidata fora do ar): o catálogo vencido serve
        catch when (guardado is { Count: > 0 }) { return guardado; }
    }

    private async Task<List<ResultadoBusca>> WikidataDescobrir(string categoria, FiltrosRoleta f, CancellationToken ct)
    {
        static bool Tem(ObraWikidata o, string[] ids) => o.Generos.Any(ids.Contains) || o.Tipos.Any(ids.Contains);

        var candidatos = new List<(ObraWikidata Obra, bool Serie)>();
        foreach (var nome in CatalogosDe(categoria))
        {
            var lista = await Catalogo(nome).WaitAsync(ct);
            candidatos.AddRange(lista
                .Where(o => categoria switch
                {
                    "filme" => !Tem(o, IdsAnimacao) && !Tem(o, IdsAnime),
                    "serie" => !Tem(o, IdsAnimacao) && !Tem(o, IdsAnime),
                    "animacao" => Tem(o, IdsAnimacao) && !Tem(o, IdsAnime),
                    _ => true,
                })
                .Select(o => (o, nome == "series")));
        }

        // Tempo: duração do filme ou do episódio (minutos), minissérie ou não; nos jogos não há essa informação
        bool Cabe((ObraWikidata Obra, bool Serie) c) => (categoria, f.Tempo, c.Serie) switch
        {
            ("jogo", _, _) => true,
            ("animacao", "medio", true) => false,
            ("animacao", "longo", false) => false,
            ("animacao", "longo", true) => true,
            (_, "pouco", false) => c.Obra.Minutos is > 0 and <= 95,
            (_, "medio", false) => c.Obra.Minutos is >= 85 and <= 140,
            (_, "longo", false) => c.Obra.Minutos is >= 140,
            (_, "pouco", true) => c.Obra.Minutos is > 0 and <= 32,
            (_, "medio", true) => c.Obra.Tipos.Contains(IdMinisserie),
            (_, "longo", true) => !c.Obra.Tipos.Contains(IdMinisserie),
            _ => true,
        };

        // Gênero escolhido (ids separados por vírgula) ou os gêneros do clima
        var generos = f.Genero is { Length: > 0 } g
            ? g.Split(',')
            : CatalogosDe(categoria).SelectMany(n => ClimaWikidata(n, f.Clima)).ToArray();
        var epoca = Anos(f.Epoca);
        var (de, ate) = epoca ?? (0, 0);
        var filtrados = candidatos
            .Where(Cabe)
            .Where(c => generos.Length == 0 || c.Obra.Generos.Any(generos.Contains))
            .Where(c => epoca is null || c.Obra.Ano is int a && a >= de && a <= ate)
            .Select(c => c.Obra)
            .ToList();

        // Sem nota na Wikidata: "bem avaliados" fica com a metade mais conhecida e "excelentes" com o quarto mais conhecido
        if (f.NotaMin > 0 && filtrados.Count > 0)
        {
            var fracao = f.NotaMin >= 4 ? 0.25 : 0.5;
            filtrados = filtrados.OrderByDescending(o => o.Fama).Take(Math.Max(1, (int)Math.Ceiling(filtrados.Count * fracao))).ToList();
        }

        var sorteio = filtrados.ToArray();
        Random.Shared.Shuffle(sorteio);
        var escolhidos = sorteio.Take(40).ToList();
        if (escolhidos.Count == 0) return [];

        // Id do artigo em português (para o Detalhar) e o pôster da Wikipedia em inglês
        var paginasPt = PaginasWikipedia("pt", escolhidos.Select(o => o.Pt), "", ct);
        var paginasEn = PaginasWikipedia("en", escolhidos.Select(o => o.En), "&prop=pageimages&piprop=thumbnail&pithumbsize=500&pilicense=any", ct);
        var (pt, en) = (await paginasPt, await paginasEn);

        var resultados = escolhidos
            .Where(o => pt.TryGetValue(o.Pt, out var p) && p["pageid"] is not null)
            .Select(o =>
            {
                var m = Complemento().Match(o.Pt);   // "Lincoln (filme)" → "Lincoln"
                return new ResultadoBusca(
                    m.Success ? o.Pt[..m.Index] : o.Pt, o.Ano,
                    en.TryGetValue(o.En, out var e) ? Texto(e["thumbnail"]?["source"]) : null,
                    null, null,
                    Juntar(o.Generos.Select(id => NomesGeneroWikidata.GetValueOrDefault(id)).Take(3)),
                    "wikipedia", pt[o.Pt]["pageid"]!.ToString());
            })
            .ToList();
        // Na roleta, a capa faz falta: fica só com os que têm, se forem suficientes
        var comCapa = resultados.Where(r => r.UrlCapa is not null).ToList();
        return comCapa.Count >= 6 ? comCapa : resultados;
    }

    /// <summary>Páginas da Wikipedia pelos títulos (até 50), seguindo normalizações e redirecionamentos, indexadas pelo título pedido.</summary>
    private async Task<Dictionary<string, JsonNode>> PaginasWikipedia(string idioma, IEnumerable<string> titulos, string extra, CancellationToken ct)
    {
        var pedidos = titulos.Distinct().Take(50).ToList();
        var j = await Get($"https://{idioma}.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&redirects=1{extra}&titles="
            + Uri.EscapeDataString(string.Join("|", pedidos)), ct: ct);
        var q = j["query"];
        var final = pedidos.ToDictionary(t => t, t => t);
        foreach (var lista in new[] { "normalized", "redirects" })
            foreach (var n in q?[lista]?.AsArray() ?? [])
                if (Texto(n?["from"]) is string de && Texto(n?["to"]) is string para)
                    foreach (var k in final.Where(kv => kv.Value == de).Select(kv => kv.Key).ToList()) final[k] = para;
        var paginas = (q?["pages"]?.AsArray() ?? [])
            .Where(p => p?["missing"] is null && Texto(p?["title"]) is not null)
            .ToDictionary(p => Texto(p!["title"])!, p => p!);
        return final.Where(kv => paginas.ContainsKey(kv.Value)).ToDictionary(kv => kv.Key, kv => paginas[kv.Value]);
    }

    // ---- Gêneros da Wikidata (os mais usados nos títulos conhecidos) ----

    /// <summary>Gêneros do filtro avançado sem chave: nome e os ids da Wikidata que ele junta.</summary>
    private static IReadOnlyList<(string Nome, string[] Ids)> GenerosWikidata(string categoria) => categoria switch
    {
        "filme" => [("Ação", ["Q188473"]), ("Aventura", ["Q319221"]), ("Comédia", ["Q157443", "Q118612349", "Q860626", "Q859369"]),
            ("Crime", ["Q959790", "Q19367312", "Q113485322"]), ("Documentário", ["Q93204"]), ("Drama", ["Q130232"]), ("Família", ["Q1361932"]),
            ("Fantasia", ["Q157394"]), ("Ficção científica", ["Q471839"]), ("Guerra", ["Q369747"]), ("História", ["Q17013749", "Q645928"]),
            ("Mistério", ["Q1200678"]), ("Musical", ["Q842256"]), ("Romance", ["Q1054574", "Q118612349"]), ("Super-herói", ["Q1535153"]),
            ("Terror", ["Q200092"]), ("Thriller", ["Q2484376", "Q11304653", "Q109733304"])],
        "serie" => [("Ação", ["Q343782"]), ("Aventura", ["Q56064758"]), ("Comédia", ["Q9335576", "Q170238", "Q859369", "Q17113138"]),
            ("Crime e policial", ["Q9335577", "Q2321734", "Q11396323", "Q56878968"]), ("Drama", ["Q1366112"]), ("Fantasia", ["Q98526245"]),
            ("Ficção científica", ["Q140472311"]), ("Histórica", ["Q20267837", "Q7168625"]), ("Mistério", ["Q101898470"]),
            ("Romance", ["Q84270297"]), ("Super-herói", ["Q141622854"]), ("Terror", ["Q20220309"]), ("Thriller", ["Q67175872", "Q56320653"])],
        "animacao" => [("Ação", ["Q188473", "Q343782"]), ("Aventura", ["Q319221", "Q56064758"]), ("Comédia", ["Q157443", "Q9335576", "Q170238", "Q859369"]),
            ("Família", ["Q1361932", "Q1273568"]), ("Fantasia", ["Q157394", "Q98526245"]), ("Ficção científica", ["Q471839", "Q140472311"]),
            ("Musical", ["Q842256"]), ("Super-herói", ["Q1535153", "Q141622854"])],
        "jogo" => [("Ação", ["Q270948", "Q343568", "Q1163960"]), ("Aventura", ["Q23916", "Q343568"]), ("Corrida", ["Q860750"]),
            ("Esportes", ["Q868217", "Q1478420"]), ("Estratégia", ["Q472055", "Q208189", "Q2176159", "Q3139142"]), ("Furtividade", ["Q858523"]),
            ("Plataforma", ["Q828322", "Q116790414", "Q116790461"]), ("Quebra-cabeça", ["Q54767"]), ("RPG", ["Q744038", "Q1422746", "Q175173"]),
            ("Terror", ["Q333967", "Q11338014", "Q604725"]), ("Tiro", ["Q185029", "Q380266"]), ("Ficção científica", ["Q27670585"])],
        _ => [],
    };

    /// <summary>Nome em português de cada gênero conhecido (para mostrar e comparar com o seu gosto).</summary>
    private static readonly Dictionary<string, string> NomesGeneroWikidata =
        new[] { "filme", "serie", "animacao", "jogo" }.SelectMany(GenerosWikidata)
            .SelectMany(g => g.Ids.Select(id => (id, g.Nome)))
            .DistinctBy(x => x.id).ToDictionary(x => x.id, x => x.Nome);

    private static string[] ClimaWikidata(string catalogo, string clima) => (catalogo, clima) switch
    {
        ("filmes", "leve") => ["Q157443", "Q118612349", "Q860626", "Q1361932", "Q1054574", "Q842256", "Q859369", "Q663106", "Q1146335"],
        ("filmes", "tenso") => ["Q200092", "Q2484376", "Q959790", "Q1200678", "Q19367312", "Q11304653", "Q109733304", "Q2421031", "Q2297927"],
        ("filmes", "emocionante") => ["Q188473", "Q319221", "Q471839", "Q157394", "Q1535153", "Q369747", "Q20443008", "Q652256"],
        ("filmes", "pensar") => ["Q130232", "Q93204", "Q645928", "Q17013749", "Q113485322", "Q1919632", "Q102429885"],
        ("series", "leve") => ["Q9335576", "Q170238", "Q859369", "Q84270297", "Q17113138", "Q1273568"],
        ("series", "tenso") => ["Q9335577", "Q67175872", "Q20220309", "Q101898470", "Q2321734", "Q56878968", "Q56320653", "Q11396323"],
        ("series", "emocionante") => ["Q343782", "Q140472311", "Q98526245", "Q56064758", "Q141622854", "Q104123273"],
        ("series", "pensar") => ["Q1366112", "Q20267837", "Q7168625", "Q643873", "Q1786567", "Q133731271", "Q112254601"],
        ("jogos", "leve") => ["Q828322", "Q116790414", "Q116790461", "Q860750", "Q54767", "Q1478420", "Q868217"],
        ("jogos", "tenso") => ["Q333967", "Q11338014", "Q604725", "Q858523"],
        ("jogos", "emocionante") => ["Q343568", "Q185029", "Q380266", "Q270948", "Q1163960", "Q1422746", "Q27670585"],
        ("jogos", "pensar") => ["Q208189", "Q2176159", "Q3139142", "Q472055", "Q744038", "Q11084095"],
        _ => [],
    };
}
