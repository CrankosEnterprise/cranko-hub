namespace CrankoHub.Models;

/// <summary>Próxima manobra da rota, lida da notificação do Waze ou do Maps.</summary>
/// <param name="Texto">Instrução, ex.: "Vire à direita na R. Augusta".</param>
/// <param name="Distancia">Distância até a manobra, ex.: "200 m" (null quando a notificação não traz).</param>
/// <param name="App">Chave do app (<see cref="AppsCarro"/>).</param>
/// <param name="Chegada">Linha extra da notificação (no Maps: tempo, distância e horário de chegada).</param>
public sealed record InstrucaoNavegacao(string Texto, string? Distancia, string App, string? Chegada);

/// <summary>O que está tocando no player ativo do celular (Spotify ou outro).</summary>
/// <param name="Capa">Imagem como data: URI (pronta para o &lt;img&gt;) ou null.</param>
/// <param name="App">Nome do pacote do player.</param>
public sealed record MusicaAtual(string? Titulo, string? Artista, string? Capa, bool Tocando, string? App);

/// <summary>Apps que o Modo Carro abre e acompanha.</summary>
public static class AppsCarro
{
    public const string Waze = "waze";
    public const string Maps = "maps";
    public const string Spotify = "spotify";

    public const string PacoteWaze = "com.waze";
    public const string PacoteMaps = "com.google.android.apps.maps";
    public const string PacoteSpotify = "com.spotify.music";

    public static string? Pacote(string? app) => app switch
    {
        Waze => PacoteWaze,
        Maps => PacoteMaps,
        Spotify => PacoteSpotify,
        _ => null,
    };

    public static string? DoPacote(string? pacote) => pacote switch
    {
        PacoteWaze => Waze,
        PacoteMaps => Maps,
        PacoteSpotify => Spotify,
        _ => null,
    };

    public static string Nome(string? app) => app switch
    {
        Waze => "Waze",
        Maps => "Maps",
        Spotify => "Spotify",
        _ => app ?? "",
    };

    /// <summary>Nome amigável do player a partir do pacote (o próprio pacote quando não é conhecido).</summary>
    public static string NomeDoPacote(string? pacote) => DoPacote(pacote) is { } app ? Nome(app) : pacote ?? "";
}
