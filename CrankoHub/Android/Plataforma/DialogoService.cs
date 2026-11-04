namespace CrankoHub.Services;

/// <summary>Versão Android: seletor de arquivos do sistema, para escolher a imagem de capa.</summary>
public class DialogoService : IDialogoService
{
    public event Func<string, Task<bool>>? AoConfirmar;

    public Task<bool> Confirmar(string mensagem) => AoConfirmar?.Invoke(mensagem) ?? Task.FromResult(false);

    public Task<string?> AbrirImagem() => Escolher("Escolha a imagem da capa", FilePickerFileType.Images);

    private static async Task<string?> Escolher(string titulo, FilePickerFileType tipos)
    {
        // O MAUI copia o arquivo escolhido para o cache do app: FullPath é um caminho comum
        var r = await MainThread.InvokeOnMainThreadAsync(() =>
            FilePicker.Default.PickAsync(new PickOptions { PickerTitle = titulo, FileTypes = tipos }));
        return r?.FullPath;
    }
}
