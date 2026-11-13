using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>Regras da Agenda: repetições, posição dos eventos na grade, mover/redimensionar, lembretes e textos.</summary>
public static class AgendaCalculos
{
    /// <summary>Encaixe ao arrastar e ao redimensionar (minutos).</summary>
    public const int Encaixe = 15;

    /// <summary>Hora em que conta o lembrete de um evento de dia inteiro.</summary>
    public const int HoraLembreteDiaInteiro = 9;

    /// <summary>Um lembrete ainda é avisado até tantos minutos depois do início (o app pode ter aberto atrasado).</summary>
    public const int ToleranciaLembrete = 15;

    /// <summary>Uma vez de um evento (o próprio, se não repete). <see cref="Data"/> identifica a ocorrência.</summary>
    public record Ocorrencia(EventoAgenda Evento, DateTime Inicio, DateTime Fim)
    {
        public DateOnly Data => DateOnly.FromDateTime(Inicio);

        /// <summary>Vai na faixa de dia inteiro (eventos de dia inteiro e os de 24 h ou mais).</summary>
        public bool NoTopo => Evento.DiaInteiro || Fim - Inicio >= TimeSpan.FromDays(1);
    }

    /// <summary>Pedaço de uma ocorrência dentro de um dia da grade, com a faixa lado a lado (como no Google).</summary>
    /// <remarks>É de um evento (<see cref="O"/>) ou de uma tarefa com horário de entrega (<see cref="Tarefa"/>).</remarks>
    public record Bloco(Ocorrencia? O, CardTarefa? Tarefa, DateTime Inicio, DateTime Fim, int Coluna, int Colunas);

    /// <summary>Quanto uma tarefa com horário ocupa na grade (ela tem só a hora da entrega).</summary>
    public const int DuracaoTarefa = 30;

    // ----- Agendas -----

    public static AgendaCor NovaPrincipal() => new() { Id = AgendaCor.Principal, Nome = "Pessoal", Cor = CoresAgenda.Todas[0] };

    public static AgendaCor? AgendaDe(DadosAgenda a, EventoAgenda e) => a.Agendas.FirstOrDefault(x => x.Id == e.AgendaId);

    public static string CorDe(DadosAgenda a, EventoAgenda e) => AgendaDe(a, e)?.Cor ?? CoresAgenda.Todas[0];

    public static bool Visivel(DadosAgenda a, string agendaId) => !a.Ocultas.Contains(agendaId);

    // ----- Ocorrências -----

    /// <summary>
    /// Ocorrências que encostam em [de, ate), ordenadas (dia inteiro primeiro, depois pelo início).
    /// <paramref name="todas"/> inclui as agendas ocultas (para os lembretes).
    /// </summary>
    public static List<Ocorrencia> Ocorrencias(DadosAgenda a, DateTime de, DateTime ate, bool todas = false)
    {
        var r = new List<Ocorrencia>();
        foreach (var e in a.Eventos)
        {
            if (!todas && !Visivel(a, e.AgendaId)) continue;
            var duracao = Duracao(e);
            // Um evento de vários dias que começou antes de "de" ainda aparece
            var primeiro = DateOnly.FromDateTime(de - duracao);
            var ultimo = DateOnly.FromDateTime(ate);
            foreach (var data in Datas(e, primeiro, ultimo))
            {
                if (e.Excecoes.Contains(data)) continue;
                var ini = data.ToDateTime(TimeOnly.FromDateTime(e.Inicio));
                var fim = ini + duracao;
                if (ini < ate && (fim > de || (fim == ini && ini >= de))) r.Add(new(e, ini, fim));
            }
        }
        return [.. r.OrderBy(o => !o.NoTopo).ThenBy(o => o.Inicio).ThenByDescending(o => o.Fim - o.Inicio).ThenBy(o => o.Evento.Titulo)];
    }

    public static List<Ocorrencia> DoDia(DadosAgenda a, DateOnly dia, bool todas = false) =>
        Ocorrencias(a, dia.ToDateTime(TimeOnly.MinValue), dia.AddDays(1).ToDateTime(TimeOnly.MinValue), todas);

    public static TimeSpan Duracao(EventoAgenda e) => e.Fim > e.Inicio ? e.Fim - e.Inicio : TimeSpan.Zero;

