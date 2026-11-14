using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Widget;
using Color = Android.Graphics.Color;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace CrankoHub.Services;

/// <summary>
/// Barra da música do Modo Carro: uma faixa fixa (em cima ou embaixo, engrenagem do Carro) desenhada por cima do Google Maps
/// e do Waze, com a capa, o nome, o artista e ⏮ ⏯ ⏭. É uma janela do tipo "sobrepor a outros apps" (só a barra recebe toques;
/// o resto da tela continua com o app de baixo) dentro de um serviço em primeiro plano, com a notificação "Modo Carro ativo"
/// e o botão Sair. Some enquanto o CrankoHub está na tela. Liga e desliga pelo <see cref="ModoCarroService"/>.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeSpecialUse | ForegroundService.TypeLocation)]
public sealed class BarraCarroService : Service
{
    private const string Tag = "CrankoHub.Carro";
    private const string AcaoSair = "crankohub.carro.SAIR";
    private const string Canal = "modo-carro-ativo";
    private const int IdNotificacao = 9103;

    private ModoCarroService? _modo;
    private ArmazenamentoService? _store;
    private IWindowManager? _janelas;
    private LinearLayout? _barra;
    private WindowManagerLayoutParams? _posicao;
    private ImageView? _capa, _tocar;
    private TextView? _titulo, _artista;
    private Handler? _principal;

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnCreate()
    {
        base.OnCreate();
        _principal = new Handler(Looper.MainLooper!);
        _modo = IPlatformApplication.Current?.Services.GetService<ModoCarroService>();
        _store = IPlatformApplication.Current?.Services.GetService<ArmazenamentoService>();
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
        Atualizar();
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        if (_modo is not null)
        {
            _modo.Mudou -= Mudou;
            _modo.ComLocalizacao = false;
        }
        if (_barra is not null)
            try { _janelas?.RemoveView(_barra); } catch { }
        _barra = null;
        base.OnDestroy();
    }

    public override void OnConfigurationChanged(global::Android.Content.Res.Configuration? newConfig)
    {
        base.OnConfigurationChanged(newConfig!);
        Posicionar(); // virou o celular: a largura muda
    }

    // ===== Notificação "Modo Carro ativo" =====

