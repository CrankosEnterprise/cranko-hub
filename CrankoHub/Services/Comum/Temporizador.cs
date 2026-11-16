namespace CrankoHub.Services;

/// <summary>
/// Timer que dispara na thread de quem chamou <see cref="Iniciar"/> quando ela tem um contexto de sincronização
/// (thread da tela no Windows e no Android), ou numa thread do pool, um disparo por vez, quando não tem (Linux).
/// Substitui o DispatcherTimer do WPF no código compartilhado.
/// </summary>
public sealed class Temporizador(TimeSpan primeiro, TimeSpan intervalo, Action acao) : IDisposable
{
    private readonly Lock _trava = new();
    private Timer? _timer;

    public void Iniciar()
    {
        var contexto = SynchronizationContext.Current;
        _timer ??= new Timer(_ =>
        {
            if (contexto is not null) contexto.Post(_ => acao(), null);
            else lock (_trava) acao();
        }, null, primeiro, intervalo);
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
