using Android;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Android.Util;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Bluetooth do carro (Configurações → Modo Carro): reconhece a central marcada como "o meu carro" e
/// <list type="bullet">
/// <item>ao conectar, avisa por notificação ou abre o Modo Carro (<see cref="Configuracoes.AoConectarCarro"/>);</item>
/// <item>ao desconectar, sai do Modo Carro (<see cref="Configuracoes.SairAoDesconectar"/>) e guarda onde o carro ficou
/// (<see cref="Configuracoes.LembrarEstacionado"/>), com notificação e card no Início.</item>
/// </list>
/// Os avisos chegam pelo <see cref="ReceptorBluetooth"/> (no manifesto: funciona com o app fechado). No MIUI, só com o
/// "Início automático" ligado. Tudo que o Android precisa liberar aparece em <see cref="Permissoes"/>, com o botão para ativar.
/// Mesma API de Services/CarroBluetoothService.cs.
/// </summary>
public sealed class CarroBluetoothService(ArmazenamentoService store, ModoCarroService modo, NavegacaoService nav)
{
    private const string Tag = "CrankoHub.Bluetooth";
    private const string Maps = "com.google.android.apps.maps";

    /// <summary>Ids fixos: a notificação nova substitui a antiga, e dá para cancelar.</summary>
    private const int IdPronto = 9101, IdEstacionado = 9102;

    public bool Disponivel => true;

    /// <summary>Mudou o carro estacionado ou o estado de uma permissão (o app voltou para a frente).</summary>
    public event Action? Mudou;

    private static Context Ctx => global::Android.App.Application.Context;

    private static string Pacote => Ctx.PackageName!;

    /// <summary>O app está na tela (MainActivity entre OnResume e OnPause).</summary>
    public bool Visivel { get; private set; }

    /// <summary>A MainActivity voltou para a frente: a pessoa pode ter ativado algo nas telas do Android.</summary>
    public void Voltou()
    {
        Visivel = true;
        Mudou?.Invoke();
    }

    public void Saiu() => Visivel = false;

    // ===== Permissões =====

    private static bool Tem(string permissao) => ContextCompat.CheckSelfPermission(Ctx, permissao) == Permission.Granted;

    private static bool TemLocalizacao => Tem(Manifest.Permission.AccessFineLocation);

    private static bool TemLocalizacaoSempre => TemLocalizacao && (!OperatingSystem.IsAndroidVersionAtLeast(29) || Tem(Manifest.Permission.AccessBackgroundLocation));

    private static bool TemBluetooth => !OperatingSystem.IsAndroidVersionAtLeast(31) || Tem(Manifest.Permission.BluetoothConnect);

    private static bool TemNotificacoes => NotificationManagerCompat.From(Ctx)!.AreNotificationsEnabled();

    private static bool PodeSobrepor => Settings.CanDrawOverlays(Ctx);

    private static bool SemRestricaoBateria => ((PowerManager)Ctx.GetSystemService(Context.PowerService)!).IsIgnoringBatteryOptimizations(Pacote);

    /// <summary>Xiaomi, Redmi e POCO (MIUI/HyperOS): têm as permissões extras de Início automático e pop-up em segundo plano.</summary>
    private static bool EhXiaomi => Build.Manufacturer?.ToLowerInvariant() is "xiaomi" or "redmi" or "poco";

