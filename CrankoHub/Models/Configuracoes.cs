namespace CrankoHub.Models;

/// <summary>Preferências do aplicativo. Só a cor de destaque e os módulos ocultos vão para a conta (tabela perfis).</summary>
public class Configuracoes
{
    /// <summary>Cor de destaque: chave da paleta (ver <see cref="Models.CorDestaque"/>) ou "#RRGGBB".</summary>
    public string CorDestaque { get; set; } = "azul";

    /// <summary>O X da janela esconde o app na bandeja (perto do relógio) em vez de fechar.</summary>
    public bool MinimizarParaBandeja { get; set; } = true;

    /// <summary>Menu lateral escondido no computador (no celular ele já fica fechado e abre por cima).</summary>
    public bool MenuOculto { get; set; }

    /// <summary>Já avisou que o app continua rodando na bandeja.</summary>
    public bool DicaBandejaMostrada { get; set; }

    public bool IniciarComWindows { get; set; }

    /// <summary>Notificações do Windows com os alertas do Início.</summary>
    public bool Avisos { get; set; } = true;

    /// <summary>Chave do alerta → dia em que foi avisado (cada alerta é avisado uma vez por dia).</summary>
    public Dictionary<string, DateOnly> AvisosEnviados { get; set; } = [];

    /// <summary>Chave da API do TMDB (filmes, séries e animações).</summary>
    public string? ChaveTmdb { get; set; }

    /// <summary>Chave da API do RAWG (jogos).</summary>
    public string? ChaveRawg { get; set; }

    /// <summary>
    /// Chave do Google Maps Platform (Maps JavaScript, Places New e Routes). Com ela, o Modo Carro usa mapa, busca e rotas
    /// do Google; sem ela, OpenStreetMap/Photon/Valhalla. Fica só no aparelho (não vai para a conta).
    /// </summary>
    public string? ChaveGoogleMaps { get; set; }

    /// <summary>ID do mapa do Google (vetorial, com inclinação e rotação). Sem ele, usa o DEMO_MAP_ID.</summary>
    public string? MapaIdGoogle { get; set; }

    /// <summary>Módulos escondidos do menu, do Início e dos avisos (os dados continuam salvos).</summary>
    public List<string> ModulosOcultos { get; set; } = [];

    public bool VerificarAtualizacoes { get; set; } = true;

    public DateOnly? UltimaVerificacaoAtualizacao { get; set; }

    /// <summary>Modo Carro: últimos destinos das rotas (fica no aparelho).</summary>
    public List<LugarBusca> DestinosRecentes { get; set; } = [];

    /// <summary>Modo Carro: avisos por voz na navegação (manobras, pedágios, radares e limite). Opcional, ligado em Configurações → Aplicativo.</summary>
    public bool VozModoCarro { get; set; }

    /// <summary>Modo Carro: calcular as rotas evitando pedágios.</summary>
    public bool EvitarPedagios { get; set; }

    /// <summary>Modo Carro: mostrar a próxima curva no visor da central (pelo Bluetooth) durante a rota.</summary>
    public bool VisorCentral { get; set; } = true;

    /// <summary>Modo Carro: mostrar o botão "Testar visor da central" (diagnóstico com log).</summary>
    public bool TesteVisor { get; set; } = true;
}
