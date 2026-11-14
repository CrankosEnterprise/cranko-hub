namespace CrankoHub.Models;

/// <summary>Preferências do aplicativo. A cor de destaque e os módulos (ocultos, ordem e personalização) vão para a conta (tabela perfis).</summary>
public class Configuracoes
{
    /// <summary>Cor de destaque: chave da paleta (ver <see cref="Models.CorDestaque"/>) ou "#RRGGBB".</summary>
    public string CorDestaque { get; set; } = "marinho";

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

    /// <summary>
    /// Entretenimento: trazer e mostrar o nome de cada episódio (sem isso, só os números). Nos animes, os nomes vêm do
    /// MyAnimeList (Jikan), que demora mais. Fica na engrenagem do Entretenimento e vai para a conta (tabela perfis).
    /// </summary>
    public bool NomesEpisodios { get; set; } = true;

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

    /// <summary>Ordem dos módulos no menu, nos atalhos e no Início (ids). Vazio = ordem padrão; os que faltarem vão para o fim.</summary>
    public List<string> OrdemModulos { get; set; } = [];

    /// <summary>Nome, descrição, cor e ícone que o usuário mudou em cada módulo (por id).</summary>
    public Dictionary<string, PersonalizacaoModulo> ModulosPersonalizados { get; set; } = [];

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

    /// <summary>Modo Carro: layout da tela (ver <see cref="LayoutsCarro"/>).</summary>
    public string LayoutCarro { get; set; } = LayoutsCarro.Lateral;

    /// <summary>
    /// Modo Carro: tamanho escolhido do painel (% da largura na paisagem, da altura no retrato), por layout.
    /// Chave: o layout, com "-r" no retrato (ver <see cref="LayoutsCarro.Chave"/>). Sem valor = o padrão do layout.
    /// </summary>
    public Dictionary<string, int> TamanhoPainelCarro { get; set; } = [];

    /// <summary>Modo Carro: painel do lado esquerdo na paisagem (em cima no retrato), em vez de à direita (embaixo).</summary>
    public bool PainelCarroEsquerda { get; set; }

    /// <summary>Modo Carro: layouts em que os atalhos (Spotify, Maps, Waze) ficam escondidos.</summary>
    public List<string> AtalhosOcultosCarro { get; set; } = [];

    /// <summary>Modo Carro: aparelhos Bluetooth marcados como "o meu carro" (centrais). Fones e caixinhas não entram.</summary>
    public List<CarroPareado> CarrosBluetooth { get; set; } = [];

    /// <summary>Modo Carro: o que fazer ao conectar no Bluetooth do carro (ver <see cref="AcoesConexao"/>).</summary>
    public string AoConectarCarro { get; set; } = AcoesConexao.Notificar;

    /// <summary>Modo Carro: sair do Modo Carro quando o Bluetooth do carro desconecta (a rota continua guardada).</summary>
    public bool SairAoDesconectar { get; set; } = true;

    /// <summary>Modo Carro: guardar onde o carro ficou quando o Bluetooth dele desconecta.</summary>
    public bool LembrarEstacionado { get; set; } = true;

    /// <summary>Modo Carro: guardar o estacionado mesmo com o Modo Carro fechado (precisa da localização "o tempo todo").</summary>
    public bool EstacionadoComAppFechado { get; set; }

    /// <summary>Modo Carro: onde o carro ficou (null = não guardado, ou já voltou ao carro).</summary>
    public CarroEstacionado? Estacionado { get; set; }
}
