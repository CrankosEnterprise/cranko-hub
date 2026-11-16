using CrankoHub.Linux;

namespace CrankoHub.Services;

/// <summary>
/// Versão Linux: ainda sem ícone na bandeja (fechar a janela encerra o app); só as notificações.
/// </summary>
public sealed class BandejaService : IBandejaService, IDisposable
{
    public void AplicarCor(string hex) { }

    public void Notificar(string titulo, string texto, string? link = null) => Notificacao.Enviar(titulo, texto);

    public void Dispose() { }
}
