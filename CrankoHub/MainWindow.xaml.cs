using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using CrankoHub.Services;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace CrankoHub;

public partial class MainWindow : Window
{
    /// <summary>Windows 11 22H2+: a moldura da janela fica translúcida (Mica), como o vidro do macOS.</summary>
    private static readonly bool VidroDisponivel = Environment.OSVersion.Version.Build >= 22621;

    private bool _vidro;

    public MainWindow()
    {
        Resources.Add("services", App.Servicos);
        // Com o vidro, o WebView nasce transparente: a barra do topo (sem fundo no CSS) mostra a moldura translúcida
        if (VidroDisponivel) Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", "00000000");
        InitializeComponent();

        var janela = App.Servicos.GetRequiredService<JanelaService>();
        // A página desenha o semáforo e serve de barra de título (Layout/LayoutPrincipal.razor)
        janela.ControlesProprios = true;
        janela.AoPedir += acao => Dispatcher.BeginInvoke(() => Atender(acao));
        AplicarTema(App.Servicos.GetRequiredService<ArmazenamentoService>().Dados.Tema);
        janela.AoMudarTema += tema => Dispatcher.Invoke(() => AplicarTema(tema));
        StateChanged += (_, _) =>
        {
            AjustarMaximizada();
            janela.DefinirMaximizada(WindowState == WindowState.Maximized);
        };
        SourceInitialized += (_, _) =>
        {
            ArredondarCantos();
            AplicarVidro();
            AplicarTema(App.Servicos.GetRequiredService<ArmazenamentoService>().Dados.Tema);
        };
    }

    private void AoInicializarWebView(object? sender, BlazorWebViewInitializingEventArgs e)
    {
        // Guarda os dados do WebView2 no perfil do usuário, e não ao lado do .exe
        // (que pode estar numa pasta sem permissão de escrita, como Program Files).
        e.UserDataFolder = Path.Combine(ArmazenamentoService.PastaLocal, "WebView2");
#if DEBUG
        // Só na depuração: CRANKOHUB_DEPURAR=1 abre o inspetor remoto do WebView2 (porta 9222) para testes e prints
        if (Environment.GetEnvironmentVariable("CRANKOHUB_DEPURAR") == "1")
            e.EnvironmentOptions = new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions("--remote-debugging-port=9222");
#endif
    }

    // As capas do Entretenimento ficam numa pasta do usuário; o WebView as enxerga por um endereço próprio
    private void AoWebViewPronto(object? sender, BlazorWebViewInitializedEventArgs e)
    {
        Directory.CreateDirectory(CapasService.Pasta);
        e.WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(CapasService.Host, CapasService.Pasta,
            Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

        // Avisa o CSS (html.vidro) para deixar o fundo da moldura transparente
        if (_vidro)
        {
            const string marcar = "document.documentElement.classList.add('vidro')";
            _ = e.WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                $"document.addEventListener('DOMContentLoaded', () => {marcar});");
            e.WebView.CoreWebView2.NavigationCompleted += (_, _) => _ = e.WebView.CoreWebView2.ExecuteScriptAsync(marcar);
        }
    }

    // ---- Barra de título (desenhada pela página) ----

    // Códigos de "onde o mouse está" do Windows: a barra de título e as bordas de redimensionar
    private const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 2;
    private static readonly Dictionary<string, int> Bordas = new()
    {
        ["w"] = 10, ["e"] = 11, ["n"] = 12, ["nw"] = 13, ["ne"] = 14, ["s"] = 15, ["sw"] = 16, ["se"] = 17,
    };

    /// <summary>Pedidos do semáforo e da barra do topo da página (JanelaService.Pedir).</summary>
    private void Atender(string acao)
    {
        switch (acao)
        {
            case "fechar": Close(); break;
            case "minimizar": WindowState = WindowState.Minimized; break;
            case "maximizar":
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                break;
            // Como se o botão tivesse sido apertado na barra de título do Windows: arrasta, encaixa nas bordas
            // (Aero Snap) e, maximizada, volta ao tamanho normal ao puxar
            case "arrastar": ComecarMovimento(HTCAPTION); break;
            default:
                if (acao.StartsWith("redimensionar:") && Bordas.TryGetValue(acao["redimensionar:".Length..], out var borda)
                    && WindowState == WindowState.Normal)
                    ComecarMovimento(borda);
                break;
        }
    }