    public IReadOnlyList<PermissaoCarro> Permissoes()
    {
        var cfg = store.Dados.Config;
        var temCarro = cfg.CarrosBluetooth.Count > 0;
        var lista = new List<PermissaoCarro>
        {
            new(PermissoesCarro.Localizacao, "Localização", "Mapa, rota e onde o carro estacionou.", TemLocalizacao, true),
            new(PermissoesCarro.Bluetooth, "Dispositivos próximos (Bluetooth)", "Reconhecer a central do carro e listar os aparelhos pareados.", TemBluetooth, true),
            new(PermissoesCarro.Notificacoes, "Notificações", "\"Modo Carro pronto\" ao ligar o carro e \"Carro estacionado\" ao desligar.", TemNotificacoes,
                temCarro && (cfg.AoConectarCarro != AcoesConexao.Nada || cfg.LembrarEstacionado)),
            new(PermissoesCarro.Sobrepor, "Sobrepor a outros apps", "Abrir o Modo Carro sozinho quando o carro conecta (opção \"Abrir direto\").", PodeSobrepor,
                cfg.AoConectarCarro == AcoesConexao.Abrir),
            new(PermissoesCarro.LocalizacaoSempre, "Localização o tempo todo", "Guardar onde estacionou mesmo com o Modo Carro fechado.", TemLocalizacaoSempre,
                cfg.LembrarEstacionado && cfg.EstacionadoComAppFechado),
            new(PermissoesCarro.Bateria, "Sem restrição de bateria", "O Android não atrasa o aviso de que o carro conectou ou desconectou.", SemRestricaoBateria, temCarro),
        };
        if (EhXiaomi)
        {
            lista.Add(new(PermissoesCarro.MiuiInicio, "Início automático (Xiaomi)", "Sem ele, o MIUI não deixa o CrankoHub saber do carro com o app fechado. Confira se está ligado.", null, temCarro));
            lista.Add(new(PermissoesCarro.MiuiPopup, "Janelas pop-up em segundo plano (Xiaomi)", "Necessário para \"Abrir direto\". Confira em Outras permissões.", null,
                cfg.AoConectarCarro == AcoesConexao.Abrir));
        }
        return lista;
    }

    public async Task Ativar(string permissao)
    {
        try
        {
            switch (permissao)
            {
                case PermissoesCarro.Localizacao:
                    if (await Pedir<Permissions.LocationWhenInUse>() != PermissionStatus.Granted) AbrirDetalhesApp();
                    break;
                case PermissoesCarro.LocalizacaoSempre:
                    // O Android só oferece "o tempo todo" depois do "enquanto usa"; no 11+ ele leva à tela de permissões
                    if (await Pedir<Permissions.LocationWhenInUse>() == PermissionStatus.Granted
                        && await Pedir<Permissions.LocationAlways>() != PermissionStatus.Granted && !TemLocalizacaoSempre)
                        AbrirDetalhesApp();
                    break;
                case PermissoesCarro.Bluetooth:
                    if (await Pedir<PermissaoBluetooth>() != PermissionStatus.Granted) AbrirDetalhesApp();
                    break;
                case PermissoesCarro.Notificacoes:
                    await BandejaService.PedirPermissao();
                    if (!TemNotificacoes)
                    {
                        if (OperatingSystem.IsAndroidVersionAtLeast(26))
                            Abrir(new Intent(Settings.ActionAppNotificationSettings).PutExtra(Settings.ExtraAppPackage, Pacote));
                        else
                            AbrirDetalhesApp();
                    }
                    break;
                case PermissoesCarro.Sobrepor:
                    Abrir(new Intent(Settings.ActionManageOverlayPermission, global::Android.Net.Uri.Parse($"package:{Pacote}")));
                    break;
                case PermissoesCarro.Bateria:
                    Abrir(new Intent(Settings.ActionRequestIgnoreBatteryOptimizations, global::Android.Net.Uri.Parse($"package:{Pacote}")));
                    break;
                case PermissoesCarro.MiuiInicio:
                    if (!Abrir(new Intent().SetClassName("com.miui.securitycenter", "com.miui.permcenter.autostart.AutoStartManagementActivity")))
                        AbrirDetalhesApp();
                    break;
                case PermissoesCarro.MiuiPopup:
                    var editor = new Intent("miui.intent.action.APP_PERM_EDITOR")
                        .SetClassName("com.miui.securitycenter", "com.miui.permcenter.permissions.PermissionsEditorActivity")
                        .PutExtra("extra_pkgname", Pacote);
                    if (!Abrir(editor)) AbrirDetalhesApp();
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Ativar {permissao}: {ex.Message}");
            AbrirDetalhesApp();
        }
        Mudou?.Invoke();
    }

    private static Task<PermissionStatus> Pedir<T>() where T : Permissions.BasePermission, new() =>
        MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<T>);

