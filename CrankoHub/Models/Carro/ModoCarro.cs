namespace CrankoHub.Models;

/// <summary>O que está tocando agora, em qualquer player (lido pelo acesso às notificações).</summary>
/// <param name="App">Nome do app que está tocando (Spotify, YouTube Music...).</param>
public sealed record MusicaAtual(string? Titulo, string? Artista, bool Tocando, string? App);

/// <summary>Posição do GPS (para a busca de destino preferir lugares perto).</summary>
public sealed record PosicaoCarro(double Latitude, double Longitude);

/// <summary>Lugar achado na busca de destino (ou guardado nos recentes).</summary>
/// <param name="Detalhe">Rua, bairro e cidade, para diferenciar resultados com o mesmo nome.</param>
public sealed record LugarBusca(string Nome, string? Detalhe, double Latitude, double Longitude);

/// <summary>Apps que o Modo Carro abre. A navegação fica com o Maps ou o Waze; o CrankoHub só põe a barra da música por cima.</summary>
public static class AppsCarro
{
    public const string Waze = "waze";
    public const string Maps = "maps";
    public const string Spotify = "spotify";

    /// <summary>Os apps de navegação, na ordem da tela do Modo Carro.</summary>
    public static readonly IReadOnlyList<string> Navegacao = [Maps, Waze];

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
        Maps => "Google Maps",
        Spotify => "Spotify",
        _ => app ?? "",
    };

    /// <summary>App de navegação válido (o padrão é o Google Maps).</summary>
    public static string NavegacaoValida(string? app) => app is Waze ? Waze : Maps;
}

/// <summary>Aparelho Bluetooth marcado como "o meu carro" (engrenagem do Carro). O nome fica salvo para aparecer mesmo com o Bluetooth desligado.</summary>
public sealed record CarroPareado(string Endereco, string Nome);

/// <summary>Aparelho pareado no celular (lista da engrenagem do Carro).</summary>
/// <param name="Audio">É de áudio (som de carro, fone, caixinha): aparece primeiro na lista.</param>
public sealed record AparelhoBluetooth(string Endereco, string Nome, bool Audio);

/// <summary>Onde o carro ficou quando o Bluetooth dele desconectou.</summary>
/// <param name="Endereco">Rua e número (do Geocoder do Android), ou null sem internet.</param>
public sealed record CarroEstacionado(double Latitude, double Longitude, DateTime Quando, string? Endereco);

/// <summary>O que fazer quando o celular conecta no Bluetooth do carro.</summary>
public static class AcoesConexao
{
    public const string Nada = "nada";
    public const string Notificar = "notificar";
    public const string Abrir = "abrir";
}

/// <summary>Uma permissão (ou ajuste do Android) que o Modo Carro precisa, com o estado para as Configurações.</summary>
/// <param name="Id">Ver <see cref="PermissoesCarro"/>.</param>
/// <param name="Ativada">null = o Android não deixa conferir (ex.: Início automático do MIUI).</param>
/// <param name="Necessaria">Precisa para o que está escolhido agora (as outras aparecem como opcionais).</param>
public sealed record PermissaoCarro(string Id, string Nome, string Motivo, bool? Ativada, bool Necessaria);

public static class PermissoesCarro
{
    public const string Localizacao = "localizacao", LocalizacaoSempre = "localizacao-sempre", Bluetooth = "bluetooth",
        Notificacoes = "notificacoes", Sobrepor = "sobrepor", Musica = "musica", Bateria = "bateria", MiuiInicio = "miui-inicio", MiuiPopup = "miui-popup";
}
