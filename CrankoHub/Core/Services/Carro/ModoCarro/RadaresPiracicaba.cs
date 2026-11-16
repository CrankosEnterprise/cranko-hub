using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Radares municipais de Piracicaba. <b>Arquivo gerado</b> por ferramentas/radares/montar.js a partir de
/// ferramentas/radares/piracicaba.js: para incluir ou tirar um radar, mexa lá e rode de novo (ver o README da pasta).
/// A lista vem da prefeitura (jornal JP, jan/2026), conferida com o cadastro de aferições do Inmetro, que dá a velocidade
/// e o sentido. Posição: número de porta pelo CNEFE do IBGE (Censo 2022), encaixado na via do OpenStreetMap; cruzamento
/// pelo ponto em comum das duas vias. Sentido: "B/C" (bairro → centro) ou "C/B" em graus onde a geometria deixou claro;
/// null = avisa nos dois sentidos. As rodovias que passam pela cidade vêm do DER.
/// <b>Não se atualiza sozinho.</b>
/// </summary>
internal static class RadaresPiracicaba
{
    public const string Versao = "nov/2026";

    // Id, latitude, longitude, km/h, sentido do trânsito (graus) e lombada
    private static readonly (string Id, double Lat, double Lon, int? Kmh, double? Sentido, bool Lombada)[] Pontos =
    [
        new("pira:1", -22.716094, -47.627677, 60, 282, false), // Av. Pádua Dias perto da R. Santos Dumont
        new("pira:2", -22.710864, -47.639229, 50, null, false), // Av. Centenário, 900 (shopping/bairro e bairro/shopping)
        new("pira:3", -22.673665, -47.690508, 50, 293, false), // Av. Corcovado, oposto ao 915 (C/B)
        new("pira:4", -22.673665, -47.690508, 50, 113, false), // Av. Corcovado, 915 (B/C)
        new("pira:5", -22.732458, -47.661449, 50, 287, false), // Av. Dr. Paulo de Moraes perto da R. Prof. Silvio A. de Souza
        new("pira:6", -22.741950, -47.646067, 50, 153, false), // Av. 31 de Março, perto do 780
        new("pira:7", -22.734107, -47.629056, 50, null, false), // Av. Vollet Sachs perto da R. Eça de Queiróz
        new("pira:8", -22.747894, -47.623196, 50, 304, false), // Av. Antonio Fazanaro, 165 m depois da Av. Rio das Pedras
        new("pira:9", -22.744243, -47.644684, 50, 333, false), // Av. 31 de Março, 1001
        new("pira:10", -22.741641, -47.626785, 50, 132, false), // Av. Rio das Pedras, 518
        new("pira:11", -22.742122, -47.625942, 50, 311, false), // Av. Rio das Pedras, 611
        new("pira:12", -22.735344, -47.639412, 50, null, false), // Av. Independência, 1524
        new("pira:13", -22.719885, -47.646310, 50, null, false), // Av. Armando de Salles Oliveira, 924
        new("pira:14", -22.735326, -47.628897, 50, 191, false), // Av. Vollet Sachs, perto do 2200
        new("pira:15", -22.726200, -47.624299, 50, 291, false), // Av. Cássio Paschoal Padovani, 215
        new("pira:16", -22.689722, -47.664070, 50, null, false), // Av. Brasília, 1080 (os dois sentidos)
        new("pira:18", -22.713468, -47.653182, null, null, true), // R. Gioconda Fiorim Oriani perto da Av. Rui Barbosa (lombada)
        new("pira:21", -22.725125, -47.633822, 50, 205, false), // Av. Independência x R. Cristiano Cleopath
        new("pira:22", -22.727459, -47.646808, 50, null, false), // Av. Armando de Salles Oliveira x R. XV de Novembro
        new("pira:23", -22.736725, -47.652179, 50, 207, false), // R. Benjamin Constant x Av. Dr. Paulo de Moraes
        new("pira:24", -22.712384, -47.656239, 50, null, false), // Av. Dona Francisca x Av. Barão de Serra Negra
        new("pira:25", -22.738807, -47.630418, 50, null, false), // Av. Vollet Sachs x Av. Rio das Pedras
        new("pira:26", -22.738025, -47.640830, 50, 274, false), // Av. Independência, rotatória com a Av. Com. Luciano Guidotti
        new("pira:27", -22.703180, -47.650258, 50, null, false), // Av. Limeira, 722 e 901
        new("pira:29", -22.752677, -47.619081, 50, 316, false), // Av. Rio das Pedras x R. Bruno Ferraioli
        new("pira:30", -22.714773, -47.647874, 50, null, false), // Av. Armando de Salles Oliveira x Av. Torquato da Silva Leitão
        new("pira:31", -22.722064, -47.644790, 50, 357, false), // Av. Armando de Salles Oliveira x R. Regente Feijó
        new("pira:32", -22.730006, -47.647950, 50, 119, false), // R. Dom Pedro I, 1145
        new("pira:33", -22.737179, -47.651277, 50, null, false), // Av. Dr. Paulo de Moraes x R. da Glória
        new("pira:34", -22.748797, -47.656200, 50, 304, false), // Av. Raposo Tavares x R. Iguatemi
        new("pira:35", -22.724010, -47.633299, 50, null, false), // Av. Independência x R. Saldanha Marinho
        new("pira:36", -22.735230, -47.648626, 50, null, false), // Av. 31 de Março x Av. Independência
        new("pira:37", -22.740228, -47.666685, 50, 259, false), // Av. Madre Maria Teodora, 913
        new("pira:38", -22.747683, -47.658095, 50, null, false), // Av. Raposo Tavares, 1234
        new("pira:39", -22.754245, -47.636774, 50, 198, false), // Av. Com. Luciano Guidotti, oposto ao 1937
        new("pira:40", -22.768289, -47.610710, 50, null, false), // Av. Rio das Pedras, 3667 (os dois sentidos)
    ];

    public static List<RadarSp> Lista() =>
        [.. Pontos.Select(p => new RadarSp(p.Id, p.Lat, p.Lon, p.Kmh, null, FontesRadar.Piracicaba, p.Lombada, p.Sentido))];
}
