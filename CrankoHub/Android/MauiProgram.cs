using CrankoHub.Services;

namespace CrankoHub.Movel;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Capas do Entretenimento: servidas em "capas/" junto com o wwwroot (ver JanelaBlazor)
        Directory.CreateDirectory(CapasService.Pasta);
        CapasService.Prefixo = "capas/";

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        var servicos = builder.Services;
        servicos.AddMauiBlazorWebView();
#if DEBUG
        servicos.AddBlazorWebViewDeveloperTools();
#endif
        servicos.AddSingleton<IDialogoService, DialogoService>();
        servicos.AddSingleton<ToastService>();
        servicos.AddSingleton<ArmazenamentoService>();
        servicos.AddSingleton<NavegacaoService>();
        servicos.AddSingleton<JanelaService>();
        servicos.AddSingleton<BandejaService>();
        servicos.AddSingleton<AvisosService>();
        servicos.AddSingleton<AtualizacaoService>();
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // A Wikipedia/Wikidata bloqueia identificações genéricas: nome + endereço do projeto
        http.DefaultRequestHeaders.UserAgent.ParseAdd(EntretenimentoBusca.UserAgent);
        servicos.AddSingleton(http);
        servicos.AddSingleton<CapasService>();
        servicos.AddSingleton<EntretenimentoBusca>();
        servicos.AddSingleton<SupabaseCliente>();
        servicos.AddSingleton<ContaService>();
        servicos.AddSingleton<SincronizacaoService>();
        servicos.AddSingleton<ModoCarroService>();

        return builder.Build();
    }
}
