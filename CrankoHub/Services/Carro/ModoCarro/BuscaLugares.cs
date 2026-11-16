using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Busca de destino do Modo Carro pelo Photon (komoot, OpenStreetMap; sem chave) e os destinos recentes.
/// A rota em si fica com o Google Maps ou o Waze (<see cref="IModoCarroService.Navegar"/>).
/// </summary>
public sealed class BuscaLugares(HttpClient http, ArmazenamentoService store)
{
    private const double RaioGraus = 3; // ~300 km em volta da posição
    private const int MaximoRecentes = 6;

    public IReadOnlyList<LugarBusca> Recentes => store.Dados.Config.DestinosRecentes;

    /// <summary>
    /// Lugares para o texto digitado, em volta da posição (~300 km). Duas buscas juntas, intercaladas:
    /// "perto de você" (ex.: a Avenida Brasil do bairro) e "mais relevante" (ex.: a Avenida Paulista da capital,
    /// que a preferência por perto escondia). Achando pouco (ou sem posição), procura no Brasil inteiro.
    /// </summary>
    public async Task<List<LugarBusca>> Buscar(string texto, PosicaoCarro? perto, CancellationToken ct)
    {
        var q = $"https://photon.komoot.io/api/?q={Uri.EscapeDataString(texto)}&limit=12";
        var lugares = new List<LugarBusca>();
        if (perto is not null)
        {
            var bbox = $"&bbox={Num(perto.Longitude - RaioGraus)},{Num(perto.Latitude - RaioGraus)},{Num(perto.Longitude + RaioGraus)},{Num(perto.Latitude + RaioGraus)}";
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

    /// <summary>Põe o lugar no topo dos recentes (sem repetir).</summary>
    public void GuardarRecente(LugarBusca lugar)
    {
        var lista = store.Dados.Config.DestinosRecentes;
        lista.RemoveAll(l => l.Nome == lugar.Nome && Distancia(l.Latitude, l.Longitude, lugar.Latitude, lugar.Longitude) < 50);
        lista.Insert(0, lugar);
        if (lista.Count > MaximoRecentes) lista.RemoveRange(MaximoRecentes, lista.Count - MaximoRecentes);
        store.Salvar();
    }

    public void EsquecerRecente(LugarBusca lugar)
    {
        if (store.Dados.Config.DestinosRecentes.Remove(lugar)) store.Salvar();
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

    private static string Num(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    /// <summary>Distância em metros entre dois pontos (fórmula de haversine).</summary>
    private static double Distancia(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000;
        double Rad(double g) => g * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }
}
