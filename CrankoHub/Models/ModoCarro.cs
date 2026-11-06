namespace CrankoHub.Models;

/// <summary>O que está tocando no Spotify (pela "Transmissão do status do dispositivo").</summary>
/// <param name="Capa">URL da capa (do oEmbed do Spotify) ou null enquanto não chega.</param>
public sealed record MusicaAtual(string? Titulo, string? Artista, string? Capa, bool Tocando);

/// <summary>Posição do GPS para o mapa do Modo Carro.</summary>
/// <param name="Direcao">Graus a partir do norte, no sentido horário (null quando o GPS não informa).</param>
/// <param name="VelocidadeKmh">Velocidade em km/h (0 quando o GPS não informa).</param>
public sealed record PosicaoCarro(double Latitude, double Longitude, double? Direcao, double VelocidadeKmh);

/// <summary>Lugar achado na busca de destino (ou guardado nos recentes).</summary>
/// <param name="Detalhe">Rua, bairro e cidade, para diferenciar resultados com o mesmo nome.</param>
public sealed record LugarBusca(string Nome, string? Detalhe, double Latitude, double Longitude)
{
    /// <summary>Sugestão do Google ainda sem coordenada: a posição é pedida só quando ela é escolhida.</summary>
    public string? PlaceId { get; init; }

    /// <summary>Distância até a sugestão informada pelo Google (só na lista da busca).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double? Metros { get; init; }
}

/// <summary>Uma manobra da rota.</summary>
/// <param name="Icone">Chave da seta (ver <c>Icones.Manobra</c>): frente, esquerda, direita, leve-esquerda, leve-direita, retorno, rotatoria, chegada.</param>
/// <param name="Inicio">Onde a manobra acontece, em metros desde o começo da rota.</param>
public sealed record PassoRota(string Instrucao, string Icone, double Inicio);

/// <summary>Andamento da viagem na posição atual.</summary>
/// <param name="Proximo">Próxima manobra.</param>
/// <param name="DistanciaProximo">Metros até a próxima manobra.</param>
/// <param name="Restante">Metros até o destino.</param>
/// <param name="ForaDaRota">A posição está longe da linha da rota (vai recalcular).</param>
/// <param name="PedagioEm">Metros até a próxima praça de pedágio (null se não houver mais).</param>
/// <param name="LimiteKmh">Limite de velocidade do trecho atual (null quando o mapa não informa).</param>
/// <param name="Radar">Próximo radar à frente (null se não houver mais).</param>
/// <param name="RadarEm">Metros até o próximo radar.</param>
public sealed record ProgressoRota(PassoRota Proximo, double DistanciaProximo, double Restante, TimeSpan TempoRestante, DateTime Chegada, bool Chegou, bool ForaDaRota,
    double? PedagioEm, int? LimiteKmh, RadarRota? Radar, double? RadarEm);

/// <summary>Radar fixo cadastrado no OpenStreetMap (<c>highway=speed_camera</c>).</summary>
/// <param name="Kmh">Velocidade fiscalizada, quando o mapa informa.</param>
/// <param name="Inicio">Onde fica, em metros desde o começo da rota (0 no cache, antes de ser colocado numa rota).</param>
public sealed record RadarRota(long Id, double Latitude, double Longitude, int? Kmh, double Inicio = 0);

/// <summary>Trecho da rota com o mesmo limite de velocidade (metros desde o começo da rota).</summary>
public sealed record TrechoLimite(double Inicio, double Fim, int Kmh);

/// <summary>Praça de pedágio no caminho da rota.</summary>
/// <param name="Via">Rodovia onde fica (ex.: "SP-348"), quando o mapa informa.</param>
/// <param name="Inicio">Onde fica, em metros desde o começo da rota.</param>
public sealed record PedagioRota(string? Via, double Latitude, double Longitude, double Inicio);

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
