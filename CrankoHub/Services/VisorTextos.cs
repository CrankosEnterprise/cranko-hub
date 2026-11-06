using System.Globalization;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// O que vai para o visor da central. Sem plataforma (testável): o VisorCarroService do Android só manda.
/// A rota vem do <see cref="RotaService"/>, igual com o Google ou com o OpenStreetMap.
/// <para>
/// A Pioneer mostra 4 linhas: título (nota), artista (pessoa), álbum (disco) e gênero (guitarra). O Spotify não manda o gênero
/// ("No data"); aqui ele leva os avisos da rota.
/// </para>
/// </summary>
public static class VisorTextos
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public const string Marca = "CrankoHub";

    /// <summary>Radar e pedágio aparecem no visor a partir dessa distância.</summary>
    public const double RadarPerto = 1000, PedagioPerto = 2000;

    /// <summary>A rota iniciada; sem rota, uma de teste (para conferir parado).</summary>
    /// <param name="tique">Conta de 5 em 5 s (anda a rota de teste).</param>
    public static TextosVisor Montar(int tique, ProgressoRota? progresso, bool calculando, MusicaAtual? musica, DateTime agora) =>
        progresso is not null ? DaRota(progresso, calculando, musica) : Simulada(tique, musica, agora);

    /// <summary>
    /// Navegando: título = próxima curva. Com música tocando: artista = música, álbum = chegada, gênero = avisos.
    /// Sem música (ou pausada): artista = chegada, álbum = avisos, gênero = CrankoHub.
    /// </summary>
    public static TextosVisor DaRota(ProgressoRota p, bool calculando, MusicaAtual? musica)
    {
        var titulo = p.Chegou ? "Você chegou ao destino"
            : calculando || p.ForaDaRota ? "Recalculando a rota"
            : $"{RotaService.TextoDistancia(p.DistanciaProximo)} · {p.Proximo.Instrucao}";
        var chegada = $"Chegada {p.Chegada:HH:mm} · {RotaService.TextoDistancia(p.Restante)}";
        var avisos = Avisos(p);
        return Musica(musica) is { } m ? new(titulo, m, chegada, avisos) : new(titulo, chegada, avisos, Marca);
    }

    /// <summary>O que vem pela frente: radar perto, pedágio perto, ou o limite da via e o tempo que falta.</summary>
    public static string Avisos(ProgressoRota p)
    {
        if (p.Chegou) return Marca;
        if (p.Radar is { } radar && p.RadarEm is { } radarEm && radarEm <= RadarPerto)
            return $"Radar a {RotaService.TextoDistancia(radarEm)}" + (radar.Kmh is { } k ? $" · {k} km/h" : "");
        if (p.PedagioEm is { } pedagioEm && pedagioEm <= PedagioPerto)
            return $"Pedágio a {RotaService.TextoDistancia(pedagioEm)}";
        var faltam = $"Faltam {RotaService.TextoTempo(p.TempoRestante)}";
        return p.LimiteKmh is { } limite ? $"Limite {limite} km/h · {faltam.ToLower(PtBr)}" : faltam;
    }

    private static readonly string[] Curvas = ["Vire à direita na Rua Teste", "Vire à esquerda na Av. Exemplo", "Na rotatória, pegue a 2ª saída"];

    /// <summary>Rota iniciada, mas o GPS ainda não deu a primeira posição nela.</summary>
    public static TextosVisor Iniciando(string? destino, MusicaAtual? musica) =>
        new(destino is null ? Marca : $"Rota para {destino}", Musica(musica) ?? Marca, Marca, Marca);

    /// <summary>Sem rota iniciada: uma curva de mentira a cada 400 m, descendo 50 m por tique (com um radar na segunda).</summary>
    public static TextosVisor Simulada(int tique, MusicaAtual? musica, DateTime agora)
    {
        var metros = 400 - tique % 8 * 50;
        var indice = tique / 8 % Curvas.Length;
        var comRadar = indice == 1;
        var p = new ProgressoRota(new PassoRota(Curvas[indice], "direita", 0), metros, 12_000 - tique * 50 % 10_000, TimeSpan.FromMinutes(18),
            agora.AddMinutes(18), false, false, null, 60,
            comRadar ? new RadarRota(0, 0, 0, 60) : null, comRadar ? metros + 100 : null);
        var t = DaRota(p, false, musica);
        return t with { Titulo = "(teste) " + t.Titulo };
    }

    /// <summary>"Artista – Música" quando o Spotify está tocando (pausado conta como sem música).</summary>
    public static string? Musica(MusicaAtual? m)
    {
        if (m is not { Tocando: true } || string.IsNullOrWhiteSpace(m.Titulo)) return null;
        return string.IsNullOrWhiteSpace(m.Artista) ? m.Titulo : $"{m.Artista} – {m.Titulo}";
    }

    public static string Hora(DateTime t) => t.ToString("HH:mm:ss", PtBr);
}
