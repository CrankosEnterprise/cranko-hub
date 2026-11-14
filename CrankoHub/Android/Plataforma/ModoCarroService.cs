using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using Android.Service.Notification;
using Android.Util;
using Android.Views;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Modo Carro no Android: a navegação fica com o Google Maps ou o Waze, e o CrankoHub põe por cima deles uma barra com a
/// música que está tocando (<see cref="BarraCarroService"/>). A música vem de qualquer player pelas sessões de mídia do
/// Android, que só ficam visíveis com o "acesso às notificações" (<see cref="OuvinteMusica"/>); sem ele, só o Spotify, pela
/// "Transmissão do status do dispositivo".
/// Mesma API de Services/Carro/ModoCarro/ModoCarroService.cs.
/// </summary>
public sealed class ModoCarroService(HttpClient http)
{
    private const string Tag = "CrankoHub.Carro";

    public bool Disponivel => true;

    /// <summary>O Modo Carro está ativo: a barra da música fica por cima do Maps/Waze até sair.</summary>
    public bool Ativo { get; private set; }

    public MusicaAtual? Musica { get; private set; }

    /// <summary>Capa do que está tocando (para a barra nativa), ou null.</summary>
    public Bitmap? Capa { get; private set; }

    /// <summary>O CrankoHub está na tela (a barra some para não ficar por cima dele).</summary>
    public bool AppNaTela { get; private set; }

    /// <summary>
    /// O serviço da barra está rodando com o tipo "localização": o Android deixa pegar o GPS com o app em segundo plano
    /// (o Bluetooth do carro usa para guardar onde estacionou).
    /// </summary>
    public bool ComLocalizacao { get; internal set; }

    public event Action? Mudou;

    private static Context Ctx => global::Android.App.Application.Context;

    /// <summary>O CrankoHub tem o "acesso às notificações" (sem ele, o Android não mostra o que os outros apps tocam).</summary>
    public static bool TemAcessoMusica => NotificationManagerCompat.GetEnabledListenerPackages(Ctx)?.Contains(Ctx.PackageName!) == true;

    internal static ComponentName Ouvinte => new(Ctx, Java.Lang.Class.FromType(typeof(OuvinteMusica)));

