using System.Globalization;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Sincroniza os dados da conta com o Supabase (README, item 14, fases 3 a 5). O dados.json é só o cache
/// local (para abrir sem internet) e as telas continuam lendo <see cref="ArmazenamentoService.Dados"/>; este serviço trabalha por trás:
/// <list type="bullet">
/// <item>Envio: compara os dados com base.json (a última versão confirmada pelo servidor), item a item pelo Id,
/// e manda só o que mudou. Itens removidos ganham excluido_em no servidor.</item>
/// <item>Recebimento: busca o que mudou no servidor desde a última vez (atualizado_em) e aplica nos dados.</item>
/// <item>Conflito: por registro, vale a última gravação no servidor.</item>
/// <item>Primeira vez (sem base.json): conta vazia recebe os dados deste aparelho; conta com dados substitui o cache daqui.</item>
/// <item>Capas: as imagens vão à parte, para o Storage, uma vez cada (ver <see cref="Capas"/>).</item>
/// </list>
/// Roda 3 s depois de cada gravação, ao abrir, quando a internet volta e a cada 5 minutos.
/// </summary>
public class SincronizacaoService(ArmazenamentoService store, SupabaseCliente supabase, ContaService conta)
{
    private static readonly string CaminhoBase = Path.Combine(ArmazenamentoService.PastaDados, "base.json");

    // Colunas em snake_case iguais às das tabelas (supabase/migrations/0001_esquema_inicial.sql).
    // Nulos são enviados: é assim que um campo apagado no app é apagado no servidor.
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSerializerOptions JsonBase = new(Json) { WriteIndented = false };

    // Formato das linhas que não têm um modelo próprio no app
    private record QuadroLinha(string Id, string Nome, string? Emoji, int Ordem);
    private record ColunaLinha(string Id, string QuadroId, string Nome, int Ordem);
    private record AgendaLinha(string Id, string Nome, string Cor, int Ordem);
    private record PerfilLinha(double? Altura, double? Meta, string? Tema, string? CorDestaque, List<string>? ModulosOcultos,
        int? DiasLembretePeso);

    /// <summary>
    /// Uma tabela do servidor: como tirar as linhas dos dados do app e como aplicar uma linha recebida.
    /// A ordem da lista <see cref="Tabelas"/> põe os pais antes dos filhos (chaves estrangeiras).
    /// </summary>
    private sealed record Tabela(
        string Nome,
        Type Tipo,
        Func<EstadoApp, IEnumerable<(string Id, object Linha)>> Ler,
        Action<EstadoApp, object?, string> Aplicar) // linha (null = excluída), id
    {
        public bool Perfil => Nome == "perfis";
    }

    private const string IdPerfil = "perfil";

