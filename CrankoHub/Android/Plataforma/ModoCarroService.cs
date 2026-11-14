using Android.Content;
using Android.Util;
using AndroidX.Core.Content;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Modo Carro no Android: a navegação fica com o Google Maps ou o Waze, e o CrankoHub põe por cima deles um aviso pequeno
/// do próximo radar (velocidade e metros que faltam), que só aparece com um radar perto (<see cref="RadarCarroService"/>).
/// Os radares do estado de SP vêm de <see cref="RadaresSp"/>.
/// Mesma API de Services/Carro/ModoCarro/ModoCarroService.cs.
/// </summary>
public sealed class ModoCarroService(RadaresSp radares)
{
    private const string Tag = "CrankoHub.Carro";

    public bool Disponivel => true;

    /// <summary>O Modo Carro está ativo: o aviso de radar fica por cima do Maps/Waze até sair.</summary>
    public bool Ativo { get; private set; }

    /// <summary>O CrankoHub está na tela (o aviso some para não ficar por cima dele).</summary>
    public bool AppNaTela { get; private set; }

    /// <summary>
    /// O serviço do aviso está rodando com o tipo "localização": o Android deixa pegar o GPS com o app em segundo plano
    /// (o aviso de radar e o "onde estacionei" do Bluetooth do carro usam).
    /// </summary>
    public bool ComLocalizacao { get; internal set; }

    /// <summary>Radar à frente agora (null = nenhum perto), atualizado pelo GPS do <see cref="RadarCarroService"/>.</summary>
    public RadarProximo? Radar { get; internal set; }

    public event Action? Mudou;

    private static Context Ctx => global::Android.App.Application.Context;

    /// <summary>A MainActivity foi para a frente ou para trás (OnResume/OnPause).</summary>
    public void AppVisivel(bool visivel)
    {
        AppNaTela = visivel;
        Mudou?.Invoke();
    }

    internal void Avisar() => Mudou?.Invoke();

    // ===== Abrir os apps =====

    public bool AbrirApp(string app)
    {
        if (AppsCarro.Pacote(app) is not { } pacote || Ctx.PackageManager!.GetLaunchIntentForPackage(pacote) is not { } intent) return false;
        return Iniciar(intent);
    }

    public bool Navegar(string app, LugarBusca? destino)
    {
        if (AppsCarro.Pacote(app) is not { } pacote || Ctx.PackageManager!.GetLaunchIntentForPackage(pacote) is not { } inicio) return false;
        var intent = inicio;
        if (destino is not null)
        {
            var ll = FormattableString.Invariant($"{destino.Latitude},{destino.Longitude}");
            var link = app == AppsCarro.Waze ? $"waze://?ll={ll}&navigate=yes" : $"google.navigation:q={ll}";
            intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(link)).SetPackage(pacote);
        }
        Entrar();
        return Iniciar(intent);
    }

    private static bool Iniciar(Intent intent)
    {
        try
        {
            var activity = Platform.CurrentActivity;
            if (activity is null) intent.AddFlags(ActivityFlags.NewTask);
            (activity ?? Ctx).StartActivity(intent);
            return true;
        }
        catch (ActivityNotFoundException)
        {
            return false;
        }
    }

    // ===== Entrar / Sair =====

    private void Entrar()
    {
        Ativo = true;
        _ = radares.Atualizar(); // mais de 7 dias: baixa de novo em segundo plano
        try
        {
            ContextCompat.StartForegroundService(Ctx, new Intent(Ctx, typeof(RadarCarroService)));
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Aviso de radar: {ex.Message}");
        }
        Mudou?.Invoke();
    }

    public void Sair()
    {
        if (!Ativo) return;
        Ativo = false;
        Radar = null;
        try { Ctx.StopService(new Intent(Ctx, typeof(RadarCarroService))); } catch { }
        Mudou?.Invoke();
    }

    public async Task<PosicaoCarro?> PosicaoRapida()
    {
        try
        {
            if (await MainThread.InvokeOnMainThreadAsync(Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>) != PermissionStatus.Granted) return null;
            var l = await Geolocation.Default.GetLastKnownLocationAsync();
            return l is null ? null : new PosicaoCarro(l.Latitude, l.Longitude);
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Posição: {ex.Message}");
            return null;
        }
    }
}
