using Android.App;
using Android.Service.Notification;
using Android.Util;
using CrankoHub.Models;
using CrankoHub.Services;

namespace CrankoHub.Movel;

/// <summary>
/// Lê as notificações de navegação do Waze e do Maps para o Modo Carro (próxima curva).
/// O acesso é liberado pelo usuário em Configurações do Android → Acesso a notificações.
/// O mesmo acesso libera a leitura do player ativo (MediaSessionManager) no <see cref="ModoCarroService"/>.
/// </summary>
[Service(Label = "CrankoHub · Modo carro", Exported = true, Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE")]
[IntentFilter(["android.service.notification.NotificationListenerService"])]
public class NavNotificationListener : NotificationListenerService
{
    private static ModoCarroService? Modo => IPlatformApplication.Current?.Services.GetService<ModoCarroService>();

    public override void OnListenerConnected()
    {
        base.OnListenerConnected();
        Modo?.IniciarMidia();
        try
        {
            foreach (var n in GetActiveNotifications() ?? []) Tratar(n);
        }
        catch { } // o sistema pode recusar logo depois de liberar o acesso
    }

    public override void OnNotificationPosted(StatusBarNotification? sbn) => Tratar(sbn);

    public override void OnNotificationRemoved(StatusBarNotification? sbn)
    {
        if (sbn?.Key is { } chave) Modo?.NotificacaoRemovida(chave);
    }

    private static void Tratar(StatusBarNotification? sbn)
    {
        if (sbn?.PackageName is not { } pacote || sbn.Key is not { } chave) return;
        if (AppsCarro.DoPacote(pacote) is not (AppsCarro.Maps or AppsCarro.Waze)) return;

        var extras = sbn.Notification?.Extras;
        var titulo = extras?.GetCharSequence(Notification.ExtraTitle)?.ToString();
        var texto = extras?.GetCharSequence(Notification.ExtraText)?.ToString();
        var sub = extras?.GetCharSequence(Notification.ExtraSubText)?.ToString();
        // Para conferir os textos reais: adb logcat -s CrankoHub.Carro
        Log.Debug("CrankoHub.Carro", $"{pacote} | título: {titulo} | texto: {texto} | sub: {sub}");

        Modo?.ReceberNotificacao(chave, pacote, titulo, texto, sub);
    }
}
