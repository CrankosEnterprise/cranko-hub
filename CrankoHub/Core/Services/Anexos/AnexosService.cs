using System.Globalization;
using System.IO.Compression;
using System.Net.Http;
using CrankoHub.Models;
using SkiaSharp;

namespace CrankoHub.Services;

/// <summary>
/// Anexos das tarefas e da manutenção do carro. Cada arquivo é compactado ao entrar, para ocupar o mínimo no aparelho e no Storage:
/// <list type="bullet">
/// <item>Imagem: até <see cref="LadoMaximo"/> px no lado maior, em WebP qualidade <see cref="QualidadeWebp"/> (com a rotação do EXIF aplicada).</item>
/// <item>Outros: Brotli no nível máximo (sem perda). Se uma amostra mostrar que não diminui (zip, mp4...), fica como está.</item>
/// </list>
/// A pasta <see cref="Pasta"/> guarda a versão compactada. Logado, ela é um cache do bucket "anexos": quem anexou envia
/// (ver SincronizacaoService) e os outros aparelhos só baixam ao abrir.
/// </summary>
public class AnexosService(SupabaseCliente supabase, ContaService conta)
{
    public static string Pasta => Path.Combine(ArmazenamentoService.PastaDados, "anexos");

    public const string Bucket = "anexos";

    /// <summary>Tamanho máximo do arquivo original.</summary>
    public const long TamanhoMaximo = 20 * 1024 * 1024;

    public const int LadoMaximo = 1920;
    public const int QualidadeWebp = 80;

    /// <summary>Arquivos de um formulário aberto que ainda não foram salvos: a limpeza não pode apagá-los. Só na thread da tela.</summary>
    public static HashSet<string> EmEdicao { get; } = [];

    /// <summary>Caminho do arquivo no bucket.</summary>
    public static string Remoto(string userId, string arquivo) => $"{userId}/{arquivo}";

    /// <summary>Lê, compacta e guarda o arquivo. Lança <see cref="AnexoErro"/> se for grande demais ou não der para ler.</summary>
    public static Task<Anexo> Importar(string caminho) => Task.Run(() =>
    {
        var info = new FileInfo(caminho);
        if (!info.Exists) throw new AnexoErro("Não foi possível ler o arquivo.");
        if (info.Length > TamanhoMaximo) throw new AnexoErro($"\"{info.Name}\" tem {Tamanho(info.Length)}; o máximo é {Tamanho(TamanhoMaximo)}.");
        byte[] bytes;
        try { bytes = File.ReadAllBytes(caminho); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new AnexoErro("Não foi possível ler o arquivo."); }

        var id = ArmazenamentoService.NovoId();
        var extensao = Path.GetExtension(info.Name).ToLowerInvariant();
        var (guardado, formato) = Compactar(bytes, extensao);
        var arquivo = id + formato switch { "webp" => ".webp", "br" => ".br", _ => SoLetras(extensao) };

        Directory.CreateDirectory(Pasta);
        File.WriteAllBytes(Path.Combine(Pasta, arquivo), guardado);
        return new Anexo
        {
            Id = id,
            Nome = info.Name,
            Tipo = TipoMime(extensao),
            Tamanho = bytes.Length,
            TamanhoGuardado = guardado.Length,
            Arquivo = arquivo,
            Formato = formato,
        };
    });

    /// <summary>
    /// Deixa o anexo pronto para abrir no programa padrão: baixa do Storage se não estiver aqui e grava a versão
    /// descompactada numa pasta temporária. Lança <see cref="AnexoErro"/> com a explicação se não der.
    /// </summary>
    public async Task<string> PrepararParaAbrir(Anexo a)
    {
        var local = Path.Combine(Pasta, Path.GetFileName(a.Arquivo));
        if (!File.Exists(local)) await Baixar(a, local);

        var nome = NomeSeguro(a.Formato == "webp" ? Path.ChangeExtension(a.Nome, ".webp") : a.Nome);
        var pasta = Path.Combine(Path.GetTempPath(), "CrankoHub", "anexos", a.Id);
        var destino = Path.Combine(pasta, nome);
        return await Task.Run(() =>
        {
            Directory.CreateDirectory(pasta);
            var bytes = File.ReadAllBytes(local);
            if (a.Formato == "br") bytes = Descompactar(bytes);
            File.WriteAllBytes(destino, bytes);
            return destino;
        });
    }

