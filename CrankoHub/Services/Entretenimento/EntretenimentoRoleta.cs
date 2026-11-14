using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Respostas do quiz da roleta. <see cref="Tipo"/> é a chave de uma <see cref="CategoriaEntretenimento"/> ou "surpresa";
/// <see cref="Tempo"/>: "pouco", "medio", "longo" ou "tantofaz"; <see cref="Clima"/>: "leve", "tenso", "emocionante",
/// "pensar" ou "tantofaz"; <see cref="Epoca"/>: "todas", "recentes", "2010", "1990" ou "classicos".
/// <see cref="Genero"/> (filtro avançado) substitui o clima.
/// </summary>
public record FiltrosRoleta(string Tipo, string Tempo, string Clima, string? Genero, string Epoca, double NotaMin);

/// <summary>
/// Roleta do Entretenimento: descobre títulos novos (que não estão na estante) nas mesmas fontes da busca.
/// Com as chaves, filmes/séries/animações vêm do TMDB e jogos do RAWG; sem elas, de um catálogo da Wikidata
/// (ver EntretenimentoRoletaWikidata.cs). Animes (AniList) e livros (Open Library) não pedem chave.
/// </summary>
public partial class EntretenimentoBusca
{
    /// <summary>
    /// Uma leva de títulos para a roleta (de uma página sorteada da fonte, para variar), sem o que já está na estante.
    /// Na "surpresa", junta todas as categorias.
    /// </summary>
    public async Task<List<ResultadoBusca>> Descobrir(FiltrosRoleta f, CancellationToken ct = default)
    {
        var cats = f.Tipo == "surpresa" ? CategoriaEntretenimento.Todas.ToList() : [CategoriaEntretenimento.Buscar(f.Tipo)];

        var respostas = await Task.WhenAll(cats.Select(async c =>
        {
            try
            {
                var itens = await DescobrirCategoria(c, f, ct);
                return (Itens: itens.Select(r => r with { Categoria = c.Chave }).ToList(), Erro: false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { return (Itens: new List<ResultadoBusca>(), Erro: true); }
        }));
        if (respostas.All(r => r.Erro)) throw new BuscaException("Não foi possível buscar sugestões agora. Verifique a internet.");

        // Fora o que já está na estante (pela fonte ou pelo título)
        var estante = store.Dados.Entretenimento;
        var ids = estante.Where(e => e.FonteId is { Length: > 0 }).Select(e => (e.Fonte, e.FonteId)).ToHashSet();
        var titulos = estante.Select(e => Normalizar(e.Titulo)).ToHashSet();
        return respostas.SelectMany(r => r.Itens)
            .Where(r => r.Titulo.Length > 0 && !ids.Contains((r.Fonte, r.FonteId)) && !titulos.Contains(Normalizar(r.Titulo)))
            .DistinctBy(r => (r.Fonte, r.FonteId))
            .ToList();
    }

    private Task<List<ResultadoBusca>> DescobrirCategoria(CategoriaEntretenimento c, FiltrosRoleta f, CancellationToken ct) => c.Api switch
    {
        "tmdb-filme" or "tmdb-serie" or "tmdb-animacao" when ChaveTmdb() is null => WikidataDescobrir(c.Chave, f, ct),
        "rawg" when ChaveRawg() is null => WikidataDescobrir(c.Chave, f, ct),
        "tmdb-filme" => TmdbDescobrir("movie", f, animacao: false, ct),
        "tmdb-serie" => TmdbDescobrir("tv", f, animacao: false, ct),
        "tmdb-animacao" => AnimacaoDescobrir(f, ct),
        "anilist" => AniListDescobrir(f, ct),
        "rawg" => RawgDescobrir(f, ct),
        _ => OpenLibraryDescobrir(f, ct),
    };

    // ---- Gêneros (filtro avançado e o clima de cada fonte) ----

    /// <summary>Gêneros do filtro avançado de uma categoria (chave enviada à fonte, nome mostrado), conforme a fonte em uso.</summary>
    public IReadOnlyList<(string Chave, string Nome)> GenerosRoleta(string categoria) =>
        UsaWikidata(categoria) ? GenerosWikidata(categoria).Select(g => (string.Join(",", g.Ids), g.Nome)).ToList() : GenerosComChave(categoria);

    /// <summary>A categoria sai do catálogo da Wikidata (falta a chave do TMDB/RAWG).</summary>
    private bool UsaWikidata(string categoria) => CategoriaEntretenimento.Buscar(categoria).Api switch
    {
        "tmdb-filme" or "tmdb-serie" or "tmdb-animacao" => ChaveTmdb() is null,
        "rawg" => ChaveRawg() is null,
        _ => false,
    };

    private static IReadOnlyList<(string Chave, string Nome)> GenerosComChave(string categoria) => categoria switch
    {
        "filme" => [("28", "Ação"), ("12", "Aventura"), ("35", "Comédia"), ("80", "Crime"), ("99", "Documentário"), ("18", "Drama"),
            ("10751", "Família"), ("14", "Fantasia"), ("878", "Ficção científica"), ("10752", "Guerra"), ("36", "História"),
            ("9648", "Mistério"), ("10402", "Música"), ("10749", "Romance"), ("27", "Terror"), ("53", "Thriller"), ("37", "Faroeste")],
        "serie" => [("10759", "Ação e aventura"), ("35", "Comédia"), ("80", "Crime"), ("99", "Documentário"), ("18", "Drama"),
            ("10751", "Família"), ("10765", "Ficção científica e fantasia"), ("10768", "Guerra e política"), ("9648", "Mistério"),
            ("10764", "Reality"), ("37", "Faroeste")],
        "animacao" => [("28", "Ação"), ("12", "Aventura"), ("35", "Comédia"), ("18", "Drama"), ("10751", "Família"),
            ("14", "Fantasia"), ("878", "Ficção científica"), ("9648", "Mistério"), ("10402", "Música")],
        "anime" => [("Action", "Ação"), ("Adventure", "Aventura"), ("Comedy", "Comédia"), ("Drama", "Drama"), ("Sports", "Esportes"),
            ("Fantasy", "Fantasia"), ("Sci-Fi", "Ficção científica"), ("Mecha", "Mecha"), ("Mystery", "Mistério"), ("Music", "Música"),
            ("Psychological", "Psicológico"), ("Romance", "Romance"), ("Slice of Life", "Slice of life"), ("Supernatural", "Sobrenatural"),
            ("Horror", "Terror"), ("Thriller", "Thriller")],
        "jogo" => [("action", "Ação"), ("adventure", "Aventura"), ("arcade", "Arcade"), ("casual", "Casual"), ("racing", "Corrida"),
            ("sports", "Esportes"), ("strategy", "Estratégia"), ("family", "Família"), ("indie", "Indie"), ("fighting", "Luta"),
            ("platformer", "Plataforma"), ("puzzle", "Quebra-cabeça"), ("role-playing-games-rpg", "RPG"), ("simulation", "Simulação"),
            ("shooter", "Tiro")],
        "livro" => [("fantasy", "Fantasia"), ("science_fiction", "Ficção científica"), ("mystery", "Mistério"), ("thriller", "Thriller"),
            ("horror", "Terror"), ("romance", "Romance"), ("humor", "Humor"), ("historical_fiction", "Ficção histórica"),
            ("biography", "Biografia"), ("philosophy", "Filosofia"), ("classics", "Clássicos"), ("adventure", "Aventura")],
        _ => [],
    };

    // Nomes do TMDB em português (os mesmos que o Detalhar grava), para mostrar e comparar com o seu gosto
    private static readonly Dictionary<int, string> GenerosTmdb = new()
    {
        [28] = "Ação", [12] = "Aventura", [16] = "Animação", [35] = "Comédia", [80] = "Crime", [99] = "Documentário", [18] = "Drama",
        [10751] = "Família", [14] = "Fantasia", [36] = "História", [27] = "Terror", [10402] = "Música", [9648] = "Mistério",
        [10749] = "Romance", [878] = "Ficção científica", [10770] = "Cinema TV", [53] = "Thriller", [10752] = "Guerra", [37] = "Faroeste",
        [10759] = "Action & Adventure", [10762] = "Kids", [10763] = "News", [10764] = "Reality", [10765] = "Sci-Fi & Fantasy",
        [10766] = "Soap", [10767] = "Talk", [10768] = "War & Politics",
    };

    /// <summary>Gêneros do TMDB de cada clima (o TMDB usa ids diferentes para filmes e séries).</summary>
    private static int[] ClimaTmdb(string clima, bool filme) => (clima, filme) switch
    {
        ("leve", true) => [35, 10751, 10749, 10402],
        ("leve", false) => [35, 10751],
        ("tenso", true) => [27, 53, 80, 9648],
        ("tenso", false) => [80, 9648],
        ("emocionante", true) => [28, 12, 878, 14],
        ("emocionante", false) => [10759, 10765],
        ("pensar", true) => [18, 99, 36],
        ("pensar", false) => [18, 99, 10768],
        _ => [],
    };

    private static string[] ClimaAniList(string clima) => clima switch
    {
        "leve" => ["Comedy", "Slice of Life", "Romance"],
        "tenso" => ["Horror", "Thriller", "Mystery", "Psychological"],
        "emocionante" => ["Action", "Adventure", "Sci-Fi", "Fantasy", "Mecha", "Sports"],
        "pensar" => ["Drama", "Psychological", "Mystery"],
        _ => [],
    };

    private static string[] ClimaLivro(string clima) => clima switch
    {
        "leve" => ["humor", "romance"],
        "tenso" => ["thriller", "horror", "mystery"],
        "emocionante" => ["fantasy", "science_fiction", "adventure"],
        "pensar" => ["philosophy", "classics", "literary_fiction"],
        _ => ["fiction"],
    };

    /// <summary>Anos de cada época (início, fim), ou null em "todas".</summary>
    private static (int De, int Ate)? Anos(string epoca)
    {
        var ano = DateTime.Now.Year;
        return epoca switch
        {
            "recentes" => (ano - 2, ano),
            "2010" => (2010, ano),
            "1990" => (1990, 2009),
            "classicos" => (1900, 1989),
            _ => null,
        };
    }

    /// <summary>Uma página sorteada entre as primeiras (as mais populares), para cada giro trazer coisas diferentes.</summary>
    private static int PaginaSorteada(int ate = 5) => Random.Shared.Next(1, ate + 1);

    /// <summary>
    /// Busca uma página sorteada; se ela vier vazia ou não existir (filtros estreitos têm poucas páginas;
    /// o RAWG responde 404), usa a primeira.
    /// </summary>
    private static async Task<JsonNode> PaginaOuPrimeira(Func<int, Task<JsonNode>> buscar, string lista)
    {
        var sorteada = PaginaSorteada();
        if (sorteada == 1) return await buscar(1);
        try
        {
            var j = await buscar(sorteada);
            if (j[lista]?.AsArray().Count > 0) return j;
        }
        catch (HttpRequestException e) when (e.StatusCode is System.Net.HttpStatusCode.NotFound) { }
        return await buscar(1);
    }

    // ---- Fontes ----

    private async Task<List<ResultadoBusca>> TmdbDescobrir(string tipo, FiltrosRoleta f, bool animacao, CancellationToken ct)
    {
        var filme = tipo == "movie";
        var p = new List<string> { "include_adult=false", "sort_by=popularity.desc", $"vote_count.gte={(filme ? 150 : 80)}" };

        // Animação é uma categoria à parte: fora dos filmes e séries, e é o filtro das animações
        int[] fora = filme ? [16] : [16, 10763, 10767, 10766, 10762];
        if (animacao) { p.Add("with_genres=16"); fora = filme ? [] : [10763, 10767, 10766]; }
        if (fora.Length > 0) p.Add("without_genres=" + string.Join(",", fora));

        int[] clima = [];
        if (f.Genero is { Length: > 0 } g && int.TryParse(g, out var gid))
        {
            if (animacao) clima = [gid]; else p.Add("with_genres=" + gid);
        }
        else
        {
            clima = ClimaTmdb(f.Clima, filme);
            // Nas animações o 16 já está no with_genres: o clima é filtrado aqui mesmo
            if (!animacao && clima.Length > 0) p.Add("with_genres=" + string.Join("|", clima));
        }

        switch (f.Tempo, filme)
        {
            case ("pouco", true): p.Add("with_runtime.lte=95"); break;
            case ("medio", true): p.Add("with_runtime.gte=85"); p.Add("with_runtime.lte=140"); break;
            case ("longo", true): p.Add("with_runtime.gte=140"); break;
            case ("pouco", false): p.Add(animacao ? "with_runtime.lte=25" : "with_runtime.lte=32"); break;
            case ("medio", false) when !animacao: p.Add("with_type=2"); break;   // minissérie
            case ("longo", false) when !animacao: p.Add("with_type=4"); break;   // série roteirizada
        }

        if (Anos(f.Epoca) is var (de, ate))
        {
            var campo = filme ? "primary_release_date" : "first_air_date";
            p.Add($"{campo}.gte={de}-01-01");
            p.Add($"{campo}.lte={ate}-12-31");
        }
        if (f.NotaMin > 0) p.Add("vote_average.gte=" + (f.NotaMin * 2).ToString(System.Globalization.CultureInfo.InvariantCulture));

        var pagina = PaginaSorteada();
        var j = await Get(UrlTmdb($"discover/{tipo}", string.Join("&", p) + "&page=" + pagina), Bearer(), ct);
        // Poucos resultados: a página sorteada pode passar do fim; tenta uma que exista
        var total = (int?)j["total_pages"] ?? 0;
        if (total > 0 && pagina > total)
            j = await Get(UrlTmdb($"discover/{tipo}", string.Join("&", p) + "&page=" + Random.Shared.Next(1, total + 1)), Bearer(), ct);

        return (j["results"]?.AsArray() ?? [])
            .Select(x => (x, generos: x?["genre_ids"]?.AsArray().Select(n => (int?)n ?? 0).ToList() ?? []))
            .Where(t => !t.generos.Any(fora.Contains) && (clima.Length == 0 || t.generos.Any(clima.Contains)))
            .Select(t => new ResultadoBusca(
                Texto(t.x?[filme ? "title" : "name"]) ?? "",
                Ano(Texto(t.x?[filme ? "release_date" : "first_air_date"])),
                Texto(t.x?["poster_path"]) is string pp ? "https://image.tmdb.org/t/p/w500" + pp : null,
                Texto(t.x?["overview"]), null,
                Juntar(t.generos.Where(id => id != 16).Select(id => GenerosTmdb.GetValueOrDefault(id))),
                "tmdb-" + tipo, t.x?["id"]?.ToString() ?? ""))
            .ToList();
    }

    // Animações: filmes e séries animados (o tempo escolhe um dos dois)
    private async Task<List<ResultadoBusca>> AnimacaoDescobrir(FiltrosRoleta f, CancellationToken ct)
    {
        string[] tipos = f.Tempo switch { "medio" => ["movie"], "longo" => ["tv"], _ => ["movie", "tv"] };
        var partes = await Task.WhenAll(tipos.Select(t => TmdbDescobrir(t, f, animacao: true, ct)));
        return partes.SelectMany(x => x).ToList();
    }

    private async Task<List<ResultadoBusca>> AniListDescobrir(FiltrosRoleta f, CancellationToken ct)
    {
        // Montado direto no texto: o AniList recusa algumas combinações com variáveis nulas
        var args = new List<string> { "type: ANIME", "isAdult: false", "sort: POPULARITY_DESC", "genre_not_in: [\"Hentai\", \"Ecchi\"]" };
        string[] generos = f.Genero is { Length: > 0 } g ? [g] : ClimaAniList(f.Clima);
        if (generos.Length > 0) args.Add("genre_in: " + JsonSerializer.Serialize(generos));
        args.AddRange(f.Tempo switch
        {
            "pouco" => ["format_in: [MOVIE]"],
            "medio" => ["format_in: [TV, ONA]", "episodes_lesser: 14"],
            "longo" => ["format_in: [TV, ONA]", "episodes_greater: 23"],
            _ => ["format_in: [TV, ONA, MOVIE]"],
        });
        if (Anos(f.Epoca) is var (de, ate)) { args.Add($"startDate_greater: {de - 1}1231"); args.Add($"startDate_lesser: {ate + 1}0101"); }
        if (f.NotaMin > 0) args.Add($"averageScore_greater: {(int)(f.NotaMin * 20) - 1}");

        async Task<JsonArray> Pagina(int pagina)
        {
            var query = $"{{ Page(page: {pagina}, perPage: 30) {{ media({string.Join(", ", args)}) {{ id title {{ romaji english }} startDate {{ year }} coverImage {{ extraLarge }} description(asHtml: false) genres studios(isMain: true) {{ nodes {{ name }} }} relations {{ edges {{ relationType }} }} }} }} }}";
            using var resp = await http.PostAsJsonAsync("https://graphql.anilist.co", new { query }, ct);
            resp.EnsureSuccessStatusCode();
            return JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct))!["data"]?["Page"]?["media"]?.AsArray() ?? [];
        }
        // O AniList às vezes devolve as páginas seguintes vazias (filtros estreitos ou limite de requisições): volta para a primeira
        var sorteada = PaginaSorteada(3);
        var media = await Pagina(sorteada);
        if (media.Count == 0 && sorteada > 1) media = await Pagina(1);
        return media
            // Só o começo de cada história: continuações (com "prequel") ficam de fora
            .Where(x => !(x?["relations"]?["edges"]?.AsArray().Any(e => Texto(e?["relationType"]) == "PREQUEL") ?? false))
            .Select(x => new ResultadoBusca(
                Texto(x?["title"]?["english"]) ?? Texto(x?["title"]?["romaji"]) ?? "",
                x?["startDate"]?["year"]?.GetValueKind() == JsonValueKind.Number ? (int)x["startDate"]!["year"]! : null,
                Texto(x?["coverImage"]?["extraLarge"]),
                Texto(x?["description"]) is string d ? Tags().Replace(d, "").Trim() : null,
                Juntar(x?["studios"]?["nodes"]?.AsArray().Select(s => Texto(s?["name"]))),
                Juntar(x?["genres"]?.AsArray().Select(Texto)),
                "anilist", x?["id"]?.ToString() ?? ""))
            .ToList();
    }

