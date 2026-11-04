using System.Text.Json;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.Util;
using Android.Views;
using AndroidX.Core.Content;
using AndroidX.Core.View;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Modo Carro no Android: GPS para o mapa, o que está tocando no Spotify (pela "Transmissão do status do dispositivo",
/// sem permissão especial) e os atalhos. Mesma API de Services/ModoCarroService.cs.
/// </summary>
public sealed class ModoCarroService(HttpClient http)
{
    private const string Log_ = "CrankoHub.Carro";

    public bool Disponivel => true;

    public PosicaoCarro? Posicao { get; private set; }

    public bool SemLocalizacao { get; private set; }

    public MusicaAtual? Musica { get; private set; }

    public event Action? Mudou;

    private static Context Ctx => global::Android.App.Application.Context;

    public bool AbrirApp(string app)
    {
        if (AppsCarro.Pacote(app) is not { } pacote || Ctx.PackageManager!.GetLaunchIntentForPackage(pacote) is not { } intent) return false;
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

    private bool _dentro;

    public async Task Entrar()
    {
        _dentro = true;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (Platform.CurrentActivity is { } a)
            {
                a.Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
                TelaCheia(a, true);
                a.RequestedOrientation = ScreenOrientation.SensorLandscape;
            }
            OuvirSpotify();
        });
        await LigarGps();
    }

    public void Sair()
    {
        _dentro = false;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Platform.CurrentActivity is { } a)
            {
                a.Window?.ClearFlags(WindowManagerFlags.KeepScreenOn);
                TelaCheia(a, false);
                a.RequestedOrientation = ScreenOrientation.Unspecified;
            }
            PararSpotify();
            DesligarGps();
        });
    }

    /// <summary>Esconde as barras do Android (status e navegação), como no Android Auto; deslizar da borda mostra de novo por um tempo.</summary>
    private static void TelaCheia(global::Android.App.Activity a, bool ligar)
    {
        if (a.Window is not { } janela || janela.DecorView is null) return;
        if (WindowCompat.GetInsetsController(janela, janela.DecorView) is not { } barras) return;
        if (ligar)
        {
            barras.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
            barras.Hide(WindowInsetsCompat.Type.SystemBars());
        }
        else barras.Show(WindowInsetsCompat.Type.SystemBars());
    }

    // ===== GPS =====

    private bool _ouvindoGps;

    /// <summary>Pede a permissão de localização (comum, sem bloqueio para app fora da Play Store) e liga o GPS.</summary>
    public async Task LigarGps()
    {
        try
        {
            var status = await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.LocationWhenInUse>);
            SemLocalizacao = status != PermissionStatus.Granted;
            Mudou?.Invoke();
            if (SemLocalizacao || _ouvindoGps || !_dentro) return;

            Geolocation.Default.LocationChanged += PosicaoMudou;
            _ouvindoGps = await MainThread.InvokeOnMainThreadAsync(() => Geolocation.Default.StartListeningForegroundAsync(
                new GeolocationListeningRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(1))));
            if (!_ouvindoGps) Geolocation.Default.LocationChanged -= PosicaoMudou;

            // Começa do último ponto conhecido, sem esperar o primeiro sinal
            if (Posicao is null && await Geolocation.Default.GetLastKnownLocationAsync() is { } ultima) Atualizar(ultima);
        }
        catch (Exception ex)
        {
            Log.Warn(Log_, $"GPS: {ex.Message}");
        }
    }

    private void DesligarGps()
    {
        if (!_ouvindoGps) return;
        _ouvindoGps = false;
        Geolocation.Default.LocationChanged -= PosicaoMudou;
        try { Geolocation.Default.StopListeningForeground(); } catch { }
    }

    private void PosicaoMudou(object? sender, GeolocationLocationChangedEventArgs e) => Atualizar(e.Location);

    private void Atualizar(Location l)
    {
        Posicao = new PosicaoCarro(l.Latitude, l.Longitude, l.Course, Math.Max(0, (l.Speed ?? 0) * 3.6));
        Mudou?.Invoke();
    }

    // ===== Spotify (Transmissão do status do dispositivo) =====

    private ReceptorSpotify? _receptor;
    private readonly Dictionary<string, string?> _capas = [];

    private void OuvirSpotify()
    {
        if (_receptor is not null) return;
        _receptor = new ReceptorSpotify(this);
        var filtro = new IntentFilter();
        filtro.AddAction("com.spotify.music.metadatachanged");
        filtro.AddAction("com.spotify.music.playbackstatechanged");
        // Android 14+: transmissões de outro app exigem o receptor marcado como exportado
        ContextCompat.RegisterReceiver(Ctx, _receptor, filtro, ContextCompat.ReceiverExported);
    }

    private void PararSpotify()
    {
        if (_receptor is null) return;
        try { Ctx.UnregisterReceiver(_receptor); } catch { }
        _receptor = null;
    }

    private string? _faixa;

    private void Recebeu(Intent intent)
    {
        var tocando = intent.HasExtra("playing") ? intent.GetBooleanExtra("playing", false) : Musica?.Tocando ?? false;
        Log.Debug(Log_, $"Spotify {intent.Action}: {intent.GetStringExtra("track")} / {intent.GetStringExtra("artist")} / tocando={tocando}");

        if (intent.Action == "com.spotify.music.metadatachanged")
        {
            _faixa = intent.GetStringExtra("id");
            var capa = _faixa is not null && _capas.TryGetValue(_faixa, out var c) ? c : null;
            Musica = new MusicaAtual(intent.GetStringExtra("track"), intent.GetStringExtra("artist"), capa, tocando);
            if (_faixa is not null && !_capas.ContainsKey(_faixa)) _ = BuscarCapa(_faixa);
        }
        else
        {
            Musica = (Musica ?? new MusicaAtual(null, null, null, tocando)) with { Tocando = tocando };
        }
        Mudou?.Invoke();
    }

    /// <summary>Capa pelo oEmbed público do Spotify (sem login): "spotify:track:ID" → thumbnail_url.</summary>
    private async Task BuscarCapa(string faixa)
    {
        string? url = null;
        try
        {
            var id = faixa.Split(':').Last();
            using var resp = await http.GetAsync($"https://open.spotify.com/oembed?url={Uri.EscapeDataString($"https://open.spotify.com/track/{id}")}");
            if (resp.IsSuccessStatusCode)
            {
                using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                if (json.RootElement.TryGetProperty("thumbnail_url", out var t)) url = t.GetString();
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Log_, $"Capa: {ex.Message}");
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _capas[faixa] = url;
            if (faixa != _faixa || Musica is null || url is null) return;
            Musica = Musica with { Capa = url };
            Mudou?.Invoke();
        });
    }

    private sealed class ReceptorSpotify(ModoCarroService modo) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent is not null) modo.Recebeu(intent);
        }
    }

    // ===== Voz (avisos da rota) =====

    private Microsoft.Maui.Media.Locale? _voz;
    private bool _procurouVoz;
    private CancellationTokenSource? _falando;

    /// <summary>Fala em pt-BR pela voz do sistema. Um aviso novo corta o que ainda estava sendo falado.</summary>
    public async Task Falar(string texto)
    {
        try
        {
            if (!_procurouVoz)
            {
                _procurouVoz = true;
                var vozes = await TextToSpeech.Default.GetLocalesAsync();
                _voz = vozes.FirstOrDefault(v => v.Language == "pt" && v.Country == "BR")
                    ?? vozes.FirstOrDefault(v => v.Language.StartsWith("pt", StringComparison.OrdinalIgnoreCase));
            }
            _falando?.Cancel();
            _falando = new CancellationTokenSource();
            await TextToSpeech.Default.SpeakAsync(texto, new SpeechOptions { Locale = _voz }, _falando.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Warn(Log_, $"Voz: {ex.Message}");
        }
    }

    // ===== Controles (teclas de mídia: vão para o player ativo, sem permissão) =====

    public void TocarPausar() => Tecla(Keycode.MediaPlayPause);

    public void Proxima() => Tecla(Keycode.MediaNext);

    public void Anterior() => Tecla(Keycode.MediaPrevious);

    private static void Tecla(Keycode tecla)
    {
        var audio = (AudioManager)Ctx.GetSystemService(Context.AudioService)!;
        audio.DispatchMediaKeyEvent(new KeyEvent(KeyEventActions.Down, tecla));
        audio.DispatchMediaKeyEvent(new KeyEvent(KeyEventActions.Up, tecla));
    }
}
