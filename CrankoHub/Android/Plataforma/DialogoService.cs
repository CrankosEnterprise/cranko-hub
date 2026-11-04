namespace CrankoHub.Services;

/// <summary>
/// Versão Android: seletor de arquivos do sistema para abrir, e o compartilhamento para salvar
/// (Drive, Arquivos, e-mail...). Não há como escolher pastas.
/// </summary>
public class DialogoService : IDialogoService
{
    public event Func<string, Task<bool>>? AoConfirmar;

    public Task<bool> Confirmar(string mensagem) => AoConfirmar?.Invoke(mensagem) ?? Task.FromResult(false);

    public async Task<bool> SalvarBackup(string nomeSugerido, string conteudo)
    {
        var caminho = Path.Combine(FileSystem.CacheDirectory, nomeSugerido);
        await File.WriteAllTextAsync(caminho, conteudo);
        await MainThread.InvokeOnMainThreadAsync(() => Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Salvar backup do CrankoHub",
            File = new ShareFile(caminho, "application/json"),
        }));
        return true;
    }

    public Task<string?> AbrirBackup() => Escolher("Escolha o backup para importar",
        new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            // Alguns gerenciadores de arquivo não reconhecem .json: aceita texto e binário também
            [DevicePlatform.Android] = ["application/json", "text/plain", "application/octet-stream"],
        }));

    public Task<string?> AbrirImagem() => Escolher("Escolha a imagem da capa", FilePickerFileType.Images);

    public Task<string?> EscolherPasta(string titulo, string? pastaInicial) => Task.FromResult<string?>(null);

    private static async Task<string?> Escolher(string titulo, FilePickerFileType tipos)
    {
        // O MAUI copia o arquivo escolhido para o cache do app: FullPath é um caminho comum
        var r = await MainThread.InvokeOnMainThreadAsync(() =>
            FilePicker.Default.PickAsync(new PickOptions { PickerTitle = titulo, FileTypes = tipos }));
        return r?.FullPath;
    }
}
