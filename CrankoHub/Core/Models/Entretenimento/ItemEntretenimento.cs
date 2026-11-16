namespace CrankoHub.Models;

/// <summary>Algo assistido, jogado ou lido — um item da estante de Entretenimento.</summary>
public class ItemEntretenimento
{
    public string Id { get; set; } = "";

    /// <summary>Chave da categoria ("filme", "jogo"...). Ver <see cref="CategoriaEntretenimento"/>.</summary>
    public string Categoria { get; set; } = "filme";

    public string Titulo { get; set; } = "";

    public int? Ano { get; set; }

    /// <summary>Quando assistiu/terminou.</summary>
    public DateOnly? Data { get; set; }

    /// <summary>De 0,5 a 5, em meias estrelas.</summary>
    public double? Nota { get; set; }

    /// <summary>"concluido", "andamento" ou "quero". Ver <see cref="StatusEntretenimento"/>.</summary>
    public string Status { get; set; } = "concluido";

    /// <summary>Nome do arquivo da capa na pasta de capas (ver <see cref="Services.CapasService"/>).</summary>
    public string? Capa { get; set; }

    public string? Sinopse { get; set; }

    /// <summary>Diretor, autor, estúdio...</summary>
    public string? Criadores { get; set; }

    public string? Generos { get; set; }

    /// <summary>De onde vieram os dados ("tmdb", "rawg", "anilist", "openlibrary").</summary>
    public string? Fonte { get; set; }

    public string? FonteId { get; set; }

    public string? Obs { get; set; }

    /// <summary>
    /// Temporadas e episódios (séries, animes e animações em série), copiados da fonte para abrir sem internet.
    /// Vazio = ainda não carregados. Ver <see cref="Services.EntretenimentoBusca.Episodios"/>.
    /// </summary>
    public List<TemporadaEntretenimento> Temporadas { get; set; } = [];

    /// <summary>Episódios assistidos, na forma de <see cref="EpisodioEntretenimento.ChaveDe"/> ("1x3" = temporada 1, episódio 3).</summary>
    public List<string> EpisodiosVistos { get; set; } = [];

    public int TotalEpisodios() => Temporadas.Sum(t => t.Episodios.Count);

    /// <summary>Quantos dos episódios carregados foram assistidos.</summary>
    public int EpisodiosAssistidos()
    {
        var vistos = EpisodiosVistos.ToHashSet();
        return Temporadas.Sum(t => t.Episodios.Count(e => vistos.Contains(EpisodioEntretenimento.ChaveDe(t.Numero, e.Numero))));
    }

    /// <summary>O primeiro episódio ainda não assistido, na ordem das temporadas.</summary>
    public (TemporadaEntretenimento Temporada, EpisodioEntretenimento Episodio)? ProximoEpisodio()
    {
        var vistos = EpisodiosVistos.ToHashSet();
        foreach (var t in Temporadas)
            foreach (var e in t.Episodios)
                if (!vistos.Contains(EpisodioEntretenimento.ChaveDe(t.Numero, e.Numero))) return (t, e);
        return null;
    }
}

/// <summary>Uma temporada (nos animes, cada temporada é um cadastro separado no AniList).</summary>
public class TemporadaEntretenimento
{
    public int Numero { get; set; }

    /// <summary>Nome próprio da temporada, quando a fonte tem ("Attack on Titan Season 2"). Sem ele: "Temporada N".</summary>
    public string? Nome { get; set; }

    public List<EpisodioEntretenimento> Episodios { get; set; } = [];
}

public class EpisodioEntretenimento
{
    public int Numero { get; set; }

    /// <summary>Título do episódio (só com "Nomes dos episódios" ligado na engrenagem do Entretenimento).</summary>
    public string? Nome { get; set; }

    /// <summary>Data de exibição, quando a fonte informa.</summary>
    public DateOnly? Data { get; set; }

    public static string ChaveDe(int temporada, int episodio) => $"{temporada}x{episodio}";
}

/// <summary>Uma prateleira da estante e a API usada para buscar os dados dela.</summary>
public record CategoriaEntretenimento(string Chave, string Nome, string Singular, string Verbo, string Api)
{
    /// <summary>Categoria que pode ter temporadas e episódios (na animação, só quando é série, não filme).</summary>
    public bool TemEpisodios => Chave is "serie" or "anime" or "animacao";

    public static readonly IReadOnlyList<CategoriaEntretenimento> Todas =
    [
        new("filme", "Filmes", "filme", "Assistido", "tmdb-filme"),
        new("serie", "Séries", "série", "Assistida", "tmdb-serie"),
        new("animacao", "Animações", "animação", "Assistida", "tmdb-animacao"),
        new("anime", "Animes", "anime", "Assistido", "anilist"),
        new("jogo", "Jogos", "jogo", "Zerado", "rawg"),
        new("livro", "Livros", "livro", "Lido", "openlibrary"),
    ];

    public static CategoriaEntretenimento Buscar(string chave) => Todas.FirstOrDefault(c => c.Chave == chave) ?? Todas[0];
}

public record StatusEntretenimento(string Chave, string Nome)
{
    public static readonly IReadOnlyList<StatusEntretenimento> Todos =
    [
        new("concluido", "Concluído"),
        new("andamento", "Em andamento"),
        new("quero", "Quero ver"),
    ];

    public static StatusEntretenimento Buscar(string chave) => Todos.FirstOrDefault(s => s.Chave == chave) ?? Todos[0];
}
