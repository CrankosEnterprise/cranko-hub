using System.Windows;
using Microsoft.Win32;

namespace CrankoHub.Services;

public class DialogoService : IDialogoService
{
    private const string FiltroJson = "Backup do CrankoHub (*.json)|*.json";

    public event Func<string, Task<bool>>? AoConfirmar;

    public Task<bool> Confirmar(string mensagem) => AoConfirmar?.Invoke(mensagem) ?? Task.FromResult(false);

    public async Task<bool> SalvarBackup(string nomeSugerido, string conteudo)
    {
        var dialogo = new SaveFileDialog
        {
            Title = "Salvar backup",
            Filter = FiltroJson,
            FileName = nomeSugerido,
            DefaultExt = ".json",
            OverwritePrompt = true,
        };
        if (await Exibir(() => dialogo.ShowDialog(Application.Current.MainWindow) == true ? dialogo.FileName : null) is not string caminho)
            return false;
        await File.WriteAllTextAsync(caminho, conteudo);
        return true;
    }

    public Task<string?> AbrirBackup()
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Escolha o backup para importar",
            Filter = FiltroJson,
            CheckFileExists = true,
        };
        return Exibir(() => dialogo.ShowDialog(Application.Current.MainWindow) == true ? dialogo.FileName : null);
    }

    public Task<string?> AbrirImagem()
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Escolha a imagem da capa",
            Filter = "Imagens (*.jpg;*.jpeg;*.png;*.webp;*.gif)|*.jpg;*.jpeg;*.png;*.webp;*.gif",
            CheckFileExists = true,
        };
        return Exibir(() => dialogo.ShowDialog(Application.Current.MainWindow) == true ? dialogo.FileName : null);
    }

    public Task<string?> EscolherPasta(string titulo, string? pastaInicial)
    {
        var dialogo = new OpenFolderDialog
        {
            Title = titulo,
            InitialDirectory = pastaInicial is not null && Directory.Exists(pastaInicial) ? pastaInicial : "",
        };
        return Exibir(() => dialogo.ShowDialog(Application.Current.MainWindow) == true ? dialogo.FolderName : null);
    }

    // Os diálogos do WPF precisam rodar na thread da interface.
    private static Task<string?> Exibir(Func<string?> abrir) =>
        Task.FromResult(Application.Current.Dispatcher.Invoke(abrir));
}
