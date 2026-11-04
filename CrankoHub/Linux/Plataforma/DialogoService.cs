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

    private static string? Exibir(Func<PhotinoWindow, string?> abrir)
    {
        if (Janela is null) return null;
        string? resultado = null;
        Janela.Invoke(() => resultado = abrir(Janela));
        return string.IsNullOrEmpty(resultado) ? null : resultado;
    }
}
