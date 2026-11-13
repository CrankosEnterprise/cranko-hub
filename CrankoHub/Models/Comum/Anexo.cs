namespace CrankoHub.Models;

/// <summary>
/// Um arquivo anexado (a uma tarefa ou a um serviço de manutenção). O arquivo guardado (pasta "anexos" e Storage)
/// é a versão compactada: imagem vira WebP, o resto vai com Brotli, e o que não diminui fica como está.
/// Ver <see cref="Services.AnexosService"/>.
/// </summary>
public class Anexo
{
    public string Id { get; set; } = "";

    /// <summary>Nome original, ex.: "boleto.pdf".</summary>
    public string Nome { get; set; } = "";

    /// <summary>Tipo (mime) do arquivo original.</summary>
    public string Tipo { get; set; } = "application/octet-stream";

    /// <summary>Tamanho original, em bytes.</summary>
    public long Tamanho { get; set; }

    /// <summary>Tamanho depois de compactar (o que ocupa no servidor), em bytes.</summary>
    public long TamanhoGuardado { get; set; }

    /// <summary>Nome do arquivo guardado, único e fixo: "&lt;id&gt;.webp", "&lt;id&gt;.br" ou "&lt;id&gt;&lt;extensão&gt;".</summary>
    public string Arquivo { get; set; } = "";

    /// <summary>"webp" (imagem convertida), "br" (Brotli) ou "original".</summary>
    public string Formato { get; set; } = "original";

    /// <summary>Do que se trata, quando a seção pede (na manutenção: "orcamento" ou "nota_fiscal"). null = sem categoria.</summary>
    public string? Categoria { get; set; }
}

/// <summary>Categoria de anexo que uma seção oferece (ex.: Orçamento e Nota fiscal na manutenção).</summary>
public record CategoriaAnexo(string Chave, string Nome)
{
    public static readonly IReadOnlyList<CategoriaAnexo> Manutencao =
    [
        new("orcamento", "Orçamento"),
        new("nota_fiscal", "Nota fiscal"),
    ];
}