    /// <summary>Datas de início das ocorrências entre <paramref name="de"/> e <paramref name="ate"/> (inclusive), sem olhar as exceções.</summary>
    public static IEnumerable<DateOnly> Datas(EventoAgenda e, DateOnly de, DateOnly ate)
    {
        var d0 = DateOnly.FromDateTime(e.Inicio);
        var fim = e.RepetirAte is { } r && r < ate ? r : ate;
        if (e.Repeticao == RepeticaoAgenda.Nenhuma)
        {
            if (d0 >= de && d0 <= ate) yield return d0;
            yield break;
        }
        if (fim < d0) yield break;
        var n = Math.Max(1, e.Intervalo);

        switch (e.Repeticao)
        {
            case RepeticaoAgenda.Diaria:
            {
                // Pula direto para perto de "de" (eventos que repetem há anos)
                var k = de > d0 ? (de.DayNumber - d0.DayNumber) / n : 0;
                for (var d = d0.AddDays(k * n); d <= fim; d = d.AddDays(n))
                    if (d >= de) yield return d;
                break;
            }
            case RepeticaoAgenda.Semanal:
            {
                var dias = (e.DiasSemana.Count > 0 ? e.DiasSemana : [(int)d0.DayOfWeek]).Where(x => x is >= 0 and <= 6).Distinct().Order().ToList();
                var semana0 = InicioDaSemana(d0);
                var k = de > semana0 ? (de.DayNumber - semana0.DayNumber) / 7 / n : 0;
                for (var s = semana0.AddDays(k * 7 * n); s <= fim; s = s.AddDays(7 * n))
                    foreach (var dia in dias)
                    {
                        var d = s.AddDays(dia);
                        if (d >= d0 && d >= de && d <= fim) yield return d;
                    }
                break;
            }
            case RepeticaoAgenda.Mensal:
            {
                var meses = de > d0 ? ((de.Year - d0.Year) * 12 + de.Month - d0.Month) / n * n : 0;
                for (var i = Math.Max(0, meses - n); ; i += n)
                {
                    var mes = new DateOnly(d0.Year, d0.Month, 1).AddMonths(i);
                    if (mes > fim) break;
                    if (d0.Day > DateTime.DaysInMonth(mes.Year, mes.Month)) continue; // dia 31 em mês de 30: não acontece (como no Google)
                    var d = new DateOnly(mes.Year, mes.Month, d0.Day);
                    if (d >= de && d <= fim) yield return d;
                }
                break;
            }
            case RepeticaoAgenda.Anual:
            {
                var anos = de.Year > d0.Year ? (de.Year - d0.Year) / n * n : 0;
                for (var i = Math.Max(0, anos - n); ; i += n)
                {
                    var ano = d0.Year + i;
                    if (ano > fim.Year) break;
                    if (d0.Month == 2 && d0.Day == 29 && !DateTime.IsLeapYear(ano)) continue;
                    var d = new DateOnly(ano, d0.Month, d0.Day);
                    if (d >= de && d <= fim) yield return d;
                }
                break;
            }
        }
    }

    public static bool Repete(EventoAgenda e) => e.Repeticao != RepeticaoAgenda.Nenhuma;

    // ----- Grade -----

    /// <summary>
    /// Pedaços das ocorrências com horário dentro do dia, com a coluna de cada um quando se sobrepõem:
    /// cada grupo de eventos que se encostam divide a largura igualmente.
    /// </summary>
    /// <param name="tarefas">Tarefas cujo prazo é neste dia; as que têm horário de entrega entram na grade.</param>
    public static List<Bloco> Blocos(IEnumerable<Ocorrencia> ocorrencias, DateOnly dia, IEnumerable<CardTarefa>? tarefas = null)
    {
        var ini0 = dia.ToDateTime(TimeOnly.MinValue);
        var fim0 = ini0.AddDays(1);
        var deEventos = ocorrencias.Where(o => !o.NoTopo && o.Inicio < fim0 && (o.Fim > ini0 || o.Inicio >= ini0))
            .Select(o => (O: (Ocorrencia?)o, T: (CardTarefa?)null, Ini: o.Inicio < ini0 ? ini0 : o.Inicio, Fim: o.Fim > fim0 ? fim0 : o.Fim));
        var deTarefas = (tarefas ?? []).Where(c => c.Prazo == dia && c.PrazoHora is not null)
            .Select(c =>
            {
                var ini = ini0 + c.PrazoHora!.Value.ToTimeSpan();
                var fim = ini.AddMinutes(DuracaoTarefa);
                return (O: (Ocorrencia?)null, T: (CardTarefa?)c, Ini: ini, Fim: fim > fim0 ? fim0 : fim);
            });
        var itens = deEventos.Concat(deTarefas)
            // Na grade, todo evento ocupa pelo menos o encaixe (um evento de 0 min também precisa aparecer)
            .Select(x => (x.O, x.T, x.Ini, x.Fim, FimVisual: x.Fim - x.Ini < TimeSpan.FromMinutes(30) ? x.Ini.AddMinutes(30) : x.Fim))
            .OrderBy(x => x.Ini).ThenByDescending(x => x.Fim - x.Ini)
            .ToList();

        var r = new List<Bloco>();
        var grupo = new List<(Ocorrencia? O, CardTarefa? T, DateTime Ini, DateTime Fim, int Coluna)>();
        var colunasFim = new List<DateTime>();
        var fimGrupo = DateTime.MinValue;

        void Fechar()
        {
            var total = colunasFim.Count;
            r.AddRange(grupo.Select(g => new Bloco(g.O, g.T, g.Ini, g.Fim, g.Coluna, total)));
            grupo.Clear();
            colunasFim.Clear();
        }

        foreach (var x in itens)
        {
            if (grupo.Count > 0 && x.Ini >= fimGrupo) Fechar();
            var col = colunasFim.FindIndex(f => f <= x.Ini);
            if (col < 0) { col = colunasFim.Count; colunasFim.Add(x.FimVisual); }
            else colunasFim[col] = x.FimVisual;
            grupo.Add((x.O, x.T, x.Ini, x.Fim, col));
            if (x.FimVisual > fimGrupo) fimGrupo = x.FimVisual;
        }
        if (grupo.Count > 0) Fechar();
        return r;
    }

