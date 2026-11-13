using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace CrankoHub.Services;

public class DialogoService : IDialogoService
{
    public event Func<string, Task<bool>>? AoConfirmar;

    public Task<bool> Confirmar(string mensagem) => AoConfirmar?.Invoke(mensagem) ?? Task.FromResult(false);

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

    public Task<string[]> AbrirArquivos(string[]? extensoes = null)
    {
        var padrao = extensoes is null ? "*.*" : string.Join(";", extensoes.Select(e => "*" + e));
        var dialogo = new OpenFileDialog
        {
            Title = "Escolha os arquivos para anexar",
            Filter = extensoes is null ? "Todos os arquivos (*.*)|*.*" : $"Arquivos aceitos ({padrao})|{padrao}",
            CheckFileExists = true,
            Multiselect = true,
        };
        return Exibir(() => dialogo.ShowDialog(Application.Current.MainWindow) == true ? dialogo.FileNames : []);
    }

    public Task AbrirNoSistema(string caminho)
    {
        Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    // Os diálogos do WPF precisam rodar na thread da interface.
    private static Task<T> Exibir<T>(Func<T> abrir) =>
        Task.FromResult(Application.Current.Dispatcher.Invoke(abrir));
}