    private static readonly Tabela[] Tabelas =
    [
        new("perfis", typeof(PerfilLinha),
            d => [(IdPerfil, new PerfilLinha(d.Dieta.Perfil.Altura, d.Dieta.Perfil.Meta, d.Tema, d.Config.CorDestaque, d.Config.ModulosOcultos,
                d.Dieta.Perfil.DiasLembretePeso))],
            (d, o, _) =>
            {
                if (o is not PerfilLinha p) return;
                d.Dieta.Perfil.Altura = p.Altura;
                d.Dieta.Perfil.Meta = p.Meta;
                if (p.DiasLembretePeso is int dias and >= 0) d.Dieta.Perfil.DiasLembretePeso = dias; // null: coluna ainda não existe
                d.Tema = p.Tema;
                if (p.CorDestaque is not null) d.Config.CorDestaque = p.CorDestaque;
                d.Config.ModulosOcultos = p.ModulosOcultos ?? [];
            }),
        Lista("registros_peso", d => d.Dieta.Registros, r => r.Id),
        Lista("veiculos", d => d.Carro.Veiculos, v => v.Id),
        Lista("abastecimentos", d => d.Carro.Abastecimentos, a => a.Id),
        Lista("manutencoes", d => d.Carro.Manutencoes, m => m.Id),
        Lista("documentos_carro", d => d.Carro.Documentos, x => x.Id),
        Lista("assinaturas", d => d.Assinaturas, a => a.Id),
        Lista("entretenimento", d => d.Entretenimento, e => e.Id),
        new("quadros", typeof(QuadroLinha),
            d => d.Tarefas.Quadros.Select((q, i) => (q.Id, (object)new QuadroLinha(q.Id, q.Nome, q.Emoji, i))),
            (d, o, id) =>
            {
                var quadros = d.Tarefas.Quadros;
                var atual = quadros.FindIndex(q => q.Id == id);
                if (o is not QuadroLinha l)
                {
                    if (atual >= 0) quadros.RemoveAt(atual);
                    return;
                }
                var q = atual >= 0 ? quadros[atual] : new Quadro { Id = id };
                q.Nome = l.Nome;
                q.Emoji = l.Emoji;
                if (atual >= 0) quadros.RemoveAt(atual);
                quadros.Insert(Math.Clamp(l.Ordem, 0, quadros.Count), q);
            }),
        new("colunas", typeof(ColunaLinha),
            d => d.Tarefas.Quadros.SelectMany(q => q.Colunas.Select((c, i) => (c.Id, (object)new ColunaLinha(c.Id, q.Id, c.Nome, i)))),
            (d, o, id) =>
            {
                foreach (var quadro in d.Tarefas.Quadros) quadro.Colunas.RemoveAll(c => c.Id == id);
                if (o is not ColunaLinha l || d.Tarefas.Quadros.FirstOrDefault(q => q.Id == l.QuadroId) is not { } q) return;
                q.Colunas.Insert(Math.Clamp(l.Ordem, 0, q.Colunas.Count), new ColunaQuadro { Id = id, Nome = l.Nome });
            }),
        Lista("cards", d => d.Tarefas.Cards, c => c.Id),
        new("agendas", typeof(AgendaLinha),
            d => d.Agenda.Agendas.Select((a, i) => (a.Id, (object)new AgendaLinha(a.Id, a.Nome, a.Cor, i))),
            (d, o, id) =>
            {
                var agendas = d.Agenda.Agendas;
                var atual = agendas.FindIndex(a => a.Id == id);
                if (o is not AgendaLinha l)
                {
                    if (atual >= 0) agendas.RemoveAt(atual);
                    return;
                }
                var a = atual >= 0 ? agendas[atual] : new AgendaCor { Id = id };
                a.Nome = l.Nome;
                a.Cor = l.Cor;
                if (atual >= 0) agendas.RemoveAt(atual);
                agendas.Insert(Math.Clamp(l.Ordem, 0, agendas.Count), a);
            }),
        Lista("eventos", d => d.Agenda.Eventos, e => e.Id),
    ];

    private static Tabela Lista<T>(string nome, Func<EstadoApp, List<T>> lista, Func<T, string> id) where T : class => new(
        nome, typeof(T),
        d => lista(d).Select(x => (id(x), (object)x)),
        (d, o, chave) =>
        {
            var l = lista(d);
            var i = l.FindIndex(x => id(x) == chave);
            if (o is T novo)
            {
                if (i >= 0) l[i] = novo; else l.Add(novo);
            }
            else if (i >= 0) l.RemoveAt(i);
        });

    /// <summary>Linhas (já no formato do servidor) de todos os dados: tabela → id → linha.</summary>
    private Dictionary<string, Dictionary<string, JsonObject>> Linhas(EstadoApp d)
    {
        var r = new Dictionary<string, Dictionary<string, JsonObject>>();
        foreach (var t in Tabelas)
        {
            var linhas = r[t.Nome] = [];
            foreach (var (id, linha) in t.Ler(d))
            {
                var l = JsonSerializer.SerializeToNode(linha, t.Tipo, Json)!.AsObject();
                TirarColunasNovas(t.Nome, l);
                linhas[id] = l;
            }
        }
        return r;
    }