    private async Task Baixar(Anexo a, string local)
    {
        if (!conta.Logado || conta.UserId is not { } userId)
            throw new AnexoErro("O arquivo não está neste aparelho.");
        try
        {
            if (!await conta.GarantirSessao()) throw new AnexoErro("Sem conexão com o servidor para baixar o arquivo.");
            var bytes = await supabase.BaixarArquivo(Bucket, Remoto(userId, Path.GetFileName(a.Arquivo)))
                ?? throw new AnexoErro("O arquivo ainda não chegou ao servidor. Abra o app no aparelho em que ele foi anexado para enviá-lo.");
            Directory.CreateDirectory(Pasta);
            await File.WriteAllBytesAsync(local, bytes);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new AnexoErro("Sem conexão com o servidor para baixar o arquivo.");
        }
        catch (SupabaseErro ex)
        {
            throw new AnexoErro($"Não foi possível baixar o arquivo: {ex.Message}");
        }
    }

    public static void Excluir(string? arquivo)
    {
        if (string.IsNullOrEmpty(arquivo)) return;
        try { File.Delete(Path.Combine(Pasta, Path.GetFileName(arquivo))); } catch { }
    }

    /// <summary>Apaga da pasta local os arquivos que não estão em <paramref name="manter"/> (tarefa excluída aqui ou em outro aparelho).</summary>
    public static void LimparOrfaos(IReadOnlySet<string> manter)
    {
        try
        {
            if (!Directory.Exists(Pasta)) return;
            // Recém-gravados ficam: podem ser de um anexo que acabou de ser compactado e ainda vai entrar no formulário
            var recente = DateTime.UtcNow.AddMinutes(-10);
            foreach (var f in Directory.EnumerateFiles(Pasta))
                if (!manter.Contains(Path.GetFileName(f)) && File.GetLastWriteTimeUtc(f) < recente)
                    try { File.Delete(f); } catch { }
        }
        catch { }
    }

    /// <summary>Anexos das tarefas.</summary>
    public static IEnumerable<Anexo> DasTarefas(EstadoApp d) => d.Tarefas.Cards.SelectMany(c => c.Anexos);

    /// <summary>Anexos dos serviços de manutenção (orçamentos e notas fiscais).</summary>
    public static IEnumerable<Anexo> DasManutencoes(EstadoApp d) => d.Carro.Manutencoes.SelectMany(m => m.Anexos);

    /// <summary>Arquivos usados em todo o app, mais os de um formulário aberto. Chamar na thread da tela.</summary>
    public static HashSet<string> EmUso(EstadoApp d)
    {
        var r = DasTarefas(d).Concat(DasManutencoes(d)).Select(a => Path.GetFileName(a.Arquivo)).ToHashSet();
        r.UnionWith(EmEdicao);
        return r;
    }

    /// <summary>Ex.: "3,2 MB → 240 KB" (ou só o tamanho, quando não deu para compactar).</summary>
    public static string TamanhoTexto(Anexo a) => a.Formato == "original" || a.TamanhoGuardado >= a.Tamanho
        ? Tamanho(a.Tamanho)
        : $"{Tamanho(a.Tamanho)} → {Tamanho(a.TamanhoGuardado)}";

    /// <summary>Tipo para o envio ao Storage (o arquivo guardado, não o original).</summary>
    public static string TipoGuardado(Anexo a) => a.Formato switch
    {
        "webp" => "image/webp",
        "br" => "application/octet-stream",
        _ => a.Tipo,
    };

    /// <summary>Ex.: "820 B", "240 KB", "3,2 MB".</summary>
    public static string Tamanho(long bytes)
    {
        var pt = CultureInfo.GetCultureInfo("pt-BR");
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{Math.Max(1, Math.Round(bytes / 1024.0)).ToString("0", pt)} KB";
        return $"{(bytes / (1024.0 * 1024)).ToString(bytes < 10 * 1024 * 1024 ? "0.0" : "0", pt)} MB";
    }

    // ----- Compactação -----

    private static (byte[] Bytes, string Formato) Compactar(byte[] bytes, string extensao)
    {
        if (ParaWebp(bytes) is { } webp && webp.Length < bytes.Length) return (webp, "webp");
        if (ImagemComPerda(extensao)) return (bytes, "original"); // jpg/webp que não diminuíram: Brotli não ajuda
        if (ValeBrotli(bytes) && Brotli(bytes) is { } br && br.Length <= bytes.Length * 0.97) return (br, "br");
        return (bytes, "original");
    }

    private static bool ImagemComPerda(string extensao) => extensao is ".jpg" or ".jpeg" or ".webp" or ".heic" or ".avif";

