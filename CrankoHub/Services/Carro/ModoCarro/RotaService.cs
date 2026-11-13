using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Navegação do Modo Carro, sem Google:
/// <list type="bullet">
/// <item>busca de destino pelo Photon (komoot, OpenStreetMap);</item>
/// <item>rotas pelo Valhalla (servidor público da FOSSGIS): até 3 opções, instruções em pt-BR, "evitar pedágios";</item>
/// <item>praças de pedágio de cada opção pelo <c>trace_attributes</c> do próprio Valhalla;</item>
/// <item>andamento calculado aqui a partir do GPS, recálculo ao sair da rota e aviso por voz.</item>
/// </list>
/// O tempo vem dos limites das vias, sem trânsito em tempo real.
/// </summary>
public sealed class RotaService(HttpClient http, ModoCarroService modo, ArmazenamentoService store)
{
    private const string Valhalla = "https://valhalla1.openstreetmap.de";
    private const double ForaDaRotaMetros = 60;   // longe assim da linha conta como fora da rota
    private const double ChegadaMetros = 30;
    private static readonly TimeSpan EsperaRecalculo = TimeSpan.FromSeconds(15);
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Rota em andamento, ou null.</summary>
    public RotaAtiva? Atual { get; private set; }

    public ProgressoRota? Progresso { get; private set; }

    /// <summary>Opções para escolher antes de iniciar (prévia), ou null.</summary>
    public IReadOnlyList<RotaAtiva>? Opcoes { get; private set; }

    /// <summary>Opção marcada na prévia.</summary>
    public int Escolhida { get; private set; }

    /// <summary>Destino da prévia ou da rota em andamento.</summary>
    public LugarBusca? Destino { get; private set; }

    /// <summary>Calculando as opções ou recalculando depois de sair da rota.</summary>
    public bool Calculando { get; private set; }

    /// <summary>Muda sempre que as linhas no mapa mudam (o mapa só redesenha nessa hora).</summary>
    public int Versao { get; private set; }

    public bool EvitarPedagios => store.Dados.Config.EvitarPedagios;

    public event Action? Mudou;

    // ===== Google (opcional) =====

    private readonly GoogleMaps _google = new(http);
    private string? _sessaoBusca;

    /// <summary>Chave do Google em Configurações → Integrações, ou null (aí tudo vem do OpenStreetMap/Valhalla).</summary>
    private string? ChaveGoogle => store.Dados.Config.ChaveGoogleMaps is { Length: > 0 } c ? c.Trim() : null;

    public bool UsaGoogle => ChaveGoogle is not null;

    /// <summary>Último erro do Google (chave inválida, API desativada, cota do dia): a busca e a rota seguiram sem ele.</summary>
    public string? ErroGoogle { get; private set; }

    // Teto de rotas pedidas por destino (prévia + recálculos): um defeito em loop não gasta a cota do Google
    private const int MaximoRotasPorViagem = 25;
    private int _rotasPedidas;

    // ===== Busca =====

    private const double RaioBuscaGraus = 3; // ~300 km em volta da posição

    /// <summary>
    /// Lugares para o texto digitado, em volta da posição (~300 km). Duas buscas juntas, intercaladas:
    /// "perto de você" (ex.: a Avenida Brasil do bairro) e "mais relevante" (ex.: a Avenida Paulista da capital,
    /// que a preferência por perto escondia). Achando pouco, amplia para o Brasil inteiro.
    /// </summary>
    public async Task<List<LugarBusca>> Buscar(string texto, PosicaoCarro? perto, CancellationToken ct)
    {
        if (ChaveGoogle is { } chave)
        {
            try
            {
                // Uma "sessão" do Google = as sugestões enquanto digita + a escolhida (cobradas juntas)
                _sessaoBusca ??= Guid.NewGuid().ToString();
                var sugestoes = await _google.Autocompletar(chave, texto, perto, _sessaoBusca, ct);
                ErroGoogle = null;
                return sugestoes;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                ErroGoogle = ex.Message; // segue pelo Photon
            }
        }

        var q =$"https://photon.komoot.io/api/?q={Uri.EscapeDataString(texto)}&limit=12";
        var lugares = new List<LugarBusca>();
        if (perto is not null)
        {
            var bbox = $"&bbox={Num(perto.Longitude - RaioBuscaGraus)},{Num(perto.Latitude - RaioBuscaGraus)},{Num(perto.Longitude + RaioBuscaGraus)},{Num(perto.Latitude + RaioBuscaGraus)}";
            var pertoDeVoce = Consultar(q + bbox + $"&lat={Num(perto.Latitude)}&lon={Num(perto.Longitude)}&location_bias_scale=0.1", null, ct);
            var relevantes = Consultar(q + bbox, null, ct);
            var (a, b) = (await pertoDeVoce, await relevantes);
            for (var i = 0; i < Math.Max(a.Count, b.Count); i++)
            {
                if (i < a.Count) Juntar(lugares, a[i]);
                if (i < b.Count) Juntar(lugares, b[i]);
            }
        }
        if (lugares.Count < 3)
            foreach (var l in await Consultar(q, "BR", ct)) Juntar(lugares, l);
        return lugares.Take(10).ToList();
    }

