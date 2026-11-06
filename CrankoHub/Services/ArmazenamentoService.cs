using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Cache local dos dados da conta, em %LocalAppData%\CrankoHub\contas\&lt;user_id&gt;\dados.json.
/// Os dados de verdade ficam no Supabase (<see cref="SincronizacaoService"/>); este arquivo só existe para o app
/// abrir sem internet e guardar as mudanças até elas subirem. Sem conta conectada não há dados: o app mostra o login.
/// </summary>
public class ArmazenamentoService
{
    /// <summary>Pasta deste computador (WebView, trava de instância, local.json).</summary>
    public static readonly string PastaLocal = ConfigLocal.Pasta;

    /// <summary>Havia uma conta conectada ao abrir o app: os dados são os dela (contas/&lt;user_id&gt;/), sincronizados com o servidor.</summary>
    public static readonly bool PorConta = ConfigLocal.Atual.ContaId is not null;

    /// <summary>Pasta do cache da conta. Sem conta, nada é gravado (o app fica na tela de login). Muda só reiniciando o app.</summary>
    public static readonly string PastaDados = ConfigLocal.Atual.ContaId is { } conta ? PastaDaConta(conta) : PastaLocal;

    public static readonly string CaminhoDados = Path.Combine(PastaDados, "dados.json");

    public static string PastaDaConta(string userId) => Path.Combine(PastaLocal, "contas", userId);

    /// <summary>Ao entrar numa conta: cria a pasta do cache. Um aparelho novo começa vazio e a primeira sincronização traz os dados.</summary>
    public static void PrepararPastaDaConta(string userId) => Directory.CreateDirectory(PastaDaConta(userId));

