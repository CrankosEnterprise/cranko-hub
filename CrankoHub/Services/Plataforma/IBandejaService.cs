namespace CrankoHub.Services;

/// <summary>
/// Notificações do sistema e, no Windows, o ícone na bandeja. Cada plataforma tem a sua versão
/// (Services/Windows, Linux/Plataforma, Android/Plataforma), registrada no início do app.
/// </summary>
public interface IBandejaService
{
    /// <summary>Mostra uma notificação; tocar/clicar nela abre <paramref name="link"/> (null = só abre o app).</summary>
    void Notificar(string titulo, string texto, string? link = null);

    /// <summary>Usa a cor de destaque ("#RRGGBB") onde a plataforma desenha algo próprio (menu da bandeja do Windows).</summary>
    void AplicarCor(string hex);
}