    // O mesmo lugar (ou trechos da mesma rua no mesmo bairro) aparece uma vez só
    private static void Juntar(List<LugarBusca> lugares, LugarBusca novo)
    {
        if (!lugares.Any(l => l.Nome == novo.Nome && (l.Detalhe == novo.Detalhe || Distancia(l.Latitude, l.Longitude, novo.Latitude, novo.Longitude) < 200)))
            lugares.Add(novo);
    }

    private async Task<List<LugarBusca>> Consultar(string url, string? pais, CancellationToken ct)
    {
        using var resp = await http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));

        var lugares = new List<LugarBusca>();
        foreach (var f in json.RootElement.GetProperty("features").EnumerateArray())
        {
            var c = f.GetProperty("geometry").GetProperty("coordinates");
            var p = f.GetProperty("properties");
            string? Prop(string nome) => p.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            if (pais is not null && Prop("countrycode") != pais) continue;

            var rua = Prop("street") is { } r ? (Prop("housenumber") is { } n ? $"{r}, {n}" : r) : null;
            var nome = Prop("name") ?? rua;
            if (nome is null) continue;
            double lat = c[1].GetDouble(), lng = c[0].GetDouble();
            var detalhe = string.Join(" · ", new[] { nome == rua ? null : rua, Prop("district"), Prop("city"), Prop("state") }
                .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
            Juntar(lugares, new LugarBusca(nome, detalhe.Length > 0 ? detalhe : null, lat, lng));
        }
        return lugares;
    }

    // ===== Prévia: opções de rota =====

    private PosicaoCarro? _origemPrevia;

    /// <summary>Calcula as opções até o destino e abre a prévia. false = não achou rota (ou sem internet).</summary>
    public async Task<bool> Planejar(LugarBusca destino, PosicaoCarro origem)
    {
        Destino = destino;
        _origemPrevia = _posicao = origem;
        Calculando = true;
        Opcoes = null;
        Versao++;
        Mudou?.Invoke();

        // Sugestão do Google: pede a posição agora (fecha a "sessão" da busca)
        if (destino.PlaceId is not null)
        {
            try
            {
                destino = await _google.Resolver(ChaveGoogle ?? "", destino, _sessaoBusca ?? Guid.NewGuid().ToString());
                Destino = destino;
            }
            catch (Exception ex)
            {
                ErroGoogle = ex.Message;
                Destino = null;
                Calculando = false;
                Mudou?.Invoke();
                return false;
            }
            finally
            {
                _sessaoBusca = null;
            }
        }
        GuardarRecente(destino);
        _rotasPedidas = 0;

        var opcoes = await Calcular(origem, destino, alternativas: 2, previa: true);
        Calculando = false;
        if (opcoes.Count == 0)
        {
            Destino = null;
            Mudou?.Invoke();
            return false;
        }
        Opcoes = opcoes;
        // Já conhecidos (da viagem ou guardados) aparecem na hora; os novos chegam em segundo plano
        foreach (var o in opcoes) o.Radares = NaRota(o, _radaresConhecidos);
        _ = AnexarRadares(opcoes);
        Escolhida = 0;
        Versao++;
        Mudou?.Invoke();
        return true;
    }

    public void Escolher(int indice)
    {
        if (Opcoes is null || indice < 0 || indice >= Opcoes.Count || indice == Escolhida) return;
        Escolhida = indice;
        Versao++;
        Mudou?.Invoke();
    }

    /// <summary>Liga/desliga "evitar pedágios" (fica guardado) e refaz as opções da prévia.</summary>
    public async Task AlternarEvitarPedagios()
    {
        store.Dados.Config.EvitarPedagios = !store.Dados.Config.EvitarPedagios;
        store.Salvar();
        if (Destino is { } destino && _origemPrevia is { } origem && Atual is null) await Planejar(destino, origem);
        else Mudou?.Invoke();
    }

    /// <summary>Começa a navegação pela opção marcada.</summary>
    public async Task Iniciar()
    {
        if (Opcoes is null || Destino is null) return;
        Atual = Opcoes[Escolhida];
        Opcoes = null;
        Progresso = null;
        _avisados.Clear();
        _foraSeguidas = 0;
        _indice = 0;
        _ultimoRecalculo = DateTime.Now;
        Versao++;
        Mudou?.Invoke();
        var pedagios = Atual.Pedagios.Count switch { 0 => "", 1 => " Com 1 pedágio.", var n => $" Com {n} pedágios." };
        // Parado o GPS não manda posição nova: calcula a primeira manobra e o tempo já com a última conhecida
        if (_posicao is { } agora) Atualizar(agora);
        await Falar($"Rota para {Destino.Nome}, {TempoFalado(TimeSpan.FromSeconds(Atual.Duracao))}.{pedagios} {SemPonto(Atual.Passos[0].Instrucao)}.");
    }

    /// <summary>Fecha a prévia ou encerra a navegação.</summary>
    public void Encerrar()
    {
        Destino = null;
        Atual = null;
        Opcoes = null;
        Progresso = null;
        Calculando = false;
        Versao++;
        Mudou?.Invoke();
    }

    // ===== Cálculo: Google (com chave) ou Valhalla =====

    /// <summary>
    /// Rota principal + alternativas, cada uma com limites e praças de pedágio. Com a chave do Google, as rotas vêm dele
    /// (trânsito; na prévia, também o preço do pedágio); se o Google falhar, cai no Valhalla.
    /// </summary>
    private async Task<List<RotaAtiva>> Calcular(PosicaoCarro origem, LugarBusca destino, int alternativas, bool previa)
    {
        if (++_rotasPedidas > MaximoRotasPorViagem) return [];
        if (ChaveGoogle is { } chave)
        {
            try
            {
                var rotas = await _google.Rotas(chave, origem, destino, alternativas > 0, store.Dados.Config.EvitarPedagios, comPedagio: previa);
                ErroGoogle = null;
                if (rotas.Count > 0)
                {
                    await Task.WhenAll(rotas.Select(Detalhar));
                    return rotas;
                }
            }
            catch (Exception ex)
            {
                ErroGoogle = ex.Message;
            }
        }
        return await CalcularValhalla(origem, destino, alternativas);
    }

    // ===== Valhalla =====

    /// <summary>Rota principal + até <paramref name="alternativas"/> alternativas pelo Valhalla.</summary>
    private async Task<List<RotaAtiva>> CalcularValhalla(PosicaoCarro origem, LugarBusca destino, int alternativas)
    {
        var rotas = new List<RotaAtiva>();
        try
        {
            var pedido = new
            {
                locations = new[]
                {
                    new { lat = origem.Latitude, lon = origem.Longitude, heading = origem.VelocidadeKmh > 10 ? origem.Direcao : null },
                    new { lat = destino.Latitude, lon = destino.Longitude, heading = (double?)null },
                },
                costing = "auto",
                costing_options = new { auto = new { exclude_tolls = store.Dados.Config.EvitarPedagios } },
                alternates = alternativas,
                language = "pt-BR",
                units = "kilometers",
            };
            using var resp = await http.PostAsync($"{Valhalla}/route",
                new StringContent(JsonSerializer.Serialize(pedido, OpcoesJson), Encoding.UTF8, "application/json"));
            using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var raiz = json.RootElement;
            if (!raiz.TryGetProperty("trip", out var principal)) return rotas;

            rotas.Add(LerViagem(principal, destino));
            if (raiz.TryGetProperty("alternates", out var alts))
                foreach (var a in alts.EnumerateArray())
                    if (a.TryGetProperty("trip", out var t)) rotas.Add(LerViagem(t, destino));

            // Limites e praças de pedágio de cada opção (em paralelo; se falhar, a rota segue sem eles)
            await Task.WhenAll(rotas.Select(Detalhar));
        }
        catch { }
        return rotas;
    }

    private static readonly JsonSerializerOptions OpcoesJson = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private static RotaAtiva LerViagem(JsonElement viagem, LugarBusca destino)
    {
        var perna = viagem.GetProperty("legs")[0];
        var forma = perna.GetProperty("shape").GetString()!;
        var resumo = viagem.GetProperty("summary");
        var rota = new RotaAtiva(destino, GoogleMaps.Decodificar(forma, 1e6), resumo.GetProperty("length").GetDouble() * 1000, resumo.GetProperty("time").GetDouble())
        {
            Forma = forma,
            TemPedagio = resumo.TryGetProperty("has_toll", out var p) && p.GetBoolean(),
        };
        foreach (var m in perna.GetProperty("maneuvers").EnumerateArray())
        {
            var instrucao = SemPonto(m.GetProperty("instruction").GetString() ?? "Siga em frente");
            var inicio = rota.MetrosAte(m.GetProperty("begin_shape_index").GetInt32());
            rota.Passos.Add(new PassoRota(instrucao, Icone(m.GetProperty("type").GetInt32()), inicio));
        }
        return rota;
    }

    /// <summary>
    /// Pede ao Valhalla os atributos da linha (<c>trace_attributes</c>): o limite de velocidade de cada trecho e os nós
    /// que são praça de pedágio. Se falhar, a rota segue sem eles.
    /// </summary>
    private const double PedacoDetalheMetros = 150_000; // o trace do servidor público tem limite de distância

    private async Task Detalhar(RotaAtiva rota)
    {
        foreach (var (de, ate) in Pedacos(rota)) await DetalharPedaco(rota, de, ate);
    }

    /// <summary>Índices [de, ate] da linha em pedaços de até ~150 km.</summary>
    private static IEnumerable<(int De, int Ate)> Pedacos(RotaAtiva rota)
    {
        var fim = rota.Coordenadas.Length - 1;
        var inicio = 0;
        for (var i = 1; i <= fim; i++)
        {
            if (i == fim || rota.MetrosAte(i) - rota.MetrosAte(inicio) >= PedacoDetalheMetros)
            {
                yield return (inicio, i);
                inicio = i;
            }
        }
    }

    /// <summary>
    /// Um pedaço da linha no <c>trace_attributes</c> do Valhalla. A rota do próprio Valhalla casa direto com o mapa dele
    /// (<c>edge_walk</c>); a do Google é encaixada (<c>map_snap</c>). Os trechos voltam numa linha própria (<c>shape</c>),
    /// e cada ponto dela é projetado na rota para saber os metros desde o começo.
    /// </summary>
    private async Task DetalharPedaco(RotaAtiva rota, int de, int ate)
    {
        try
        {
            var pontos = rota.Coordenadas[de..(ate + 1)];
            var pedido = new
            {
                encoded_polyline = GoogleMaps.Codificar(pontos, 1e6),
                shape_match = rota.DoGoogle ? "map_snap" : "edge_walk",
                costing = "auto",
                filters = new
                {
                    attributes = new[] { "shape", "edge.begin_shape_index", "edge.end_shape_index", "edge.speed_limit", "edge.names", "node.type" },
                    action = "include",
                },
            };
            using var resp = await http.PostAsync($"{Valhalla}/trace_attributes",
                new StringContent(JsonSerializer.Serialize(pedido), Encoding.UTF8, "application/json"));
            using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (!json.RootElement.TryGetProperty("edges", out var arestas)) return;
            var forma = json.RootElement.TryGetProperty("shape", out var s) && s.GetString() is { Length: > 0 } f ? GoogleMaps.Decodificar(f, 1e6) : pontos;

            // Projeta sempre para frente, perto do último ponto achado (a linha é percorrida em ordem)
            var janela = de;
            double Metros(int indice)
            {
                var p = forma[Math.Clamp(indice, 0, forma.Length - 1)];
                var (ao, idx, _) = rota.Projetar(p[1], p[0], Math.Max(0, janela - 5), janela + 400);
                janela = idx;
                return ao;
            }

            foreach (var a in arestas.EnumerateArray())
            {
                var iDe = a.TryGetProperty("begin_shape_index", out var b) ? b.GetInt32() : 0;
                var iAte = a.TryGetProperty("end_shape_index", out var e) ? e.GetInt32() : iDe;
                var metrosDe = Metros(iDe);
                var metrosAte = Metros(iAte);

                // Limite: 0 = desconhecido; trechos seguidos com o mesmo limite viram um só
                if (a.TryGetProperty("speed_limit", out var sl) && sl.ValueKind == JsonValueKind.Number && sl.GetInt32() is var kmh and > 0 and < 200 && metrosAte > metrosDe)
                {
                    if (rota.Limites.Count > 0 && rota.Limites[^1] is var ultimo && ultimo.Kmh == kmh && metrosDe - ultimo.Fim < 30)
                        rota.Limites[^1] = ultimo with { Fim = metrosAte };
                    else rota.Limites.Add(new TrechoLimite(metrosDe, metrosAte, kmh));
                }

                if (!a.TryGetProperty("end_node", out var no) || !no.TryGetProperty("type", out var tipo) || tipo.GetString() != "toll_booth") continue;
                var ponto = forma[Math.Clamp(iAte, 0, forma.Length - 1)];
                string? via = a.TryGetProperty("names", out var nomes) && nomes.GetArrayLength() > 0 ? nomes[0].GetString() : null;
                // Duas cabines da mesma praça (pistas separadas) contam uma vez só
                if (rota.Pedagios.Any(p => Math.Abs(p.Inicio - metrosAte) < 500)) continue;
                rota.Pedagios.Add(new PedagioRota(via, ponto[1], ponto[0], metrosAte));
            }
        }
        catch { }
    }

    // ===== Radares (OpenStreetMap, pelo Overpass; ficam guardados no aparelho) =====

    private readonly RadaresOsm _radares = new(http);
    private List<RadarRota> _radaresConhecidos = [];

    /// <summary>Busca os radares em volta das rotas sem travar a tela; quando chegam, cada rota ganha os seus.</summary>
    private async Task AnexarRadares(IReadOnlyList<RotaAtiva> rotas)
    {
        var radares = await _radares.Perto(rotas);
        _radaresConhecidos = _radaresConhecidos.Concat(radares).DistinctBy(r => r.Id).ToList();
        foreach (var rota in rotas) rota.Radares = NaRota(rota, _radaresConhecidos);
        Versao++;
        Mudou?.Invoke();
    }

    /// <summary>Radares em cima da linha da rota (até 25 m dela), com a posição em metros desde o começo.</summary>
    private static List<RadarRota> NaRota(RotaAtiva rota, IEnumerable<RadarRota> radares)
    {
        var lista = new List<RadarRota>();
        foreach (var r in radares)
        {
            var (ao, _, distancia) = rota.Projetar(r.Latitude, r.Longitude, 0, int.MaxValue);
            if (distancia > 25 || lista.Any(x => Math.Abs(x.Inicio - ao) < 60)) continue;
            lista.Add(r with { Inicio = ao });
        }
        return lista.OrderBy(r => r.Inicio).ToList();
    }

    /// <summary>Seta da manobra a partir do tipo do Valhalla (enum <c>Maneuver::Type</c>).</summary>
    private static string Icone(int tipo) => tipo switch
    {
        4 or 5 or 6 => "chegada",
        9 or 18 or 20 or 23 or 37 => "leve-direita",
        10 or 11 or 2 => "direita",
        12 or 13 => "retorno",
        14 or 15 or 3 => "esquerda",
        16 or 19 or 21 or 24 or 38 => "leve-esquerda",
        26 or 27 => "rotatoria",
        _ => "frente",
    };

    // ===== Andamento =====

    private DateTime _ultimoRecalculo;
    private int _foraSeguidas, _indice;
    private PosicaoCarro? _posicao; // última posição recebida (para calcular na hora ao iniciar ou recalcular)

    /// <summary>Nova posição do GPS: atualiza a próxima manobra, o tempo de chegada, a voz e recalcula se saiu da rota.</summary>
    public void Atualizar(PosicaoCarro p)
    {
        _posicao = p;
        if (Atual is not { } rota || Calculando) return;

        var (ao, indice, distanciaDaLinha) = rota.Projetar(p.Latitude, p.Longitude, Math.Max(0, _indice - 5), _indice + 300);
        if (distanciaDaLinha > ForaDaRotaMetros)
        {
            // Pode ter pulado um pedaço (túnel, GPS ruim): procura na rota inteira antes de dizer que saiu
            var inteira = rota.Projetar(p.Latitude, p.Longitude, 0, int.MaxValue);
            if (inteira.Distancia < distanciaDaLinha) (ao, indice, distanciaDaLinha) = inteira;
        }
        _indice = indice;

        var fora = distanciaDaLinha > ForaDaRotaMetros;
        _foraSeguidas = fora ? _foraSeguidas + 1 : 0;

        var restante = Math.Max(0, rota.Distancia - ao);
        var chegou = restante < ChegadaMetros || Distancia(p.Latitude, p.Longitude, rota.Destino.Latitude, rota.Destino.Longitude) < ChegadaMetros;
        var proximo = rota.Passos.FirstOrDefault(s => s.Inicio > ao + 5) ?? rota.Passos[^1];
        var tempo = TimeSpan.FromSeconds(rota.Duracao * (restante / Math.Max(1, rota.Distancia)));
        var pedagio = rota.Pedagios.FirstOrDefault(x => x.Inicio > ao);
        var radar = rota.Radares.FirstOrDefault(x => x.Inicio > ao);
        var trecho = rota.TrechoEm(ao);
        Progresso = new ProgressoRota(proximo, Math.Max(0, proximo.Inicio - ao), restante, tempo, DateTime.Now + tempo, chegou, fora,
            pedagio is null ? null : pedagio.Inicio - ao, trecho?.Kmh, radar, radar is null ? null : radar.Inicio - ao);

        // Acima do limite por 3 s seguidos (com 7 km/h de folga): avisa uma vez por trecho
        var acima = trecho is not null && p.VelocidadeKmh > trecho.Kmh + 7;
        _acimaDesde = acima ? _acimaDesde ?? DateTime.Now : null;
        Mudou?.Invoke();

        _ = AvisarPorVoz(Progresso, p, pedagio?.Inicio,
            acima && DateTime.Now - _acimaDesde >= TimeSpan.FromSeconds(3) ? trecho : null);

        // Fora da rota por 3 leituras seguidas (uns 3 s): recalcula a partir de onde está
        if (_foraSeguidas >= 3 && !chegou && DateTime.Now - _ultimoRecalculo > EsperaRecalculo)
        {
            _foraSeguidas = 0;
            _ = Recalcular(p);
        }
    }

    private async Task Recalcular(PosicaoCarro p)
    {
        if (Atual is not { } atual) return;
        _ultimoRecalculo = DateTime.Now;
        Calculando = true;
        Mudou?.Invoke();
        var nova = (await Calcular(p, atual.Destino, alternativas: 0, previa: false)).FirstOrDefault();
        Calculando = false;
        if (nova is null || Atual is null) // sem internet, ou encerrou no meio: segue com a rota antiga
        {
            Mudou?.Invoke();
            return;
        }
        nova.Radares = NaRota(nova, _radaresConhecidos);
        _ = AnexarRadares([nova]);
        Atual = nova;
        Progresso = null;
        _avisados.Clear();
        _indice = 0;
        Versao++;
        Mudou?.Invoke();
        if (_posicao is { } agora) Atualizar(agora);
        await Falar("Rota recalculada.");
    }

    // ===== Voz =====

    // Avisos já falados: por manobra "longe" (ao virar a próxima) e "perto" (logo antes); por praça de pedágio, radar e trecho com limite
    private readonly HashSet<string> _avisados = [];
    private DateTime? _acimaDesde;
    private bool _falando;

    /// <summary>
    /// Um aviso por vez (a cada leitura do GPS). Ordem: chegada, radar, pedágio, limite, manobra.
    /// Enquanto fala, os outros esperam a próxima leitura (as condições continuam valendo).
    /// </summary>
    private async Task AvisarPorVoz(ProgressoRota pr, PosicaoCarro p, double? pedagioInicio, TrechoLimite? acimaDoLimite)
    {
        if (!store.Dados.Config.VozModoCarro || _falando) return;
        if (pr.Chegou)
        {
            if (_avisados.Add("chegada")) await Falar("Você chegou ao destino.");
            return;
        }

        if (pr.Radar is { } radar && pr.RadarEm is { } rad && rad is > 80 and <= 500 && _avisados.Add($"radar{radar.Id}"))
        {
            await Falar($"Radar em {DistanciaFalada(rad)}" + (radar.Kmh is { } k ? $", {k} quilômetros por hora." : "."));
            return;
        }

        if (pr.PedagioEm is { } ped && ped is > 300 and <= 1500 && _avisados.Add($"pedagio{pedagioInicio:F0}"))
        {
            await Falar($"Pedágio em {DistanciaFalada(ped)}.");
            return;
        }

        if (acimaDoLimite is not null && _avisados.Add($"limite{acimaDoLimite.Inicio:F0}"))
        {
            await Falar($"Atenção, o limite aqui é {acimaDoLimite.Kmh} quilômetros por hora.");
            return;
        }

        var chave = $"{pr.Proximo.Inicio:F0}";
        var instrucao = char.ToLower(pr.Proximo.Instrucao[0], PtBr) + pr.Proximo.Instrucao[1..];
        // "Perto": uns 8 s antes na velocidade atual, nunca menos de 120 m
        var perto = Math.Max(120, p.VelocidadeKmh / 3.6 * 8);
        if (pr.DistanciaProximo <= perto)
        {
            if (_avisados.Add(chave + "perto"))
            {
                _avisados.Add(chave + "longe");
                await Falar(pr.DistanciaProximo < 40 ? $"Agora, {instrucao}." : $"Em {DistanciaFalada(pr.DistanciaProximo)}, {instrucao}.");
            }
        }
        else if (pr.DistanciaProximo <= 2000 && _avisados.Add(chave + "longe"))
        {
            await Falar($"Em {DistanciaFalada(pr.DistanciaProximo)}, {instrucao}.");
        }
    }

    /// <summary>Fala só com a voz ligada em Configurações → Aplicativo → Modo Carro.</summary>
    private async Task Falar(string texto)
    {
        if (!store.Dados.Config.VozModoCarro) return;
        _falando = true;
        try { await modo.Falar(texto); }
        finally { _falando = false; }
    }

    // ===== Recentes =====

    public IReadOnlyList<LugarBusca> Recentes => store.Dados.Config.DestinosRecentes;

    private void GuardarRecente(LugarBusca lugar)
    {
        var lista = store.Dados.Config.DestinosRecentes;
        lista.RemoveAll(l => l.Nome == lugar.Nome && Distancia(l.Latitude, l.Longitude, lugar.Latitude, lugar.Longitude) < 50);
        lista.Insert(0, lugar);
        if (lista.Count > 6) lista.RemoveRange(6, lista.Count - 6);
        store.Salvar();
    }

    // ===== Textos =====

    /// <summary>Distância para a tela: "80 m", "350 m", "1,2 km", "15 km".</summary>
    public static string TextoDistancia(double metros) => metros switch
    {
        < 100 => $"{Math.Round(metros / 10) * 10:F0} m",
        < 1000 => $"{Math.Round(metros / 50) * 50:F0} m",
        < 10000 => (metros / 1000).ToString("0.0", PtBr) + " km",
        _ => $"{metros / 1000:F0} km",
    };

    /// <summary>Duração para a tela: "25 min", "2h05".</summary>
    public static string TextoTempo(TimeSpan t) =>
        t.TotalMinutes >= 60 ? $"{(int)t.TotalHours}h{t.Minutes:00}" : $"{Math.Max(1, Math.Round(t.TotalMinutes)):F0} min";

    private static string TempoFalado(TimeSpan t)
    {
        var min = (int)Math.Max(1, Math.Round(t.TotalMinutes));
        if (min < 60) return min == 1 ? "1 minuto" : $"{min} minutos";
        var (h, m) = (min / 60, min % 60);
        var horas = h == 1 ? "1 hora" : $"{h} horas";
        return m == 0 ? horas : $"{horas} e {m} minutos";
    }

    /// <summary>Distância para a voz: "200 metros", "1 quilômetro", "1,5 quilômetros".</summary>
    private static string DistanciaFalada(double metros)
    {
        if (metros < 950) return $"{Math.Max(50, Math.Round(metros / 50) * 50):F0} metros";
        var km = Math.Round(metros / 100) / 10;
        return km == 1 ? "1 quilômetro" : km.ToString("0.#", PtBr) + " quilômetros";
    }

    private static string SemPonto(string s) => s.TrimEnd().TrimEnd('.');

    private static string Num(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    /// <summary>Distância em metros entre dois pontos (fórmula de haversine).</summary>
    public static double Distancia(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000;
        double Rad(double g) => g * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }
}

