using System.Globalization;

namespace CrankoHub.Models;

/// <summary>Cor de destaque do app (menu selecionado, botões, links...), nas versões clara e escura.</summary>
/// <param name="Texto">Cor do texto escrito sobre o destaque.</param>
/// <param name="TextoEscuro">Cor do texto sobre o destaque no tema escuro (null = a mesma do claro).</param>
public record CorDestaque(string Chave, string Nome, string Claro, string Escuro, string Texto = "#FFFFFF", string? TextoEscuro = null)
{
    /// <summary>Marinho (a cor do logo, padrão) e as cores de destaque do macOS.</summary>
    public static readonly IReadOnlyList<CorDestaque> Paleta =
    [
        // No escuro o marinho vira um lilás claro, com texto escuro por cima
        new("marinho", "Marinho", "#24346F", "#A9B8FF", "#FFFFFF", "#0A1030"),
        new("azul", "Azul", "#007AFF", "#0A84FF"),
        new("roxo", "Roxo", "#AF52DE", "#BF5AF2"),
        new("rosa", "Rosa", "#FF2D55", "#FF375F"),
        new("vermelho", "Vermelho", "#FF3B30", "#FF453A"),
        new("laranja", "Laranja", "#FF9500", "#FF9F0A"),
        new("amarelo", "Amarelo", "#FFCC00", "#FFD60A", "#1D1D1F"),
        new("verde", "Verde", "#34C759", "#30D158"),
        new("grafite", "Grafite", "#8E8E93", "#98989D"),
    ];

    /// <summary>Resolve o que está salvo nas configurações: uma chave da paleta ou um "#RRGGBB".</summary>
    public static CorDestaque DaConfig(string? valor) =>
        Paleta.FirstOrDefault(c => c.Chave == valor)
        ?? (valor is { Length: 7 } && valor[0] == '#' && int.TryParse(valor[1..], NumberStyles.HexNumber, null, out _)
            ? Personalizada(valor)
            : Paleta[0]);

    /// <summary>Cor escolhida pelo usuário: a mesma nos dois temas, com texto claro ou escuro conforme o brilho.</summary>
    public static CorDestaque Personalizada(string hex)
    {
        var rgb = int.Parse(hex[1..], NumberStyles.HexNumber);
        var luminancia = (0.299 * (rgb >> 16 & 0xFF) + 0.587 * (rgb >> 8 & 0xFF) + 0.114 * (rgb & 0xFF)) / 255;
        return new(hex.ToUpperInvariant(), "Personalizada", hex, hex, luminancia > 0.7 ? "#1D1D1F" : "#FFFFFF");
    }
}
