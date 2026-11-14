using Microsoft.AspNetCore.Components;

namespace CrankoHub.Components;

/// <summary>Ícones SVG (traço) usados na interface.</summary>
public static class Icones
{
    // Mesmo desenho do ícone do app (Assets/icone.ico, Linux/icone.png): C de módulos brancos em 3D com o H no meio.
    // O H fica sempre no lilás da marca (classes lm-h / lm-h-lado no app.css), qualquer que seja a cor de destaque.
    private const string LogoSegmentos = """<path d="M362.1 149.9A150 150 0 0 0 215.3 111.6"/><path d="M193.2 119.8A150 150 0 0 0 106.5 244.2"/><path d="M106.5 267.8A150 150 0 0 0 193.2 392.2"/><path d="M215.3 400.4A150 150 0 0 0 362.1 362.1"/>""";
    private const string LogoH = "M214 200V304M298 200V304M214 252H298";
    public static readonly MarkupString Logo = new($$"""<svg viewBox="0 0 512 512" class="logo-marca"><defs><radialGradient id="logo-fundo" cx=".5" cy=".35" r=".8"><stop offset="0" stop-color="#1b2f63"/><stop offset="1" stop-color="#060b1d"/></radialGradient></defs><rect width="512" height="512" rx="115" fill="url(#logo-fundo)" stroke="none"/><g fill="none" stroke-width="56" stroke-linecap="butt" transform="translate(-3 -10)"><g stroke="#7d89b0" transform="translate(5 16)">{{LogoSegmentos}}</g><g stroke="#a9b6dc" transform="translate(2.5 8)">{{LogoSegmentos}}</g><g stroke="#f1f4ff">{{LogoSegmentos}}</g><path class="lm-h-lado" d="{{LogoH}}" stroke-width="42" stroke-linecap="round" transform="translate(4 13)"/><path class="lm-h" d="{{LogoH}}" stroke-width="42" stroke-linecap="round"/></g></svg>""");
    public static readonly MarkupString Inicio = new("""<svg viewBox="0 0 24 24"><path d="M3 10.5 12 3l9 7.5V20a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1z"/></svg>""");
    public static readonly MarkupString Dieta = new("""<svg viewBox="0 0 24 24"><path d="M12 21s-7.5-4.6-9.3-9.6C1.4 7.6 4 4 7.6 4c2 0 3.4 1.1 4.4 2.6C13 5.1 14.4 4 16.4 4 20 4 22.6 7.6 21.3 11.4 19.5 16.4 12 21 12 21z"/><polyline points="6 12 9.5 12 11 9.5 13 14.5 14.5 12 18 12"/></svg>""");
    public static readonly MarkupString Carro = new("""<svg viewBox="0 0 24 24"><path d="M4.5 15.5H3.3a.8.8 0 0 1-.8-.8v-2c0-.6.4-1.1 1-1.3l3.5-.9 2.6-3.2c.4-.5 1-.8 1.6-.8H15c.6 0 1.2.3 1.6.8l2.6 3.2 1.6.4c.6.2 1.1.7 1.1 1.4v2.4a.8.8 0 0 1-.8.8H19M8.5 15.5H15M7 10.5h12.2M12.5 6.5v4"/><circle cx="6.5" cy="15.5" r="2"/><circle cx="17" cy="15.5" r="2"/></svg>""");
    public static readonly MarkupString Assinaturas = new("""<svg viewBox="0 0 24 24"><rect x="3" y="5" width="18" height="15" rx="2.5"/><path d="M3 10h18M8 3v4M16 3v4"/><path d="M8.5 14.5h3"/></svg>""");
    public static readonly MarkupString Entretenimento = new("""<svg viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="16" rx="2.5"/><path d="M3 9h18M7.5 4l2 5M13 4l2 5"/><path d="m10.5 12.5 4 2.5-4 2.5z"/></svg>""");
    public static readonly MarkupString Tarefas = new("""<svg viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="16" rx="2.5"/><path d="M9 4v16M15 4v16"/><path d="M5.5 8h1.5M11 8h2M11 11.5h2M17 8h1.5"/></svg>""");
    public static readonly MarkupString Agenda = new("""<svg viewBox="0 0 24 24"><rect x="3" y="5" width="18" height="16" rx="2.5"/><path d="M3 10h18M8 3v4M16 3v4"/><path d="M7.5 14h2M11 14h2M14.5 14h2M7.5 17.5h2M11 17.5h2"/></svg>""");
    public static readonly MarkupString Relogio = new("""<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></svg>""");
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
    // Layout do Modo Carro: tela com uma área grande e um painel ao lado
    public static readonly MarkupString Layout = new("""<svg viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="16" rx="2.5"/><path d="M14.5 4v16M14.5 10H21"/></svg>""");
    public static readonly MarkupString Recolher = new("""<svg viewBox="0 0 24 24"><path d="m6 9 6 6 6-6"/></svg>""");
    public static readonly MarkupString Navegacao =new("""<svg viewBox="0 0 24 24"><path d="M12 2.5 19.5 21 12 17l-7.5 4z"/></svg>""");
    public static readonly MarkupString Busca = new("""<svg viewBox="0 0 24 24"><circle cx="11" cy="11" r="7"/><path d="m20 20-4-4"/></svg>""");
    public static readonly MarkupString Fechar = new("""<svg viewBox="0 0 24 24"><path d="M6 6l12 12M18 6 6 18"/></svg>""");
    public static readonly MarkupString Anexo = new("""<svg viewBox="0 0 24 24"><path d="m20.5 11.5-8.3 8.3a5 5 0 0 1-7-7l8.6-8.6a3.3 3.3 0 0 1 4.7 4.7l-8.6 8.6a1.7 1.7 0 0 1-2.4-2.4l7.9-7.9"/></svg>""");

