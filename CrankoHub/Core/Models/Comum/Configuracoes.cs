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

    /// <summary>Módulos escondidos do menu, do Início e dos avisos (os dados continuam salvos).</summary>
    public List<string> ModulosOcultos { get; set; } = [];

    /// <summary>Ordem dos módulos no menu, nos atalhos e no Início (ids). Vazio = ordem padrão; os que faltarem vão para o fim.</summary>
    public List<string> OrdemModulos { get; set; } = [];

    /// <summary>Nome, descrição, cor e ícone que o usuário mudou em cada módulo (por id).</summary>
    public Dictionary<string, PersonalizacaoModulo> ModulosPersonalizados { get; set; } = [];

    public bool VerificarAtualizacoes { get; set; } = true;

    public DateOnly? UltimaVerificacaoAtualizacao { get; set; }

    /// <summary>Modo Carro: últimos destinos escolhidos (fica no aparelho).</summary>
    public List<LugarBusca> DestinosRecentes { get; set; } = [];

    /// <summary>Modo Carro: app de navegação escolhido por último (ver <see cref="AppsCarro.Navegacao"/>).</summary>
    public string AppNavegacaoCarro { get; set; } = AppsCarro.Maps;

    /// <summary>Modo Carro: onde fica o aviso de radar por cima do Maps/Waze, na borda esquerda (ver <see cref="PosicoesAvisoRadar"/>). Engrenagem do Carro.</summary>
    public string AvisoRadarPosicao { get; set; } = PosicoesAvisoRadar.Meio;

    /// <summary>Modo Carro: tocar um som quando aparece um radar novo à frente.</summary>
    public bool AvisoRadarSom { get; set; } = true;

    /// <summary>Modo Carro: qual som (ver <see cref="TonsAvisoRadar"/>).</summary>
    public string AvisoRadarTom { get; set; } = TonsAvisoRadar.Duplo;

    /// <summary>Modo Carro: a quantos metros do radar o aviso aparece (300 a 2.000).</summary>
    public int AvisoRadarDistancia { get; set; } = 1000;

    /// <summary>Modo Carro: segundos que o aviso fica na tela (0 = até passar pelo radar).</summary>
    public int AvisoRadarTempo { get; set; }

    /// <summary>Modo Carro: aparelhos Bluetooth marcados como "o meu carro" (centrais). Fones e caixinhas não entram.</summary>
    public List<CarroPareado> CarrosBluetooth { get; set; } = [];

    /// <summary>Modo Carro: o que fazer ao conectar no Bluetooth do carro (ver <see cref="AcoesConexao"/>).</summary>
    public string AoConectarCarro { get; set; } = AcoesConexao.Notificar;

    /// <summary>Modo Carro: sair do Modo Carro quando o Bluetooth do carro desconecta.</summary>
    public bool SairAoDesconectar { get; set; } = true;

    /// <summary>Modo Carro: guardar onde o carro ficou quando o Bluetooth dele desconecta.</summary>
    public bool LembrarEstacionado { get; set; } = true;

    /// <summary>Modo Carro: guardar o estacionado mesmo sem o Modo Carro ativo (precisa da localização "o tempo todo").</summary>
    public bool EstacionadoComAppFechado { get; set; }

    /// <summary>Modo Carro: onde o carro ficou (null = não guardado, ou já voltou ao carro).</summary>
    public CarroEstacionado? Estacionado { get; set; }
}
