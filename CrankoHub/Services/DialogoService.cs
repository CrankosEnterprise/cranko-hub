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

    // Os diálogos do WPF precisam rodar na thread da interface.
    private static Task<string?> Exibir(Func<string?> abrir) =>
        Task.FromResult(Application.Current.Dispatcher.Invoke(abrir));
}