    /// <summary>Seta da manobra da rota (chaves do <c>RotaService</c>).</summary>
    public static MarkupString Manobra(string chave) => chave switch
    {
        "esquerda" => new("""<svg viewBox="0 0 24 24"><path d="M17 21v-8a4 4 0 0 0-4-4H5"/><path d="M9 5 5 9l4 4"/></svg>"""),
        "direita" => new("""<svg viewBox="0 0 24 24"><path d="M7 21v-8a4 4 0 0 1 4-4h8"/><path d="m15 5 4 4-4 4"/></svg>"""),
        "leve-esquerda" => new("""<svg viewBox="0 0 24 24"><path d="M15 21v-7L8 7"/><path d="M8 13V7h6"/></svg>"""),
        "leve-direita" => new("""<svg viewBox="0 0 24 24"><path d="M9 21v-7l7-7"/><path d="M10 7h6v6"/></svg>"""),
        "retorno" => new("""<svg viewBox="0 0 24 24"><path d="M17 21V9a4.5 4.5 0 0 0-9 0v7"/><path d="m4 12 4 4 4-4"/></svg>"""),
        "rotatoria" => new("""<svg viewBox="0 0 24 24"><circle cx="12" cy="9" r="4.5"/><path d="M12 21v-7.5M15.2 5.8 19 2M15 2h4v4"/></svg>"""),
        "chegada" => new("""<svg viewBox="0 0 24 24"><path d="M6 21V4"/><path d="M6 4h11l-2.5 4.5L17 13H6"/></svg>"""),
        _ => new("""<svg viewBox="0 0 24 24"><path d="M12 21V4"/><path d="m6 10 6-6 6 6"/></svg>"""),
    };

