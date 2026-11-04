using Android.App;
using Android.Content;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace CrankoHub.Services;

/// <summary>
/// Versão Android: não há bandeja; só as notificações do sistema (enquanto o app está aberto).
/// Mantém a mesma API da versão Windows para as telas compilarem sem mudança.
/// </summary>
public sealed class BandejaService : IDisposable
{
    /// <summary>Extra do Intent com a página a abrir ao tocar na notificação (lido pela MainActivity).</summary>
    public const string ExtraLink = "crankohub.link";

    private const string Canal = "avisos";
    private static int _proximoId = 1;

#pragma warning disable CS0067 // eventos usados só pela bandeja do Windows
    public event Action<string?>? AoAbrir;
    public event Action? AoVerificarAvisos;
    public event Action? AoSair;
#pragma warning restore CS0067

    public void AplicarCor(string hex) { }

    public void Notificar(string titulo, string texto, string? link = null)
    {
        try
        {
            var ctx = global::Android.App.Application.Context;
            if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
                ContextCompat.CheckSelfPermission(ctx, global::Android.Manifest.Permission.PostNotifications) != global::Android.Content.PM.Permission.Granted)
                return;
            CriarCanal(ctx);

            var abrir = ctx.PackageManager!.GetLaunchIntentForPackage(ctx.PackageName!)!;
            abrir.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            if (link is not null) abrir.PutExtra(ExtraLink, link);
            var id = Interlocked.Increment(ref _proximoId);
            var toque = PendingIntent.GetActivity(ctx, id, abrir, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            var aviso = new NotificationCompat.Builder(ctx, Canal);
            aviso.SetSmallIcon(Resource.Mipmap.appicon);
            aviso.SetContentTitle(titulo);
            aviso.SetContentText(texto);
            aviso.SetStyle(new NotificationCompat.BigTextStyle().BigText(texto));
            aviso.SetContentIntent(toque);
            aviso.SetAutoCancel(true);
            NotificationManagerCompat.From(ctx)!.Notify(id, aviso.Build());
        }
        catch { } // sem permissão ou canal bloqueado: segue sem notificação
    }

    /// <summary>Android 13+: pede a permissão de notificações na primeira vez que o app abre.</summary>
    public static async Task PedirPermissao()
    {
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
                await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.PostNotifications>);
        }
        catch { }
    }

    private static void CriarCanal(Context ctx)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
        var gerenciador = (NotificationManager)ctx.GetSystemService(Context.NotificationService)!;
        if (gerenciador.GetNotificationChannel(Canal) is not null) return;
        gerenciador.CreateNotificationChannel(new NotificationChannel(Canal, "Avisos", NotificationImportance.Default)
        {
            Description = "Vencimentos de manutenção, documentos, assinaturas e tarefas",
        });
    }

    public void Dispose() { }
}
