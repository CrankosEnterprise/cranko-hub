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
        // Com o vidro, o WebView nasce transparente: a barra lateral (sem fundo no CSS) mostra a moldura translúcida
        if (VidroDisponivel) Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", "00000000");
        InitializeComponent();

        AplicarTema(App.Servicos.GetRequiredService<ArmazenamentoService>().Dados.Tema);
        App.Servicos.GetRequiredService<JanelaService>().AoMudarTema += tema => Dispatcher.Invoke(() => AplicarTema(tema));
        StateChanged += (_, _) => AjustarMaximizada();
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

    // ---- Barra de título estilo Mac ----

    private void AoFechar(object sender, RoutedEventArgs e) => Close();

    private void AoMinimizar(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void AoMaximizar(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    // Sem a moldura do Windows, a janela maximizada passaria um pouco das bordas da tela
    private void AjustarMaximizada()
    {
        // Borda de redimensionamento + a borda "acolchoada" do Windows (4px)
        var b = SystemParameters.WindowResizeBorderThickness;
        raiz.Margin = WindowState == WindowState.Maximized
            ? new Thickness(b.Left + 4, b.Top + 4, b.Right + 4, b.Bottom + 4)
            : new Thickness(0);
    }

    /// <summary>Cores da barra de acordo com o tema do app ("light", "dark" ou null = tema do Windows).</summary>
    private void AplicarTema(string? tema)
    {
        var escuro = tema switch { "dark" => true, "light" => false, _ => WindowsEstaEscuro() };
        Color Cor(string hex) => (Color)ColorConverter.ConvertFromString(hex);
        tituloJanela.Foreground = new SolidColorBrush(Cor(escuro ? "#C7C7CC" : "#48484A"));
        Resources["BolinhaInativa"] = new SolidColorBrush(Cor(escuro ? "#4A4A4A" : "#CFCFCF"));
        // Mesma cor da moldura no CSS (--window); com o vidro, transparente para o Mica aparecer
        Background = _vidro ? Brushes.Transparent : new SolidColorBrush(Cor(escuro ? "#0E0E10" : "#E8E8ED"));

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
