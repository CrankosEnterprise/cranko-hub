using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CrankoHub.Components;

/// <summary>
/// Ponte com wwwroot/js/agenda.js (cliques e arrastos na grade de horas da Agenda).
/// O módulo é importado uma vez; a grade avisa a página pelos métodos [JSInvokable] dela (Clicou, Abriu, Soltou).
/// </summary>
public sealed class AgendaInterop(IJSRuntime js) : IAsyncDisposable
{
    internal const string Caminho = "./js/agenda.js";
    internal const string Iniciar = "iniciar";
    internal const string Rolar = "rolar";
    internal const string Descartar = "descartar";

    private IJSObjectReference? _modulo;

    private async ValueTask<IJSObjectReference> Modulo() =>
        _modulo ??= await js.InvokeAsync<IJSObjectReference>("import", Caminho);

    /// <summary>Liga os eventos na grade e rola até <paramref name="rolarMinutos"/> (minutos desde a meia-noite).</summary>
    public async ValueTask IniciarAsync<T>(ElementReference grade, DotNetObjectReference<T> pagina, int pxPorHora, int rolarMinutos) where T : class
    {
        var m = await Modulo();
        await m.InvokeVoidAsync(Iniciar, grade, pagina, pxPorHora, rolarMinutos);
    }

    public async ValueTask RolarAsync(int minutos)
    {
        if (_modulo is not null) await _modulo.InvokeVoidAsync(Rolar, minutos);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_modulo is not null)
            {
                await _modulo.InvokeVoidAsync(Descartar);
                await _modulo.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { } // a WebView já fechou
    }
}
