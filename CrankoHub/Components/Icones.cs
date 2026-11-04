using Microsoft.AspNetCore.Components;

namespace CrankoHub.Components;

/// <summary>Ícones SVG (traço) usados na interface.</summary>
public static class Icones
{
    // Mesmo desenho do ícone do app (Assets/icone.ico, Linux/icone.png): quatro quadrados arredondados
    public static readonly MarkupString Logo = new("""<svg viewBox="0 0 24 24" class="logo-grade"><rect x="3.5" y="3.5" width="7.5" height="7.5" rx="2.2"/><rect x="13" y="3.5" width="7.5" height="7.5" rx="2.2" opacity=".85"/><rect x="3.5" y="13" width="7.5" height="7.5" rx="2.2" opacity=".85"/><rect x="13" y="13" width="7.5" height="7.5" rx="2.2" opacity=".65"/></svg>""");
    public static readonly MarkupString Inicio = new("""<svg viewBox="0 0 24 24"><path d="M3 10.5 12 3l9 7.5V20a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1z"/></svg>""");
    public static readonly MarkupString Dieta = new("""<svg viewBox="0 0 24 24"><path d="M12 21s-7.5-4.6-9.3-9.6C1.4 7.6 4 4 7.6 4c2 0 3.4 1.1 4.4 2.6C13 5.1 14.4 4 16.4 4 20 4 22.6 7.6 21.3 11.4 19.5 16.4 12 21 12 21z"/><polyline points="6 12 9.5 12 11 9.5 13 14.5 14.5 12 18 12"/></svg>""");
    public static readonly MarkupString Carro = new("""<svg viewBox="0 0 24 24"><path d="M4.5 15.5H3.3a.8.8 0 0 1-.8-.8v-2c0-.6.4-1.1 1-1.3l3.5-.9 2.6-3.2c.4-.5 1-.8 1.6-.8H15c.6 0 1.2.3 1.6.8l2.6 3.2 1.6.4c.6.2 1.1.7 1.1 1.4v2.4a.8.8 0 0 1-.8.8H19M8.5 15.5H15M7 10.5h12.2M12.5 6.5v4"/><circle cx="6.5" cy="15.5" r="2"/><circle cx="17" cy="15.5" r="2"/></svg>""");
    public static readonly MarkupString Assinaturas = new("""<svg viewBox="0 0 24 24"><rect x="3" y="5" width="18" height="15" rx="2.5"/><path d="M3 10h18M8 3v4M16 3v4"/><path d="M8.5 14.5h3"/></svg>""");
    public static readonly MarkupString Entretenimento = new("""<svg viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="16" rx="2.5"/><path d="M3 9h18M7.5 4l2 5M13 4l2 5"/><path d="m10.5 12.5 4 2.5-4 2.5z"/></svg>""");
    public static readonly MarkupString Tarefas = new("""<svg viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="16" rx="2.5"/><path d="M9 4v16M15 4v16"/><path d="M5.5 8h1.5M11 8h2M11 11.5h2M17 8h1.5"/></svg>""");
    public static readonly MarkupString Dados =new("""<svg viewBox="0 0 24 24"><ellipse cx="12" cy="5.5" rx="8" ry="2.8"/><path d="M4 5.5v13c0 1.5 3.6 2.8 8 2.8s8-1.3 8-2.8v-13M4 12c0 1.5 3.6 2.8 8 2.8s8-1.3 8-2.8"/></svg>""");
    public static readonly MarkupString Engrenagem = new("""<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/></svg>""");
    public static readonly MarkupString Usuario = new("""<svg viewBox="0 0 24 24"><circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0 1 16 0"/></svg>""");
    public static readonly MarkupString Sair = new("""<svg viewBox="0 0 24 24"><path d="M15 4h3a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-3M10 17l-5-5 5-5M5 12h11"/></svg>""");
    public static readonly MarkupString Sincronizar = new("""<svg viewBox="0 0 24 24"><path d="M20 11a8 8 0 0 0-14.3-4.9L4 8M4 4v4h4M4 13a8 8 0 0 0 14.3 4.9L20 16M20 20v-4h-4"/></svg>""");
    public static readonly MarkupString Menu =new("""<svg viewBox="0 0 24 24"><path d="M4 7h16M4 12h16M4 17h16"/></svg>""");
    public static readonly MarkupString Lua = new("""<svg class="i-moon" viewBox="0 0 24 24"><path d="M20 14.5A8 8 0 0 1 9.5 4 8 8 0 1 0 20 14.5z"/></svg>""");
    public static readonly MarkupString Sol = new("""<svg class="i-sun" viewBox="0 0 24 24"><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/></svg>""");
    public static readonly MarkupString Editar = new("""<svg viewBox="0 0 24 24"><path d="M4 20h4L19 9l-4-4L4 16z"/><path d="m13.5 6.5 4 4"/></svg>""");
    public static readonly MarkupString Excluir = new("""<svg viewBox="0 0 24 24"><path d="M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3"/></svg>""");
    public static readonly MarkupString Info = new("""<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="M12 11v5M12 7.5v.5"/></svg>""");
    public static readonly MarkupString Tocar = new("""<svg viewBox="0 0 24 24"><path d="M7 4.5v15l12.5-7.5z"/></svg>""");
    public static readonly MarkupString Pausar = new("""<svg viewBox="0 0 24 24"><path d="M8 5v14M16 5v14"/></svg>""");
    public static readonly MarkupString Proxima = new("""<svg viewBox="0 0 24 24"><path d="M5 5v14l10-7zM19 5v14"/></svg>""");
    public static readonly MarkupString Anterior = new("""<svg viewBox="0 0 24 24"><path d="M19 5v14L9 12zM5 5v14"/></svg>""");
    public static readonly MarkupString Musica = new("""<svg viewBox="0 0 24 24"><path d="M9 18V5l11-2v13"/><circle cx="6" cy="18" r="3"/><circle cx="17" cy="16" r="3"/></svg>""");
    public static readonly MarkupString Navegacao = new("""<svg viewBox="0 0 24 24"><path d="M12 2.5 19.5 21 12 17l-7.5 4z"/></svg>""");
    public static readonly MarkupString Seta =new("""<svg viewBox="0 0 24 24"><path d="M5 12h14M13 6l6 6-6 6"/></svg>""");
    public static readonly MarkupString Balanca = new("""<svg viewBox="0 0 24 24"><rect x="3" y="3" width="18" height="18" rx="4"/><path d="M8 9a5 5 0 0 1 8 0l-2.5 2.5"/><circle cx="12" cy="12.5" r=".8"/></svg>""");
    public static readonly MarkupString Bomba = new("""<svg viewBox="0 0 24 24"><path d="M4 21V5a2 2 0 0 1 2-2h7a2 2 0 0 1 2 2v16M3 21h13M4 10h11"/><path d="M15 8h2a2 2 0 0 1 2 2v6a1.5 1.5 0 0 0 3 0V8l-3-3"/></svg>""");
    public static readonly MarkupString Ferramenta = new("""<svg viewBox="0 0 24 24"><path d="M14.7 6.3a4 4 0 0 0 5 5L21 13l-8 8-2-2 6.2-6.2a4 4 0 0 1-5-5L10 5.6 5.6 10 3 7.4 7.4 3z"/></svg>""");
    public static readonly MarkupString Coracao = new("""<svg viewBox="0 0 24 24"><path d="M12 21s-7.5-4.6-9.3-9.6C1.4 7.6 4 4 7.6 4c2 0 3.4 1.1 4.4 2.6C13 5.1 14.4 4 16.4 4 20 4 22.6 7.6 21.3 11.4 19.5 16.4 12 21 12 21z"/></svg>""");
}
