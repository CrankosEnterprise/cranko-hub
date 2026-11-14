namespace CrankoHub.Models;

/// <summary>Radar fixo do estado de São Paulo (DER, CET ou OpenStreetMap: ver <see cref="FontesRadar"/>).</summary>
/// <param name="Id">Único entre as fontes (o código da fonte com um prefixo).</param>
/// <param name="Kmh">Velocidade fiscalizada para carros, ou null quando a fonte não informa.</param>
/// <param name="Direcao">Eixo da via em graus (0 = norte), quando o OSM informa: vale para os dois sentidos.</param>
/// <param name="Fonte">Ver <see cref="FontesRadar"/>.</param>
/// <param name="Lombada">Lombada eletrônica (redutor do DER), em vez de radar.</param>
/// <param name="Sentido">Sentido do trânsito fiscalizado em graus (DER: Norte 0, Leste 90...), ou null para os dois.</param>
public sealed record RadarSp(string Id, double Lat, double Lon, int? Kmh, double? Direcao, string Fonte = FontesRadar.Osm,
    bool Lombada = false, double? Sentido = null);

/// <summary>De onde vêm os radares: os oficiais primeiro, e o OpenStreetMap preenche o resto do estado.</summary>
public static class FontesRadar
{
    public const string Der = "DER", Cet = "CET", Pardal = "PARDAL", Osm = "OSM";

    public static readonly IReadOnlyList<string> Todas = [Der, Cet, Pardal, Osm];

    public static string Nome(string fonte) => fonte switch
    {
        Der => "DER (rodovias)",
        Cet => "CET (capital)",
        Pardal => "Inmetro e rodovias federais",
        _ => "OpenStreetMap (outras cidades)",
    };
}

/// <summary>Som do aviso de radar (engrenagem do Carro).</summary>
public static class TonsAvisoRadar
{
    public const string Curto = "curto", Duplo = "duplo", Alerta = "alerta", Notificacao = "notificacao";

    public static readonly IReadOnlyList<(string Valor, string Nome)> Todos =
        [(Curto, "Bipe curto"), (Duplo, "Bipe duplo"), (Alerta, "Alerta"), (Notificacao, "Som de notificação do celular")];

    public static string Valido(string? tom) => tom is Curto or Alerta or Notificacao ? tom : Duplo;
}

/// <summary>O radar mais perto à frente do carro e quantos metros faltam.</summary>
public sealed record RadarProximo(RadarSp Radar, double Metros);

/// <summary>Onde o aviso de radar fica na borda esquerda da tela, por cima do Maps/Waze.</summary>
public static class PosicoesAvisoRadar
{
    public const string Cima = "cima", Meio = "meio", Baixo = "baixo";

    public static string Valida(string? posicao) => posicao is Cima or Baixo ? posicao : Meio;
}

/// <summary>Posição do GPS (para a busca de destino preferir lugares perto).</summary>
public sealed record PosicaoCarro(double Latitude, double Longitude);

/// <summary>Lugar achado na busca de destino (ou guardado nos recentes).</summary>
/// <param name="Detalhe">Rua, bairro e cidade, para diferenciar resultados com o mesmo nome.</param>
public sealed record LugarBusca(string Nome, string? Detalhe, double Latitude, double Longitude);

/// <summary>Apps que o Modo Carro abre. A navegação fica com o Maps ou o Waze; o CrankoHub só põe o aviso de radar por cima.</summary>
public static class AppsCarro
{
    public const string Waze = "waze";
    public const string Maps = "maps";

    /// <summary>Os apps de navegação, na ordem da tela do Modo Carro.</summary>
    public static readonly IReadOnlyList<string> Navegacao = [Maps, Waze];

    public static string? Pacote(string? app) => app switch
    {
        Waze => "com.waze",
        Maps => "com.google.android.apps.maps",
        _ => null,
    };

    public static string Nome(string? app) => app switch
    {
        Waze => "Waze",
        Maps => "Google Maps",
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
        Notificacoes = "notificacoes", Sobrepor = "sobrepor", Bateria = "bateria", MiuiInicio = "miui-inicio", MiuiPopup = "miui-popup";
}
