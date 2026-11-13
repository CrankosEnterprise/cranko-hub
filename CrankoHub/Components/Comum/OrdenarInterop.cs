using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CrankoHub.Components;

/// <summary>
/// Ponte com wwwroot/js/ordenar.js: lista reordenável arrastando pela alça ([data-alca]) de cada item ([data-id]).
/// Ao soltar, o JS chama o método [JSInvokable] <c>metodo(id, novoIndice)</c> do componente.
/// </summary>
public sealed class OrdenarInterop(IJSRuntime js) : IAsyncDisposable
{
    internal const string Caminho = "./js/ordenar.js";

    private IJSObjectReference? _modulo;
    private ElementReference? _raiz;

    public async ValueTask IniciarAsync<T>(ElementReference raiz, DotNetObjectReference<T> componente, string metodo) where T : class
    {
        _modulo ??= await js.InvokeAsync<IJSObjectReference>("import", Caminho);
        _raiz = raiz;
        await _modulo.InvokeVoidAsync("iniciar", raiz, componente, metodo);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_modulo is not null)
            {
                if (_raiz is { } raiz) await _modulo.InvokeVoidAsync("descartar", raiz);
                await _modulo.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { } // a WebView já fechou
    }
}
