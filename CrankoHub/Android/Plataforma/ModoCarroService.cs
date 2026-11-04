using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Media;
using Android.Media.Session;
using Android.Views;
using AndroidX.Core.App;
using CrankoHub.Models;
using CrankoHub.Movel;
using AndroidUri = Android.Net.Uri;
using Settings = Android.Provider.Settings;

namespace CrankoHub.Services;

/// <summary>
/// Modo Carro no Android: rota do Waze/Maps (pelas notificações, ver <see cref="NavNotificationListener"/>),
/// o player ativo (MediaSession, sem SDK do Spotify) e os atalhos. Mesma API de Services/ModoCarroService.cs.
/// </summary>
public sealed class ModoCarroService
{
    public bool Disponivel => true;

    public InstrucaoNavegacao? Instrucao { get; private set; }

    public MusicaAtual? Musica { get; private set; }

    public event Action? Mudou;

    private static Context Ctx => global::Android.App.Application.Context;

    private static ComponentName Ouvinte => new(Ctx, Java.Lang.Class.FromType(typeof(NavNotificationListener)));

    public bool TemAcessoNotificacoes =>
        NotificationManagerCompat.GetEnabledListenerPackages(Ctx)?.Contains(Ctx.PackageName!) == true;

    public void PedirAcessoNotificacoes() => Abrir(new Intent(Settings.ActionNotificationListenerSettings));

    public void AbrirInfoDoApp() =>
        Abrir(new Intent(Settings.ActionApplicationDetailsSettings, AndroidUri.Parse("package:" + Ctx.PackageName)));

    public bool Navegar(string destino, string app)
    {
        var q = AndroidUri.Encode(destino);
        var intent = app == AppsCarro.Maps
            ? new Intent(Intent.ActionView, AndroidUri.Parse($"google.navigation:q={q}"))
            : new Intent(Intent.ActionView, AndroidUri.Parse($"https://waze.com/ul?q={q}&navigate=yes"));
        // Com o pacote fixo, app não instalado dá erro (em vez de abrir o navegador)
        intent.SetPackage(AppsCarro.Pacote(app));
        return Abrir(intent);
    }

    public bool AbrirApp(string app) =>
        AppsCarro.Pacote(app) is { } pacote && Ctx.PackageManager!.GetLaunchIntentForPackage(pacote) is { } intent && Abrir(intent);

    private static bool Abrir(Intent intent)
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