    /// <summary>Android 12+: só "Dispositivos próximos → conectar" (o nome e o endereço dos aparelhos). Não precisa procurar aparelhos.</summary>
    private sealed class PermissaoBluetooth : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            OperatingSystem.IsAndroidVersionAtLeast(31) ? [(Manifest.Permission.BluetoothConnect, true)] : [];
    }

    private static void AbrirDetalhesApp() =>
        Abrir(new Intent(Settings.ActionApplicationDetailsSettings, global::Android.Net.Uri.Parse($"package:{Pacote}")));

    /// <summary>Abre uma tela do Android (ou de outro app). false = ela não existe neste aparelho.</summary>
    private static bool Abrir(Intent intent)
    {
        try
        {
            var activity = Platform.CurrentActivity;
            if (activity is null) intent.AddFlags(ActivityFlags.NewTask);
            (activity ?? Ctx).StartActivity(intent);
            return true;
        }
        catch (Exception ex) when (ex is ActivityNotFoundException or Java.Lang.SecurityException)
        {
            return false;
        }
    }

    // ===== Aparelhos pareados =====

    public IReadOnlyList<AparelhoBluetooth> Pareados()
    {
        if (!TemBluetooth) return [];
        try
        {
            var adaptador = ((BluetoothManager)Ctx.GetSystemService(Context.BluetoothService)!).Adapter;
            if (adaptador?.BondedDevices is not { } pareados) return [];
            return pareados
                .Where(d => d.Address is not null)
                .Select(d => new AparelhoBluetooth(d.Address!, d.Name ?? d.Address!, d.BluetoothClass?.MajorDeviceClass == MajorDeviceClass.AudioVideo))
                .OrderByDescending(a => a.Audio).ThenBy(a => a.Nome, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Pareados: {ex.Message}");
            return [];
        }
    }

    // ===== Conectou / desconectou (ReceptorBluetooth) =====

    private string? _conectado;
    private ReceptorDesbloqueio? _esperandoDesbloqueio;

    private bool EhCarro(string endereco) =>
        store.Dados.Config.CarrosBluetooth.Any(c => string.Equals(c.Endereco, endereco, StringComparison.OrdinalIgnoreCase));

    /// <summary>Chamado pelo <see cref="ReceptorBluetooth"/> (fora da thread principal, dentro do goAsync).</summary>
    public async Task Recebeu(bool conectou, string endereco)
    {
        if (!EhCarro(endereco)) return;
        Log.Info(Tag, $"Carro {(conectou ? "conectou" : "desconectou")}: {endereco}");
        if (conectou) await MainThread.InvokeOnMainThreadAsync(() => Conectou(endereco));
        else await Desconectou(endereco);
    }

    private void Conectou(string endereco)
    {
        _conectado = endereco;
        var cfg = store.Dados.Config;

        // Voltou ao carro: o estacionado já não serve
        if (cfg.Estacionado is not null)
        {
            cfg.Estacionado = null;
            store.Salvar();
            BandejaService.Cancelar(IdEstacionado);
            Mudou?.Invoke();
        }

        if (modo.Dentro || cfg.AoConectarCarro == AcoesConexao.Nada) return;

        if (cfg.AoConectarCarro == AcoesConexao.Abrir && PodeSobrepor)
        {
            // Bloqueado: não abre por cima da tela de bloqueio; espera desbloquear (a notificação fica de reserva)
            var teclado = (KeyguardManager)Ctx.GetSystemService(Context.KeyguardService)!;
            if (!teclado.IsKeyguardLocked)
            {
                AbrirModoCarro();
                return;
            }
            EsperarDesbloqueio();
        }
        BandejaService.MostrarCarro(IdPronto, "Modo Carro pronto", "O celular conectou no carro. Toque para abrir o Modo Carro.", IntentModoCarro());
    }

    private static Intent IntentModoCarro()
    {
        var abrir = Ctx.PackageManager!.GetLaunchIntentForPackage(Pacote)!;
        abrir.SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        abrir.PutExtra(BandejaService.ExtraLink, "carro/modo");
        return abrir;
    }

    private void AbrirModoCarro()
    {
        BandejaService.Cancelar(IdPronto);
        if (modo.Dentro) return;
        try
        {
            Ctx.StartActivity(IntentModoCarro());
        }
        catch (Exception ex)
        {
            // O sistema barrou (ex.: MIUI sem "pop-up em segundo plano"): fica a notificação
            Log.Warn(Tag, $"Abrir direto: {ex.Message}");
            BandejaService.MostrarCarro(IdPronto, "Modo Carro pronto", "O celular conectou no carro. Toque para abrir o Modo Carro.", IntentModoCarro());
        }
    }

    private void EsperarDesbloqueio()
    {
        if (_esperandoDesbloqueio is not null) return;
        _esperandoDesbloqueio = new ReceptorDesbloqueio(this);
        ContextCompat.RegisterReceiver(Ctx, _esperandoDesbloqueio, new IntentFilter(Intent.ActionUserPresent), ContextCompat.ReceiverNotExported);
    }

    private void Desbloqueou()
    {
        PararDeEsperar();
        // Só se o carro continua conectado e a opção não mudou
        if (_conectado is not null && store.Dados.Config.AoConectarCarro == AcoesConexao.Abrir) AbrirModoCarro();
    }

    private void PararDeEsperar()
    {
        if (_esperandoDesbloqueio is null) return;
        try { Ctx.UnregisterReceiver(_esperandoDesbloqueio); } catch { }
        _esperandoDesbloqueio = null;
    }

    private sealed class ReceptorDesbloqueio(CarroBluetoothService servico) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent) => MainThread.BeginInvokeOnMainThread(servico.Desbloqueou);
    }

    private async Task Desconectou(string endereco)
    {
        var cfg = store.Dados.Config;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (_conectado == endereco) _conectado = null;
            PararDeEsperar();
            BandejaService.Cancelar(IdPronto);
            if (modo.Dentro && cfg.SairAoDesconectar) nav.Navegar("carro");
        });

        if (!cfg.LembrarEstacionado) return;
        if (await PosicaoDoCarro() is not { } p)
        {
            Log.Info(Tag, "Estacionou: sem posição (Modo Carro fechado e sem a localização o tempo todo)");
            return;
        }
        var estacionado = new CarroEstacionado(p.Latitude, p.Longitude, DateTime.Now, await Endereco(p.Latitude, p.Longitude));
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            cfg.Estacionado = estacionado;
            store.Salvar();
            Mudou?.Invoke();
        });
        var onde = estacionado.Endereco is { } e ? $"Na {e}, às {estacionado.Quando:HH:mm}." : $"Às {estacionado.Quando:HH:mm}.";
        BandejaService.MostrarCarro(IdEstacionado, "Carro estacionado", $"{onde} Toque para ver como chegar a pé.", IntentComoChegar(estacionado));
    }

    /// <summary>
    /// Onde o carro ficou: a posição do Modo Carro (se é recente); senão, pede ao GPS, mas só se o Android deixa:
    /// com o app na tela, ou com a localização "o tempo todo" e a opção ligada.
    /// </summary>
    private async Task<(double Latitude, double Longitude)?> PosicaoDoCarro()
    {
        if (modo.Posicao is { } p && DateTime.Now - modo.PosicaoEm < TimeSpan.FromMinutes(3)) return (p.Latitude, p.Longitude);

        var cfg = store.Dados.Config;
        if (!TemLocalizacao || !(Visivel || cfg.EstacionadoComAppFechado && TemLocalizacaoSempre)) return null;
        try
        {
            var ultima = await MainThread.InvokeOnMainThreadAsync(() => Geolocation.Default.GetLastKnownLocationAsync());
            if (ultima is not null && DateTimeOffset.Now - ultima.Timestamp < TimeSpan.FromMinutes(2)) return (ultima.Latitude, ultima.Longitude);
            var agora = await MainThread.InvokeOnMainThreadAsync(() =>
                Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(8))));
            if (agora is not null) return (agora.Latitude, agora.Longitude);
            return ultima is not null ? (ultima.Latitude, ultima.Longitude) : null;
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"GPS do estacionado: {ex.Message}");
            return null;
        }
    }

    /// <summary>Rua e número pelo Geocoder do Android (sem chave). null sem internet ou sem resposta em 5 s.</summary>
    private static async Task<string?> Endereco(double lat, double lng)
    {
        try
        {
            var busca = Geocoding.Default.GetPlacemarksAsync(lat, lng);
            if (await Task.WhenAny(busca, Task.Delay(TimeSpan.FromSeconds(5))) != busca) return null;
            if ((await busca)?.FirstOrDefault() is not { } l) return null;
            var rua = string.Join(", ", new[] { l.Thoroughfare, l.SubThoroughfare }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var bairro = string.IsNullOrWhiteSpace(l.SubLocality) ? l.Locality : l.SubLocality;
            return string.IsNullOrWhiteSpace(rua) ? bairro : string.IsNullOrWhiteSpace(bairro) ? rua : $"{rua} - {bairro}";
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Endereço: {ex.Message}");
            return null;
        }
    }

    // ===== Carro estacionado =====

    /// <summary>Google Maps a pé até o carro (sem o Maps instalado, o link abre no navegador).</summary>
    private static Intent IntentComoChegar(CarroEstacionado e)
    {
        var destino = FormattableString.Invariant($"{e.Latitude},{e.Longitude}");
        var intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse($"https://www.google.com/maps/dir/?api=1&destination={destino}&travelmode=walking"));
        if (Ctx.PackageManager!.GetLaunchIntentForPackage(Maps) is not null) intent.SetPackage(Maps);
        intent.AddFlags(ActivityFlags.NewTask);
        return intent;
    }

    public void ComoChegar()
    {
        if (store.Dados.Config.Estacionado is not { } e) return;
        try { Ctx.StartActivity(IntentComoChegar(e)); }
        catch (ActivityNotFoundException ex) { Log.Warn(Tag, $"Como chegar: {ex.Message}"); }
    }

    public void EsquecerEstacionado()
    {
        store.Dados.Config.Estacionado = null;
        store.Salvar();
        BandejaService.Cancelar(IdEstacionado);
        Mudou?.Invoke();
    }
}

