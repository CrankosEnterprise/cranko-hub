namespace CrankoHub.Models;

/// <summary>Preferências do aplicativo. Só a cor de destaque e os módulos ocultos vão para a conta (tabela perfis).</summary>
public class Configuracoes
{
    /// <summary>O X da janela esconde o app na bandeja (perto do relógio) em vez de fechar.</summary>
    /// <summary>Cor de destaque: chave da paleta (ver <see cref="Models.CorDestaque"/>) ou "#RRGGBB".</summary>
    public string CorDestaque { get; set; } = "azul";

    public bool MinimizarParaBandeja { get; set; } = true;

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

    /// <summary>Módulos escondidos do menu, do Início e dos avisos (os dados continuam salvos).</summary>
    public List<string> ModulosOcultos { get; set; } = [];

    public bool VerificarAtualizacoes { get; set; } = true;

    public DateOnly? UltimaVerificacaoAtualizacao { get; set; }
}
