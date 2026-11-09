using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CrankoHub.Components;

/// <summary>
/// Ponte com wwwroot/js/tarefas.js (arrastar cards entre colunas e reordenar os quadros).
/// O módulo é importado uma vez; ao soltar, ele chama os métodos [JSInvokable] da página (MoveuCard, MoveuQuadro).
/// </summary>
public sealed class TarefasInterop(IJSRuntime js) : IAsyncDisposable
{
    internal const string Caminho = "./js/tarefas.js";

    private IJSObjectReference? _modulo;

    /// <summary>Liga os eventos de arrasto em <paramref name="raiz"/> (o card que contém as abas e o quadro).</summary>
    public async ValueTask IniciarAsync<T>(ElementReference raiz, DotNetObjectReference<T> pagina) where T : class
    {
        _modulo ??= await js.InvokeAsync<IJSObjectReference>("import", Caminho);
        await _modulo.InvokeVoidAsync("iniciar", raiz, pagina);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_modulo is not null)
            {
                await _modulo.InvokeVoidAsync("descartar");
                await _modulo.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { } // a WebView já fechou
    }
}
