namespace CrankoHub.Services;

/// <summary>
/// Expõe para o Blazor as janelas nativas (confirmação, abrir/salvar arquivo).
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

    /// <summary>
    /// Pergunta onde salvar o backup .json e grava o conteúdo (no Android, abre o compartilhamento).
    /// Retorna false se o usuário cancelar.
    /// </summary>
    Task<bool> SalvarBackup(string nomeSugerido, string conteudo);

    /// <summary>Escolhe um backup .json para importar. Retorna null se cancelar.</summary>
    Task<string?> AbrirBackup();

    /// <summary>Escolhe uma imagem (capa). Retorna null se cancelar.</summary>
    Task<string?> AbrirImagem();

    /// <summary>Escolhe uma pasta. Retorna null se cancelar (ou se a plataforma não tiver pastas, como o Android).</summary>
    Task<string?> EscolherPasta(string titulo, string? pastaInicial);
}