    private void ComecarMovimento(int alvo)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        // O pedido vem da página com um pequeno atraso: se o botão já foi solto, o Windows entraria num modo
        // de mover/redimensionar que segue o cursor até o próximo clique
        if (hwnd == IntPtr.Zero || !BotaoPrincipalApertado()) return;
        ReleaseCapture();
        SendMessage(hwnd, WM_NCLBUTTONDOWN, alvo, IntPtr.Zero);
    }

    private static bool BotaoPrincipalApertado()
    {
        const int SM_SWAPBUTTON = 23, VK_LBUTTON = 0x01, VK_RBUTTON = 0x02;
        var botao = GetSystemMetrics(SM_SWAPBUTTON) != 0 ? VK_RBUTTON : VK_LBUTTON;
        return (GetAsyncKeyState(botao) & 0x8000) != 0;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int tecla);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int indice);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int mensagem, int wParam, IntPtr lParam);

    // Sem a moldura do Windows, a janela maximizada passaria um pouco das bordas da tela
    private void AjustarMaximizada()
    {
        // Borda de redimensionamento + a borda "acolchoada" do Windows (4px)
        var b = SystemParameters.WindowResizeBorderThickness;
        raiz.Margin = WindowState == WindowState.Maximized
            ? new Thickness(b.Left + 4, b.Top + 4, b.Right + 4, b.Bottom + 4)
            : new Thickness(0);
    }

    /// <summary>Fundo e moldura da janela de acordo com o tema do app ("light", "dark" ou null = tema do Windows).</summary>
    private void AplicarTema(string? tema)
    {
        var escuro = tema switch { "dark" => true, "light" => false, _ => WindowsEstaEscuro() };
        Color Cor(string hex) => (Color)ColorConverter.ConvertFromString(hex);
        // Mesma cor da moldura no CSS (--window); com o vidro, transparente para o Mica aparecer
        Background = _vidro ? Brushes.Transparent : new SolidColorBrush(Cor(escuro ? "#070B16" : "#E9EBEF"));

        // Modo escuro da moldura (o Mica fica escuro também)
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
            var valor = escuro ? 1 : 0;
            _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref valor, sizeof(int));
        }
    }

    private static bool WindowsEstaEscuro()
    {
        using var chave = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return chave?.GetValue("AppsUseLightTheme") is int claro && claro == 0;
    }

    // Mantém os cantos arredondados do Windows 11 (no Windows 10 a chamada só falha, sem efeito)
    private void ArredondarCantos()
    {
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2;
        var preferencia = DWMWCP_ROUND;
        _ = DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preferencia, sizeof(int));
    }

    // Mica: o Windows desenha o fundo translúcido atrás da janela inteira; o WPF e o WebView deixam o fundo transparente
    private void AplicarVidro()
    {
        if (!VidroDisponivel) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        const int DWMWA_SYSTEMBACKDROP_TYPE = 38, DWMSBT_MAINWINDOW = 2;
        var tipo = DWMSBT_MAINWINDOW;
        if (DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref tipo, sizeof(int)) != 0) return;

        if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } alvo) alvo.BackgroundColor = Colors.Transparent;
        // O WindowChrome reaplica a "moldura de vidro" sozinho; -1 = a janela inteira
        WindowChrome.GetWindowChrome(this).GlassFrameThickness = new Thickness(-1);
        var margens = new Margens { Esquerda = -1, Direita = -1, Topo = -1, Base = -1 };
        _ = DwmExtendFrameIntoClientArea(hwnd, ref margens);

        // Com a moldura estendida, o Windows desenharia os botões dele (–, □, X) por cima; sem o menu de sistema eles somem
        const int GWL_STYLE = -16, WS_SYSMENU = 0x80000;
        SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
        _vidro = true;
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int indice);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int indice, int valor);

    [StructLayout(LayoutKind.Sequential)]
    private struct Margens { public int Esquerda, Direita, Topo, Base; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int atributo, ref int valor, int tamanho);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margens margens);

    // ---- Fechar ----

    // O X esconde na bandeja (o app continua avisando); "Sair" no menu do ícone fecha de verdade
    protected override void OnClosing(CancelEventArgs e)
    {
        var store = App.Servicos.GetRequiredService<ArmazenamentoService>();
        var cfg = store.Dados.Config;
        if (!App.Saindo && cfg.MinimizarParaBandeja)
        {
            e.Cancel = true;
            Hide();
            if (!cfg.DicaBandejaMostrada)
            {
                App.Servicos.GetRequiredService<BandejaService>().Notificar("O CrankoHub continua aberto",
                    "Ele fica perto do relógio para te avisar dos vencimentos. Para fechar de vez: botão direito no ícone > Sair.");
                cfg.DicaBandejaMostrada = true;
                store.Salvar();
            }
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (!App.Saindo) ((App)Application.Current).Sair();
    }
}
