using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Locations;
using Android.Media;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Color = Android.Graphics.Color;
using Location = Android.Locations.Location;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Aviso de radar do Modo Carro: uma pílula pequena por cima do Google Maps e do Waze, na borda esquerda (em cima, no meio
/// ou embaixo, engrenagem do Carro), com a velocidade do radar à frente e quantos metros faltam. Só aparece com um radar a
/// até <see cref="Configuracoes.AvisoRadarDistancia"/> metros e some depois de passar por ele (ou depois de
/// <see cref="Configuracoes.AvisoRadarTempo"/> segundos); no resto do tempo não cobre nada.
/// É uma janela do tipo "sobrepor a outros apps" dentro de um serviço em primeiro plano do tipo "localização" (o GPS segue
/// com o Maps/Waze na frente), com a notificação "Modo Carro ativo" e o botão Sair. Some enquanto o CrankoHub está na tela.
/// Liga e desliga pelo <see cref="ModoCarroService"/>; os radares vêm do <see cref="RadaresSp"/>.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeSpecialUse | ForegroundService.TypeLocation)]
public sealed class RadarCarroService : Service
{
    private const string Tag = "CrankoHub.Carro";
    private const string AcaoSair = "crankohub.carro.SAIR";
    private const string Canal = "modo-carro-ativo";
    private const int IdNotificacao = 9103;

    /// <summary>Abaixo disso (≈ 10 km/h) o rumo do GPS não vale: mantém o radar que já estava.</summary>
    private const float Parado = 2.8f;

    private ModoCarroService? _modo;
    private ArmazenamentoService? _store;
    private RadaresSp? _radares;
    private IWindowManager? _janelas;
    private LinearLayout? _pilula;
    private WindowManagerLayoutParams? _posicao;
    private string? _posicaoAtual;
    private TextView? _placa, _metros, _rotulo;
    private GradientDrawable? _fundo;
    private Handler? _principal;
    private LocationManager? _gps;
    private OuvinteGps? _ouvinte;
    private string? _avisado;
    private DateTime _avisadoEm;
    private double _kmhAgora;

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnCreate()
    {
        base.OnCreate();
        _principal = new Handler(Looper.MainLooper!);
        var servicos = IPlatformApplication.Current?.Services;
        _modo = servicos?.GetService<ModoCarroService>();
        _store = servicos?.GetService<ArmazenamentoService>();
        _radares = servicos?.GetService<RadaresSp>();
        if (_modo is not null) _modo.Mudou += Mudou;
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // O Android exige o primeiro plano logo depois do StartForegroundService, mesmo que o serviço vá parar em seguida
        PrimeiroPlano();
        if (intent?.Action == AcaoSair || _modo is not { Ativo: true })
        {
            _modo?.Sair();
            StopSelf();
            return StartCommandResult.NotSticky;
        }
        OuvirGps();
        Atualizar();
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        PararGps();
        if (_modo is not null)
        {
            _modo.Mudou -= Mudou;
            _modo.ComLocalizacao = false;
            _modo.Radar = null;
        }
        if (_pilula is not null)
            try { _janelas?.RemoveView(_pilula); } catch { }
        _pilula = null;
        base.OnDestroy();
    }

    // ===== Notificação "Modo Carro ativo" =====

    private void PrimeiroPlano()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var gerenciador = (NotificationManager)GetSystemService(NotificationService)!;
            gerenciador.CreateNotificationChannel(new NotificationChannel(Canal, "Modo Carro ativo", NotificationImportance.Low)
            {
                Description = "Fica enquanto o aviso de radar acompanha o Maps ou o Waze",
            });
        }

        var abrir = PackageManager!.GetLaunchIntentForPackage(PackageName!)!;
        abrir.SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        abrir.PutExtra(BandejaService.ExtraLink, "carro/modo");
        var toque = PendingIntent.GetActivity(this, IdNotificacao, abrir, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var sair = PendingIntent.GetService(this, IdNotificacao + 1, new Intent(this, typeof(RadarCarroService)).SetAction(AcaoSair),
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var gps = ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) == Permission.Granted;
        var aviso = new NotificationCompat.Builder(this, Canal)
            .SetSmallIcon(Resource.Drawable.ic_notificacao)!
            .SetColor(unchecked((int)0xFF5856D6))!
            .SetContentTitle("Modo Carro ativo")!
            .SetContentText(gps ? "Avisa dos radares por cima do Maps e do Waze. Toque para trocar o destino."
                : "Sem a localização liberada, o aviso de radar não funciona.")!
            .SetContentIntent(toque)!
            .SetOngoing(true)!
            .SetCategory(NotificationCompat.CategoryNavigation)!
            .AddAction(0, "Sair do Modo Carro", sair)!
            .Build()!;

        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(34))
            {
                // A localização só entra no tipo com a permissão dada (sem ela, o Android barra o serviço)
                StartForeground(IdNotificacao, aviso, ForegroundService.TypeSpecialUse | (gps ? ForegroundService.TypeLocation : 0));
            }
            else
            {
                StartForeground(IdNotificacao, aviso);
            }
            if (_modo is not null) _modo.ComLocalizacao = gps;
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Primeiro plano: {ex.Message}");
        }
    }

    // ===== GPS =====

    private void OuvirGps()
    {
        if (_ouvinte is not null || _modo is not { ComLocalizacao: true }) return;
        try
        {
            _gps = (LocationManager)GetSystemService(LocationService)!;
            _ouvinte = new OuvinteGps(this);
            _gps.RequestLocationUpdates(LocationManager.GpsProvider, 1000, 3, _ouvinte, Looper.MainLooper);
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"GPS: {ex.Message}");
            _ouvinte = null;
        }
    }

    private void PararGps()
    {
        if (_ouvinte is not null)
            try { _gps?.RemoveUpdates(_ouvinte); } catch { }
        _ouvinte = null;
    }

    private void Posicao(Location l)
    {
        if (_modo is null || _radares is null) return;
        _kmhAgora = l.HasSpeed ? l.Speed * 3.6 : 0;
        RadarProximo? radar;
        if (l.HasBearing && l.Speed >= Parado)
        {
            radar = _radares.Proximo(l.Latitude, l.Longitude, l.Bearing);
        }
        else
        {
            // Parado (semáforo, trânsito): o rumo não vale; só atualiza os metros do radar que já estava
            radar = _modo.Radar is { } r
                ? r with { Metros = RadaresSp.Distancia(l.Latitude, l.Longitude, r.Radar.Lat, r.Radar.Lon) }
                : null;
        }
        var cfg = _store?.Dados.Config;
        if (radar is not null && radar.Metros > Math.Clamp(cfg?.AvisoRadarDistancia ?? 1000, 100, (int)RadaresSp.Alcance)) radar = null;

        if (radar is not null && radar.Radar.Id != _avisado)
        {
            _avisado = radar.Radar.Id;
            _avisadoEm = DateTime.Now;
            if (cfg?.AvisoRadarSom != false) SomRadar.Tocar(cfg?.AvisoRadarTom);
            // Tempo fixo: some sozinha mesmo que o GPS não mande posição nova (parado no trânsito)
            if (cfg is { AvisoRadarTempo: > 0 } c) _principal?.PostDelayed(Atualizar, c.AvisoRadarTempo * 1000L + 100);
        }
        _modo.Radar = radar;
        Atualizar();
    }

    private sealed class OuvinteGps(RadarCarroService servico) : Java.Lang.Object, ILocationListener
    {
        public void OnLocationChanged(Location location) => servico.Posicao(location);

        public void OnProviderDisabled(string provider) { }

        public void OnProviderEnabled(string provider) { }

        public void OnStatusChanged(string? provider, [GeneratedEnum] Availability status, Bundle? extras) { }
    }

    // ===== Pílula =====

    private void Mudou() => _principal?.Post(Atualizar);

    private int Dp(float v) => (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, v, Resources!.DisplayMetrics);

    private string PosicaoEscolhida => PosicoesAvisoRadar.Valida(_store?.Dados.Config.AvisoRadarPosicao);

    private void Atualizar()
    {
        if (_modo is not { Ativo: true }) return;
        if (_pilula is null && !Criar()) return;

        var radar = _modo.Radar;
        var tempo = _store?.Dados.Config.AvisoRadarTempo ?? 0;
        var noTempo = tempo <= 0 || DateTime.Now - _avisadoEm < TimeSpan.FromSeconds(tempo);
        _pilula!.Visibility = radar is not null && noTempo && !_modo.AppNaTela ? ViewStates.Visible : ViewStates.Gone;
        // A posição mudou na engrenagem do Carro (enquanto o CrankoHub estava na tela)
        if (_posicaoAtual != PosicaoEscolhida) Posicionar();
        if (radar is null) return;

        var kmh = radar.Radar.Kmh;
        _placa!.Text = kmh?.ToString() ?? "?";
        _rotulo!.Text = radar.Radar.Lombada ? "Lombada" : "Radar";
        _metros!.Text = radar.Metros < 50 ? "agora" : $"{Math.Round(radar.Metros / 10) * 10:0} m";
        var acima = kmh is { } limite && _kmhAgora > limite + 2;
        _fundo!.SetColor(acima ? Color.Argb(245, 200, 30, 30) : Color.Argb(240, 22, 22, 24));
        _pilula.ContentDescription = $"{_rotulo.Text} de {(kmh is null ? "velocidade desconhecida" : $"{kmh} quilômetros por hora")} a {_metros.Text}";
    }

    /// <summary>Monta a pílula (só com a permissão de sobrepor a outros apps). false = sem a permissão ainda.</summary>
    private bool Criar()
    {
        if (!Settings.CanDrawOverlays(this)) return false;
        _janelas = GetSystemService(WindowService).JavaCast<IWindowManager>();
        if (_janelas is null) return false;

        var pilula = new LinearLayout(this) { Orientation = global::Android.Widget.Orientation.Horizontal };
        pilula.SetGravity(GravityFlags.CenterVertical);
        pilula.SetPadding(Dp(6), Dp(6), Dp(14), Dp(6));
        _fundo = new GradientDrawable();
        _fundo.SetColor(Color.Argb(240, 22, 22, 24));
        _fundo.SetCornerRadius(Dp(40));
        _fundo.SetStroke(Dp(1), Color.Argb(30, 255, 255, 255));
        pilula.Background = _fundo;
        pilula.Elevation = Dp(8);

        // Placa de velocidade: círculo branco com aro vermelho e o número no meio
        _placa = new TextView(this) { Gravity = GravityFlags.Center };
        _placa.SetTextSize(ComplexUnitType.Sp, 19);
        _placa.SetTextColor(Color.Black);
        _placa.SetTypeface(Typeface.DefaultBold, TypefaceStyle.Bold);
        var placa = new GradientDrawable();
        placa.SetShape(ShapeType.Oval);
        placa.SetColor(Color.White);
        placa.SetStroke(Dp(5), Color.Rgb(220, 30, 30));
        _placa.Background = placa;
        pilula.AddView(_placa, new LinearLayout.LayoutParams(Dp(52), Dp(52)));

        var textos = new LinearLayout(this) { Orientation = global::Android.Widget.Orientation.Vertical };
        textos.SetPadding(Dp(10), 0, 0, 0);
        var rotulo = _rotulo = new TextView(this) { Text = "Radar" };
        rotulo.SetTextSize(ComplexUnitType.Sp, 12);
        rotulo.SetTextColor(Color.Argb(190, 255, 255, 255));
        _metros = new TextView(this);
        _metros.SetTextSize(ComplexUnitType.Sp, 20);
        _metros.SetTextColor(Color.White);
        _metros.SetTypeface(Typeface.DefaultBold, TypefaceStyle.Bold);
        textos.AddView(rotulo);
        textos.AddView(_metros);
        pilula.AddView(textos);

        var tipo = OperatingSystem.IsAndroidVersionAtLeast(26) ? WindowManagerTypes.ApplicationOverlay : WindowManagerTypes.Phone;
        // Sem foco e sem toques: um toque sem querer na pílula vai para o Maps/Waze embaixo dela
        _posicao = new WindowManagerLayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, tipo,
            WindowManagerFlags.NotFocusable | WindowManagerFlags.NotTouchable, global::Android.Graphics.Format.Translucent);
        Medir(_posicao);
        pilula.Visibility = ViewStates.Gone;
        try
        {
            _janelas.AddView(pilula, _posicao);
            _pilula = pilula;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Aviso de radar: {ex.Message}");
            return false;
        }
    }

    /// <summary>Borda esquerda; em cima (abaixo da faixa da manobra), no meio ou embaixo (acima do tempo de chegada).</summary>
    private void Medir(WindowManagerLayoutParams p)
    {
        _posicaoAtual = PosicaoEscolhida;
        var vertical = _posicaoAtual switch
        {
            PosicoesAvisoRadar.Cima => GravityFlags.Top,
            PosicoesAvisoRadar.Baixo => GravityFlags.Bottom,
            _ => GravityFlags.CenterVertical,
        };
        p.Gravity = GravityFlags.Left | vertical;
        p.X = Dp(10);
        p.Y = _posicaoAtual switch
        {
            PosicoesAvisoRadar.Cima => Dp(150),
            PosicoesAvisoRadar.Baixo => Dp(130),
            _ => 0,
        };
    }

    private void Posicionar()
    {
        if (_pilula is null || _posicao is null || _janelas is null) return;
        Medir(_posicao);
        try { _janelas.UpdateViewLayout(_pilula, _posicao); } catch { }
    }
}

