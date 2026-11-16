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
        servicos.AddSingleton<CabecalhoService>();
        // Partes de plataforma (Android/Plataforma); as telas usam só as interfaces
        servicos.AddSingleton<IBandejaService, BandejaService>();
        servicos.AddSingleton<IAtualizacaoService, AtualizacaoService>();
        servicos.AddSingleton<ICofreSessao, CofreSessao>();
        servicos.AddSingleton<IInicializacaoSistema, InicializacaoAndroid>();
        // A MainActivity e o RadarCarroService pedem as classes concretas (membros só do Android)
        servicos.AddSingleton<ModoCarroService>();
        servicos.AddSingleton<IModoCarroService>(s => s.GetRequiredService<ModoCarroService>());
        servicos.AddSingleton<CarroBluetoothService>();
        servicos.AddSingleton<ICarroBluetoothService>(s => s.GetRequiredService<CarroBluetoothService>());
        servicos.AddSingleton<AvisosService>();
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // A Wikipedia/Wikidata bloqueia identificações genéricas: nome + endereço do projeto
        http.DefaultRequestHeaders.UserAgent.ParseAdd(EntretenimentoBusca.UserAgent);
        servicos.AddSingleton(http);
        servicos.AddSingleton<CapasService>();
        servicos.AddSingleton<AnexosService>();
        servicos.AddSingleton<EntretenimentoBusca>();
        servicos.AddSingleton<FipeService>();
        servicos.AddSingleton<CarrosNaWeb>();
        servicos.AddSingleton<SupabaseCliente>();
        servicos.AddSingleton<ContaService>();
        servicos.AddSingleton<SincronizacaoService>();
        servicos.AddSingleton<RadaresSp>();
        servicos.AddSingleton<BuscaLugares>();

        return builder.Build();
    }
}
