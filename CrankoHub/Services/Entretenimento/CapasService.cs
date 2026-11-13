using System.Net.Http;
using SkiaSharp;

namespace CrankoHub.Services;

/// <summary>
/// Capas da estante de Entretenimento, guardadas no PC para aparecerem sem internet.
/// O WebView enxerga a pasta pelo endereço <see cref="Host"/> (ver MainWindow).
/// Logado, a pasta é um cache das capas da conta no Storage (ver SincronizacaoService).
/// </summary>
public class CapasService(HttpClient http)
{
    public static readonly string Pasta = Path.Combine(ArmazenamentoService.PastaDados, "capas");

    public const string Host = "capas.crankohub";

    /// <summary>Largura máxima das capas: o suficiente para a estante e ~100–200 KB por imagem.</summary>
    public const int LarguraMaxima = 600;

    /// <summary>Início do endereço das capas (a versão Linux serve a pasta em "capas/").</summary>
    public static string Prefixo { get; set; } = $"https://{Host}/";

    public static string? Url(string? arquivo) => string.IsNullOrEmpty(arquivo) ? null : Prefixo + arquivo;

    /// <summary>Baixa a imagem e devolve o nome do arquivo salvo (ou null se falhar).</summary>
    public async Task<string?> Baixar(string url)
    {
        try
        {
            var bytes = await http.GetByteArrayAsync(url);
            return Salvar(bytes, Path.GetExtension(new Uri(url).AbsolutePath));
        }
        catch { return null; }
    }

    /// <summary>Copia uma imagem escolhida no PC.</summary>
    public static string? Importar(string caminho)
    {
        try { return Salvar(File.ReadAllBytes(caminho), Path.GetExtension(caminho)); }
        catch { return null; }
    }

    public static void Excluir(string? arquivo)
    {
        if (string.IsNullOrEmpty(arquivo)) return;
        try { File.Delete(Path.Combine(Pasta, Path.GetFileName(arquivo))); } catch { }
    }

    /// <summary>
    /// Reduz a imagem para <see cref="LarguraMaxima"/> de largura, no mesmo formato da extensão (o nome do arquivo não muda).
    /// Imagens menores, gif e o que não der para ler voltam como estão.
    /// </summary>
    public static byte[] Reduzir(byte[] bytes, string extensao)
    {
        var formato = extensao.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            ".png" => SKEncodedImageFormat.Png,
            ".webp" => SKEncodedImageFormat.Webp,
            _ => (SKEncodedImageFormat?)null,
        };
        if (formato is null) return bytes;
        try
        {
            using var original = SKBitmap.Decode(bytes);
            if (original is null || original.Width <= LarguraMaxima) return bytes;
            var altura = Math.Max(1, (int)Math.Round(original.Height * (double)LarguraMaxima / original.Width));
            using var menor = original.Resize(new SKImageInfo(LarguraMaxima, altura), new SKSamplingOptions(SKCubicResampler.Mitchell));
            if (menor is null) return bytes;
            using var imagem = SKImage.FromBitmap(menor);
            using var dados = imagem.Encode(formato.Value, 85);
            return dados is not null && dados.Size < bytes.Length ? dados.ToArray() : bytes;
        }
        catch { return bytes; }
    }

    /// <summary>Tipo da imagem pelo nome do arquivo (para o envio ao Storage).</summary>
    public static string TipoMime(string arquivo) => Path.GetExtension(arquivo).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "image/jpeg",
    };

    private static string Salvar(byte[] bytes, string extensao)
    {
        Directory.CreateDirectory(Pasta);
        extensao = extensao.ToLowerInvariant();
        if (extensao is not (".png" or ".webp" or ".jpeg" or ".gif")) extensao = ".jpg";
        var nome = ArmazenamentoService.NovoId() + extensao;
        File.WriteAllBytes(Path.Combine(Pasta, nome), Reduzir(bytes, extensao));
        return nome;
    }
}