    private void PrimeiroPlano()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var gerenciador = (NotificationManager)GetSystemService(NotificationService)!;
            if (gerenciador.GetNotificationChannel(Canal) is null)
                gerenciador.CreateNotificationChannel(new NotificationChannel(Canal, "Modo Carro ativo", NotificationImportance.Low)
                {
                    Description = "Fica enquanto a barra da música está por cima do Maps ou do Waze",
                });
        }

        var abrir = PackageManager!.GetLaunchIntentForPackage(PackageName!)!;
        abrir.SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        abrir.PutExtra(BandejaService.ExtraLink, "carro/modo");
        var toque = PendingIntent.GetActivity(this, IdNotificacao, abrir, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var sair = PendingIntent.GetService(this, IdNotificacao + 1, new Intent(this, typeof(BarraCarroService)).SetAction(AcaoSair),
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var aviso = new NotificationCompat.Builder(this, Canal)
            .SetSmallIcon(Resource.Drawable.ic_notificacao)!
            .SetColor(unchecked((int)0xFF5856D6))!
            .SetContentTitle("Modo Carro ativo")!
            .SetContentText("A barra da música fica por cima do Maps e do Waze. Toque para trocar o destino.")!
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
                var gps = ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) == Permission.Granted;
                StartForeground(IdNotificacao, aviso, ForegroundService.TypeSpecialUse | (gps ? ForegroundService.TypeLocation : 0));
                if (_modo is not null) _modo.ComLocalizacao = gps;
            }
            else
            {
                StartForeground(IdNotificacao, aviso);
                if (_modo is not null) _modo.ComLocalizacao = true;
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Primeiro plano: {ex.Message}");
        }
    }

    // ===== Barra =====

    private void Mudou() => _principal?.Post(Atualizar);

    private int Dp(float v) => (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, v, Resources!.DisplayMetrics);

    private bool EmCima => _store?.Dados.Config.BarraCarroEmCima == true;

    private void Atualizar()
    {
        if (_modo is not { Ativo: true }) return;
        if (_barra is null && !Criar()) return;

        _barra!.Visibility = _modo.AppNaTela ? ViewStates.Gone : ViewStates.Visible;
        // A borda mudou na engrenagem do Carro (enquanto o CrankoHub estava na tela)
        if (_posicao is not null && ((_posicao.Gravity & GravityFlags.Top) == GravityFlags.Top) != EmCima) Posicionar();

        var m = _modo.Musica;
        var acesso = ModoCarroService.TemAcessoMusica;
        _titulo!.Text = m?.Titulo ?? "Nada tocando";
        _artista!.Text = m is not null ? m.Artista ?? m.App ?? "" : acesso ? "Abra o Spotify ou outro player" : "Libere o acesso às notificações em Configurações → Modo Carro";
        if (_modo.Capa is { } capa) _capa!.SetImageBitmap(capa);
        else _capa!.SetImageResource(global::Android.Resource.Drawable.IcMediaPlay);
        _capa.SetScaleType(_modo.Capa is null ? ImageView.ScaleType.Center! : ImageView.ScaleType.CenterCrop!);
        _tocar!.SetImageResource(m?.Tocando == true ? global::Android.Resource.Drawable.IcMediaPause : global::Android.Resource.Drawable.IcMediaPlay);
    }

    /// <summary>Monta a barra (só com a permissão de sobrepor a outros apps). false = sem a permissão ainda.</summary>
    private bool Criar()
    {
        if (!Settings.CanDrawOverlays(this)) return false;
        _janelas = GetSystemService(WindowService).JavaCast<IWindowManager>();
        if (_janelas is null) return false;

        var barra = new LinearLayout(this) { Orientation = global::Android.Widget.Orientation.Horizontal };
        barra.SetGravity(GravityFlags.CenterVertical);
        barra.SetPadding(Dp(8), Dp(6), Dp(4), Dp(6));
        var fundo = new GradientDrawable();
        fundo.SetColor(Color.Argb(240, 22, 22, 24));
        fundo.SetCornerRadius(Dp(18));
        fundo.SetStroke(Dp(1), Color.Argb(30, 255, 255, 255));
        barra.Background = fundo;
        barra.Elevation = Dp(8);

        _capa = new ImageView(this);
        var fundoCapa = new GradientDrawable();
        fundoCapa.SetColor(Color.Argb(255, 44, 44, 48));
        fundoCapa.SetCornerRadius(Dp(10));
        _capa.Background = fundoCapa;
        _capa.ClipToOutline = true;
        _capa.Click += (_, _) => AbrirCrankoHub();
        barra.AddView(_capa, new LinearLayout.LayoutParams(Dp(46), Dp(46)));

        var textos = new LinearLayout(this) { Orientation = global::Android.Widget.Orientation.Vertical };
        textos.SetPadding(Dp(10), 0, Dp(4), 0);
        _titulo = Texto(15, Color.White, negrito: true);
        _artista = Texto(13, Color.Argb(170, 255, 255, 255), negrito: false);
        textos.AddView(_titulo);
        textos.AddView(_artista);
        textos.Click += (_, _) => AbrirCrankoHub();
        barra.AddView(textos, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

        barra.AddView(Botao(global::Android.Resource.Drawable.IcMediaPrevious, "Anterior", () => _modo?.Anterior()));
        _tocar = Botao(global::Android.Resource.Drawable.IcMediaPlay, "Tocar ou pausar", () => _modo?.TocarPausar());
        barra.AddView(_tocar);
        barra.AddView(Botao(global::Android.Resource.Drawable.IcMediaNext, "Próxima", () => _modo?.Proxima()));

        var tipo = OperatingSystem.IsAndroidVersionAtLeast(26) ? WindowManagerTypes.ApplicationOverlay : WindowManagerTypes.Phone;
        // Sem foco: o teclado e os toques fora da barra continuam indo para o Maps/Waze
        _posicao = new WindowManagerLayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, tipo,
            WindowManagerFlags.NotFocusable, Format.Translucent);
        Medir(_posicao);
        try
        {
            _janelas.AddView(barra, _posicao);
            _barra = barra;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Barra: {ex.Message}");
            return false;
        }
    }

    /// <summary>Largura (a tela menos as margens, até 560 dp) e borda (Configuracoes.BarraCarroEmCima).</summary>
    private void Medir(WindowManagerLayoutParams p)
    {
        p.Width = Math.Min(Resources!.DisplayMetrics!.WidthPixels - Dp(16), Dp(560));
        p.Gravity = (EmCima ? GravityFlags.Top : GravityFlags.Bottom) | GravityFlags.CenterHorizontal;
        p.Y = Dp(8);
    }

    private void Posicionar()
    {
        if (_barra is null || _posicao is null || _janelas is null) return;
        Medir(_posicao);
        try { _janelas.UpdateViewLayout(_barra, _posicao); } catch { }
    }

    private TextView Texto(float sp, Color cor, bool negrito)
    {
        var t = new TextView(this);
        t.SetTextSize(ComplexUnitType.Sp, sp);
        t.SetTextColor(cor);
        t.SetSingleLine(true);
        t.Ellipsize = TextUtils.TruncateAt.End;
        if (negrito) t.SetTypeface(Typeface.DefaultBold, TypefaceStyle.Bold);
        return t;
    }

    private ImageView Botao(int icone, string nome, Action acao)
    {
        var b = new ImageView(this) { ContentDescription = nome, Clickable = true, Focusable = false };
        b.SetImageResource(icone);
        b.SetColorFilter(Color.White);
        b.SetScaleType(ImageView.ScaleType.Center!);
        b.Click += (_, _) => acao();
        b.LayoutParameters = new LinearLayout.LayoutParams(Dp(48), Dp(48));
        return b;
    }

    /// <summary>Toque na capa ou no nome: volta para a tela do Modo Carro (trocar o destino ou o app).</summary>
    private void AbrirCrankoHub()
    {
        var abrir = PackageManager!.GetLaunchIntentForPackage(PackageName!)!;
        abrir.SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        abrir.PutExtra(BandejaService.ExtraLink, "carro/modo");
        try { StartActivity(abrir); }
        catch (Exception ex) { Log.Warn(Tag, $"Abrir: {ex.Message}"); }
    }
}