/// <summary>Uma rota calculada: a linha (para o mapa e para medir o andamento), as manobras e as praças de pedágio.</summary>
public sealed class RotaAtiva
{
    public RotaAtiva(LugarBusca destino, double[][] coordenadas, double distancia, double duracao)
    {
        Destino = destino;
        Coordenadas = coordenadas;
        Distancia = distancia;
        Duracao = duracao;
        // Metros acumulados até cada ponto da linha
        _acumulado = new double[coordenadas.Length];
        for (var i = 1; i < coordenadas.Length; i++)
            _acumulado[i] = _acumulado[i - 1] + RotaService.Distancia(coordenadas[i - 1][1], coordenadas[i - 1][0], coordenadas[i][1], coordenadas[i][0]);
    }

    public LugarBusca Destino { get; }

    /// <summary>Pontos da linha como [longitude, latitude] (o formato do mapa).</summary>
    public double[][] Coordenadas { get; }

    /// <summary>A linha no formato do Valhalla (para pedir as praças de pedágio).</summary>
    public string Forma { get; init; } = "";

    /// <summary>Metros e segundos da rota inteira.</summary>
    public double Distancia { get; }

    public double Duracao { get; }

    /// <summary>A rota passa por via com pedágio (mesmo que a praça não tenha sido achada).</summary>
    public bool TemPedagio { get; init; }

