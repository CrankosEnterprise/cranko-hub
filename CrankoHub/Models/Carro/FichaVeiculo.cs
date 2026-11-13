namespace CrankoHub.Models;

/// <summary>
/// Ficha técnica opcional de um veículo: digitada à mão ou preenchida pela FIPE (<see cref="Services.FipeService"/>).
/// cv e torque a FIPE não informa: são sempre digitados.
/// </summary>
public class FichaVeiculo
{
    // Identificação
    /// <summary>Versão completa (ex.: "ONIX HATCH LT 1.0 12V Flex 5p Mec.").</summary>
    public string? Versao { get; set; }
    public string? Placa { get; set; }
    public string? Cor { get; set; }
    public int? AnoFabricacao { get; set; }

    // Motor
    /// <summary>Cilindrada em litros (ex.: 1.0, 1.6).</summary>
    public double? Cilindrada { get; set; }
    public int? Valvulas { get; set; }
    public bool? Turbo { get; set; }
    public double? PotenciaCv { get; set; }
    public double? TorqueKgfm { get; set; }
    /// <summary>Texto livre: Flex, Gasolina, Etanol, Diesel, Elétrico, Híbrido.</summary>
    public string? Combustivel { get; set; }

    // Transmissão e carroceria
    /// <summary>Manual, Automático, CVT ou Automatizado.</summary>
    public string? Cambio { get; set; }
    public int? Marchas { get; set; }
    public string? Tracao { get; set; }
    public int? Portas { get; set; }
    public string? Carroceria { get; set; }

    // Medidas e consumo
    public double? TanqueLitros { get; set; }
    public double? PortaMalasLitros { get; set; }
    public double? PesoKg { get; set; }
    /// <summary>Consumo oficial (Inmetro) em km/l.</summary>
    public double? ConsumoCidade { get; set; }
    public double? ConsumoEstrada { get; set; }

    // FIPE
    public string? CodigoFipe { get; set; }
    public double? ValorFipe { get; set; }
    /// <summary>Mês de referência do valor (ex.: "novembro de 2026").</summary>
    public string? ReferenciaFipe { get; set; }

    /// <summary>Ex.: "1.0 12V Turbo".</summary>
    public string? MotorResumo()
    {
        var partes = new List<string>();
        if (Cilindrada is double c) partes.Add(c.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        if (Valvulas is int v) partes.Add($"{v}V");
        if (Turbo == true) partes.Add("Turbo");
        return partes.Count > 0 ? string.Join(" ", partes) : null;
    }
}
