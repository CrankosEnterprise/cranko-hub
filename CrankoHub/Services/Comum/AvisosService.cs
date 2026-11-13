using System.Windows.Threading;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// De hora em hora (e logo depois de abrir) manda notificações
/// com os alertas do Início, cada um no máximo uma vez por dia.
/// </summary>
public class AvisosService(ArmazenamentoService store, BandejaService bandeja)
{
    private DispatcherTimer? _timer, _lembretes;

    /// <summary>De quanto em quanto tempo os lembretes da Agenda são conferidos.</summary>
    public static readonly TimeSpan IntervaloLembretes = TimeSpan.FromSeconds(30);

    public void Iniciar()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _timer.Tick += (_, _) =>
        {
            _timer.Interval = TimeSpan.FromHours(1);
            Verificar();
        };
        _timer.Start();

        // Os lembretes da Agenda têm hora marcada: conferidos a cada 30 s
        _lembretes = new DispatcherTimer { Interval = IntervaloLembretes };
        _lembretes.Tick += (_, _) => LembretesDaAgenda();
        _lembretes.Start();
    }

    // Cada ocorrência avisa uma vez (a chave tem o horário dela), na hora do lembrete
    private void LembretesDaAgenda()
    {
        if (!ArmazenamentoService.PorConta) return;
        var cfg = store.Dados.Config;
        if (!cfg.Avisos || !Modulo.Visivel(cfg, Modulo.Agenda)) return;

        var agora = DateTime.Now;
        var novos = AgendaCalculos.LembretesAgora(store.Dados.Agenda, agora)
            .Where(o => !cfg.AvisosEnviados.ContainsKey(AgendaCalculos.ChaveLembrete(o)))
            .ToList();
        if (novos.Count == 0) return;

        foreach (var o in novos)
        {
            var e = o.Evento;
            var titulo = string.IsNullOrWhiteSpace(e.Titulo) ? "(sem título)" : e.Titulo;
            string quando;
            if (e.DiaInteiro) quando = o.Data == Formatacao.Hoje ? "Hoje, dia inteiro" : "Amanhã, dia inteiro";
            else
            {
                var falta = (int)Math.Round((o.Inicio - agora).TotalMinutes);
                quando = falta switch
                {
                    <= 0 => $"Agora, às {AgendaCalculos.Hora(o.Inicio)}",
                    < 60 => $"Em {falta} min, às {AgendaCalculos.Hora(o.Inicio)}",
                    _ => $"{(o.Data == Formatacao.Hoje ? "Hoje" : "Amanhã")} às {AgendaCalculos.Hora(o.Inicio)}",
                };
            }
            bandeja.Notificar(titulo, quando + (string.IsNullOrEmpty(e.Local) ? "" : " · " + e.Local),
                $"agenda?dia={Formatacao.Iso(o.Data)}&visao=dia");
            // Guarda a data da ocorrência (pode ser amanhã): a limpeza em Verificar só apaga as de dias que já passaram
            cfg.AvisosEnviados[AgendaCalculos.ChaveLembrete(o)] = o.Data;
        }
        store.Salvar();
    }

    /// <param name="manual">Pedido pelo usuário: avisa tudo de novo e diz quando não há nada.</param>
    public void Verificar(bool manual = false)
    {
        if (!ArmazenamentoService.PorConta) return; // tela de login: ainda não há dados
        var cfg = store.Dados.Config;
        if (!cfg.Avisos && !manual) return;

        var hoje = Formatacao.Hoje;
        ResumoDasTarefas(hoje, manual);
        var alertas = Alertas.Gerar(store.Dados, hoje)
            .Where(a => manual || !cfg.AvisosEnviados.TryGetValue(a.Chave, out var dia) || dia != hoje)
            .ToList();

        if (alertas.Count == 0)
        {
            if (manual) bandeja.Notificar("Tudo em dia", "Nada precisa da sua atenção agora.");
            return;
        }

        if (alertas.Count == 1)
            bandeja.Notificar(alertas[0].Titulo, alertas[0].Detalhe, alertas[0].Link);
        else
            bandeja.Notificar($"{alertas.Count} coisas precisam de atenção",
                string.Join("\n", alertas.Take(3).Select(a => "• " + a.Titulo)) + (alertas.Count > 3 ? "\n..." : ""), "");

        // Guarda o que já foi avisado hoje e esquece os dias anteriores
        foreach (var chave in cfg.AvisosEnviados.Where(p => p.Value < hoje).Select(p => p.Key).ToList())
            cfg.AvisosEnviados.Remove(chave);
        foreach (var a in alertas)
            cfg.AvisosEnviados[a.Chave] = hoje;
        store.Salvar();
    }

    /// <summary>Hora a partir da qual sai o resumo do dia das tarefas.</summary>
    public const int HoraResumoTarefas = 20;

    // À noite, uma vez por dia: o que foi feito e o que ficou pendente
    private void ResumoDasTarefas(DateOnly hoje, bool manual)
    {
        const string chave = "tarefas:resumo";
        var cfg = store.Dados.Config;
        if (!Modulo.Visivel(cfg, Modulo.Tarefas)) return;
        if (!manual && (DateTime.Now.Hour < HoraResumoTarefas || cfg.AvisosEnviados.TryGetValue(chave, out var dia) && dia == hoje)) return;

        var r = TarefasCalculos.ResumoDoDia(store.Dados.Tarefas, hoje);
        var pendentes = r.ParaHoje.Count + r.Atrasados.Count;
        if (r.ConcluidosHoje.Count + pendentes == 0) return;

        bandeja.Notificar($"Resumo do dia: {r.ConcluidosHoje.Count} {Formatacao.Plural(r.ConcluidosHoje.Count, "tarefa concluída", "tarefas concluídas")}",
            pendentes == 0 ? "Nada ficou para trás. Bom trabalho!" :
                $"{pendentes} {Formatacao.Plural(pendentes, "ficou pendente", "ficaram pendentes")}:\n" +
                string.Join("\n", r.Atrasados.Concat(r.ParaHoje).Take(3).Select(c => "• " + c.Titulo)) + (pendentes > 3 ? "\n..." : ""),
            "tarefas?quadro=hoje");
        cfg.AvisosEnviados[chave] = hoje;
        store.Salvar();
    }
}