    /// <summary>Rota calculada pelo Google (com trânsito); senão, pelo Valhalla.</summary>
    public bool DoGoogle { get; init; }

    /// <summary>Preço estimado do pedágio na rota inteira ("R$ 32,40"), quando o Google informa.</summary>
    public string? PrecoPedagio { get; init; }

    /// <summary>Segundos a mais por causa do trânsito agora (só nas rotas do Google).</summary>
    public double Transito { get; init; }

    public List<PassoRota> Passos { get; } = [];

    public List<PedagioRota> Pedagios { get; } = [];

    /// <summary>Trechos com limite de velocidade conhecido, em ordem.</summary>
    public List<TrechoLimite> Limites { get; } = [];

    /// <summary>Radares em cima da linha, em ordem (chegam depois, em segundo plano).</summary>
    public List<RadarRota> Radares { get; set; } = [];

    /// <summary>Trecho com limite onde fica o ponto <paramref name="ao"/> (metros desde o começo), ou null.</summary>
    public TrechoLimite? TrechoEm(double ao)
    {
        int de = 0, ate = Limites.Count - 1;
        while (de <= ate)
        {
            var meio = (de + ate) / 2;
            var t = Limites[meio];
            if (ao < t.Inicio) ate = meio - 1;
            else if (ao > t.Fim) de = meio + 1;
            else return t;
        }
        return null;
    }