    // As linhas são sempre geradas pelo mesmo caminho (modelo → JSON), então comparar o texto basta
    private static bool Igual(JsonObject a, JsonObject b) => a.ToJsonString() == b.ToJsonString();

    // ----- Estado -----

    /// <summary>
    /// Base: a última versão de cada linha confirmada pelo servidor, e até onde já recebemos (cursor por tabela).
    /// Gravada em base.json, na pasta da conta. Sem ela, a próxima sincronização é a "primeira".
    /// </summary>
    private sealed class Base
    {
        public Dictionary<string, Dictionary<string, JsonObject>> Linhas { get; set; } = [];
        public Dictionary<string, DateTimeOffset> Cursores { get; set; } = [];

        /// <summary>Capas (nomes de arquivo) que já estão no Storage. Só estas podem ser apagadas de lá por este computador.</summary>
        public HashSet<string> Capas { get; set; } = [];
    }

    private Base? _base;
    private readonly SemaphoreSlim _rodando = new(1, 1);
    private bool _denovo;
    private Timer? _espera, _periodico;

    /// <summary>Executa na thread da tela do Blazor (definido pelo layout). Sem ele, a sincronização espera.</summary>
    public Func<Func<Task>, Task>? NaTela { get; set; }

    public bool Ativa { get; private set; }

    public bool Sincronizando { get; private set; }

    public DateTime? Ultima { get; private set; }

    /// <summary>Problema da última tentativa (sem internet, sessão expirada...). null = tudo certo.</summary>
    public string? Problema { get; private set; }

    /// <summary>Mudou o status (para o card "Conta"). Pode vir de outra thread.</summary>
    public event Action? Mudou;

    /// <summary>Chegaram mudanças do servidor e já estão em <see cref="ArmazenamentoService.Dados"/> (chamado na thread da tela).</summary>
    public event Action? Recebeu;

