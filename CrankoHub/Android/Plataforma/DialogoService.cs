namespace CrankoHub.Services;

/// <summary>Versão Android: seletor de arquivos do sistema (capa e anexos) e abertura pelo app padrão.</summary>
public class DialogoService : IDialogoService
{
    public event Func<string, Task<bool>>? AoConfirmar;

    public Task<bool> Confirmar(string mensagem) => AoConfirmar?.Invoke(mensagem) ?? Task.FromResult(false);

    public async Task<string?> AbrirImagem()
    {
        // O MAUI copia o arquivo escolhido para o cache do app: FullPath é um caminho comum
        var r = await MainThread.InvokeOnMainThreadAsync(() =>
            FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Escolha a imagem da capa", FileTypes = FilePickerFileType.Images }));
        return r?.FullPath;
    }

    public async Task<string[]> AbrirArquivos(string[]? extensoes = null)
    {
        // O seletor do Android filtra por tipo (mime), não por extensão
        var tipos = extensoes is null ? null : new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Android] = extensoes.Select(AnexosService.TipoMime).Distinct().ToArray(),
        });
        var r = await MainThread.InvokeOnMainThreadAsync(() =>
            FilePicker.Default.PickMultipleAsync(new PickOptions { PickerTitle = "Escolha os arquivos para anexar", FileTypes = tipos }));
        return r?.Where(f => f is not null).Select(f => f.FullPath).ToArray() ?? [];
    }

    public Task AbrirNoSistema(string caminho) =>
        MainThread.InvokeOnMainThreadAsync(() => Launcher.Default.OpenAsync(new OpenFileRequest(Path.GetFileName(caminho), new ReadOnlyFile(caminho))));
}
