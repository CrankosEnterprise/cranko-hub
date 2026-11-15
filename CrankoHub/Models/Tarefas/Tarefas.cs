namespace CrankoHub.Models;

/// <summary>Quadros de tarefas (estilo Kanban) e os cards de todos eles.</summary>
public class DadosTarefas
{
    public List<Quadro> Quadros { get; set; } = [];

    /// <summary>Id do quadro aberto, ou <see cref="Quadro.Hoje"/> para a visão do dia.</summary>
    public string Ativo { get; set; } = "";

    public List<CardTarefa> Cards { get; set; } = [];

    /// <summary>
    /// Na coluna de concluídas, esconder as feitas há mais de tantos dias (0 = mostrar todas).
    /// Fica na engrenagem das Tarefas.
    /// </summary>
    public int DiasOcultarConcluidas { get; set; } = PadraoDiasOcultarConcluidas;

    public const int PadraoDiasOcultarConcluidas = 14;

    /// <summary>
    /// Etiquetas criadas (botão de etiquetas no topo das Tarefas ou menu de etiquetas do card), em minúsculas e sem "#".
    /// Ficam aqui mesmo sem nenhum card usando; as que já estavam só nos cards entram na normalização.
    /// </summary>
    public List<string> Etiquetas { get; set; } = [];

    public const int TamanhoMaximoEtiqueta = 30;

    /// <summary>Como a etiqueta é guardada: sem espaços nas pontas, sem "#", em minúsculas.</summary>
    public static string NormalizarEtiqueta(string? e)
    {
        var n = (e ?? "").Trim().TrimStart('#').Trim().ToLowerInvariant();
        return n.Length > TamanhoMaximoEtiqueta ? n[..TamanhoMaximoEtiqueta].TrimEnd() : n;
    }

    /// <summary>Todas as etiquetas em ordem: as criadas e as que algum card usa (ex.: vindas de outro aparelho antes do perfil).</summary>
    public List<string> TodasEtiquetas() =>
        Etiquetas.Concat(Cards.SelectMany(c => c.Etiquetas)).Select(NormalizarEtiqueta).Where(e => e.Length > 0).Distinct().Order().ToList();
}

/// <summary>Um quadro (Trabalho, Faculdade, Pessoal...) com suas colunas.</summary>
public class Quadro
{
    /// <summary>Id especial da visão "Hoje", que junta os cards de todos os quadros.</summary>
    public const string Hoje = "hoje";

    public string Id { get; set; } = "";

    public string Nome { get; set; } = "";

    public string? Emoji { get; set; }

    /// <summary>A última coluna é a de concluídos.</summary>
    public List<ColunaQuadro> Colunas { get; set; } = [];
}

public class ColunaQuadro
{
    public string Id { get; set; } = "";

    public string Nome { get; set; } = "";
}

public class CardTarefa
{
    public string Id { get; set; } = "";

    public string QuadroId { get; set; } = "";

    public string ColunaId { get; set; } = "";

    public string Titulo { get; set; } = "";

    public string? Descricao { get; set; }

    public DateOnly? Prazo { get; set; }

    /// <summary>Horário de entrega no dia do prazo (null = sem hora marcada). Só vale com <see cref="Prazo"/>.</summary>
    public TimeOnly? PrazoHora { get; set; }

    /// <summary>"baixa", "media" ou "alta". Ver <see cref="PrioridadeTarefa"/>.</summary>
    public string Prioridade { get; set; } = "media";

    public List<string> Etiquetas { get; set; } = [];

    public List<ItemChecklist> Checklist { get; set; } = [];

    /// <summary>Arquivos anexados, guardados compactados (ver <see cref="Services.AnexosService"/>).</summary>
    public List<Anexo> Anexos { get; set; } = [];

    /// <summary>Marcado para aparecer em "Hoje" mesmo sem prazo.</summary>
    public bool Foco { get; set; }

    /// <summary>Posição dentro da coluna (menor primeiro).</summary>
    public double Ordem { get; set; }

    public DateOnly Criado { get; set; }

    /// <summary>Quando entrou na coluna de concluídos (null se não está lá).</summary>
    public DateOnly? Concluido { get; set; }
}

public class ItemChecklist
{
    public string Texto { get; set; } = "";

    public bool Feito { get; set; }
}

/// <param name="Classe">Cor do badge (mesmas classes de <see cref="Services.Alerta"/>).</param>
public record PrioridadeTarefa(string Chave, string Nome, string Classe)
{
    public static readonly IReadOnlyList<PrioridadeTarefa> Todas =
    [
        new("alta", "Alta", "critical"),
        new("media", "Média", "warning"),
        new("baixa", "Baixa", ""),
    ];

    public static PrioridadeTarefa Buscar(string chave) => Todas.FirstOrDefault(p => p.Chave == chave) ?? Todas[1];
}
