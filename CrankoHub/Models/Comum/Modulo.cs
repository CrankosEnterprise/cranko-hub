using System.Globalization;
using CrankoHub.Components;
using Microsoft.AspNetCore.Components;

namespace CrankoHub.Models;

/// <summary>
/// Uma área do app. Em Configurações → Módulos o usuário esconde, reordena e muda nome, descrição, cor e ícone
/// (<see cref="Configuracoes.OrdemModulos"/> e <see cref="Configuracoes.ModulosPersonalizados"/>).
/// <see cref="Todos"/> são os padrões; para mostrar na tela, use <see cref="De"/>, <see cref="Ordenados"/> ou <see cref="Visiveis"/>.
/// </summary>
public record Modulo(string Id, string Nome, string Descricao, MarkupString Icone, string Cor)
{
    public const string Dieta = "dieta";
    public const string Carro = "carro";
    public const string Assinaturas = "assinaturas";
    public const string Entretenimento = "entretenimento";
    public const string Tarefas = "tarefas";
    public const string Agenda = "agenda";

    // Cores padrão: as do sistema da Apple
    public static readonly IReadOnlyList<Modulo> Todos =
    [
        new(Dieta, "Dieta", "Peso, medidas e meta", Icones.Dieta, "#FF2D55"),
        new(Carro, "Carro", "Abastecimentos, manutenção e documentos", Icones.Carro, "#007AFF"),
        new(Assinaturas, "Assinaturas", "Serviços recorrentes e cobranças", Icones.Assinaturas, "#AF52DE"),
        new(Entretenimento, "Entretenimento", "Livros, filmes, séries, animes e jogos", Icones.Entretenimento, "#FF9500"),
        new(Tarefas, "Tarefas", "Quadros Kanban e tarefas do dia", Icones.Tarefas, "#34C759"),
        new(Agenda, "Agenda", "Compromissos, horários e lembretes", Icones.Agenda, "#30B0C7"),
    ];

    /// <summary>O link do módulo é o próprio Id (ex.: "dieta").</summary>
    public string Link => Id;

    /// <summary>A definição original (sem o que o usuário mudou).</summary>
    public Modulo Padrao => Buscar(Id) ?? this;

    /// <summary>Módulo padrão (sem personalização).</summary>
    public static Modulo? Buscar(string id) => Todos.FirstOrDefault(m => m.Id == id);

    /// <summary>O módulo com nome, descrição, cor e ícone escolhidos pelo usuário.</summary>
    public static Modulo? De(Configuracoes cfg, string id) => Buscar(id) is { } m ? Personalizar(cfg, m) : null;

    /// <summary>Cor do módulo (já com a escolha do usuário).</summary>
    public static string CorDe(Configuracoes cfg, string id) => De(cfg, id)?.Cor ?? "var(--accent)";

    private static Modulo Personalizar(Configuracoes cfg, Modulo m) =>
        cfg.ModulosPersonalizados.TryGetValue(m.Id, out var p)
            ? m with
            {
                Nome = p.Nome ?? m.Nome,
                Descricao = p.Descricao ?? m.Descricao,
                Cor = p.Cor ?? m.Cor,
                Icone = Icones.IconeModulo(p.Icone) ?? m.Icone,
            }
            : m;

    /// <summary>Todos os módulos, personalizados, na ordem do usuário (os que faltarem na ordem vão para o fim).</summary>
    public static IEnumerable<Modulo> Ordenados(Configuracoes cfg) =>
        Todos.OrderBy(m => cfg.OrdemModulos.IndexOf(m.Id) is var i and >= 0 ? i : int.MaxValue)
            .ThenBy(m => IndicePadrao(m.Id))
            .Select(m => Personalizar(cfg, m));

    /// <summary>Os ids na ordem atual (completa).</summary>
    public static List<string> OrdemAtual(Configuracoes cfg) => Ordenados(cfg).Select(m => m.Id).ToList();

    private static int IndicePadrao(string id) => Todos.ToList().FindIndex(m => m.Id == id);

    public static bool Visivel(Configuracoes cfg, string id) => !cfg.ModulosOcultos.Contains(id);

    public static IEnumerable<Modulo> Visiveis(Configuracoes cfg) => Ordenados(cfg).Where(m => Visivel(cfg, m.Id));

    /// <summary>"#RRGGBB" válido (o que o seletor de cor devolve).</summary>
    public static bool CorValida(string? cor) =>
        cor is { Length: 7 } && cor[0] == '#' && int.TryParse(cor[1..], NumberStyles.HexNumber, null, out _);
}