    private async Task<List<ResultadoBusca>> RawgDescobrir(FiltrosRoleta f, CancellationToken ct)
    {
        var p = new List<string> { "page_size=40", "ordering=-added", "exclude_additions=true", "key=" + Uri.EscapeDataString(ChaveRawg() ?? "") };
        if (f.Genero is { Length: > 0 } g) p.Add("genres=" + g);
        else
        {
            switch (f.Clima)
            {
                case "leve": p.Add("genres=casual,family,puzzle,platformer,racing,sports"); break;
                case "tenso": p.Add("tags=horror"); break;
                case "emocionante": p.Add("genres=action,adventure,shooter"); break;
                case "pensar": p.Add("genres=strategy,puzzle,simulation"); break;
            }
        }
        if (Anos(f.Epoca) is var (de, ate)) p.Add($"dates={de}-01-01,{ate}-12-31");
        if (f.NotaMin > 0) p.Add($"metacritic={(int)(f.NotaMin * 20)},100");

        var j = await PaginaOuPrimeira(n => Get("https://api.rawg.io/api/games?" + string.Join("&", p) + "&page=" + n, ct: ct), "results");
        // O RAWG não filtra pela duração: usa a média de horas jogadas (0 = sem informação, fica de fora quando o tempo importa)
        bool Cabe(int horas) => f.Tempo switch
        {
            "pouco" => horas is > 0 and <= 10,
            "medio" => horas is > 10 and <= 30,
            "longo" => horas > 30,
            _ => true,
        };
        return (j["results"]?.AsArray() ?? [])
            .Where(x => Cabe((int?)x?["playtime"] ?? 0))
            .Select(x => new ResultadoBusca(
                Texto(x?["name"]) ?? "", Ano(Texto(x?["released"])), Texto(x?["background_image"]), null, null,
                Juntar(x?["genres"]?.AsArray().Select(g => Texto(g?["name"]))), "rawg", x?["id"]?.ToString() ?? ""))
            .ToList();
    }