/// <summary>
/// Avisos do Android de que um aparelho Bluetooth conectou ou desconectou. Fica no manifesto (estes avisos são exceção à
/// regra do Android 8+ para transmissões implícitas), então chega mesmo com o app fechado. Repassa ao <see cref="CarroBluetoothService"/>.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(["android.bluetooth.device.action.ACL_CONNECTED", "android.bluetooth.device.action.ACL_DISCONNECTED"])]
public sealed class ReceptorBluetooth : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action is not { } acao) return;
        BluetoothDevice? aparelho = OperatingSystem.IsAndroidVersionAtLeast(33)
            ? intent.GetParcelableExtra(BluetoothDevice.ExtraDevice, Java.Lang.Class.FromType(typeof(BluetoothDevice))) as BluetoothDevice
#pragma warning disable CA1422 // antes do Android 13 só existe a versão sem o tipo
            : intent.GetParcelableExtra(BluetoothDevice.ExtraDevice) as BluetoothDevice;
#pragma warning restore CA1422
        string? endereco;
        try { endereco = aparelho?.Address; }
        catch (Java.Lang.SecurityException) { endereco = null; } // sem a permissão do Bluetooth
        if (endereco is null || IPlatformApplication.Current?.Services.GetService<CarroBluetoothService>() is not { } servico) return;

        // Até ~10 s para buscar a posição e o endereço antes de o Android encerrar o receptor
        var pendente = GoAsync();
        _ = Task.Run(async () =>
        {
            try { await servico.Recebeu(acao.EndsWith("ACL_CONNECTED"), endereco); }
            catch (Exception ex) { Log.Warn("CrankoHub.Bluetooth", $"Receptor: {ex.Message}"); }
            finally { pendente?.Finish(); }
        });
    }
}