    public static readonly MarkupString Seta =new("""<svg viewBox="0 0 24 24"><path d="M5 12h14M13 6l6 6-6 6"/></svg>""");
    public static readonly MarkupString Balanca = new("""<svg viewBox="0 0 24 24"><rect x="3" y="3" width="18" height="18" rx="4"/><path d="M8 9a5 5 0 0 1 8 0l-2.5 2.5"/><circle cx="12" cy="12.5" r=".8"/></svg>""");
    public static readonly MarkupString Bomba = new("""<svg viewBox="0 0 24 24"><path d="M4 21V5a2 2 0 0 1 2-2h7a2 2 0 0 1 2 2v16M3 21h13M4 10h11"/><path d="M15 8h2a2 2 0 0 1 2 2v6a1.5 1.5 0 0 0 3 0V8l-3-3"/></svg>""");
    public static readonly MarkupString Ferramenta = new("""<svg viewBox="0 0 24 24"><path d="M14.7 6.3a4 4 0 0 0 5 5L21 13l-8 8-2-2 6.2-6.2a4 4 0 0 1-5-5L10 5.6 5.6 10 3 7.4 7.4 3z"/></svg>""");
    public static readonly MarkupString Mais = new("""<svg viewBox="0 0 24 24"><path d="M12 5v14M5 12h14"/></svg>""");
    public static readonly MarkupString Alvo = new("""<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="5"/><circle cx="12" cy="12" r="1"/></svg>""");
    public static readonly MarkupString Coracao = new("""<svg viewBox="0 0 24 24"><path d="M12 21s-7.5-4.6-9.3-9.6C1.4 7.6 4 4 7.6 4c2 0 3.4 1.1 4.4 2.6C13 5.1 14.4 4 16.4 4 20 4 22.6 7.6 21.3 11.4 19.5 16.4 12 21 12 21z"/></svg>""");
    public static readonly MarkupString Estrela = new("""<svg viewBox="0 0 24 24"><path d="m12 3 2.8 5.7 6.2.9-4.5 4.4 1.1 6.2L12 17.3l-5.6 2.9 1.1-6.2L3 9.6l6.2-.9z"/></svg>""");
    public static readonly MarkupString Livro = new("""<svg viewBox="0 0 24 24"><path d="M4 5.5A2.5 2.5 0 0 1 6.5 3H20v15H6.5A2.5 2.5 0 0 0 4 20.5z"/><path d="M4 20.5A2.5 2.5 0 0 0 6.5 23H20v-5"/></svg>""");
    public static readonly MarkupString Dinheiro = new("""<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="M15 9.2c-.5-1-1.6-1.6-3-1.6-1.7 0-3 .9-3 2.2 0 3 6 1.6 6 4.4 0 1.3-1.3 2.2-3 2.2-1.4 0-2.6-.6-3.1-1.7M12 6v1.6M12 16.4V18"/></svg>""");
    public static readonly MarkupString Haltere = new("""<svg viewBox="0 0 24 24"><path d="M6.5 6.5v11M17.5 6.5v11M3.5 9v6M20.5 9v6M6.5 12h11"/></svg>""");
    public static readonly MarkupString Pasta = new("""<svg viewBox="0 0 24 24"><path d="M3 7a2 2 0 0 1 2-2h4l2 2.5h8a2 2 0 0 1 2 2V18a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/></svg>""");
    public static readonly MarkupString Alca = new("""<svg viewBox="0 0 24 24"><circle cx="9" cy="6" r="1.6"/><circle cx="15" cy="6" r="1.6"/><circle cx="9" cy="12" r="1.6"/><circle cx="15" cy="12" r="1.6"/><circle cx="9" cy="18" r="1.6"/><circle cx="15" cy="18" r="1.6"/></svg>""");
    public static readonly MarkupString Subir = new("""<svg viewBox="0 0 24 24"><path d="m6 15 6-6 6 6"/></svg>""");

    /// <summary>Ícones que o usuário pode escolher para um módulo (Configurações → Módulos). A chave fica salva na conta.</summary>
    public static readonly IReadOnlyList<(string Chave, string Nome, MarkupString Svg)> DeModulo =
    [
        ("dieta", "Saúde", Dieta),
        ("carro", "Carro", Carro),
        ("assinaturas", "Cartão", Assinaturas),
        ("entretenimento", "Filme", Entretenimento),
        ("tarefas", "Quadro", Tarefas),
        ("agenda", "Calendário", Agenda),
        ("coracao", "Coração", Coracao),
        ("balanca", "Balança", Balanca),
        ("haltere", "Treino", Haltere),
        ("alvo", "Meta", Alvo),
        ("estrela", "Estrela", Estrela),
        ("livro", "Livro", Livro),
        ("musica", "Música", Musica),
        ("dinheiro", "Dinheiro", Dinheiro),
        ("bomba", "Combustível", Bomba),
        ("ferramenta", "Ferramenta", Ferramenta),
        ("relogio", "Relógio", Relogio),
        ("pasta", "Pasta", Pasta),
        ("casa", "Casa", Inicio),
    ];

    /// <summary>O ícone do catálogo <see cref="DeModulo"/> com essa chave (null se não existir).</summary>
    public static MarkupString? IconeModulo(string? chave) =>
        DeModulo.FirstOrDefault(i => i.Chave == chave) is { Chave: not null } i ? i.Svg : null;
}
