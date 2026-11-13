using Android.Content;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using Android.Util;
using Android.Views;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// A rota no visor da central pelo Bluetooth (AVRCP). O CrankoHub abre o próprio controle de mídia (<see cref="MediaSession"/>)
/// e preenche título, artista e álbum com a próxima curva e a música; a central exibe esses textos como se fossem da música.
/// A música continua no Spotify: só o texto é nosso. Os botões da central chegam aqui e são repassados ao Spotify.
/// <para>
/// Testado no carro (Pioneer AVH-X2550BT + POCO F3, Android 13; ver Informacoes/updates.md): o Android manda ao visor o app que
/// <b>começou a tocar por último</b> (logcat "Avrcp_ext: Changing addressed media session"). Por isso o CrankoHub toca um áudio mudo,
/// sem pedir o foco de áudio (o Spotify não pausa), e o reinicia quando o Spotify começa a tocar ou troca de música.
/// </para>
/// Liga sozinho durante a rota (<see cref="Configuracoes.VisorCentral"/>, com o Bluetooth de áudio conectado) e no teste com log
/// (<see cref="Iniciar"/>; log em visor-log.txt na pasta do app e no logcat "CrankoHub.Visor"). Mesma API de Services/Carro/ModoCarro/VisorCarroService.cs.
/// </summary>
public sealed class VisorCarroService(ModoCarroService modo, RotaService rota, ArmazenamentoService store)
{
    private const string Tag = "CrankoHub.Visor";
    private const string Spotify = "com.spotify.music";
    private const int MaxLinhas = 300;

    /// <summary>Espera o Spotify assumir o visor antes de retomar (no carro, a troca para ele levou menos de 0,1 s).</summary>
    private static readonly TimeSpan EsperaRetomar = TimeSpan.FromMilliseconds(1500);

    public bool Disponivel => true;

    public bool Testando { get; private set; }

    /// <summary>O visor está com o CrankoHub (no teste ou durante a rota).</summary>
    public bool Ligado => _sessao is not null;

    public int Etapa { get; private set; }

    public TextosVisor? Enviando { get; private set; }

    private readonly List<string> _linhas = [];

    public IReadOnlyList<string> Linhas => _linhas;

    public event Action? Mudou;

    private static Context Ctx => global::Android.App.Application.Context;

    public static string CaminhoLog => Path.Combine(FileSystem.AppDataDirectory, "visor-log.txt");

    private bool _noModoCarro;
    private MediaSession? _sessao;
    private AudioTrack? _mudo;
    private Timer? _relogio;
    private int _tique;
    private DateTime _inicioEtapa;
    private readonly Dictionary<int, string> _respostas = [];
    private string? _musicaLogada, _curvaLogada;
    private bool _spotifyTocava;
    private string? _faixaVista;
    private CancellationTokenSource? _retomar;

    // ===== Modo Carro aberto / fechado =====

