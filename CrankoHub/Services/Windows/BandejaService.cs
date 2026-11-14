using System.Windows;
using Forms = System.Windows.Forms;

namespace CrankoHub.Services;

/// <summary>Ícone do CrankoHub na bandeja do Windows (perto do relógio) e as notificações.</summary>
public sealed class BandejaService : IDisposable
{
    private readonly Forms.NotifyIcon _icone;
    private readonly System.Drawing.Icon _iconeOriginal;
    private readonly MenuRenderer _renderer = new();
    private string? _linkDoBalao;

    /// <summary>Pedido para mostrar a janela; o texto é a página a abrir (ou null).</summary>
    public event Action<string?>? AoAbrir;
    public event Action? AoVerificarAvisos;
    public event Action? AoSair;

    public BandejaService()
    {
        var menu = new Forms.ContextMenuStrip
        {
            Font = new System.Drawing.Font("Segoe UI", 9.75f),
            ShowImageMargin = false,
            Padding = new Forms.Padding(5, 5, 5, 5),
            DropShadowEnabled = false,
            Renderer = _renderer,
        };
        // Cantos arredondados e sombra do próprio Windows 11, como os menus do macOS
        menu.HandleCreated += (_, _) =>
        {
            int arredondado = 2;
            DwmSetWindowAttribute(menu.Handle, 33, ref arredondado, sizeof(int));
        };
        Item(menu, "Abrir o CrankoHub", () => AoAbrir?.Invoke(null));
        Item(menu, "Verificar avisos agora", () => AoVerificarAvisos?.Invoke());
        menu.Items.Add(new Forms.ToolStripSeparator());
        Item(menu, "Sair", () => AoSair?.Invoke());

        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/icone.ico")).Stream;
        _iconeOriginal = new System.Drawing.Icon(stream);
        _icone = new Forms.NotifyIcon
        {
            Icon = _iconeOriginal,
            Text = "CrankoHub",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icone.DoubleClick += (_, _) => AoAbrir?.Invoke(null);
        _icone.BalloonTipClicked += (_, _) => AoAbrir?.Invoke(_linkDoBalao);
    }

    /// <summary>Pinta o menu da bandeja com a cor de destaque ("#RRGGBB"). O ícone fica sempre o original,
    /// com o H no lilás da marca (layout "Fusão": o logo não muda com a cor de destaque).</summary>
    public void AplicarCor(string hex) => _renderer.Destaque = System.Drawing.ColorTranslator.FromHtml(hex);

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int atributo, ref int valor, int tamanho);

    private static Forms.ToolStripMenuItem Item(Forms.ContextMenuStrip menu, string texto, Action acao)
    {
        var item = new Forms.ToolStripMenuItem(texto, null, (_, _) => acao())
        {
            Padding = new Forms.Padding(6, 3, 30, 3),
        };
        menu.Items.Add(item);
        return item;
    }

    /// <summary>Visual limpo para o menu: fundo claro, borda suave e destaque arredondado.</summary>
    private sealed class MenuRenderer : Forms.ToolStripProfessionalRenderer
    {
        private static readonly System.Drawing.Color Fundo = System.Drawing.Color.FromArgb(246, 246, 246);
        private static readonly System.Drawing.Color Borda = System.Drawing.Color.FromArgb(222, 222, 222);
        public System.Drawing.Color Destaque { get; set; } = System.Drawing.Color.FromArgb(0, 122, 255);

        public MenuRenderer() => RoundedEdges = false;

        protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e) =>
            e.Graphics.Clear(Fundo);

        // A borda e a sombra ficam por conta do Windows (cantos arredondados)
        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e) { }

        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            var r = new System.Drawing.Rectangle(0, 0, e.Item.Width - 1, e.Item.Height - 1);
            using var caminho = new System.Drawing.Drawing2D.GraphicsPath();
            const int d = 10;
            caminho.AddArc(r.X, r.Y, d, d, 180, 90);
            caminho.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            caminho.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            caminho.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            caminho.CloseFigure();
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pincel = new System.Drawing.SolidBrush(Destaque);
            e.Graphics.FillPath(pincel, caminho);
        }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item is Forms.ToolStripMenuItem)
                e.TextColor = e.Item.Selected ? (Destaque.GetBrightness() > 0.7f ? System.Drawing.Color.FromArgb(29, 29, 31) : System.Drawing.Color.White) : System.Drawing.Color.FromArgb(30, 30, 34);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
        {
            using var caneta = new System.Drawing.Pen(Borda);
            int y = e.Item.Height / 2;
            e.Graphics.DrawLine(caneta, 10, y, e.Item.Width - 10, y);
        }
    }

    /// <summary>Mostra uma notificação do Windows; clicar nela abre <paramref name="link"/>.</summary>
    public void Notificar(string titulo, string texto, string? link = null)
    {
        _linkDoBalao = link;
        _icone.ShowBalloonTip(10000, titulo, texto, Forms.ToolTipIcon.None);
    }

    public void Dispose()
    {
        _icone.Visible = false;
        _icone.Dispose();
    }
}