    private async Task<List<ResultadoBusca>> OpenLibraryDescobrir(FiltrosRoleta f, CancellationToken ct)
    {
        string[] assuntos = f.Genero is { Length: > 0 } g ? [g] : ClimaLivro(f.Clima);
        var q = "(" + string.Join(" OR ", assuntos.Select(a => "subject:" + a)) + ")";
        if (Anos(f.Epoca) is var (de, ate)) q += $" AND first_publish_year:[{de} TO {ate}]";

        var j = await PaginaOuPrimeira(n => Get($"https://openlibrary.org/search.json?sort=rating&limit=40&page={n}"
            + "&fields=key,title,author_name,first_publish_year,cover_i,subject,number_of_pages_median,ratings_average"
            + "&q=" + Uri.EscapeDataString(q), ct: ct), "docs");
        bool Cabe(int paginas) => f.Tempo switch
        {
            "pouco" => paginas is > 0 and <= 200,
            "medio" => paginas is > 200 and <= 400,
            "longo" => paginas > 400,
            _ => true,
        };
        return (j["docs"]?.AsArray() ?? [])
            .Where(x => x?["cover_i"] is not null)   // na roleta, só livros com capa
            .Where(x => Cabe((int?)x?["number_of_pages_median"] ?? 0))
            .Where(x => f.NotaMin <= 0 || ((double?)x?["ratings_average"] ?? 0) >= f.NotaMin)
            .Select(x => new ResultadoBusca(
                Texto(x?["title"]) ?? "",
                x?["first_publish_year"]?.GetValueKind() == JsonValueKind.Number ? (int)x["first_publish_year"]! : null,
                $"https://covers.openlibrary.org/b/id/{x!["cover_i"]}-L.jpg", null,
                Juntar(x["author_name"]?.AsArray().Take(3).Select(Texto)),
                Juntar(x["subject"]?.AsArray().Take(3).Select(Texto)),
                "openlibrary", Texto(x["key"]) ?? ""))
            .ToList();
    }

