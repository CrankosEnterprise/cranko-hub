namespace CrankoHub.Models;

/// <summary>Agenda (calendário): as agendas coloridas, os eventos e o que a tela lembra (visão, agendas ocultas).</summary>
public class DadosAgenda
{
    /// <summary>A ordem da lista é a da lateral (vai para o servidor em agendas.ordem).</summary>
    public List<AgendaCor> Agendas { get; set; } = [];

    public List<EventoAgenda> Eventos { get; set; } = [];

    // ----- Só deste aparelho (não sincroniza, como Tarefas.Ativo) -----

    /// <summary>"dia", "semana" ou "mes". Ver <see cref="VisaoAgenda"/>.</summary>
    public string Visao { get; set; } = VisaoAgenda.Semana;

    /// <summary>Ids das agendas desligadas na lateral (os eventos delas não aparecem).</summary>
    public List<string> Ocultas { get; set; } = [];

    /// <summary>Mostrar as tarefas com prazo no dia do prazo.</summary>
    public bool MostrarTarefas { get; set; } = true;

    /// <summary>Ids dos quadros de Tarefas desligados na lateral (as tarefas deles não aparecem).</summary>
    public List<string> QuadrosOcultos { get; set; } = [];
}

public static class VisaoAgenda
{
    public const string Dia = "dia";
    public const string Semana = "semana";
    public const string Mes = "mes";
}

/// <summary>Uma agenda (Pessoal, Trabalho, Faculdade...) com a cor dos seus eventos.</summary>
public class AgendaCor
{
    /// <summary>
    /// Id fixo da agenda criada sozinha. Dois aparelhos que criam a agenda padrão geram a mesma linha no servidor,
    /// então ela não aparece duplicada depois de sincronizar.
    /// </summary>
    public const string Principal = "principal";

    public string Id { get; set; } = "";

    public string Nome { get; set; } = "";

    /// <summary>Cor em hexadecimal (ex.: "#007aff").</summary>
    public string Cor { get; set; } = CoresAgenda.Todas[0];
}

/// <summary>Cores prontas para as agendas (as cores do sistema da Apple, como no resto do app).</summary>
public static class CoresAgenda
{
    public static readonly IReadOnlyList<string> Todas =
    [
        "#007aff", "#34c759", "#ff9500", "#ff2d55", "#af52de", "#5856d6", "#30b0c7", "#ff3b30", "#a2845e", "#8e8e93",
    ];
}

/// <summary>Um compromisso. Os horários são do relógio local (sem fuso), como num papel de agenda.</summary>
public class EventoAgenda
{
    public string Id { get; set; } = "";

    public string AgendaId { get; set; } = "";

    public string Titulo { get; set; } = "";

    public string? Descricao { get; set; }

    public string? Local { get; set; }

    /// <summary>Início da primeira ocorrência. No dia inteiro, só a data conta (00:00).</summary>
    public DateTime Inicio { get; set; }

    /// <summary>Fim da primeira ocorrência. No dia inteiro, 00:00 do dia seguinte ao último dia.</summary>
    public DateTime Fim { get; set; }

    public bool DiaInteiro { get; set; }

    /// <summary>"" (não repete), "diaria", "semanal", "mensal" ou "anual". Ver <see cref="RepeticaoAgenda"/>.</summary>
    public string Repeticao { get; set; } = "";

    /// <summary>A cada N dias/semanas/meses/anos.</summary>
    public int Intervalo { get; set; } = 1;

    /// <summary>Na repetição semanal: os dias (0 = domingo ... 6 = sábado). Vazio = o dia do início.</summary>
    public List<int> DiasSemana { get; set; } = [];

    /// <summary>Último dia em que ainda repete (null = para sempre).</summary>
    public DateOnly? RepetirAte { get; set; }

    /// <summary>Datas de ocorrências que não acontecem (excluídas ou editadas "só este evento").</summary>
    public List<DateOnly> Excecoes { get; set; } = [];

    /// <summary>Avisar tantos minutos antes (null = sem lembrete). No dia inteiro, conta a partir das 9h do dia.</summary>
    public int? Lembrete { get; set; }

    public DateOnly Criado { get; set; }
}

public record RepeticaoAgenda(string Chave, string Nome, string Unidade, string Unidades)
{
    public const string Nenhuma = "";
    public const string Diaria = "diaria";
    public const string Semanal = "semanal";
    public const string Mensal = "mensal";
    public const string Anual = "anual";

    public static readonly IReadOnlyList<RepeticaoAgenda> Todas =
    [
        new(Nenhuma, "Não repete", "", ""),
        new(Diaria, "Todo dia", "dia", "dias"),
        new(Semanal, "Toda semana", "semana", "semanas"),
        new(Mensal, "Todo mês", "mês", "meses"),
        new(Anual, "Todo ano", "ano", "anos"),
    ];

    public static RepeticaoAgenda Buscar(string chave) => Todas.FirstOrDefault(r => r.Chave == chave) ?? Todas[0];
}

/// <summary>Opções de lembrete do editor (minutos antes).</summary>
public static class LembretesAgenda
{
    public static readonly IReadOnlyList<(int? Minutos, string Nome)> Todos =
    [
        (null, "Sem lembrete"),
        (0, "Na hora"),
        (5, "5 minutos antes"),
        (10, "10 minutos antes"),
        (15, "15 minutos antes"),
        (30, "30 minutos antes"),
        (60, "1 hora antes"),
        (120, "2 horas antes"),
        (1440, "1 dia antes"),
    ];
}
