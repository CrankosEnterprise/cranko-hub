using System.Diagnostics;
using Photino.NET;

namespace CrankoHub.Services;

/// <summary>Versão Linux: diálogo de arquivo do GTK, pela janela do Photino.</summary>
public class DialogoService : IDialogoService
{
    /// <summary>Janela principal (definida no Program.cs).</summary>
    public static PhotinoWindow? Janela { get; set; }

    public event Func<string, Task<bool>>? AoConfirmar;

    public Task<bool> Confirmar(string mensagem) => AoConfirmar?.Invoke(mensagem) ?? Task.FromResult(false);

    public Task<string?> AbrirImagem() =>
        Task.FromResult(Exibir(j => j.ShowOpenFile("Escolha a imagem da capa", null, false,
            [("Imagens", ["*.jpg", "*.jpeg", "*.png", "*.webp", "*.gif"])]).FirstOrDefault()));

    public Task<string[]> AbrirArquivos(string[]? extensoes = null) =>
        Task.FromResult(Exibir(j => j.ShowOpenFile("Escolha os arquivos para anexar", null, true,
            extensoes is null ? [("Todos os arquivos", ["*"])] : [("Arquivos aceitos", extensoes.Select(e => "*" + e).ToArray())])) ?? []);

    public Task AbrirNoSistema(string caminho)
    {
        var info = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
        info.ArgumentList.Add(caminho);
        Process.Start(info);
        return Task.CompletedTask;
    }

    private static string? Exibir(Func<PhotinoWindow, string?> abrir)
    {
        var r = Exibir<string?>(abrir);
        return string.IsNullOrEmpty(r) ? null : r;
    }

    private static T? Exibir<T>(Func<PhotinoWindow, T> abrir)
    {
        if (Janela is null) return default;
        T? resultado = default;
        Janela.Invoke(() => resultado = abrir(Janela));
        return resultado;
    }
}
