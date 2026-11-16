using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Temporadas e episódios de séries, animes e animações em série:
/// <list type="bullet">
/// <item>Com a chave do TMDB: o próprio TMDB (nomes em português).</item>
/// <item>Sem chave: o TVmaze (nomes em inglês), achado pela Wikidata ou pelo título.</item>
/// <item>Animes: o AniList (cada temporada é um cadastro, seguido pelas "sequências") e, com os nomes ligados,
/// o Jikan (MyAnimeList) para o título de cada episódio.</item>
/// </list>
/// </summary>
public partial class EntretenimentoBusca
{
    // Temporadas seguidas no AniList (evita laço sem fim em cadastros mal ligados)
    private const int MaximoTemporadasAnime = 15;

    /// <summary>
    /// Busca as temporadas e episódios do item. Devolve null quando não acha (ou quando é um filme).
    /// Episódios que ainda não foram ao ar ficam de fora (entram no "Atualizar" depois que estrearem).
    /// </summary>
    public async Task<List<TemporadaEntretenimento>?> Episodios(ItemEntretenimento item, CancellationToken ct = default)
    {
        if (!CategoriaEntretenimento.Buscar(item.Categoria).TemEpisodios || item.Fonte == "tmdb-movie") return null;
        try
        {
            var temporadas = item.Fonte switch
            {
                "tmdb-tv" when ChaveTmdb() is not null => await EpisodiosTmdb(item.FonteId!, ct),
                "anilist" when int.TryParse(item.FonteId, out var id) => await EpisodiosAniList(id, ct),
                "wikipedia" => await EpisodiosWikipedia(item, ct),
                _ => await EpisodiosPorTitulo(item, ct),
            };
            temporadas?.RemoveAll(t => t.Episodios.Count == 0);
            return temporadas is { Count: > 0 } ? temporadas : null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.NotFound) { return null; }
        catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.Unauthorized)
        {
            throw new BuscaException("A chave da API foi recusada. Confira nas Configurações.");
        }
        catch (Exception)
        {
            throw new BuscaException("Não foi possível carregar os episódios agora. Verifique a internet e tente de novo.");
        }
    }

    // ---- TMDB ----

    private async Task<List<TemporadaEntretenimento>> EpisodiosTmdb(string id, CancellationToken ct)
    {
        var serie = await Get(UrlTmdb($"tv/{id}", ""), Bearer(), ct);
        var numeros = (serie["seasons"]?.AsArray() ?? [])
            .Select(s => (Numero: (int?)s?["season_number"] ?? 0, Nome: Texto(s?["name"])))
            .Where(s => s.Numero > 0) // a 0 é a de "Especiais"
            .ToList();
        var temporadas = new List<TemporadaEntretenimento>();
        // O TMDB junta até 20 temporadas numa chamada só (append_to_response)
        foreach (var lote in numeros.Chunk(20))
        {
            var j = await Get(UrlTmdb($"tv/{id}", "append_to_response=" + string.Join(',', lote.Select(s => $"season/{s.Numero}"))), Bearer(), ct);
            foreach (var (numero, nome) in lote)
            {
                var episodios = (j[$"season/{numero}"]?["episodes"]?.AsArray() ?? [])
                    .Select(e => new EpisodioEntretenimento
                    {
                        Numero = (int?)e?["episode_number"] ?? 0,
                        // Sem tradução, o TMDB manda "Episódio 3": é o mesmo que não ter nome
                        Nome = Texto(e?["name"]) is string n && !NomeGenerico().IsMatch(n) ? n : null,
                        Data = Data(Texto(e?["air_date"])),
                    });
                temporadas.Add(Temporada(numero, nome is not null && !NomeGenerico().IsMatch(nome) ? nome : null, episodios));
            }
        }
        return temporadas;
    }

    // ---- TVmaze (sem chave) ----

    private async Task<List<TemporadaEntretenimento>> EpisodiosTvmaze(string id, CancellationToken ct)
    {
        var j = await Get($"https://api.tvmaze.com/shows/{Uri.EscapeDataString(id)}/episodes", ct: ct);
        return (j.AsArray())
            .Where(e => e?["number"]?.GetValueKind() == JsonValueKind.Number) // especiais vêm sem número
            .GroupBy(e => (int?)e!["season"] ?? 1)
            .OrderBy(g => g.Key)
            .Select(g => Temporada(g.Key, null, g.Select(e => new EpisodioEntretenimento
            {
                Numero = (int)e!["number"]!,
                Nome = Texto(e["name"]) is string n && !NomeGenerico().IsMatch(n) ? n : null,
                Data = Data(Texto(e["airdate"])),
            })))
            .ToList();
    }

    /// <summary>Série achada pela Wikipedia: o ID do TVmaze está na Wikidata (o mesmo usado para o pôster).</summary>
    private async Task<List<TemporadaEntretenimento>?> EpisodiosWikipedia(ItemEntretenimento item, CancellationToken ct)
    {
        var pagina = (await Get(UrlWikipedia($"pageids={Uri.EscapeDataString(item.FonteId ?? "")}&prop=pageprops&ppprop=wikibase_item"), ct: ct))
            ["query"]?["pages"]?[0];
        if (Texto(pagina?["pageprops"]?["wikibase_item"]) is string qid)
        {
            var claims = (await Get($"https://www.wikidata.org/w/api.php?action=wbgetentities&format=json&props=claims&ids={qid}", ct: ct))
                ["entities"]?[qid]?["claims"];
            if (Texto(claims?["P8600"]?[0]?["mainsnak"]?["datavalue"]?["value"]) is string idTvmaze)
                return await EpisodiosTvmaze(idTvmaze, ct);
        }
        // Animação sem TVmaze na Wikidata quase sempre é filme: não arrisca achar outra coisa pelo título
        return item.Categoria == "animacao" ? null : await EpisodiosPorTitulo(item, ct);
    }

    /// <summary>Item cadastrado à mão (ou de uma fonte sem episódios): procura pelo título e, se tiver, pelo ano.</summary>
    private async Task<List<TemporadaEntretenimento>?> EpisodiosPorTitulo(ItemEntretenimento item, CancellationToken ct)
    {
        var termo = Uri.EscapeDataString(item.Titulo);
        if (item.Categoria == "anime")
        {
            const string query = "query ($q: String) { Page(perPage: 5) { media(search: $q, type: ANIME) { id startDate { year } } } }";
            var j = await AniListQuery(query, new { q = item.Titulo }, ct);
            var lista = j["data"]?["Page"]?["media"]?.AsArray() ?? [];
            var achado = lista.FirstOrDefault(m => (int?)m?["startDate"]?["year"] == item.Ano) ?? lista.FirstOrDefault();
            return (int?)achado?["id"] is int id ? await EpisodiosAniList(id, ct) : null;
        }
        if (ChaveTmdb() is not null)
        {
            var j = await Get(UrlTmdb("search/tv", "query=" + termo), Bearer(), ct);
            var lista = j["results"]?.AsArray() ?? [];
            var achado = lista.FirstOrDefault(s => Ano(Texto(s?["first_air_date"])) == item.Ano) ?? lista.FirstOrDefault();
            return achado?["id"]?.ToString() is string id ? await EpisodiosTmdb(id, ct) : null;
        }
        var busca = (await Get($"https://api.tvmaze.com/search/shows?q={termo}", ct: ct)).AsArray();
        var show = busca.Select(r => r?["show"]).FirstOrDefault(s => Ano(Texto(s?["premiered"])) == item.Ano) ?? busca.FirstOrDefault()?["show"];
        return show?["id"]?.ToString() is string idShow ? await EpisodiosTvmaze(idShow, ct) : null;
    }

    // ---- AniList + Jikan (animes) ----

    private async Task<List<TemporadaEntretenimento>?> EpisodiosAniList(int id, CancellationToken ct)
    {
        const string query = "query ($id: Int) { Media(id: $id) { id idMal format episodes title { romaji english } nextAiringEpisode { episode } "
            + "relations { edges { relationType node { id type format } } } } }";
        var temporadas = new List<(TemporadaEntretenimento Temporada, int? Mal)>();
        var vistos = new HashSet<int>();
        int? atual = id;
        while (atual is int idAtual && vistos.Add(idAtual) && temporadas.Count < MaximoTemporadasAnime)
        {
            var m = (await AniListQuery(query, new { id = idAtual }, ct))["data"]?["Media"];
            if (m is null) break;
            // Filme de anime não tem episódios para marcar
            if (temporadas.Count == 0 && Texto(m["format"]) == "MOVIE") return null;
            // Em exibição: o total ainda não existe, vale o último que já foi ao ar
            var total = (int?)m["episodes"] ?? ((int?)m["nextAiringEpisode"]?["episode"] - 1) ?? 0;
            var nome = Texto(m["title"]?["english"]) ?? Texto(m["title"]?["romaji"]);
            temporadas.Add((Temporada(temporadas.Count + 1, nome,
                Enumerable.Range(1, Math.Max(total, 0)).Select(n => new EpisodioEntretenimento { Numero = n })), (int?)m["idMal"]));
            // A próxima temporada é a "sequência" em formato de série (filmes e OVAs no meio ficam de fora)
            atual = (int?)(m["relations"]?["edges"]?.AsArray() ?? [])
                .FirstOrDefault(e => Texto(e?["relationType"]) == "SEQUEL" && Texto(e?["node"]?["type"]) == "ANIME"
                                     && Texto(e?["node"]?["format"]) is "TV" or "TV_SHORT" or "ONA")?["node"]?["id"];
        }

        // Nomes dos episódios pelo MyAnimeList. Falhou (limite de chamadas, fora do ar)? Ficam só os números,
        // e as outras temporadas nem tentam (cada falha esperaria o tempo limite inteiro).
        if (store.Dados.Config.NomesEpisodios)
            foreach (var (temporada, mal) in temporadas)
            {
                if (mal is not int idMal) continue;
                try { await NomesJikan(temporada, idMal, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch { break; }
            }
        return temporadas.Select(t => t.Temporada).ToList();
    }

    /// <summary>Preenche nomes e datas pelo Jikan (100 episódios por página; até 3 chamadas por segundo).</summary>
    private async Task NomesJikan(TemporadaEntretenimento temporada, int idMal, CancellationToken ct)
    {
        var porNumero = temporada.Episodios.ToDictionary(e => e.Numero);
        for (var pagina = 1; pagina <= 30; pagina++)
        {
            await Task.Delay(400, ct);
            var j = await Get($"https://api.jikan.moe/v4/anime/{idMal}/episodes?page={pagina}", ct: ct);
            foreach (var e in j["data"]?.AsArray() ?? [])
            {
                if ((int?)e?["mal_id"] is not int n) continue;
                // O AniList pode não saber o total (em exibição): o que o MyAnimeList já tem entra também
                if (!porNumero.TryGetValue(n, out var ep)) temporada.Episodios.Add(porNumero[n] = ep = new() { Numero = n });
                ep.Nome = Texto(e?["title"]) ?? ep.Nome;
                ep.Data = Data(Texto(e?["aired"])) ?? ep.Data;
            }
            if (j["pagination"]?["has_next_page"]?.GetValueKind() != JsonValueKind.True) break;
        }
        temporada.Episodios.Sort((a, b) => a.Numero.CompareTo(b.Numero));
    }

    private async Task<JsonNode> AniListQuery(string query, object variables, CancellationToken ct)
    {
        using var resp = await http.PostAsJsonAsync("https://graphql.anilist.co", new { query, variables }, ct);
        resp.EnsureSuccessStatusCode();
        return JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct))!;
    }

    // ---- Auxiliares ----

    /// <summary>Monta a temporada em ordem, sem repetidos e sem o que ainda não foi ao ar.</summary>
    private static TemporadaEntretenimento Temporada(int numero, string? nome, IEnumerable<EpisodioEntretenimento> episodios)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        return new()
        {
            Numero = numero,
            Nome = nome,
            Episodios = episodios.Where(e => e.Numero > 0 && (e.Data is null || e.Data <= hoje))
                .DistinctBy(e => e.Numero).OrderBy(e => e.Numero).ToList(),
        };
    }

    private static DateOnly? Data(string? texto) =>
        texto is { Length: >= 10 } && DateOnly.TryParseExact(texto[..10], "yyyy-MM-dd", out var d) ? d : null;

    // "Episódio 3", "Episode 3", "Temporada 2", "Season 2": nomes que a fonte põe quando não tem o de verdade
    [GeneratedRegex(@"^(Epis[oó]dio|Episode|Temporada|Season)\s*\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex NomeGenerico();
}
