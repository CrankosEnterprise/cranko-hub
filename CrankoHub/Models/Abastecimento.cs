using System.Text.Json.Serialization;

namespace CrankoHub.Models;

public class Abastecimento
{
    public string Id { get; set; } = "";

    public string VeiculoId { get; set; } = "";

    public DateOnly Data { get; set; }

    /// <summary>Chave do combustível ("gasolina", "etanol", "diesel"). Ver <see cref="Combustivel"/>.</summary>
    public string Combustivel { get; set; } = "gasolina";

    public double? Odometro { get; set; }

    /// <summary>Km rodados desde o abastecimento anterior. 0 = ponto de partida (ver <see cref="Inicial"/>).</summary>
    public double Km { get; set; }

    /// <summary>Litros colocados na bomba (base do preço por litro).</summary>
    public double Litros { get; set; }

    /// <summary>
    /// Litros gastos desde o abastecimento anterior, quando diferem dos colocados (computador de bordo ou
    /// estimados pelo marcador de combustível). Null: gasto = colocados (tanque cheio nos dois).
    /// </summary>
    public double? LitrosGastos { get; set; }

    /// <summary>Ponteiro do marcador antes de abastecer, em oitavos (0 = vazio, 8 = cheio).</summary>
    public int? NivelAntes { get; set; }

    /// <summary>Completou o tanque neste abastecimento (depois dele o nível é 8/8).</summary>
    public bool TanqueCheio { get; set; }

    /// <summary><see cref="LitrosGastos"/> veio do marcador (aproximado).</summary>
    public bool GastoEstimado { get; set; }

    /// <summary>Valor total pago (R$).</summary>
    public double? Valor { get; set; }

    public string? Posto { get; set; }

    public string? Obs { get; set; }

    /// <summary>
    /// Ponto de partida (primeiro abastecimento, ou recomeço depois de um que não foi anotado): sem km rodados,
    /// não tem consumo; só guarda odômetro, tanque e ponteiro como base do próximo. Os gastos contam normalmente.
    /// </summary>
    [JsonIgnore]
    public bool Inicial => Km <= 0;

    /// <summary>Litros usados no consumo (km/l).</summary>
    [JsonIgnore]
    public double Consumidos => LitrosGastos is > 0 ? LitrosGastos.Value : Litros;

    [JsonIgnore]
    public double Kml => Km / Consumidos;
}
