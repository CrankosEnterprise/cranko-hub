using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using CrankoHub.Services;

namespace CrankoHub.Movel;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        AbrirLink(Intent);
    }

    // Toque numa notificação com o app já aberto
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        AbrirLink(intent);
    }

    private static void AbrirLink(Intent? intent)
    {
        if (intent?.GetStringExtra(BandejaService.ExtraLink) is not string link) return;
        intent.RemoveExtra(BandejaService.ExtraLink);
        // Espera o Blazor montar a tela antes de navegar
        _ = Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(_ =>
            MainThread.BeginInvokeOnMainThread(() =>
                IPlatformApplication.Current?.Services.GetService<NavegacaoService>()?.Navegar(link)));
    }
}
