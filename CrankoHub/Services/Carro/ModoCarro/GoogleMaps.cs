using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Google Maps Platform para o Modo Carro (só com a chave em Configurações → Integrações; ela fica no aparelho):
/// busca pelo Places API (New) e rotas pelo Routes API, com trânsito, alternativas e pedágio.
/// Cada chamada é cobrada pelo Google acima da cota grátis do mês: a busca só roda depois de uma pausa na digitação
/// e o <see cref="RotaService"/> limita os recálculos.
/// </summary>
public sealed class GoogleMaps(HttpClient http)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Erro do Google (chave inválida, API não ativada, cota do dia esgotada…), com a mensagem dele.</summary>
    public sealed class Erro(string mensagem) : Exception(mensagem);

    // ===== Busca (Places API New) =====

    /// <summary>
    /// Sugestões para o texto digitado (bairros, endereços com número, estabelecimentos), perto da posição.
    /// Vêm sem coordenada (<see cref="LugarBusca.PlaceId"/>): a posição é pedida só para a escolhida (<see cref="Resolver"/>).
    /// </summary>
    public async Task<List<LugarBusca>> Autocompletar(string chave, string texto, PosicaoCarro? perto, string sessao, CancellationToken ct)
    {
        var corpo = new Dictionary<string, object>
        {
            ["input"] = texto,
            ["languageCode"] = "pt-BR",
            ["regionCode"] = "br",
            ["includedRegionCodes"] = new[] { "br" },
            ["sessionToken"] = sessao,
        };
        if (perto is not null)
        {
            var ponto = new { latitude = perto.Latitude, longitude = perto.Longitude };
            corpo["locationBias"] = new { circle = new { center = ponto, radius = 50000.0 } };
            corpo["origin"] = ponto; // devolve a distância até cada sugestão
        }

        using var json = await Enviar(HttpMethod.Post, "https://places.googleapis.com/v1/places:autocomplete", chave, corpo, null, ct);
        var lugares = new List<LugarBusca>();
        if (!json.RootElement.TryGetProperty("suggestions", out var sugestoes)) return lugares;
        foreach (var s in sugestoes.EnumerateArray())
        {
            if (!s.TryGetProperty("placePrediction", out var p)) continue;
            var id = p.GetProperty("placeId").GetString()!;
            var formato = p.TryGetProperty("structuredFormat", out var f) ? f : default;
            var nome = Texto(formato, "mainText") ?? Texto(p, "text") ?? "";
            var detalhe = Texto(formato, "secondaryText");
            double? metros = p.TryGetProperty("distanceMeters", out var d) ? d.GetDouble() : null;
            lugares.Add(new LugarBusca(nome, detalhe, 0, 0) { PlaceId = id, Metros = metros });
        }
        return lugares;

        static string? Texto(JsonElement e, string nome) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v) && v.TryGetProperty("text", out var t) ? t.GetString() : null;
    }

    /// <summary>Posição da sugestão escolhida (Place Details, só os campos da faixa mais barata: location).</summary>
    public async Task<LugarBusca> Resolver(string chave, LugarBusca lugar, string sessao)
    {
        var url = $"https://places.googleapis.com/v1/places/{Uri.EscapeDataString(lugar.PlaceId!)}?languageCode=pt-BR&regionCode=BR&sessionToken={Uri.EscapeDataString(sessao)}";
        using var json = await Enviar(HttpMethod.Get, url, chave, null, "location", default);
        var loc = json.RootElement.GetProperty("location");
        return lugar with { Latitude = loc.GetProperty("latitude").GetDouble(), Longitude = loc.GetProperty("longitude").GetDouble(), PlaceId = null, Metros = null };
    }

    // ===== Rotas (Routes API) =====

    /// <summary>
    /// Rotas com trânsito (TRAFFIC_AWARE). <paramref name="comPedagio"/> pede o preço estimado do pedágio
    /// (faixa Enterprise do Google, cota grátis menor): só na prévia, não nos recálculos.
    /// </summary>
    public async Task<List<RotaAtiva>> Rotas(string chave, PosicaoCarro origem, LugarBusca destino, bool alternativas, bool evitarPedagios, bool comPedagio)
    {
        var de = new Dictionary<string, object> { ["location"] = new { latLng = new { latitude = origem.Latitude, longitude = origem.Longitude } } };
        // Com o carro andando, a direção evita rota que começa com retorno
        if (origem.VelocidadeKmh > 10 && origem.Direcao is { } dir)
            de["location"] = new { latLng = new { latitude = origem.Latitude, longitude = origem.Longitude }, heading = (int)Math.Round(dir) % 360 };

        var corpo = new Dictionary<string, object>
        {
            ["origin"] = de,
            ["destination"] = new { location = new { latLng = new { latitude = destino.Latitude, longitude = destino.Longitude } } },
            ["travelMode"] = "DRIVE",
            ["routingPreference"] = "TRAFFIC_AWARE",
            ["computeAlternativeRoutes"] = alternativas,
            ["routeModifiers"] = new { avoidTolls = evitarPedagios },
            ["languageCode"] = "pt-BR",
            ["regionCode"] = "BR",
            ["units"] = "METRIC",
        };
        if (comPedagio) corpo["extraComputations"] = new[] { "TOLLS" };

        const string campos = "routes.duration,routes.staticDuration,routes.distanceMeters,routes.polyline.encodedPolyline," +
                              "routes.legs.steps.startLocation,routes.legs.steps.navigationInstruction,routes.travelAdvisory.tollInfo";
        using var json = await Enviar(HttpMethod.Post, "https://routes.googleapis.com/directions/v2:computeRoutes", chave, corpo, campos, default);

        var rotas = new List<RotaAtiva>();
        if (!json.RootElement.TryGetProperty("routes", out var lista)) return rotas;
        foreach (var r in lista.EnumerateArray())
        {
            var coords = Decodificar(r.GetProperty("polyline").GetProperty("encodedPolyline").GetString()!, 1e5);
            var duracao = Segundos(r, "duration");
            var semTransito = Segundos(r, "staticDuration");
            var pedagio = r.TryGetProperty("travelAdvisory", out var adv) && adv.TryGetProperty("tollInfo", out var toll) ? toll : default;
            var rota = new RotaAtiva(destino, coords, r.TryGetProperty("distanceMeters", out var dm) ? dm.GetDouble() : 0, duracao)
            {
                DoGoogle = true,
                TemPedagio = pedagio.ValueKind == JsonValueKind.Object,
                PrecoPedagio = Preco(pedagio),
                Transito = semTransito > 0 ? Math.Max(0, duracao - semTransito) : 0,
            };

            // A instrução do passo vale para o começo dele: a manobra fica na posição do startLocation
            var desde = 0;
            foreach (var passo in r.GetProperty("legs")[0].GetProperty("steps").EnumerateArray())
            {
                if (!passo.TryGetProperty("navigationInstruction", out var ni)) continue;
                var texto = ni.TryGetProperty("instructions", out var t) ? t.GetString() : null;
                if (string.IsNullOrWhiteSpace(texto)) continue;
                var ll = passo.GetProperty("startLocation").GetProperty("latLng");
                var (inicio, indice, _) = rota.Projetar(ll.GetProperty("latitude").GetDouble(), ll.GetProperty("longitude").GetDouble(), desde, int.MaxValue);
                desde = indice;
                // "Vire à direita na Rua X\nO destino estará à esquerda" → uma linha só
                var instrucao = string.Join(" · ", texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).TrimEnd('.');
                rota.Passos.Add(new PassoRota(instrucao, Icone(ni.TryGetProperty("maneuver", out var m) ? m.GetString() : null), inicio));
            }
            rota.Passos.Add(new PassoRota("Você chegou ao destino", "chegada", rota.Distancia));
            rotas.Add(rota);
        }
        return rotas;
    }

    private static double Segundos(JsonElement r, string nome) =>
        r.TryGetProperty(nome, out var v) && double.TryParse(v.GetString()?.TrimEnd('s'), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : 0;

    /// <summary>Soma do preço estimado ("R$ 32,40"), quando o Google informa.</summary>
    private static string? Preco(JsonElement pedagio)
    {
        if (pedagio.ValueKind != JsonValueKind.Object || !pedagio.TryGetProperty("estimatedPrice", out var precos) || precos.GetArrayLength() == 0) return null;
        decimal total = 0;
        foreach (var p in precos.EnumerateArray())
        {
            var unidades = p.TryGetProperty("units", out var u) && decimal.TryParse(u.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ? x : 0;
            var nanos = p.TryGetProperty("nanos", out var n) ? n.GetInt64() : 0;
            total += unidades + nanos / 1_000_000_000m;
        }
        return total > 0 ? total.ToString("C", PtBr) : null;
    }

    /// <summary>Seta da manobra a partir do enum <c>Maneuver</c> do Routes API.</summary>
    private static string Icone(string? manobra) => manobra switch
    {
        "TURN_LEFT" or "TURN_SHARP_LEFT" => "esquerda",
        "TURN_RIGHT" or "TURN_SHARP_RIGHT" => "direita",
        "TURN_SLIGHT_LEFT" or "RAMP_LEFT" or "FORK_LEFT" => "leve-esquerda",
        "TURN_SLIGHT_RIGHT" or "RAMP_RIGHT" or "FORK_RIGHT" or "MERGE" => "leve-direita",
        "UTURN_LEFT" or "UTURN_RIGHT" => "retorno",
        "ROUNDABOUT_LEFT" or "ROUNDABOUT_RIGHT" => "rotatoria",
        _ => "frente",
    };

    // ===== Comum =====

    private async Task<JsonDocument> Enviar(HttpMethod metodo, string url, string chave, object? corpo, string? campos, CancellationToken ct)
    {
        using var pedido = new HttpRequestMessage(metodo, url);
        pedido.Headers.Add("X-Goog-Api-Key", chave.Trim());
        if (campos is not null) pedido.Headers.Add("X-Goog-FieldMask", campos);
        if (corpo is not null) pedido.Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json");
        using var resp = await http.SendAsync(pedido, ct);
        var texto = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            // {"error": {"code": 403, "message": "...", "status": "PERMISSION_DENIED"}}
            string? mensagem = null;
            try
            {
                using var erro = JsonDocument.Parse(texto);
                mensagem = erro.RootElement.GetProperty("error").GetProperty("message").GetString();
            }
            catch { }
            throw new Erro($"Google Maps ({(int)resp.StatusCode}): {mensagem ?? resp.ReasonPhrase}");
        }
        return JsonDocument.Parse(texto);
    }

    /// <summary>Linha codificada (polyline do Google, precisão 1e5; do Valhalla, 1e6) → [longitude, latitude].</summary>
    public static double[][] Decodificar(string s, double escala)
    {
        var pontos = new List<double[]>();
        int i = 0, lat = 0, lng = 0;
        while (i < s.Length)
        {
            lat += Proximo();
            lng += Proximo();
            pontos.Add([lng / escala, lat / escala]);
        }
        return pontos.ToArray();

        int Proximo()
        {
            int resultado = 0, deslocamento = 0, b;
            do
            {
                b = s[i++] - 63;
                resultado |= (b & 0x1f) << deslocamento;
                deslocamento += 5;
            } while (b >= 0x20);
            return (resultado & 1) != 0 ? ~(resultado >> 1) : resultado >> 1;
        }
    }

    /// <summary>[longitude, latitude] → linha codificada (para mandar uma linha do Google ao Valhalla, precisão 1e6).</summary>
    public static string Codificar(IEnumerable<double[]> pontos, double escala)
    {
        var sb = new StringBuilder();
        long antLat = 0, antLng = 0;
        foreach (var p in pontos)
        {
            long lat = (long)Math.Round(p[1] * escala), lng = (long)Math.Round(p[0] * escala);
            Escrever(lat - antLat);
            Escrever(lng - antLng);
            (antLat, antLng) = (lat, lng);
        }
        return sb.ToString();

        void Escrever(long v)
        {
            var x = v < 0 ? ~(v << 1) : v << 1;
            while (x >= 0x20)
            {
                sb.Append((char)((0x20 | (x & 0x1f)) + 63));
                x >>= 5;
            }
            sb.Append((char)(x + 63));
        }
    }
}