    private static readonly JsonSerializerOptions OpcoesJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // mantém acentos legíveis no arquivo
        WriteIndented = true,
    };

    private readonly ToastService _toast;

    public ArmazenamentoService(ToastService toast)
    {
        _toast = toast;
        Dados = Carregar();

        // "Iniciar com o Windows" era guardado nos dados; agora é de cada computador
        var local = ConfigLocal.Atual;
        if (local.IniciarComWindows is null)
        {
            local.IniciarComWindows = Dados.Config.IniciarComWindows;
            try { local.Salvar(); } catch { }
        }
    }

    public EstadoApp Dados { get; private set; }

    /// <summary>Os dados foram gravados (a sincronização com a conta espera uns segundos e envia as mudanças).</summary>
    public event Action? Salvou;

    /// <summary>Substitui todos os dados (pelos da conta, na primeira sincronização) e grava.</summary>
    public void Substituir(EstadoApp novo)
    {
        Dados = Normalizar(novo);
        Salvar();
    }

    /// <summary>Depois de mudar <see cref="Dados"/> direto (mudanças vindas do servidor): normaliza e grava.</summary>
    public void NormalizarESalvar()
    {
        Dados = Normalizar(Dados);
        Salvar();
    }

    /// <summary>Avisa o menu quando um módulo é mostrado ou escondido.</summary>
    public event Action? ModulosMudaram;

    public void MostrarModulo(string id, bool visivel)
    {
        Dados.Config.ModulosOcultos.Remove(id);
        if (!visivel) Dados.Config.ModulosOcultos.Add(id);
        Salvar();
        ModulosMudaram?.Invoke();
    }

    /// <summary>Id curto e único (mesmo estilo do app web).</summary>
    public static string NovoId()
    {
        const string base36 = "0123456789abcdefghijklmnopqrstuvwxyz";
        var sb = new StringBuilder();
        for (var t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); t > 0; t /= 36)
            sb.Insert(0, base36[(int)(t % 36)]);
        for (var i = 0; i < 5; i++)
            sb.Append(base36[Random.Shared.Next(36)]);
        return sb.ToString();
    }

    public void Salvar()
    {
        if (!PorConta) return; // tela de login: não há conta para guardar
        try
        {
            Directory.CreateDirectory(PastaDados);
            // Grava num arquivo temporário e troca no final, para nunca deixar o arquivo pela metade
            var temporario = CaminhoDados + ".tmp";
            File.WriteAllText(temporario, JsonSerializer.Serialize(Dados, OpcoesJson));
            File.Move(temporario, CaminhoDados, overwrite: true);
        }
        catch (Exception ex)
        {
            _toast.Erro($"Não foi possível salvar os dados: {ex.Message}");
            return;
        }
        Salvou?.Invoke();
    }

    private static EstadoApp Carregar()
    {
        if (!PorConta) return Novo();
        try
        {
            if (File.Exists(CaminhoDados) && JsonSerializer.Deserialize<EstadoApp>(File.ReadAllText(CaminhoDados), OpcoesJson) is { } estado)
                return Normalizar(estado);
        }
        catch
        {
            // Cache corrompido: começa do zero e a sincronização traz tudo de novo do servidor
            try { File.Delete(Path.Combine(PastaDados, "base.json")); } catch { }
        }
        return Novo();
    }

    private static EstadoApp Novo()
    {
        var veiculo = new Veiculo { Id = NovoId(), Nome = "Meu carro" };
        // Tarefas começa sem quadros: o usuário cria os seus
        return new EstadoApp
        {
            Carro = new DadosCarro { Veiculos = [veiculo], Ativo = veiculo.Id },
            Tarefas = new DadosTarefas { Quadros = [], Ativo = Quadro.Hoje },
        };
    }

    // Garante que nada essencial esteja faltando (arquivos antigos ou editados à mão)
    private static EstadoApp Normalizar(EstadoApp s)
    {
        s.Dieta ??= new DadosDieta();
        s.Dieta.Perfil ??= new PerfilDieta();
        s.Dieta.Registros ??= [];
        s.Dieta.Registros.RemoveAll(r => r is null);
        s.Carro ??= new DadosCarro();
        s.Carro.Abastecimentos ??= [];
        s.Carro.Abastecimentos.RemoveAll(a => a is null);
        s.Carro.Manutencoes ??= []; // backups antigos (e do app web) não têm manutenções
        s.Carro.Manutencoes.RemoveAll(m => m is null);
        s.Carro.Documentos ??= [];
        s.Carro.Documentos.RemoveAll(x => x is null);
        s.Carro.Veiculos ??= [];
        s.Carro.Veiculos.RemoveAll(v => v is null);
        if (s.Carro.Veiculos.Count == 0)
            s.Carro.Veiculos = Novo().Carro.Veiculos;
        if (!s.Carro.Veiculos.Any(v => v.Id == s.Carro.Ativo))
            s.Carro.Ativo = s.Carro.Veiculos[0].Id;
        s.Config ??= new Configuracoes();
        s.Config.AvisosEnviados ??= [];
        s.Config.ModulosOcultos ??= [];
        s.Config.DestinosRecentes ??= [];
        s.Config.ModulosOcultos.RemoveAll(id => Modulo.Buscar(id) is null);
        s.Assinaturas ??= []; // backups antigos (e do app web) não têm assinaturas
        s.Assinaturas.RemoveAll(a => a is null);
        s.Entretenimento ??= [];
        s.Entretenimento.RemoveAll(e => e is null);

        foreach (var r in s.Dieta.Registros.Where(r => string.IsNullOrEmpty(r.Id))) r.Id = NovoId();
        foreach (var a in s.Carro.Abastecimentos.Where(a => string.IsNullOrEmpty(a.Id))) a.Id = NovoId();
        foreach (var m in s.Carro.Manutencoes.Where(m => string.IsNullOrEmpty(m.Id))) m.Id = NovoId();
        foreach (var x in s.Carro.Documentos.Where(x => string.IsNullOrEmpty(x.Id))) x.Id = NovoId();
        foreach (var a in s.Assinaturas.Where(a => string.IsNullOrEmpty(a.Id))) a.Id = NovoId();
        foreach (var e in s.Entretenimento.Where(e => string.IsNullOrEmpty(e.Id))) e.Id = NovoId();

        // Tarefas: backups antigos não têm; todo quadro precisa de pelo menos uma coluna
        var t = s.Tarefas ??= new DadosTarefas();
        t.Quadros ??= [];
        t.Quadros.RemoveAll(q => q is null);
        foreach (var q in t.Quadros)
        {
            if (string.IsNullOrEmpty(q.Id)) q.Id = NovoId();
            q.Colunas ??= [];
            q.Colunas.RemoveAll(c => c is null);
            foreach (var c in q.Colunas.Where(c => string.IsNullOrEmpty(c.Id))) c.Id = NovoId();
            if (q.Colunas.Count == 0) q.Colunas = TarefasCalculos.NovoQuadro(q.Nome).Colunas;
        }
        if (t.Ativo != Quadro.Hoje && t.Quadros.All(q => q.Id != t.Ativo))
            t.Ativo = t.Quadros.Count > 0 ? t.Quadros[0].Id : Quadro.Hoje;
        t.Cards ??= [];
        t.Cards.RemoveAll(c => c is null || t.Quadros.All(q => q.Id != c.QuadroId));
        foreach (var c in t.Cards)
        {
            if (string.IsNullOrEmpty(c.Id)) c.Id = NovoId();
            c.Etiquetas ??= [];
            c.Checklist ??= [];
            c.Checklist.RemoveAll(i => i is null);
            var q = t.Quadros.First(q => q.Id == c.QuadroId);
            if (q.Colunas.All(col => col.Id != c.ColunaId)) c.ColunaId = q.Colunas[0].Id;
            if (c.Prazo is null) c.PrazoHora = null;
        }

        // Agenda: sempre pelo menos uma agenda (a principal tem id fixo, para não duplicar entre aparelhos)
        var ag = s.Agenda ??= new DadosAgenda();
        ag.Agendas ??= [];
        ag.Agendas.RemoveAll(x => x is null);
        foreach (var x in ag.Agendas.Where(x => string.IsNullOrEmpty(x.Id))) x.Id = NovoId();
        foreach (var x in ag.Agendas.Where(x => string.IsNullOrEmpty(x.Cor))) x.Cor = CoresAgenda.Todas[0];
        if (ag.Agendas.Count == 0) ag.Agendas.Add(AgendaCalculos.NovaPrincipal());
        ag.Ocultas ??= [];
        ag.Ocultas.RemoveAll(id => ag.Agendas.All(x => x.Id != id));
        ag.QuadrosOcultos ??= [];
        ag.QuadrosOcultos.RemoveAll(id => t.Quadros.All(q => q.Id != id));
        if (ag.Visao is not (VisaoAgenda.Dia or VisaoAgenda.Semana or VisaoAgenda.Mes)) ag.Visao = VisaoAgenda.Semana;
        ag.Eventos ??= [];
        ag.Eventos.RemoveAll(e => e is null);
        foreach (var e in ag.Eventos)
        {
            if (string.IsNullOrEmpty(e.Id)) e.Id = NovoId();
            if (ag.Agendas.All(x => x.Id != e.AgendaId)) e.AgendaId = ag.Agendas[0].Id;
            e.Titulo ??= "";
            e.Repeticao ??= RepeticaoAgenda.Nenhuma;
            e.DiasSemana ??= [];
            e.Excecoes ??= [];
            if (e.Intervalo < 1) e.Intervalo = 1;
            if (e.Fim < e.Inicio) e.Fim = e.DiaInteiro ? e.Inicio.Date.AddDays(1) : e.Inicio.AddHours(1);
        }
        return s;
    }
}
