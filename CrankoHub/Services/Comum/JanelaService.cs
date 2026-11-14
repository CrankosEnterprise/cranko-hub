namespace CrankoHub.Services;

/// <summary>
/// Ponte entre a página e a janela nativa: avisa quando o tema muda e, no Windows (janela sem moldura),
/// recebe os pedidos do semáforo e da barra do topo (fechar, minimizar, maximizar, arrastar, redimensionar).
/// </summary>
public class JanelaService
{
    /// <summary>Novo tema: "light", "dark" ou null (segue o Windows).</summary>
    public event Action<string?>? AoMudarTema;

    public void TemaMudou(string? tema) => AoMudarTema?.Invoke(tema);

    /// <summary>
    /// true quando a janela não tem moldura do sistema e a página desenha o semáforo e serve de barra de título
    /// (Windows). No Linux a janela tem a moldura do sistema; no Android não há janela.
    /// </summary>
    public bool ControlesProprios { get; set; }

    /// <summary>Pedido da página: "fechar", "minimizar", "maximizar", "arrastar" ou "redimensionar:&lt;borda&gt;".</summary>
    public event Action<string>? AoPedir;

    public void Pedir(string acao) => AoPedir?.Invoke(acao);

    /// <summary>Janela maximizada: a página tira as bordas de redimensionar e os cantos arredondados.</summary>
    public bool Maximizada { get; private set; }

    /// <summary>A janela maximizou ou voltou ao tamanho normal.</summary>
    public event Action? Mudou;

    public void DefinirMaximizada(bool valor)
    {
        if (Maximizada == valor) return;
        Maximizada = valor;
        Mudou?.Invoke();
    }
}
