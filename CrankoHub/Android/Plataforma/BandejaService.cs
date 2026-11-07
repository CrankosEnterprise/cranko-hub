using Android.App;
using Android.Content;
using AndroidX.Core.App;

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

    // Pede a permissão (ou avisa que está bloqueada) no máximo uma vez por abertura do app
    private static bool _jaPediu;

    public void Notificar(string titulo, string texto, string? link = null)
    {
        var ctx = global::Android.App.Application.Context;
        if (!NotificationManagerCompat.From(ctx)!.AreNotificationsEnabled())
        {
            if (!_jaPediu)
            {
                _jaPediu = true;
                _ = PermitirENotificar(titulo, texto, link);
            }
            return;
        }
        Mostrar(ctx, titulo, texto, link);
    }

    /// <summary>
    /// O pedido feito ao abrir o app pode não aparecer (a tela ainda não estava pronta, ou o sistema bloqueia,
    /// como no MIUI). Então pede de novo na hora de notificar; se continuar bloqueado, diz onde ativar.
    /// </summary>
    private static async Task PermitirENotificar(string titulo, string texto, string? link)
    {
        var ctx = global::Android.App.Application.Context;
        await PedirPermissao();
        if (NotificationManagerCompat.From(ctx)!.AreNotificationsEnabled())
        {
            Mostrar(ctx, titulo, texto, link);
            return;
        }
        await MainThread.InvokeOnMainThreadAsync(() =>
            global::Android.Widget.Toast.MakeText(ctx, "As notificações do CrankoHub estão desativadas. Ative em Configurações do Android → Apps → CrankoHub → Notificações.", global::Android.Widget.ToastLength.Long)!.Show());
    }

    private static void Mostrar(Context ctx, string titulo, string texto, string? link)
    {
        try
        {
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

    private const string CanalCarro = "modo-carro";

    /// <summary>
    /// Aviso do Modo Carro (canal próprio, "Modo Carro"): o toque abre <paramref name="toque"/> (o Modo Carro, ou o Google Maps).
    /// O id fixo faz o aviso novo substituir o anterior e permite <see cref="Cancelar"/>.
    /// </summary>
    public static void MostrarCarro(int id, string titulo, string texto, Intent toque)
    {
        var ctx = global::Android.App.Application.Context;
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                var gerenciador = (NotificationManager)ctx.GetSystemService(Context.NotificationService)!;
                if (gerenciador.GetNotificationChannel(CanalCarro) is null)
                    gerenciador.CreateNotificationChannel(new NotificationChannel(CanalCarro, "Modo Carro", NotificationImportance.High)
                    {
                        Description = "Modo Carro pronto ao ligar o carro e onde o carro ficou estacionado",
                    });
            }
            var pendente = PendingIntent.GetActivity(ctx, id, toque, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            var aviso = new NotificationCompat.Builder(ctx, CanalCarro);
            aviso.SetSmallIcon(Resource.Mipmap.appicon);
            aviso.SetContentTitle(titulo);
            aviso.SetContentText(texto);
            aviso.SetStyle(new NotificationCompat.BigTextStyle().BigText(texto));
            aviso.SetContentIntent(pendente);
            aviso.SetAutoCancel(true);
            aviso.SetCategory(NotificationCompat.CategoryNavigation);
            NotificationManagerCompat.From(ctx)!.Notify(id, aviso.Build());
        }
        catch { } // sem permissão ou canal bloqueado: segue sem notificação
    }

    public static void Cancelar(int id)
    {
        try { NotificationManagerCompat.From(global::Android.App.Application.Context)!.Cancel(id); }
        catch { }
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
        gerenciador.CreateNotificationChannel(new NotificationChannel(Canal, "Avisos", NotificationImportance.High)
        {
            Description = "Vencimentos de manutenção, documentos, assinaturas e tarefas",
        });
    }

    public void Dispose() { }
}
