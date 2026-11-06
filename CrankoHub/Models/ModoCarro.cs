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

/// <summary>
/// Teste do visor da central (Pioneer) pelo Bluetooth: o CrankoHub manda os próprios textos de "título / artista / álbum"
/// (AVRCP) e a pessoa conta no app o que apareceu.
/// <para>
/// 1º teste no carro (06/11/2026, Pioneer AVH-X2550BT + POCO F3, Android 13): só o texto não basta; com um áudio mudo tocando,
/// o Android passa o visor para o CrankoHub e os botões da central chegam aqui. Mas o Android devolve o visor ao Spotify
/// sempre que ele começa a tocar (play ou troca de música). Este 2º teste confere a retomada do visor nesses momentos.
/// </para>
/// </summary>
/// <param name="Sim">Texto do botão de resposta positiva (null = etapa sem resposta, fica até parar o teste).</param>
/// <param name="Nao">Texto do botão de resposta negativa.</param>
/// <param name="Segundos">Avança sozinha depois desse tempo (0 = fica até parar o teste).</param>
public sealed record EtapaVisor(string Nome, string Instrucao, string? Sim, string? Nao, int Segundos);

public static class TesteVisor
{
    public static readonly IReadOnlyList<EtapaVisor> Etapas =
    [
        new("Rota com a música tocando", "Deixe a música tocando. O visor deve mostrar a próxima curva (e a música embaixo), com a distância diminuindo. Sem rota iniciada, aparece uma rota de teste.",
            "A curva apareceu", "Só aparece a música", 90),
        new("Pausar, tocar e trocar de música", "Pela central: pause, dê play de novo e passe para a próxima música. Depois de cada uma, a curva deve voltar ao visor em uns 2 s.",
            "A curva voltou", "Ficou só a música", 120),
        new("Rota", "O visor fica mostrando a rota. Use à vontade e toque em Parar o teste no fim.", null, null, 0),
    ];

    /// <summary>Respostas que a pessoa dá a cada etapa (vão para o log).</summary>
    public const string Apareceu = "apareceu", NaoApareceu = "nao-apareceu", MusicaParou = "musica-parou", BotoesFuncionam = "botoes-ok",
        AcentosComDefeito = "acentos-com-defeito";
}

/// <summary>Os campos que vão para o visor da central (AVRCP). Na Pioneer, o gênero é a linha com a guitarra.</summary>
public sealed record TextosVisor(string Titulo, string Artista, string Album, string Genero);