    /// <summary>Imagem estática que o Skia lê → WebP reduzido; senão null (gif animado, não imagem...).</summary>
    private static byte[]? ParaWebp(byte[] bytes)
    {
        try
        {
            using var dados = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(dados);
            if (codec is null || codec.FrameCount > 1) return null;
            using var original = SKBitmap.Decode(codec);
            if (original is null) return null;

            var escala = Math.Min(1.0, (double)LadoMaximo / Math.Max(original.Width, original.Height));
            var menor = original;
            if (escala < 1)
            {
                var info = new SKImageInfo(Math.Max(1, (int)Math.Round(original.Width * escala)), Math.Max(1, (int)Math.Round(original.Height * escala)));
                menor = original.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));
                if (menor is null) return null;
            }
            try
            {
                using var orientada = Orientar(menor, codec.EncodedOrigin);
                using var imagem = SKImage.FromBitmap(orientada ?? menor);
                using var webp = imagem.Encode(SKEncodedImageFormat.Webp, QualidadeWebp);
                return webp?.ToArray();
            }
            finally { if (!ReferenceEquals(menor, original)) menor.Dispose(); }
        }
        catch { return null; }
    }

    /// <summary>Aplica a orientação do EXIF (fotos de celular vêm "deitadas" com a rotação só anotada). null = já está certa.</summary>
    private static SKBitmap? Orientar(SKBitmap b, SKEncodedOrigin origem)
    {
        if (origem is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default) return null;
        var troca = origem is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        float w = b.Width, h = b.Height;
        var r = new SKBitmap(troca ? b.Height : b.Width, troca ? b.Width : b.Height, b.ColorType, b.AlphaType);
        using var c = new SKCanvas(r);
        c.Clear(SKColors.Transparent);
        switch (origem)
        {
            case SKEncodedOrigin.TopRight: c.Translate(w, 0); c.Scale(-1, 1); break;
            case SKEncodedOrigin.BottomRight: c.Translate(w, h); c.RotateDegrees(180); break;
            case SKEncodedOrigin.BottomLeft: c.Translate(0, h); c.Scale(1, -1); break;
            case SKEncodedOrigin.LeftTop: c.RotateDegrees(90); c.Scale(1, -1); break;
            case SKEncodedOrigin.RightTop: c.Translate(h, 0); c.RotateDegrees(90); break;
            case SKEncodedOrigin.RightBottom: c.Translate(h, w); c.RotateDegrees(90); c.Scale(-1, 1); break;
            case SKEncodedOrigin.LeftBottom: c.Translate(0, w); c.RotateDegrees(270); break;
        }
        using var imagem = SKImage.FromBitmap(b);
        c.DrawImage(imagem, 0, 0, SKSamplingOptions.Default);
        return r;
    }

    // Amostra rápida (1 MB, nível 5): se nem ela diminui, o arquivo já vem compactado (zip, mp4, docx...) e o nível 11 seria só espera
    private static bool ValeBrotli(byte[] bytes)
    {
        const int Amostra = 1024 * 1024;
        if (bytes.Length <= Amostra) return true;
        var trecho = bytes.AsSpan(0, Amostra);
        var destino = new byte[BrotliEncoder.GetMaxCompressedLength(Amostra)];
        return BrotliEncoder.TryCompress(trecho, destino, out var n, 5, 22) && n <= Amostra * 0.97;
    }

    private static byte[]? Brotli(byte[] bytes)
    {
        var destino = new byte[BrotliEncoder.GetMaxCompressedLength(bytes.Length)];
        return BrotliEncoder.TryCompress(bytes, destino, out var n, 11, 24) ? destino.AsSpan(0, n).ToArray() : null;
    }

    private static byte[] Descompactar(byte[] bytes)
    {
        using var entrada = new MemoryStream(bytes);
        using var brotli = new BrotliStream(entrada, CompressionMode.Decompress);
        using var saida = new MemoryStream();
        brotli.CopyTo(saida);
        return saida.ToArray();
    }

    // ----- Nomes -----

    private static string SoLetras(string extensao) =>
        extensao.Length is > 1 and <= 10 && extensao.Skip(1).All(char.IsAsciiLetterOrDigit) ? extensao : "";

    private static string NomeSeguro(string nome)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var r = new string(nome.Select(ch => invalidos.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return r.Length == 0 ? "anexo" : r;
    }

    public static string TipoMime(string extensao) => extensao.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".heic" => "image/heic",
        ".pdf" => "application/pdf",
        ".txt" => "text/plain",
        ".csv" => "text/csv",
        ".json" => "application/json",
        ".xml" => "application/xml",
        ".html" or ".htm" => "text/html",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".ppt" => "application/vnd.ms-powerpoint",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".odt" => "application/vnd.oasis.opendocument.text",
        ".ods" => "application/vnd.oasis.opendocument.spreadsheet",
        ".zip" => "application/zip",
        ".mp3" => "audio/mpeg",
        ".mp4" => "video/mp4",
        _ => "application/octet-stream",
    };
}

/// <summary>Não deu para anexar ou abrir; a mensagem vai direto para o toast.</summary>
public class AnexoErro(string mensagem) : Exception(mensagem);
