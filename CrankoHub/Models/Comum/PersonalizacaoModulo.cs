namespace CrankoHub.Models;

/// <summary>
/// O que o usuário mudou num módulo (Configurações → Módulos). Cada campo null = o padrão do módulo.
/// Vai para a conta (tabela perfis, coluna modulos_personalizados).
/// </summary>
public class PersonalizacaoModulo
{
    public const int MaxNome = 24;
    public const int MaxDescricao = 60;

    public string? Nome { get; set; }

    public string? Descricao { get; set; }

    /// <summary>"#RRGGBB".</summary>
    public string? Cor { get; set; }

    /// <summary>Chave do catálogo de ícones (<see cref="Components.Icones.DeModulo"/>).</summary>
    public string? Icone { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool Vazia => Nome is null && Descricao is null && Cor is null && Icone is null;
}