    /// <summary>Domingo da semana (a semana começa no domingo, como no Google Agenda em português).</summary>
    public static DateOnly InicioDaSemana(DateOnly d) => d.AddDays(-(int)d.DayOfWeek);

    /// <summary>Os 42 dias (6 semanas) da grade do mês.</summary>
    public static List<DateOnly> DiasDoMes(int ano, int mes)
    {
        var inicio = InicioDaSemana(new DateOnly(ano, mes, 1));
        return [.. Enumerable.Range(0, 42).Select(inicio.AddDays)];
    }

    public static DateTime Arredondar(DateTime t, int minutos = Encaixe)
    {
        var m = (int)Math.Round((t.Hour * 60 + t.Minute) / (double)minutos) * minutos;
        return t.Date.AddMinutes(m);
    }

    // ----- Mudanças -----

    /// <summary>
    /// "Só este evento" de uma repetição: cria um evento avulso igual à ocorrência e tira a data da repetição.
    /// Devolve o evento novo (já na lista), que recebe as mudanças.
    /// </summary>
    public static EventoAgenda SoEsta(DadosAgenda a, EventoAgenda e, DateOnly data, DateOnly hoje)
    {
        var ini = data.ToDateTime(TimeOnly.FromDateTime(e.Inicio));
        var novo = new EventoAgenda
        {
            Id = ArmazenamentoService.NovoId(),
            AgendaId = e.AgendaId,
            Titulo = e.Titulo,
            Descricao = e.Descricao,
            Local = e.Local,
            Inicio = ini,
            Fim = ini + Duracao(e),
            DiaInteiro = e.DiaInteiro,
            Lembrete = e.Lembrete,
            Criado = hoje,
        };
        if (!e.Excecoes.Contains(data)) e.Excecoes.Add(data);
        a.Eventos.Add(novo);
        return novo;
    }

    /// <summary>Exclui só uma ocorrência (o resto da repetição continua).</summary>
    public static void ExcluirOcorrencia(EventoAgenda e, DateOnly data)
    {
        if (!e.Excecoes.Contains(data)) e.Excecoes.Add(data);
    }

    /// <summary>
    /// Move e/ou redimensiona: o início anda <paramref name="minutosInicio"/> e o fim <paramref name="minutosFim"/>.
    /// Numa repetição semanal, mudar de dia também muda os dias da semana.
    /// </summary>
    public static void Deslocar(EventoAgenda e, int minutosInicio, int minutosFim)
    {
        var ini = e.Inicio.AddMinutes(minutosInicio);
        var fim = e.Fim.AddMinutes(minutosFim);
        if (fim < ini.AddMinutes(Encaixe)) fim = ini.AddMinutes(Encaixe);
        var dias = (DateOnly.FromDateTime(ini).DayNumber - DateOnly.FromDateTime(e.Inicio).DayNumber) % 7;
        if (e.Repeticao == RepeticaoAgenda.Semanal && dias != 0 && e.DiasSemana.Count > 0)
            e.DiasSemana = [.. e.DiasSemana.Select(d => ((d + dias) % 7 + 7) % 7).Distinct().Order()];
        (e.Inicio, e.Fim) = (ini, fim);
    }

    // ----- Lembretes -----

    /// <summary>Hora em que a ocorrência "começa" para o lembrete (9h no dia inteiro).</summary>
    public static DateTime ReferenciaLembrete(Ocorrencia o) =>
        o.Evento.DiaInteiro ? o.Data.ToDateTime(new TimeOnly(HoraLembreteDiaInteiro, 0)) : o.Inicio;

