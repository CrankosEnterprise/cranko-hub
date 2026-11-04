using CrankoHub.Services;

namespace CrankoHub.Movel;

/// <summary>Aplicativo MAUI: uma página só, com o Blazor ocupando a tela inteira.</summary>
public class App(IServiceProvider servicos) : Application
{
    private bool _iniciado;

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var janela = new Window(new PaginaPrincipal()) { Title = "CrankoHub" };
        janela.Created += (_, _) => Iniciar();
        return janela;
    }

    // Mesma sequência do App.xaml.cs (Windows) e do Program.cs (Linux), sem bandeja nem instância única
    private void Iniciar()
    {
        if (_iniciado) return;
        _iniciado = true;
        servicos.GetRequiredService<ArmazenamentoService>();
        servicos.GetRequiredService<AvisosService>().Iniciar();
        servicos.GetRequiredService<AtualizacaoService>().Iniciar();
        _ = servicos.GetRequiredService<ContaService>().Restaurar();
        servicos.GetRequiredService<SincronizacaoService>().Iniciar();
        _ = BandejaService.PedirPermissao();
    }
}

/// <summary>A tela do app: o BlazorWebView com o componente Main em #app.</summary>
public class PaginaPrincipal : ContentPage
{
    public PaginaPrincipal()
    {
        var web = new JanelaBlazor { HostPage = "wwwroot/index.html" };
        web.RootComponents.Add(new Microsoft.AspNetCore.Components.WebView.Maui.RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(Main),
        });
        Content = web;
    }
}