/// <summary>
/// Som do aviso de radar (<see cref="TonsAvisoRadar"/>). Os bipes são gerados aqui e tocam como "orientação de navegação"
/// (o mesmo canal da voz do Maps/Waze): saem no volume de mídia, pelo som do carro no Bluetooth, e só abaixam a música por
/// um instante (não pausam). O ToneGenerator no volume das notificações ficava mudo com o volume de notificação baixo,
/// no Não perturbe e em vários Xiaomi.
/// </summary>
public static class SomRadar
{
    private const int Taxa = 44100;

    /// <summary>Bipes: (frequência em Hz, duração em ms); frequência 0 = silêncio.</summary>
    private static (int Hz, int Ms)[] Bipes(string tom) => tom switch
    {
        TonsAvisoRadar.Curto => [(1000, 220)],
        TonsAvisoRadar.Alerta => [(1200, 160), (0, 70), (900, 160), (0, 70), (1200, 160), (0, 70), (900, 220)],
        _ => [(1000, 150), (0, 90), (1300, 180)],
    };

    private static AudioAttributes Atributos() => new AudioAttributes.Builder()
        .SetUsage(AudioUsageKind.AssistanceNavigationGuidance)!
        .SetContentType(AudioContentType.Sonification)!
        .Build()!;

    public static void Tocar(string? tom)
    {
        try
        {
            var escolhido = TonsAvisoRadar.Valido(tom);
            var ctx = global::Android.App.Application.Context;
            if (escolhido == TonsAvisoRadar.Notificacao)
            {
                var toque = RingtoneManager.GetRingtone(ctx, RingtoneManager.GetDefaultUri(RingtoneType.Notification));
                if (toque is null) return;
                toque.AudioAttributes = Atributos();
                toque.Play();
                return;
            }

            var amostras = Gerar(Bipes(escolhido));
            var atributos = Atributos();
            var trilha = new AudioTrack.Builder()
                .SetAudioAttributes(atributos)!
                .SetAudioFormat(new AudioFormat.Builder()
                    .SetEncoding(global::Android.Media.Encoding.Pcm16bit)!
                    .SetSampleRate(Taxa)!
                    .SetChannelMask(ChannelOut.Mono)!
                    .Build()!)!
                .SetBufferSizeInBytes(amostras.Length * 2)!
                .SetTransferMode(AudioTrackMode.Static)!
                .Build()!;
            trilha.Write(amostras, 0, amostras.Length);

            // Abaixa a música só durante o bipe (Android 8+); no 7 o bipe toca junto
            var audio = (AudioManager?)ctx.GetSystemService(Context.AudioService);
            AudioFocusRequestClass? foco = null;
            if (audio is not null && OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                foco = new AudioFocusRequestClass.Builder(AudioFocus.GainTransientMayDuck).SetAudioAttributes(atributos)!.Build();
                audio.RequestAudioFocus(foco!);
            }

            trilha.Play();
            var ms = amostras.Length * 1000L / Taxa;
            new Handler(Looper.MainLooper!).PostDelayed(() =>
            {
                try { trilha.Stop(); } catch { }
                trilha.Release();
                if (foco is not null && OperatingSystem.IsAndroidVersionAtLeast(26)) audio?.AbandonAudioFocusRequest(foco);
            }, ms + 300);
        }
        catch (Exception ex)
        {
            Log.Warn("CrankoHub.Carro", $"Som do radar: {ex.Message}");
        }
    }

    /// <summary>PCM 16 bits mono, com 6 ms de rampa em cada ponta do bipe (sem estalo).</summary>
    private static short[] Gerar((int Hz, int Ms)[] bipes)
    {
        var total = bipes.Sum(b => b.Ms * Taxa / 1000);
        var pcm = new short[total];
        var i = 0;
        var rampa = Taxa * 6 / 1000;
        foreach (var (hz, ms) in bipes)
        {
            var n = ms * Taxa / 1000;
            for (var k = 0; k < n; k++, i++)
            {
                if (hz == 0) continue;
                var envelope = Math.Min(1.0, Math.Min(k, n - 1 - k) / (double)rampa);
                pcm[i] = (short)(Math.Sin(2 * Math.PI * hz * k / Taxa) * envelope * 0.85 * short.MaxValue);
            }
        }
        return pcm;
    }
}