    /// <summary>Ocorrências cujo lembrete já chegou e que ainda não começaram (ou começaram há pouco).</summary>
    public static List<Ocorrencia> LembretesAgora(DadosAgenda a, DateTime agora)
    {
        // O maior lembrete é de 1 dia: olhar até 2 dias para frente basta
        return Ocorrencias(a, agora.AddHours(-1), agora.AddDays(2), todas: true)
            .Where(o => o.Evento.Lembrete is int min && ReferenciaLembrete(o).AddMinutes(-min) <= agora
                        && agora <= ReferenciaLembrete(o).AddMinutes(ToleranciaLembrete))
            .ToList();
    }

    /// <summary>Chave do aviso em Config.AvisosEnviados (uma por ocorrência).</summary>
    public static string ChaveLembrete(Ocorrencia o) => $"agenda:{o.Evento.Id}:{o.Inicio:yyyy-MM-ddTHH:mm}";

    // ----- Textos -----

    public static string Hora(DateTime t) => t.ToString("HH:mm");

    /// <summary>"14:00 – 15:30", "Dia inteiro" ou "14:00 – 10:00 (qui)" quando acaba noutro dia.</summary>
    public static string Horario(Ocorrencia o)
    {
        if (o.Evento.DiaInteiro)
        {
            var dias = (int)Math.Round((o.Fim - o.Inicio).TotalDays);
            return dias > 1 ? $"Dia inteiro · {dias} dias" : "Dia inteiro";
        }
        if (o.Fim == o.Inicio) return Hora(o.Inicio);
        var outroDia = o.Fim.Date != o.Inicio.Date && o.Fim != o.Inicio.Date.AddDays(1);
        return $"{Hora(o.Inicio)} – {Hora(o.Fim)}{(outroDia ? $" ({DiaSemanaCurto(DateOnly.FromDateTime(o.Fim))} {o.Fim.Day})" : "")}";
    }

    public static string DiaSemanaCurto(DateOnly d) => DiasCurtos[(int)d.DayOfWeek];

    public static readonly string[] DiasCurtos = ["dom", "seg", "ter", "qua", "qui", "sex", "sáb"];

    public static readonly string[] DiasLetra = ["D", "S", "T", "Q", "Q", "S", "S"];

    public static string MesAno(DateOnly d) => d.ToString("MMMM 'de' yyyy", Formatacao.PtBr);

    /// <summary>Título do período mostrado no topo.</summary>
    public static string TituloPeriodo(string visao, DateOnly dia)
    {
        switch (visao)
        {
            case VisaoAgenda.Mes:
                return MesAno(dia);
            case VisaoAgenda.Semana:
            {
                var ini = InicioDaSemana(dia);
                var fim = ini.AddDays(6);
                if (ini.Month == fim.Month) return $"{ini.Day} – {fim.Day} de {fim.ToString("MMMM 'de' yyyy", Formatacao.PtBr)}";
                if (ini.Year == fim.Year) return $"{Formatacao.DataCurta(ini)} – {Formatacao.DataCurta(fim)} de {fim.Year}";
                return $"{Formatacao.DataCurtaAno(ini)} – {Formatacao.DataCurtaAno(fim)}";
            }
            default:
                return Formatacao.DataPorExtenso(dia.ToDateTime(TimeOnly.MinValue)) + $" de {dia.Year}";
        }
    }

    /// <summary>Resumo da repetição para o editor e para o detalhe (ex.: "Toda semana: seg, qua, sex · até 20/12/2026").</summary>
    public static string TextoRepeticao(EventoAgenda e)
    {
        if (!Repete(e)) return "Não repete";
        var r = RepeticaoAgenda.Buscar(e.Repeticao);
        var n = Math.Max(1, e.Intervalo);
        var texto = n == 1 ? r.Nome : $"A cada {n} {r.Unidades}";
        if (e.Repeticao == RepeticaoAgenda.Semanal)
        {
            var dias = e.DiasSemana.Count > 0 ? e.DiasSemana : [(int)e.Inicio.DayOfWeek];
            texto += ": " + string.Join(", ", dias.Order().Select(d => DiasCurtos[d]));
        }
        if (e.RepetirAte is { } ate) texto += $" · até {Formatacao.DataBR(ate)}";
        return texto;
    }

    // ----- Início -----

    public record Resumo(List<Ocorrencia> Proximos, int Hoje, int Amanha, int Semana);

    /// <summary>O que ainda vem hoje, e quantos eventos há hoje, amanhã e nos próximos 7 dias.</summary>
    public static Resumo ResumoDe(DadosAgenda a, DateTime agora)
    {
        var hoje = DateOnly.FromDateTime(agora);
        var doHoje = DoDia(a, hoje);
        return new(
            doHoje.Where(o => o.NoTopo || o.Fim > agora).ToList(),
            doHoje.Count,
            DoDia(a, hoje.AddDays(1)).Count,
            Ocorrencias(a, hoje.ToDateTime(TimeOnly.MinValue), hoje.AddDays(7).ToDateTime(TimeOnly.MinValue)).Count);
    }
}