    private readonly double[] _acumulado;

    /// <summary>Metros desde o começo da rota até o ponto <paramref name="indice"/> da linha.</summary>
    public double MetrosAte(int indice) => _acumulado.Length == 0 ? 0 : _acumulado[Math.Clamp(indice, 0, _acumulado.Length - 1)];

    /// <summary>
    /// Ponto da linha mais perto da posição, procurando entre os segmentos [de, ate]: devolve os metros desde o começo da rota,
    /// o índice do segmento e a distância (em metros) da posição até a linha.
    /// </summary>
    public (double Ao, int Indice, double Distancia) Projetar(double lat, double lng, int de, int ate)
    {
        if (Coordenadas.Length < 2)
            return (0, 0, Coordenadas.Length == 0 ? double.MaxValue : RotaService.Distancia(lat, lng, Coordenadas[0][1], Coordenadas[0][0]));

        var melhor = (Ao: 0.0, Indice: 0, Distancia: double.MaxValue);
        var fim = Math.Min(Coordenadas.Length - 2, ate);
        // Plano local em metros em volta da posição (preciso o bastante para alguns km)
        var kx = 111320 * Math.Cos(lat * Math.PI / 180);
        const double ky = 110540;
        for (var i = Math.Max(0, de); i <= fim; i++)
        {
            double ax = (Coordenadas[i][0] - lng) * kx, ay = (Coordenadas[i][1] - lat) * ky;
            double bx = (Coordenadas[i + 1][0] - lng) * kx, by = (Coordenadas[i + 1][1] - lat) * ky;
            double dx = bx - ax, dy = by - ay;
            var len2 = dx * dx + dy * dy;
            var t = len2 == 0 ? 0 : Math.Clamp(-(ax * dx + ay * dy) / len2, 0, 1);
            double px = ax + t * dx, py = ay + t * dy;
            var d = Math.Sqrt(px * px + py * py);
            if (d < melhor.Distancia) melhor = (_acumulado[i] + t * (_acumulado[i + 1] - _acumulado[i]), i, d);
        }
        return melhor;
    }
}
