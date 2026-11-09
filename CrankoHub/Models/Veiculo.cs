namespace CrankoHub.Models;

public class Veiculo
{
    public string Id { get; set; } = "";

    public string Nome { get; set; } = "";

    /// <summary>Obrigatória no cadastro; nula nos veículos criados antes da ficha.</summary>
    public string? Marca { get; set; }

    /// <summary>Ano-modelo. Obrigatório no cadastro; nulo nos veículos criados antes da ficha.</summary>
    public int? Ano { get; set; }

    /// <summary>Dados opcionais (motor, câmbio, FIPE...).</summary>
    public FichaVeiculo? Ficha { get; set; }
}
