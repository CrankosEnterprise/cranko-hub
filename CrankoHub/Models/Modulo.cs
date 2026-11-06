using CrankoHub.Components;
using Microsoft.AspNetCore.Components;

namespace CrankoHub.Models;

/// <summary>Uma área do app que pode ser escondida em Configurações (menu, Início, alertas e avisos).</summary>
public record Modulo(string Id, string Nome, string Descricao, MarkupString Icone)
{
    public const string Dieta = "dieta";
    public const string Carro = "carro";
    public const string Assinaturas = "assinaturas";
    public const string Entretenimento = "entretenimento";
    public const string Tarefas = "tarefas";
    public const string Agenda = "agenda";

    public static readonly IReadOnlyList<Modulo> Todos =
    [
        new(Dieta, "Dieta", "Peso, medidas, IMC e meta", Icones.Dieta),
        new(Carro, "Carro", "Abastecimentos, manutenção e documentos", Icones.Carro),
        new(Assinaturas, "Assinaturas", "Serviços recorrentes e cobranças", Icones.Assinaturas),
        new(Entretenimento, "Entretenimento", "Livros, filmes, séries, animes e jogos", Icones.Entretenimento),
        new(Tarefas, "Tarefas", "Quadros Kanban e tarefas do dia", Icones.Tarefas),
        new(Agenda, "Agenda", "Compromissos, horários e lembretes", Icones.Agenda),
    ];

    /// <summary>O link do módulo é o próprio Id (ex.: "dieta").</summary>
    public string Link => Id;

    /// <summary>Cor do ícone do módulo no menu e nos cartões do Início (cores do sistema da Apple).</summary>
    public string Cor => CorDe(Id);

    public static string CorDe(string id) => id switch
    {
        Dieta => "#ff2d55",
        Carro => "#007aff",
        Assinaturas => "#af52de",
        Entretenimento => "#ff9500",
        Tarefas => "#34c759",
        Agenda => "#30b0c7",
        _ => "var(--accent)",
    };

    public static Modulo? Buscar(string id) => Todos.FirstOrDefault(m => m.Id == id);

    public static bool Visivel(Configuracoes cfg, string id) => !cfg.ModulosOcultos.Contains(id);

    public static IEnumerable<Modulo> Visiveis(Configuracoes cfg) => Todos.Where(m => Visivel(cfg, m.Id));
}