    /// <summary>A MainActivity foi para a frente ou para trás (OnResume/OnPause).</summary>
    public void AppVisivel(bool visivel)
    {
        AppNaTela = visivel;
        // Voltou das telas do Android: o acesso às notificações pode ter acabado de ser liberado
        if (visivel) OuvirMusica();
        Mudou?.Invoke();
    }

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
        if (!Ativo)
        {
            Ativo = true;
            OuvirMusica();
        }
        try
        {
            ContextCompat.StartForegroundService(Ctx, new Intent(Ctx, typeof(BarraCarroService)));
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Barra: {ex.Message}");
        }
        Mudou?.Invoke();
    }

    public void Sair()
    {
        if (!Ativo) return;
        Ativo = false;
        PararMusica();
        try { Ctx.StopService(new Intent(Ctx, typeof(BarraCarroService))); } catch { }
        Musica = null;
        Capa = null;
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

    // ===== Música (sessões de mídia de qualquer player) =====

    private MediaSessionManager? _sessoes;
    private OuvinteSessoes? _ouvinteSessoes;
    private readonly List<(MediaController Controle, RetornoControle Retorno)> _controles = [];
    private MediaController? _atual;

    /// <summary>
    /// Passa a acompanhar os players (só com o Modo Carro ativo). Com o acesso às notificações, todos pelas sessões de mídia;
    /// sem ele (o Android pode barrar essa permissão em APK de fora da Play Store), só o Spotify pela transmissão dele.
    /// </summary>
    internal void OuvirMusica()
    {
        if (!Ativo || _ouvinteSessoes is not null) return;
        if (!TemAcessoMusica)
        {
            OuvirSpotify();
            return;
        }
        PararSpotify();
        try
        {
            _sessoes = (MediaSessionManager)Ctx.GetSystemService(Context.MediaSessionService)!;
            _ouvinteSessoes = new OuvinteSessoes(this);
            _sessoes.AddOnActiveSessionsChangedListener(_ouvinteSessoes, Ouvinte, new Handler(Looper.MainLooper!));
            SessoesMudaram(_sessoes.GetActiveSessions(Ouvinte));
        }
        catch (Java.Lang.SecurityException ex)
        {
            // O acesso foi tirado no meio do caminho
            Log.Warn(Tag, $"Sessões de mídia: {ex.Message}");
            _ouvinteSessoes = null;
            OuvirSpotify();
        }
    }

    private void PararMusica()
    {
        if (_sessoes is not null && _ouvinteSessoes is not null)
            try { _sessoes.RemoveOnActiveSessionsChangedListener(_ouvinteSessoes); } catch { }
        _ouvinteSessoes = null;
        Soltar();
        _atual = null;
        PararSpotify();
    }

    private void Soltar()
    {
        foreach (var (c, r) in _controles)
            try { c.UnregisterCallback(r); } catch { }
        _controles.Clear();
    }

    private void SessoesMudaram(IList<MediaController>? lista)
    {
        Soltar();
        foreach (var c in lista ?? [])
        {
            if (c.PackageName == Ctx.PackageName) continue;
            var r = new RetornoControle(this);
            c.RegisterCallback(r, new Handler(Looper.MainLooper!));
            _controles.Add((c, r));
        }
        Escolher();
    }

    /// <summary>
    /// O player que aparece na barra: o que está tocando; senão o que já estava; senão o primeiro
    /// (o Android entrega a lista na ordem de prioridade).
    /// </summary>
    private void Escolher()
    {
        var controles = _controles.Select(x => x.Controle).ToList();
        _atual = controles.FirstOrDefault(Tocando)
            ?? controles.FirstOrDefault(c => _atual is not null && c.SessionToken.Equals(_atual.SessionToken))
            ?? controles.FirstOrDefault();
        Ler(_atual);
    }

    private static bool Tocando(MediaController c) => c.PlaybackState?.State is PlaybackStateCode.Playing or PlaybackStateCode.Buffering;

    private void Ler(MediaController? c)
    {
        if (c?.Metadata is not { } m)
        {
            Musica = null;
            Capa = null;
            Mudou?.Invoke();
            return;
        }
        var titulo = m.GetString(MediaMetadata.MetadataKeyTitle) ?? m.GetString(MediaMetadata.MetadataKeyDisplayTitle);
        var artista = m.GetString(MediaMetadata.MetadataKeyArtist) ?? m.GetString(MediaMetadata.MetadataKeyAlbumArtist)
            ?? m.GetString(MediaMetadata.MetadataKeyDisplaySubtitle);
        Capa = m.GetBitmap(MediaMetadata.MetadataKeyAlbumArt) ?? m.GetBitmap(MediaMetadata.MetadataKeyArt) ?? m.GetBitmap(MediaMetadata.MetadataKeyDisplayIcon);
        Musica = new MusicaAtual(titulo, artista, Tocando(c), NomeApp(c.PackageName));
        Mudou?.Invoke();
    }

    /// <summary>Nome do app (null quando o Android não deixa ver o pacote).</summary>
    private static string? NomeApp(string? pacote)
    {
        if (pacote is null) return null;
        try
        {
            var pm = Ctx.PackageManager!;
            return pm.GetApplicationLabel(pm.GetApplicationInfo(pacote, 0));
        }
        catch
        {
            return null;
        }
    }

    private sealed class OuvinteSessoes(ModoCarroService modo) : Java.Lang.Object, MediaSessionManager.IOnActiveSessionsChangedListener
    {
        public void OnActiveSessionsChanged(IList<MediaController>? controllers) => modo.SessoesMudaram(controllers);
    }

    private sealed class RetornoControle(ModoCarroService modo) : MediaController.Callback
    {
        public override void OnMetadataChanged(MediaMetadata? metadata) => modo.Escolher();

        public override void OnPlaybackStateChanged(PlaybackState? state) => modo.Escolher();
    }

    // ===== Reserva sem o acesso às notificações: Spotify ("Transmissão do status do dispositivo") =====

    private ReceptorSpotify? _spotify;
    private string? _faixa;
    private readonly Dictionary<string, Bitmap?> _capas = [];

    private void OuvirSpotify()
    {
        if (_spotify is not null) return;
        _spotify = new ReceptorSpotify(this);
        var filtro = new IntentFilter();
        filtro.AddAction("com.spotify.music.metadatachanged");
        filtro.AddAction("com.spotify.music.playbackstatechanged");
        // Android 14+: transmissões de outro app exigem o receptor marcado como exportado
        ContextCompat.RegisterReceiver(Ctx, _spotify, filtro, ContextCompat.ReceiverExported);
    }

    private void PararSpotify()
    {
        if (_spotify is null) return;
        try { Ctx.UnregisterReceiver(_spotify); } catch { }
        _spotify = null;
    }

    private void RecebeuSpotify(Intent intent)
    {
        if (_spotify is null) return;
        var tocando = intent.HasExtra("playing") ? intent.GetBooleanExtra("playing", false) : Musica?.Tocando ?? false;
        if (intent.Action == "com.spotify.music.metadatachanged")
        {
            _faixa = intent.GetStringExtra("id");
            Capa = _faixa is not null && _capas.TryGetValue(_faixa, out var c) ? c : null;
            Musica = new MusicaAtual(intent.GetStringExtra("track"), intent.GetStringExtra("artist"), tocando, "Spotify");
            if (_faixa is not null && !_capas.ContainsKey(_faixa)) _ = BuscarCapa(_faixa);
        }
        else
        {
            Musica = (Musica ?? new MusicaAtual(null, null, tocando, "Spotify")) with { Tocando = tocando };
        }
        Mudou?.Invoke();
    }

    /// <summary>Capa pelo oEmbed público do Spotify (sem login): "spotify:track:ID" → thumbnail_url → imagem.</summary>
    private async Task BuscarCapa(string faixa)
    {
        Bitmap? capa = null;
        try
        {
            var id = faixa.Split(':').Last();
            using var json = System.Text.Json.JsonDocument.Parse(
                await http.GetStringAsync($"https://open.spotify.com/oembed?url={Uri.EscapeDataString($"https://open.spotify.com/track/{id}")}"));
            if (json.RootElement.TryGetProperty("thumbnail_url", out var t) && t.GetString() is { } url)
            {
                var bytes = await http.GetByteArrayAsync(url);
                capa = await BitmapFactory.DecodeByteArrayAsync(bytes, 0, bytes.Length);
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Capa: {ex.Message}");
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _capas[faixa] = capa;
            if (faixa != _faixa || _spotify is null || capa is null) return;
            Capa = capa;
            Mudou?.Invoke();
        });
    }

    private sealed class ReceptorSpotify(ModoCarroService modo) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent is not null) modo.RecebeuSpotify(intent);
        }
    }

    // ===== Controles =====

    public void TocarPausar()
    {
        if (_atual?.GetTransportControls() is { } t)
        {
            if (Tocando(_atual)) t.Pause(); else t.Play();
        }
        else Tecla(Keycode.MediaPlayPause);
    }

    public void Proxima()
    {
        if (_atual?.GetTransportControls() is { } t) t.SkipToNext(); else Tecla(Keycode.MediaNext);
    }

    public void Anterior()
    {
        if (_atual?.GetTransportControls() is { } t) t.SkipToPrevious(); else Tecla(Keycode.MediaPrevious);
    }

    /// <summary>Sem acesso às sessões: tecla de mídia, que vai para o player ativo.</summary>
    private static void Tecla(Keycode tecla)
    {
        var audio = (AudioManager)Ctx.GetSystemService(Context.AudioService)!;
        audio.DispatchMediaKeyEvent(new KeyEvent(KeyEventActions.Down, tecla));
        audio.DispatchMediaKeyEvent(new KeyEvent(KeyEventActions.Up, tecla));
    }
}

/// <summary>
/// "Acesso às notificações" do CrankoHub (Configurações → Modo Carro). Não lê nenhuma notificação: só existe porque o Android
/// exige um ouvinte de notificações liberado para mostrar as sessões de mídia dos outros apps (o que está tocando).
/// </summary>
[global::Android.App.Service(Exported = true, Label = "CrankoHub: música no Modo Carro", Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE")]
[global::Android.App.IntentFilter(["android.service.notification.NotificationListenerService"])]
public sealed class OuvinteMusica : NotificationListenerService
{
    public override void OnListenerConnected()
    {
        base.OnListenerConnected();
        MainThread.BeginInvokeOnMainThread(() => IPlatformApplication.Current?.Services.GetService<ModoCarroService>()?.OuvirMusica());
    }
}
