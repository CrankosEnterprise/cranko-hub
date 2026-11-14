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

    /// <summary>As áreas que aparecem como atalhos no menu (nome, link e cor "#RRGGBB"), lidas toda vez que ele abre.</summary>
    public Func<IEnumerable<(string Nome, string Link, string Cor)>>? Atalhos { get; set; }

    public BandejaService()
    {
        var menu = new Forms.ContextMenuStrip
        {
            Font = new System.Drawing.Font("Segoe UI", 9.75f),
            ShowImageMargin = false,
            Padding = new Forms.Padding(5, 5, 5, 5),
            DropShadowEnabled = false,
            Renderer = _renderer,
            MinimumSize = new System.Drawing.Size(230, 0),
        };
        // Cantos arredondados e sombra do próprio Windows 11, como os menus do macOS
        menu.HandleCreated += (_, _) =>
        {
            int arredondado = 2;
            DwmSetWindowAttribute(menu.Handle, 33, ref arredondado, sizeof(int));
        };
        // Monta na hora de abrir: as áreas seguem o que está visível, a ordem, o nome e a cor escolhidos
        menu.Opening += (_, _) => Montar(menu);

        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/icone.ico")).Stream;
        var bytesIcone = new MemoryStream();
        stream.CopyTo(bytesIcone);
        bytesIcone.Position = 0;
        _iconeOriginal = new System.Drawing.Icon(bytesIcone);
        try { RegistrarIdentidade(bytesIcone.ToArray()); } catch { } // sem isso o aviso só fica com o ícone do .exe
        _icone = new Forms.NotifyIcon
        {
            Icon = _iconeOriginal,
            Text = "CrankoHub",
            ContextMenuStrip = menu,
            Visible = true,
        };
        Montar(menu);
        _icone.DoubleClick += (_, _) => AoAbrir?.Invoke(null);
        _icone.BalloonTipClicked += (_, _) => AoAbrir?.Invoke(_linkDoBalao);
    }

    private const string IdApp = "CrankoHub";

    /// <summary>
    /// Dá ao processo uma identidade própria (AppUserModelID) com nome e ícone registrados em HKCU.
    /// As notificações usam esse ícone no lugar do ícone do .exe, que o Windows guarda em cache pelo caminho do
    /// arquivo (e por isso continuava mostrando o logo antigo). O PNG leva um hash no nome: logo novo, arquivo novo,
    /// cache novo.
    /// </summary>
    private static void RegistrarIdentidade(byte[] ico)
    {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ico))[..10].ToLowerInvariant();
        var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrankoHub");
        Directory.CreateDirectory(pasta);
        var png = Path.Combine(pasta, $"icone-notificacao-{hash}.png");
        if (!File.Exists(png))
        {
            foreach (var antigo in Directory.GetFiles(pasta, "icone-notificacao-*.png"))
                try { File.Delete(antigo); } catch { }
            using var maior = new System.Drawing.Icon(new MemoryStream(ico), 256, 256);
            using var bmp = maior.ToBitmap();
            bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png);
        }
        using (var chave = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($@"Software\Classes\AppUserModelId\{IdApp}"))
        {
            chave.SetValue("DisplayName", "CrankoHub");
            chave.SetValue("IconUri", png);
        }
        SetCurrentProcessExplicitAppUserModelID(IdApp);
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string id);

    /// <summary>Pinta o menu da bandeja com a cor de destaque ("#RRGGBB"). O ícone fica sempre o original,
    /// com o H no lilás da marca (layout "Fusão": o logo não muda com a cor de destaque).</summary>
    public void AplicarCor(string hex) => _renderer.Destaque = System.Drawing.ColorTranslator.FromHtml(hex);

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int atributo, ref int valor, int tamanho);

    // Glifos do Segoe Fluent Icons (Windows 11) / Segoe MDL2 Assets (Windows 10): mesmos códigos nos dois
    private const string GlifoInicio = "", GlifoAvisos = "", GlifoConfig = "", GlifoSair = "";

    private void Montar(Forms.ContextMenuStrip menu)
    {
        menu.SuspendLayout();
        foreach (var antigo in menu.Items.Cast<Forms.ToolStripItem>().ToList()) antigo.Dispose();
        menu.Items.Clear();

        menu.Items.Add(new Cabecalho(_iconeOriginal, () => { menu.Close(); AoAbrir?.Invoke(null); }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new ItemMenu("Início", () => AoAbrir?.Invoke("")) { Glifo = GlifoInicio });
        foreach (var (nome, link, cor) in Atalhos?.Invoke() ?? [])
        {
            System.Drawing.Color? ponto = null;
            try { ponto = System.Drawing.ColorTranslator.FromHtml(cor); } catch { }
            menu.Items.Add(new ItemMenu(nome, () => AoAbrir?.Invoke(link)) { Ponto = ponto });
        }
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new ItemMenu("Verificar avisos", () => AoVerificarAvisos?.Invoke()) { Glifo = GlifoAvisos });
        menu.Items.Add(new ItemMenu("Configurações", () => AoAbrir?.Invoke("configuracoes")) { Glifo = GlifoConfig });
        menu.Items.Add(new ItemMenu("Sair", () => AoSair?.Invoke()) { Glifo = GlifoSair });
        menu.ResumeLayout();
    }

    /// <summary>Linha do menu com um ícone (glifo) ou o pontinho da cor da área à esquerda do texto.</summary>
    private sealed class ItemMenu : Forms.ToolStripMenuItem
    {
        public const int Recuo = 30;
        public string? Glifo;
        public System.Drawing.Color? Ponto;

        public ItemMenu(string texto, Action acao) : base(texto, null, (_, _) => acao()) =>
            Padding = new Forms.Padding(6, 4, 30 + Recuo, 4);
    }

    /// <summary>Topo do menu: logo, nome e versão (clicar abre o app).</summary>
    private sealed class Cabecalho : Forms.ToolStripLabel
    {
        private readonly System.Drawing.Icon _logo;
        private readonly string _versao = "v" + AtualizacaoService.Texto(AtualizacaoService.VersaoAtual);

        public Cabecalho(System.Drawing.Icon logo, Action abrir)
        {
            _logo = logo;
            AutoSize = false;
            Size = new System.Drawing.Size(220, 38);
            Click += (_, _) => abrir();
        }

        protected override void OnPaint(Forms.PaintEventArgs e)
        {
            var g = e.Graphics;
            const int lado = 22;
            int y = (Height - lado) / 2;
            using (var icone = new System.Drawing.Icon(_logo, 32, 32))
                g.DrawIcon(icone, new System.Drawing.Rectangle(8, y, lado, lado));
            using var negrito = new System.Drawing.Font(Font.FontFamily, 10.5f, System.Drawing.FontStyle.Bold);
            var areaNome = new System.Drawing.Rectangle(8 + lado + 8, 0, Width - lado - 24, Height);
            Forms.TextRenderer.DrawText(g, "CrankoHub", negrito, areaNome, System.Drawing.Color.FromArgb(29, 29, 31),
                Forms.TextFormatFlags.VerticalCenter | Forms.TextFormatFlags.Left | Forms.TextFormatFlags.NoPadding);
            var areaVersao = new System.Drawing.Rectangle(0, 0, Width - 10, Height);
            Forms.TextRenderer.DrawText(g, _versao, Font, areaVersao, System.Drawing.Color.FromArgb(134, 134, 139),
                Forms.TextFormatFlags.VerticalCenter | Forms.TextFormatFlags.Right | Forms.TextFormatFlags.NoPadding);
        }
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

        private static readonly string FonteGlifos =
            new System.Drawing.Text.InstalledFontCollection().Families.Any(f => f.Name == "Segoe Fluent Icons")
                ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item is Forms.ToolStripMenuItem)
                e.TextColor = e.Item.Selected ? (Destaque.GetBrightness() > 0.7f ? System.Drawing.Color.FromArgb(29, 29, 31) : System.Drawing.Color.White) : System.Drawing.Color.FromArgb(30, 30, 34);
            if (e.Item is ItemMenu item)
            {
                var r = e.TextRectangle;
                e.TextRectangle = new System.Drawing.Rectangle(r.X + ItemMenu.Recuo, r.Y, r.Width, r.Height);
                var caixa = new System.Drawing.Rectangle(r.X, 0, ItemMenu.Recuo - 8, e.Item.Height);
                if (item.Glifo is { } glifo)
                {
                    using var fonte = new System.Drawing.Font(FonteGlifos, 10.5f);
                    var cor = e.Item.Selected ? e.TextColor : System.Drawing.Color.FromArgb(90, 90, 96);
                    Forms.TextRenderer.DrawText(e.Graphics, glifo, fonte, caixa, cor,
                        Forms.TextFormatFlags.HorizontalCenter | Forms.TextFormatFlags.VerticalCenter | Forms.TextFormatFlags.NoPadding);
                }
                else if (item.Ponto is { } ponto)
                {
                    const int d = 9;
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using var pincel = new System.Drawing.SolidBrush(e.Item.Selected ? e.TextColor : ponto);
                    e.Graphics.FillEllipse(pincel, caixa.X + (caixa.Width - d) / 2f, (caixa.Height - d) / 2f, d, d);
                }
            }
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