    /// <summary>Abriu o Modo Carro: passa a acompanhar a rota e liga o visor quando ela começar.</summary>
    public void Entrar()
    {
        if (_noModoCarro) return;
        _noModoCarro = true;
        modo.Mudou += Atualizou;
        rota.Mudou += Atualizou;
        _relogio = new Timer(_ => MainThread.BeginInvokeOnMainThread(Tique), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        Avaliar();
    }

    /// <summary>Saiu do Modo Carro: para o teste e devolve o visor ao Spotify.</summary>
    public void Sair()
    {
        if (!_noModoCarro) return;
        Parar();
        _noModoCarro = false;
        modo.Mudou -= Atualizou;
        rota.Mudou -= Atualizou;
        _relogio?.Dispose();
        _relogio = null;
        Avaliar();
    }

    // Liga durante o teste, ou com a opção ligada + rota iniciada + Bluetooth de áudio conectado
    private void Avaliar()
    {
        var deve = _noModoCarro && (Testando || store.Dados.Config.VisorCentral && rota.Atual is not null && TemBluetooth());
        if (deve && !Ligado) Ligar();
        else if (!deve && Ligado) Desligar();
    }

    private static bool TemBluetooth()
    {
        try
        {
            var audio = (AudioManager)Ctx.GetSystemService(Context.AudioService)!;
            return audio.GetDevices(GetDevicesTargets.Outputs)?.Any(d => d.Type == AudioDeviceType.BluetoothA2dp) == true;
        }
        catch
        {
            return false;
        }
    }

    private void Ligar()
    {
        PrepararArquivo();
        Registrar(Testando ? "Visor: ligado para o teste" : $"Visor: ligado para a rota até {rota.Atual?.Destino.Nome}");
        try
        {
            var sessao = new MediaSession(Ctx, "CrankoHubVisor");
            sessao.SetCallback(new Retorno(this));
            sessao.SetPlaybackState(new PlaybackState.Builder()
                .SetActions(PlaybackState.ActionPlay | PlaybackState.ActionPause | PlaybackState.ActionPlayPause |
                            PlaybackState.ActionSkipToNext | PlaybackState.ActionSkipToPrevious | PlaybackState.ActionStop)!
                .SetState(PlaybackStateCode.Playing, 0, 1f)!
                .Build());
            sessao.Active = true;
            _sessao = sessao;
        }
        catch (Exception ex)
        {
            Registrar($"ERRO ao abrir o controle de mídia: {ex.Message}");
            return;
        }
        _spotifyTocava = modo.Musica?.Tocando == true;
        _faixaVista = modo.Musica?.Titulo;
        _curvaLogada = null;
        Audio(true);
        RegistrarSaidas();
        Enviar(forcar: true);
        Mudou?.Invoke();
    }

    private void Desligar()
    {
        _retomar?.Cancel();
        Audio(false);
        try
        {
            _sessao?.Release();
        }
        catch { }
        _sessao = null;
        Enviando = null;
        Registrar("Visor: desligado (volta para o Spotify)");
        Mudou?.Invoke();
    }

    // ===== Teste com log =====

    public void Iniciar()
    {
        if (Testando || !_noModoCarro) return;
        Testando = true;
        Etapa = 0;
        _respostas.Clear();
        _linhas.Clear();
        PrepararArquivo();
        Registrar("===== Teste do visor =====");
        Registrar($"Aparelho: {Build.Manufacturer} {Build.Model} · Android {Build.VERSION.Release} (API {(int)Build.VERSION.SdkInt}) · {Build.Display}");
        Registrar($"Mapa: {(rota.UsaGoogle ? "Google" : "OpenStreetMap")} · rota {(rota.Atual is null ? "nenhuma" : "iniciada")} · visor na rota {(store.Dados.Config.VisorCentral ? "ligado" : "desligado")}");
        _musicaLogada = null;
        RegistrarMusica();
        // Com a rota já no visor, o teste aproveita o que está ligado
        Avaliar();
        ComecarEtapa();
    }

    public void Parar()
    {
        if (!Testando) return;
        Testando = false;
        Registrar("Respostas: " + (_respostas.Count == 0 ? "nenhuma"
            : string.Join(" · ", _respostas.Select(r => $"etapa {r.Key + 1} = {r.Value}"))));
        Registrar("===== Fim do teste =====");
        Avaliar(); // continua ligado se houver rota (e a opção estiver ligada)
        Mudou?.Invoke();
    }

    public void ProximaEtapa()
    {
        if (!Testando || Etapa >= TesteVisor.Etapas.Count - 1) return;
        Etapa++;
        ComecarEtapa();
    }

    private void ComecarEtapa()
    {
        _tique = 0;
        _inicioEtapa = DateTime.Now;
        Registrar($"--- Etapa {Etapa + 1}: {TesteVisor.Etapas[Etapa].Nome} ---");
        RegistrarSaidas();
        Enviar(forcar: true);
        Mudou?.Invoke();
    }

    public void Anotar(string resposta)
    {
        if (!Testando) return;
        Registrar($"VOCÊ (etapa {Etapa + 1}): {resposta}");
        if (resposta is TesteVisor.Apareceu or TesteVisor.NaoApareceu)
        {
            _respostas[Etapa] = resposta;
            ProximaEtapa();
        }
        else _respostas[Etapa] = _respostas.TryGetValue(Etapa, out var r) ? $"{r}+{resposta}" : resposta;
        Mudou?.Invoke();
    }

    // ===== A cada 5 s e a cada mudança =====

    private void Tique()
    {
        if (!_noModoCarro) return;
        _tique++;
        Avaliar(); // o Bluetooth pode ter conectado ou caído
        if (Testando)
        {
            var e = TesteVisor.Etapas[Etapa];
            if (e.Segundos > 0 && DateTime.Now - _inicioEtapa >= TimeSpan.FromSeconds(e.Segundos))
            {
                Registrar($"Etapa {Etapa + 1} sem resposta em {e.Segundos} s: seguindo");
                ProximaEtapa();
                return;
            }
        }
        if (!Ligado) return;
        // Garantia: se o visor voltou ao Spotify por outro motivo (ex.: adiantar a música), retoma a cada 30 s
        if (_tique % 6 == 0 && modo.Musica?.Tocando == true) Retomar("garantia a cada 30 s");
        Enviar();
        if (Testando) Mudou?.Invoke();
    }

    private void Atualizou()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_noModoCarro) return;
            Avaliar(); // rota iniciada ou encerrada
            if (!Ligado) return;
            RegistrarMusica();
            ConferirSpotify();
            Enviar();
        });
    }

    // ===== Retomar o visor =====

    private void ConferirSpotify()
    {
        var m = modo.Musica;
        var tocando = m?.Tocando == true;
        if (tocando && !_spotifyTocava) Retomar("o Spotify começou a tocar");
        else if (tocando && m!.Titulo != _faixaVista) Retomar("o Spotify trocou de música");
        _spotifyTocava = tocando;
        _faixaVista = m?.Titulo;
    }

    private void Retomar(string motivo)
    {
        _retomar?.Cancel();
        var cts = _retomar = new CancellationTokenSource();
        _ = Task.Delay(EsperaRetomar, cts.Token).ContinueWith(t =>
        {
            if (t.IsCanceled) return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (cts.IsCancellationRequested || _mudo is null) return;
                Registrar($"Retomando o visor ({motivo})");
                // O novo começa antes de o antigo parar: o visor não fica sem dono nem por um instante
                var antigo = _mudo;
                _mudo = null;
                Audio(true);
                Soltar(antigo);
            });
        }, TaskScheduler.Default);
    }

    // ===== Textos para a central =====

    private void Enviar(bool forcar = false)
    {
        if (_sessao is null) return;
        var progresso = rota.Atual is not null ? rota.Progresso : null;
        var t = Testando ? VisorTextos.Montar(_tique, progresso, rota.Calculando, modo.Musica, DateTime.Now)
            : progresso is not null ? VisorTextos.DaRota(progresso, rota.Calculando, modo.Musica)
            : VisorTextos.Iniciando(rota.Atual?.Destino.Nome, modo.Musica);
        if (!forcar && t == Enviando) return;
        Enviando = t;
        try
        {
            _sessao.SetMetadata(new MediaMetadata.Builder()
                .PutString(MediaMetadata.MetadataKeyTitle, t.Titulo)!
                .PutString(MediaMetadata.MetadataKeyArtist, t.Artista)!
                .PutString(MediaMetadata.MetadataKeyAlbum, t.Album)!
                .PutString(MediaMetadata.MetadataKeyGenre, t.Genero)! // a linha com a guitarra na Pioneer
                .PutString(MediaMetadata.MetadataKeyDisplayTitle, t.Titulo)!
                .PutString(MediaMetadata.MetadataKeyDisplaySubtitle, t.Artista)!
                .PutLong(MediaMetadata.MetadataKeyDuration, 0)!
                .Build());
            // No teste, tudo; na rota, só quando a curva muda (uma viagem longa não enche o log)
            var curva = progresso?.Proximo.Instrucao ?? t.Titulo;
            if (Testando || curva != _curvaLogada) Registrar($"→ visor: {t.Titulo} | {t.Artista} | {t.Album} | {t.Genero}");
            _curvaLogada = curva;
        }
        catch (Exception ex)
        {
            Registrar($"ERRO ao mandar o texto: {ex.Message}");
        }
    }

    // ===== Áudio mudo (faz o Android tratar o CrankoHub como o player que está tocando) =====

    private void Audio(bool ligar)
    {
        if (ligar == (_mudo is not null)) return;
        if (!ligar)
        {
            Soltar(_mudo);
            _mudo = null;
            return;
        }
        try
        {
            const int taxa = 44100, bytes = taxa * 4; // 1 s de estéreo 16 bits, repetido sem fim
            var faixa = new AudioTrack.Builder()
                .SetAudioAttributes(new AudioAttributes.Builder().SetUsage(AudioUsageKind.Media)!.SetContentType(AudioContentType.Music)!.Build()!)
                .SetAudioFormat(new AudioFormat.Builder().SetEncoding(Encoding.Pcm16bit)!.SetSampleRate(taxa)!.SetChannelMask(ChannelOut.Stereo)!.Build()!)
                .SetTransferMode(AudioTrackMode.Static)
                .SetBufferSizeInBytes(bytes)
                .Build();
            faixa.Write(new byte[bytes], 0, bytes);
            faixa.SetLoopPoints(0, bytes / 4, -1);
            faixa.Play(); // sem pedir o foco de áudio: o Spotify segue tocando por cima
            _mudo = faixa;
        }
        catch (Exception ex)
        {
            Registrar($"ERRO no áudio mudo: {ex.Message}");
        }
    }

    private static void Soltar(AudioTrack? faixa)
    {
        if (faixa is null) return;
        try
        {
            faixa.Stop();
            faixa.Release();
        }
        catch { }
    }

    // ===== Botões da central (chegam ao nosso controle de mídia) → Spotify =====

    private void Botao(string nome, Keycode tecla)
    {
        Registrar($"BOTÃO da central: {nome}");
        // O nosso controle continua "tocando" (para seguir com o visor) e a tecla vai direto ao Spotify
        try
        {
            foreach (var acao in new[] { KeyEventActions.Down, KeyEventActions.Up })
            {
                var i = new Intent(Intent.ActionMediaButton);
                i.SetPackage(Spotify);
                i.PutExtra(Intent.ExtraKeyEvent, new KeyEvent(acao, tecla));
                Ctx.SendBroadcast(i);
            }
        }
        catch (Exception ex)
        {
            Registrar($"   ERRO ao repassar ao Spotify: {ex.Message}");
        }
    }

    private sealed class Retorno(VisorCarroService visor) : MediaSession.Callback
    {
        // O nosso controle fica sempre "tocando": tocar e pausar viram a mesma tecla de alternar no Spotify
        public override void OnPlay() => visor.Botao("tocar", Keycode.MediaPlayPause);

        public override void OnPause() => visor.Botao("pausar", Keycode.MediaPlayPause);

        public override void OnSkipToNext() => visor.Botao("próxima", Keycode.MediaNext);

        public override void OnSkipToPrevious() => visor.Botao("anterior", Keycode.MediaPrevious);

        public override void OnStop() => visor.Botao("parar", Keycode.MediaStop);
    }

    // ===== Log =====

    private void RegistrarMusica()
    {
        var m = modo.Musica;
        var texto = m is null ? "Spotify: nada recebido ainda" : $"Spotify: {m.Artista} – {m.Titulo} ({(m.Tocando ? "tocando" : "pausado")})";
        if (texto == _musicaLogada) return;
        _musicaLogada = texto;
        Registrar(texto);
    }

    // Saída de áudio (a central aparece como A2DP) e quantos players estão tocando
    private void RegistrarSaidas()
    {
        try
        {
            var audio = (AudioManager)Ctx.GetSystemService(Context.AudioService)!;
            var bt = audio.GetDevices(GetDevicesTargets.Outputs)?
                .Where(d => d.Type is AudioDeviceType.BluetoothA2dp or AudioDeviceType.BluetoothSco)
                .Select(d => $"{d.ProductName} ({d.Type})").ToList() ?? [];
            Registrar($"Bluetooth de áudio: {(bt.Count == 0 ? "NENHUM conectado" : string.Join(", ", bt))}");
            var players = OperatingSystem.IsAndroidVersionAtLeast(26) ? audio.ActivePlaybackConfigurations?.Count.ToString() ?? "?" : "?";
            Registrar($"Música ativa no sistema: {audio.IsMusicActive} · players tocando: {players}");
        }
        catch (Exception ex)
        {
            Registrar($"ERRO ao ler as saídas de áudio: {ex.Message}");
        }
    }

    private void Registrar(string texto)
    {
        var linha = $"{VisorTextos.Hora(DateTime.Now)}  {texto}";
        Log.Info(Tag, texto);
        _linhas.Add(linha);
        if (_linhas.Count > MaxLinhas) _linhas.RemoveAt(0);
        try
        {
            File.AppendAllText(CaminhoLog, linha + "\n");
        }
        catch { }
    }

    // Guarda o histórico, mas não deixa o arquivo crescer sem fim
    private static void PrepararArquivo()
    {
        try
        {
            if (File.Exists(CaminhoLog) && new FileInfo(CaminhoLog).Length > 512 * 1024) File.Delete(CaminhoLog);
        }
        catch { }
    }

    public async Task CompartilharLog()
    {
        if (!File.Exists(CaminhoLog)) return;
        // Copia para o cache: o arquivo original continua sendo escrito
        var copia = Path.Combine(FileSystem.CacheDirectory, $"visor-log-{DateTime.Now:yyyyMMdd-HHmm}.txt");
        File.Copy(CaminhoLog, copia, overwrite: true);
        await Share.Default.RequestAsync(new ShareFileRequest { Title = "Log do visor da central", File = new ShareFile(copia, "text/plain") });
    }
}