    // ---- Seu gosto: gêneros do que você deu nota alta pesam mais no sorteio ----

    // Mesmo gênero com nomes diferentes nas fontes (TMDB em português, AniList/RAWG em inglês)
    private static readonly Dictionary<string, string> SinonimosGenero = new()
    {
        ["comedy"] = "comedia", ["humor"] = "comedia", ["action"] = "acao", ["adventure"] = "aventura", ["horror"] = "terror",
        ["sci-fi"] = "ficcao cientifica", ["science fiction"] = "ficcao cientifica", ["fantasy"] = "fantasia", ["mystery"] = "misterio",
        ["documentary"] = "documentario", ["family"] = "familia", ["animation"] = "animacao", ["suspense"] = "thriller",
        ["war"] = "guerra", ["history"] = "historia", ["music"] = "musica", ["western"] = "faroeste", ["sports"] = "esporte",
        ["psychological"] = "psicologico", ["politics"] = "politica",
    };

    private static IEnumerable<string> GenerosCanonicos(string? generos) =>
        (generos ?? "").Split([',', '&', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalizar).Where(g => g.Length > 0)
            .Select(g => SinonimosGenero.GetValueOrDefault(g, g)).Distinct();

    /// <summary>Quantas vezes cada gênero aparece nos itens da estante com nota 4 ou mais.</summary>
    public static Dictionary<string, int> GenerosFavoritos(IEnumerable<ItemEntretenimento> estante) =>
        estante.Where(e => e.Nota >= 4).SelectMany(e => GenerosCanonicos(e.Generos))
            .GroupBy(g => g).ToDictionary(g => g.Key, g => g.Count());

    /// <summary>Quantos gêneros do resultado estão entre os seus favoritos.</summary>
    public static int Afinidade(ResultadoBusca r, IReadOnlyDictionary<string, int> favoritos) =>
        GenerosCanonicos(r.Generos).Count(favoritos.ContainsKey);
}
