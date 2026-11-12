namespace CrankoHub.Services;

/// <summary>
/// Expõe para o Blazor as janelas nativas (confirmação, escolha de imagem e de arquivos) e a abertura de arquivos.
/// Os métodos são assíncronos porque no Android os seletores de arquivo não bloqueiam a tela.
/// </summary>
public interface IDialogoService
{
    /// <summary>
    /// Pergunta Sim/Não numa janela do próprio app (ver Components/Confirmacao.razor).
    /// A primeira linha é o título; o restante, a explicação. Retorna true se o usuário confirmar.
    /// </summary>
    Task<bool> Confirmar(string mensagem);

    /// <summary>Avisa o componente de confirmação que há uma pergunta para mostrar.</summary>
    event Func<string, Task<bool>>? AoConfirmar;

    /// <summary>Escolhe uma imagem (capa). Retorna null se cancelar.</summary>
    Task<string?> AbrirImagem();

    /// <summary>Escolhe um ou mais arquivos para anexar (só destas extensões, ex.: ".pdf"; null = qualquer tipo). Vazio se cancelar.</summary>
    Task<string[]> AbrirArquivos(string[]? extensoes = null);

    /// <summary>Abre o arquivo no programa padrão do sistema.</summary>
    Task AbrirNoSistema(string caminho);
}