    public void Iniciar()
    {
        if (!ArmazenamentoService.PorConta || !SupabaseConfig.Configurado) return;
        Ativa = true;
        _espera = new Timer(_ => _ = Sincronizar());
        _periodico = new Timer(_ => _ = Sincronizar(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        store.Salvou += () => _espera.Change(3000, Timeout.Infinite);
        NetworkChange.NetworkAvailabilityChanged += (_, e) => { if (e.IsAvailable) _espera.Change(3000, Timeout.Infinite); };
        _espera.Change(1500, Timeout.Infinite);
    }

    /// <summary>Envia as mudanças daqui e recebe as do servidor. Nunca lança: problemas ficam em <see cref="Problema"/>.</summary>
    public async Task Sincronizar()
    {
        if (!Ativa) return;
        if (!await _rodando.WaitAsync(0))
        {
            _denovo = true; // já está rodando: roda de novo no fim, para pegar o que mudou nesse meio-tempo
            return;
        }
        try
        {
            do
            {
                _denovo = false;
                await Rodada();
            } while (_denovo);
        }
        finally { _rodando.Release(); }
    }

    private async Task Rodada()
    {
        if (NaTela is null)
        {
            _espera?.Change(2000, Timeout.Infinite); // a janela ainda está abrindo
            return;
        }
        if (!conta.Logado)
        {
            Avisar(problema: "Sessão encerrada. Entre na conta de novo para voltar a sincronizar.");
            return;
        }

        Sincronizando = true;
        Mudou?.Invoke();
        try
        {
            if (!await conta.GarantirSessao())
            {
                Avisar(problema: conta.Logado ? "Sem conexão com o servidor. As mudanças ficam guardadas e vão depois." : "Sessão encerrada. Entre na conta de novo para voltar a sincronizar.");
                return;
            }
            await VerificarTabelasNovas();
            _base ??= LerBase();
            if (_base is null) await Primeira();
            else
            {
                await Enviar(_base);
                await Receber(_base);
            }
            await Capas(_base!);
            Avisar(problema: _faltam.Count == 0 ? null
                : $"Falta rodar no Supabase: {string.Join(", ", _faltam.Order().Select(m => "supabase/migrations/" + m))}. O resto está sincronizado.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Avisar(problema: "Sem conexão com o servidor. As mudanças ficam guardadas e vão depois.");
        }
        catch (Exception ex)
        {
            Avisar(problema: $"Não foi possível sincronizar: {ex.Message}");
        }
    }

    // ----- Tabelas que vieram depois (migrations rodadas à mão) -----

    /// <summary>Tabelas de migrations novas: enquanto não existirem no servidor, ficam de fora e o resto sincroniza normalmente.</summary>
    private static readonly (string Tabela, string Migration)[] TabelasNovas =
    [
        ("agendas", "0003_agenda.sql"),
        ("eventos", "0003_agenda.sql"),
    ];

    /// <summary>Colunas novas em tabelas que já existiam: enquanto faltarem, a coluna não é enviada nem recebida.</summary>
    private static readonly (string Tabela, string Coluna, string Migration)[] ColunasNovas =
    [
        ("cards", "prazo_hora", "0004_tarefas_hora.sql"),
        ("manutencoes", "servico", "0005_manutencao_servico.sql"),
        ("veiculos", "marca", "0006_veiculo_ficha.sql"),
        ("veiculos", "ano", "0006_veiculo_ficha.sql"),
        ("veiculos", "ficha", "0006_veiculo_ficha.sql"),
        ("perfis", "dias_lembrete_peso", "0007_dieta_lembrete_peso.sql"),
    ];

    private readonly HashSet<string> _semTabela = [];
    private readonly HashSet<(string Tabela, string Coluna)> _semColuna = [];
    private readonly HashSet<string> _faltam = []; // migrations que ainda não rodaram
    private bool _tabelasConferidas;

    private async Task VerificarTabelasNovas()
    {
        if (_tabelasConferidas) return;
        _semTabela.Clear();
        _semColuna.Clear();
        _faltam.Clear();
        foreach (var (nome, migration) in TabelasNovas)
        {
            try { await supabase.Enviar(HttpMethod.Get, $"/rest/v1/{nome}?select=id&limit=1"); }
            // PGRST205: a tabela não está no esquema (a migration ainda não rodou)
            catch (SupabaseErro ex) when (ex.Status == 404 || ex.Codigo is "PGRST205" or "42P01")
            {
                _semTabela.Add(nome);
                _faltam.Add(migration);
            }
        }
        foreach (var (tabela, coluna, migration) in ColunasNovas)
        {
            try { await supabase.Enviar(HttpMethod.Get, $"/rest/v1/{tabela}?select={coluna}&limit=1"); }
            // 42703: a coluna não existe; PGRST204: não está no cache do esquema
            catch (SupabaseErro ex) when (ex.Codigo is "42703" or "PGRST204")
            {
                _semColuna.Add((tabela, coluna));
                _faltam.Add(migration);
            }
        }
        _tabelasConferidas = _faltam.Count == 0;
    }

    /// <summary>Tira da linha as colunas que o servidor ainda não tem (para enviar e para comparar com a base do mesmo jeito).</summary>
    private void TirarColunasNovas(string tabela, JsonObject linha)
    {
        foreach (var (t, coluna) in _semColuna)
            if (t == tabela) linha.Remove(coluna);
    }

    private void Avisar(string? problema)
    {
        Sincronizando = false;
        Problema = problema;
        if (problema is null) Ultima = DateTime.Now;
        Mudou?.Invoke();
    }

    // ----- Envio -----

    private async Task Enviar(Base b)
    {
        var locais = await NaTelaRetornar(() => Linhas(store.Dados));
        var agora = DateTimeOffset.UtcNow.ToString("o");

        foreach (var t in Tabelas)
        {
            if (_semTabela.Contains(t.Nome)) continue;
            var local = locais[t.Nome];
            var anterior = b.Linhas.GetValueOrDefault(t.Nome) ?? [];

            var mudadas = local.Where(kv => !anterior.TryGetValue(kv.Key, out var a) || !Igual(a, kv.Value))
                               .Where(kv => PaisExistem(t.Nome, kv.Value, locais))
                               .ToList();
            foreach (var lote in mudadas.Chunk(500))
            {
                var corpo = new JsonArray();
                foreach (var (_, linha) in lote)
                {
                    var l = linha.DeepClone().AsObject();
                    l["user_id"] = conta.UserId;
                    if (!t.Perfil) l["excluido_em"] = null; // recriado depois de excluído
                    corpo.Add(l);
                }
                await supabase.Enviar(HttpMethod.Post, $"/rest/v1/{t.Nome}?on_conflict={(t.Perfil ? "user_id" : "user_id,id")}", corpo,
                    "resolution=merge-duplicates,return=minimal");
                foreach (var (id, linha) in lote) anterior[id] = linha;
            }

            var removidas = t.Perfil ? new List<string>() : anterior.Keys.Where(id => !local.ContainsKey(id)).ToList();
            foreach (var lote in removidas.Chunk(100))
            {
                await supabase.Enviar(HttpMethod.Patch, $"/rest/v1/{t.Nome}?id=in.({string.Join(',', lote.Select(Uri.EscapeDataString))})",
                    new JsonObject { ["excluido_em"] = agora }, "return=minimal");
                foreach (var id in lote) anterior.Remove(id);
            }

            b.Linhas[t.Nome] = anterior;
            if (mudadas.Count > 0 || removidas.Count > 0) GravarBase(b);
        }
    }

    // Um filho cujo pai não existe aqui não pode subir (o servidor recusaria pela chave estrangeira)
    private static bool PaisExistem(string tabela, JsonObject l, Dictionary<string, Dictionary<string, JsonObject>> locais)
    {
        bool Tem(string pai, string campo) => l[campo]?.GetValue<string>() is not { } id || locais[pai].ContainsKey(id);
        return tabela switch
        {
            "abastecimentos" or "manutencoes" or "documentos_carro" => Tem("veiculos", "veiculo_id"),
            "colunas" => Tem("quadros", "quadro_id"),
            "cards" => Tem("quadros", "quadro_id") && Tem("colunas", "coluna_id"),
            "eventos" => Tem("agendas", "agenda_id"),
            _ => true,
        };
    }

    // ----- Recebimento -----

    private async Task Receber(Base b)
    {
        // Busca tudo o que mudou em todas as tabelas e aplica de uma vez (pais antes dos filhos)
        var recebidas = new List<(Tabela T, string Id, JsonObject? Linha)>();
        var cursores = new Dictionary<string, DateTimeOffset>();
        foreach (var t in Tabelas)
        {
            if (_semTabela.Contains(t.Nome)) continue;
            var (linhas, cursor) = await Buscar(t, b.Cursores.TryGetValue(t.Nome, out var c) ? c : null);
            if (cursor is { } novo) cursores[t.Nome] = novo;
            var anterior = b.Linhas.GetValueOrDefault(t.Nome) ?? [];
            foreach (var (id, linha) in linhas)
            {
                // Volta também o que acabamos de enviar (e a margem de 1 min repete linhas): só aplica o que é diferente
                var igual = linha is null ? !anterior.ContainsKey(id) : anterior.TryGetValue(id, out var a) && Igual(a, linha);
                if (!igual) recebidas.Add((t, id, linha));
            }
        }

        if (recebidas.Count > 0)
        {
            await NaTela!(() =>
            {
                foreach (var (t, id, linha) in recebidas)
                    t.Aplicar(store.Dados, linha?.Deserialize(t.Tipo, Json), id);
                store.NormalizarESalvar();
                // A base passa a ter o que veio do servidor (assim não é reenviado)
                foreach (var (t, id, linha) in recebidas)
                {
                    var anterior = b.Linhas.TryGetValue(t.Nome, out var x) ? x : b.Linhas[t.Nome] = [];
                    if (linha is null) anterior.Remove(id); else anterior[id] = linha;
                }
                Recebeu?.Invoke();
                return Task.CompletedTask;
            });
        }

        foreach (var (nome, cursor) in cursores) b.Cursores[nome] = cursor;
        GravarBase(b);
    }

    /// <summary>
    /// Linhas da tabela alteradas depois do cursor (todas, se null), já normalizadas para comparar com a base.
    /// Linha null = excluída. Retorna também o novo cursor (maior atualizado_em visto).
    /// </summary>
    private async Task<(List<(string Id, JsonObject? Linha)> Linhas, DateTimeOffset? Cursor)> Buscar(Tabela t, DateTimeOffset? desde)
    {
        var r = new List<(string, JsonObject?)>();
        DateTimeOffset? cursor = null;
        // Margem de 1 min: gravações em andamento podem terminar com um atualizado_em um pouco anterior ao último visto
        var filtro = desde is { } d ? $"&atualizado_em=gt.{Uri.EscapeDataString(d.AddMinutes(-1).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'"))}" : "";
        var ordem = t.Perfil ? "atualizado_em.asc" : "atualizado_em.asc,id.asc";
        const int Pagina = 1000;
        for (var inicio = 0; ; inicio += Pagina)
        {
            var resposta = await supabase.Enviar(HttpMethod.Get, $"/rest/v1/{t.Nome}?select=*&order={ordem}&limit={Pagina}&offset={inicio}{filtro}");
            var linhas = resposta?.AsArray() ?? [];
            foreach (var n in linhas)
            {
                var o = n!.AsObject();
                if (o["atualizado_em"]?.GetValue<string>() is { } s && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var quando) && (cursor is null || quando > cursor))
                    cursor = quando;
                var id = t.Perfil ? IdPerfil : o["id"]!.GetValue<string>();
                var excluida = !t.Perfil && o["excluido_em"] is JsonValue;
                // Passa pelo modelo e volta: fica no mesmo formato das linhas geradas pelo app
                JsonObject? linha = null;
                if (!excluida)
                {
                    linha = JsonSerializer.SerializeToNode(o.Deserialize(t.Tipo, Json), t.Tipo, Json)!.AsObject();
                    TirarColunasNovas(t.Nome, linha);
                }
                r.Add((id, linha));
            }
            if (linhas.Count < Pagina) break;
        }
        // Quadros, colunas e agendas entram na posição da "ordem": aplicar em ordem crescente monta a lista certa
        if (t.Nome is "quadros" or "colunas" or "agendas")
            r = [.. r.OrderBy(x => x.Item2?["ordem"]?.GetValue<int>() ?? 0)];
        return (r, cursor);
    }

    // ----- Capas (fase 5) -----

    /// <summary>
    /// As imagens não vão nas linhas do Entretenimento: cada capa vai uma vez para o Storage ("capas/&lt;user_id&gt;/&lt;arquivo&gt;"),
    /// já que o nome do arquivo é único e nunca muda. Sobe a que o servidor não tem, baixa a que falta aqui e apaga
    /// do servidor a que nenhum item usa mais. Roda depois das linhas, então os itens daqui já batem com os do servidor.
    /// </summary>
    private async Task Capas(Base b)
    {
        const string Bucket = "capas";
        var usadas = await NaTelaRetornar(() => store.Dados.Entretenimento
            .Select(e => e.Capa).OfType<string>().Where(c => c.Length > 0).Select(Path.GetFileName).OfType<string>().ToHashSet());
        string Remoto(string arquivo) => $"{conta.UserId}/{arquivo}";
        var baixou = false;

        foreach (var arquivo in usadas)
        {
            var local = Path.Combine(CapasService.Pasta, arquivo);
            try
            {
                if (File.Exists(local))
                {
                    if (b.Capas.Contains(arquivo)) continue;
                    // Capas de antes da fase 5 ainda estão no tamanho original: reduz antes de subir
                    var bytes = File.ReadAllBytes(local);
                    var menor = CapasService.Reduzir(bytes, Path.GetExtension(arquivo));
                    if (menor != bytes) File.WriteAllBytes(local, menor);
                    await supabase.EnviarArquivo(Bucket, Remoto(arquivo), menor, CapasService.TipoMime(arquivo));
                }
                else
                {
                    // null: o outro computador ainda não subiu; tenta na próxima rodada
                    if (await supabase.BaixarArquivo(Bucket, Remoto(arquivo)) is not { } bytes) continue;
                    Directory.CreateDirectory(CapasService.Pasta);
                    File.WriteAllBytes(local, bytes);
                    baixou = true;
                }
                b.Capas.Add(arquivo);
                GravarBase(b);
            }
            catch (SupabaseErro) { } // uma capa recusada não segura as outras nem os dados
        }

        foreach (var arquivo in b.Capas.Where(c => !usadas.Contains(c)).ToList())
        {
            try
            {
                await supabase.ExcluirArquivo(Bucket, Remoto(arquivo));
                b.Capas.Remove(arquivo);
                GravarBase(b);
            }
            catch (SupabaseErro) { }
        }

        // As capas aparecem sem reiniciar (a página aberta é recriada)
        if (baixou) await NaTela!(() => { Recebeu?.Invoke(); return Task.CompletedTask; });
    }

    // ----- Primeira sincronização (fase 4) -----

    private async Task Primeira()
    {
        var b = new Base();
        var doServidor = new List<(Tabela T, string Id, JsonObject Linha)>();
        foreach (var t in Tabelas)
        {
            if (_semTabela.Contains(t.Nome)) continue;
            var (linhas, cursor) = await Buscar(t, null);
            if (cursor is { } c) b.Cursores[t.Nome] = c;
            doServidor.AddRange(linhas.Where(l => l.Linha is not null).Select(l => (t, l.Id, l.Linha!)));
        }

        // Só o perfil não conta: ele é criado sozinho
        if (doServidor.Any(l => !l.T.Perfil))
        {
            // Conta com dados: eles substituem o cache deste aparelho
            await NaTela!(() =>
            {
                var novo = new EstadoApp { Tema = store.Dados.Tema, Config = store.Dados.Config };
                foreach (var (t, id, linha) in doServidor)
                    t.Aplicar(novo, linha.Deserialize(t.Tipo, Json), id);
                foreach (var (t, id, linha) in doServidor)
                    (b.Linhas.TryGetValue(t.Nome, out var x) ? x : b.Linhas[t.Nome] = [])[id] = linha;
                store.Substituir(novo);
                Recebeu?.Invoke();
                return Task.CompletedTask;
            });
        }
        // Conta vazia: a base fica vazia e o envio abaixo sobe tudo o que há aqui

        _base = b;
        GravarBase(b);
        await Enviar(b); // o que o app completou ao normalizar (ou tudo, na conta vazia)
    }

    // ----- base.json -----

    private static Base? LerBase()
    {
        try { return File.Exists(CaminhoBase) ? JsonSerializer.Deserialize<Base>(File.ReadAllText(CaminhoBase), JsonBase) : null; }
        catch { return null; } // corrompida: refaz pela primeira sincronização (nada se perde, os locais ficam guardados)
    }

    private static void GravarBase(Base b)
    {
        var temporario = CaminhoBase + ".tmp";
        File.WriteAllText(temporario, JsonSerializer.Serialize(b, JsonBase));
        File.Move(temporario, CaminhoBase, overwrite: true);
    }

    private async Task<T> NaTelaRetornar<T>(Func<T> f)
    {
        T r = default!;
        await NaTela!(() => { r = f(); return Task.CompletedTask; });
        return r;
    }
}
