namespace CrankoHub.Models;

/// <summary>O que está tocando no Spotify (pela "Transmissão do status do dispositivo").</summary>
/// <param name="Capa">URL da capa (do oEmbed do Spotify) ou null enquanto não chega.</param>
public sealed record MusicaAtual(string? Titulo, string? Artista, string? Capa, bool Tocando);

/// <summary>Posição do GPS para o mapa do Modo Carro.</summary>
/// <param name="Direcao">Graus a partir do norte, no sentido horário (null quando o GPS não informa).</param>
/// <param name="VelocidadeKmh">Velocidade em km/h (0 quando o GPS não informa).</param>
public sealed record PosicaoCarro(double Latitude, double Longitude, double? Direcao, double VelocidadeKmh);

/// <summary>Apps que o Modo Carro abre.</summary>
public static class AppsCarro
{
    public const string Waze = "waze";
    public const string Maps = "maps";
    public const string Spotify = "spotify";

    public static string? Pacote(string? app) => app switch
    {
        Waze => "com.waze",
        Maps => "com.google.android.apps.maps",
        Spotify => "com.spotify.music",
        _ => null,
    };

    public static string Nome(string? app) => app switch
    {
        Waze => "Waze",
        Maps => "Maps",
        Spotify => "Spotify",
        _ => app ?? "",
    };
}