    public void Entrar() => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (Platform.CurrentActivity is { } a)
        {
            a.Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
            a.RequestedOrientation = ScreenOrientation.SensorLandscape;
        }
        IniciarMidia();
    });

    public void Sair() => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (Platform.CurrentActivity is not { } a) return;
        a.Window?.ClearFlags(WindowManagerFlags.KeepScreenOn);
        a.RequestedOrientation = ScreenOrientation.Unspecified;
    });

    // ===== Navegação (chamado pelo NavNotificationListener) =====

    private string? _chaveNotificacao;

    internal void ReceberNotificacao(string chave, string pacote, string? titulo, string? texto, string? subTexto)
    {
        if (NavegacaoNotificacao.Ler(pacote, titulo, texto, subTexto) is not { } instrucao) return;
        _chaveNotificacao = chave;
        if (instrucao == Instrucao) return;
        Instrucao = instrucao;
        Mudou?.Invoke();
    }

    /// <summary>A notificação da rota sumiu: a navegação acabou.</summary>
    internal void NotificacaoRemovida(string chave)
    {
        if (chave != _chaveNotificacao || Instrucao is null) return;
        Instrucao = null;
        _chaveNotificacao = null;
        Mudou?.Invoke();
    }

    // ===== Player (MediaSession) =====

    private MediaSessionManager? _sessoes;
    private SessoesOuvinte? _ouvinteSessoes;
    private MediaController? _controle;
    private ControleCallback? _callback;
    private string? _capaDe, _capa;

    /// <summary>Começa a acompanhar o player. Só funciona com o acesso às notificações liberado. Chamar na thread principal.</summary>
    internal void IniciarMidia()
    {
        if (_sessoes is not null || !TemAcessoNotificacoes) return;
        try
        {
            var sessoes = (MediaSessionManager)Ctx.GetSystemService(Context.MediaSessionService)!;
            _ouvinteSessoes = new SessoesOuvinte(this);
            sessoes.AddOnActiveSessionsChangedListener(_ouvinteSessoes, Ouvinte);
            _sessoes = sessoes;
            EscolherSessao(sessoes.GetActiveSessions(Ouvinte));
        }
        catch (Java.Lang.SecurityException)
        {
            _sessoes = null; // acesso retirado no meio do caminho
        }
    }

    private void EscolherSessao(IList<MediaController>? lista)
    {
        // Prefere o que está tocando; senão o primeiro (o sistema ordena pelo uso mais recente)
        var nova = lista?.FirstOrDefault(c => c.PlaybackState?.State == PlaybackStateCode.Playing) ?? lista?.FirstOrDefault();
        if (nova?.SessionToken is not null && nova.SessionToken.Equals(_controle?.SessionToken))
        {
            AtualizarMusica();
            return;
        }
        if (_controle is not null && _callback is not null) _controle.UnregisterCallback(_callback);
        _controle = nova;
        if (_controle is not null)
        {
            _callback ??= new ControleCallback(this);
            _controle.RegisterCallback(_callback);
        }
        AtualizarMusica();
    }

    private void AtualizarMusica()
    {
        if (_controle is null)
        {
            if (Musica is null) return;
            Musica = null;
            Mudou?.Invoke();
            return;
        }

        var md = _controle.Metadata;
        var titulo = md?.GetString(MediaMetadata.MetadataKeyTitle) ?? md?.GetString(MediaMetadata.MetadataKeyDisplayTitle);
        var artista = md?.GetString(MediaMetadata.MetadataKeyArtist) ?? md?.GetString(MediaMetadata.MetadataKeyAlbumArtist);
        var tocando = _controle.PlaybackState?.State is PlaybackStateCode.Playing or PlaybackStateCode.Buffering;

        // A capa só é convertida quando a música muda
        var de = $"{_controle.PackageName}|{titulo}|{artista}";
        if (de != _capaDe)
        {
            _capaDe = de;
            _capa = Capa(md);
        }

        var musica = new MusicaAtual(titulo, artista, _capa, tocando, _controle.PackageName);
        if (musica == Musica) return;
        Musica = musica;
        Mudou?.Invoke();
    }

    /// <summary>Capa reduzida para 300 px, como data: URI JPEG.</summary>
    private static string? Capa(MediaMetadata? md)
    {
        try
        {
            var bmp = md?.GetBitmap(MediaMetadata.MetadataKeyAlbumArt) ?? md?.GetBitmap(MediaMetadata.MetadataKeyArt)
                ?? md?.GetBitmap(MediaMetadata.MetadataKeyDisplayIcon);
            if (bmp is null || bmp.Width <= 0) return null;
            var largura = Math.Min(300, bmp.Width);
            using var reduzida = Bitmap.CreateScaledBitmap(bmp, largura, Math.Max(1, bmp.Height * largura / bmp.Width), true);
            using var ms = new MemoryStream();
            reduzida.Compress(Bitmap.CompressFormat.Jpeg!, 80, ms);
            return "data:image/jpeg;base64," + Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return null;
        }
    }

    public void TocarPausar()
    {
        if (_controle?.GetTransportControls() is { } t)
        {
            if (Musica?.Tocando == true) t.Pause(); else t.Play();
        }
        else Tecla(Keycode.MediaPlayPause);
    }

    public void Proxima()
    {
        if (_controle?.GetTransportControls() is { } t) t.SkipToNext(); else Tecla(Keycode.MediaNext);
    }

    public void Anterior()
    {
        if (_controle?.GetTransportControls() is { } t) t.SkipToPrevious(); else Tecla(Keycode.MediaPrevious);
    }

    /// <summary>Sem acesso às sessões: manda a tecla de mídia para o player ativo (sem capa nem título).</summary>
    private static void Tecla(Keycode tecla)
    {
        var audio = (AudioManager)Ctx.GetSystemService(Context.AudioService)!;
        audio.DispatchMediaKeyEvent(new KeyEvent(KeyEventActions.Down, tecla));
        audio.DispatchMediaKeyEvent(new KeyEvent(KeyEventActions.Up, tecla));
    }

    private sealed class SessoesOuvinte(ModoCarroService modo) : Java.Lang.Object, MediaSessionManager.IOnActiveSessionsChangedListener
    {
        public void OnActiveSessionsChanged(IList<MediaController>? controllers) => modo.EscolherSessao(controllers);
    }

    private sealed class ControleCallback(ModoCarroService modo) : MediaController.Callback
    {
        public override void OnMetadataChanged(MediaMetadata? metadata) => modo.AtualizarMusica();

        public override void OnPlaybackStateChanged(PlaybackState? state) => modo.AtualizarMusica();

        public override void OnSessionDestroyed() => modo.EscolherSessao(modo._sessoes?.GetActiveSessions(Ouvinte));
    }
}
