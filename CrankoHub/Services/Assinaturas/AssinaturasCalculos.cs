using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>Contas das Assinaturas: próxima cobrança, custo mensal equivalente e totais.</summary>
public static class AssinaturasCalculos
{
    /// <summary>Sugestões para o campo de categoria.</summary>
    public static readonly IReadOnlyList<string> CategoriasSugeridas =
        ["Streaming", "Música", "Software", "Nuvem", "Jogos", "Notícias", "Academia", "Educação", "Telefonia", "Outros"];

    public const string SemCategoria = "Sem categoria";

    /// <summary>Primeira cobrança a partir de hoje (inclusive), avançando o vencimento pelo ciclo.</summary>
    public static DateOnly ProximaCobranca(Assinatura a, DateOnly hoje)
    {
        var ciclo = CicloCobranca.Buscar(a.Ciclo);
        if (a.Vencimento >= hoje) return a.Vencimento;
        // Sempre conta a partir da data original, para que o dia 31 não "escorregue" para 28
        var n = 1;
        while (ciclo.Avancar(a.Vencimento, n) < hoje) n++;
        return ciclo.Avancar(a.Vencimento, n);
    }

    public static double PorMes(Assinatura a) => CicloCobranca.Buscar(a.Ciclo).PorMes(a.Valor);

    public static string CategoriaDe(Assinatura a) => string.IsNullOrWhiteSpace(a.Categoria) ? SemCategoria : a.Categoria;

    /// <summary>Todas as cobranças das assinaturas ativas entre hoje e hoje + dias, em ordem.</summary>
    public static List<(Assinatura Assinatura, DateOnly Data)> CobrancasAte(IEnumerable<Assinatura> lista, DateOnly hoje, int dias)
    {
        var limite = hoje.AddDays(dias);
        var saida = new List<(Assinatura, DateOnly)>();
        foreach (var a in lista.Where(a => a.Ativa))
        {
            // Assinaturas semanais podem cobrar mais de uma vez no período
            for (var data = ProximaCobranca(a, hoje); data <= limite; data = ProximaCobranca(a, data.AddDays(1)))
                saida.Add((a, data));
        }
        return saida.OrderBy(x => x.Item2).ThenBy(x => x.Item1.Nome).ToList();
    }

    /// <summary>Custo mensal equivalente por categoria, do maior para o menor.</summary>
    public static List<BarraDado> PorCategoria(IEnumerable<Assinatura> lista) =>
        lista.Where(a => a.Ativa)
            .GroupBy(CategoriaDe)
            .Select(g => new BarraDado(g.Key, g.Sum(PorMes), $"{g.Key} · {g.Count()} {Formatacao.Plural(g.Count(), "assinatura", "assinaturas")}"))
            .OrderByDescending(b => b.Value)
            .ToList();

    // Cores das marcas mais comuns (o nome é comparado sem acento e em minúsculas, por "contém")
    private static readonly (string Trecho, string Cor)[] CoresMarcas =
    [
        ("netflix", "#E50914"), ("spotify", "#1DB954"), ("icloud", "#3693F3"), ("apple", "#5E5E63"),
        ("chatgpt", "#10A37F"), ("openai", "#10A37F"), ("claude", "#D97757"), ("amazon", "#00A8E1"), ("prime", "#00A8E1"),
        ("game pass", "#107C10"), ("xbox", "#107C10"), ("playstation", "#0070D1"), ("psn", "#0070D1"),
        ("youtube", "#FF0033"), ("smart fit", "#FFB800"), ("smartfit", "#FFB800"), ("disney", "#113CCF"),
        ("hbo", "#5822B4"), ("max", "#002BE7"), ("globoplay", "#FB0234"), ("deezer", "#A238FF"), ("paramount", "#0064FF"),
        ("crunchyroll", "#F47521"), ("google", "#4285F4"), ("microsoft", "#D83B01"), ("office", "#D83B01"),
        ("nubank", "#820AD1"), ("ifood", "#EA1D2C"), ("uber", "#1F1F1F"), ("canva", "#00C4CC"), ("notion", "#2F2F2F"),
        ("adobe", "#FA0F00"), ("duolingo", "#58CC02"), ("twitch", "#9146FF"), ("discord", "#5865F2"),
    ];

    /// <summary>
    /// Cor do serviço: o quadradinho com a inicial, a rosca e os cartões. A da marca, se conhecida;
    /// senão uma cor estável a partir do nome.
    /// </summary>
    public static string CorDe(Assinatura a)
    {
        var nome = Formatacao.SemAcentos(a.Nome).ToLowerInvariant();
        foreach (var (trecho, cor) in CoresMarcas)
            if (nome.Contains(trecho)) return cor;
        return Formatacao.CorEstavel(nome);
    }

    /// <summary>Letra do quadradinho do serviço.</summary>
    public static string InicialDe(Assinatura a) => a.Nome.Trim() is { Length: > 0 } n ? n[..1].ToUpperInvariant() : "?";

    /// <summary>Texto e classe CSS do selo "faltam X dias".</summary>
    public static (string Texto, string Classe) Prazo(int dias) => dias switch
    {
        0 => ("hoje", "critical"),
        1 => ("amanhã", "serious"),
        <= 3 => ($"em {dias} dias", "serious"),
        <= 7 => ($"em {dias} dias", "warning"),
        _ => ($"em {dias} dias", ""),
    };
}
