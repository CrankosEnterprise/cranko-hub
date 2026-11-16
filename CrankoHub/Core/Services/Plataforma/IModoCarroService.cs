using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Modo Carro: aviso de radar por cima do Maps/Waze. Só existe no Android (Android/Plataforma/ModoCarroService.cs);
/// no Windows e no Linux fica o <see cref="ModoCarroIndisponivel"/>.
/// </summary>
public interface IModoCarroService
{
    bool Disponivel { get; }

    /// <summary>O Modo Carro está ativo: o aviso de radar fica por cima do Maps/Waze até sair.</summary>
    bool Ativo { get; }

    /// <summary>Radar à frente agora (null = nenhum perto).</summary>
    RadarProximo? Radar { get; }

    event Action? Mudou;

    /// <summary>Abre o app (<see cref="AppsCarro"/>) na tela inicial dele. false = app não instalado.</summary>
    bool AbrirApp(string app);

    /// <summary>
    /// Liga o Modo Carro (aviso de radar por cima) e abre o Maps ou o Waze já navegando até <paramref name="destino"/>
    /// (null = só abre o app). false = o app não está instalado.
    /// </summary>
    bool Navegar(string app, LugarBusca? destino);

    /// <summary>Toca o som do aviso de radar (<see cref="TonsAvisoRadar"/>), para testar na engrenagem do Carro.</summary>
    void TocarAviso(string tom);

    /// <summary>Desliga o Modo Carro: tira o aviso e a notificação.</summary>
    void Sair();

    /// <summary>Última posição conhecida do GPS (sem esperar), para a busca preferir lugares perto. null sem permissão.</summary>
    Task<PosicaoCarro?> PosicaoRapida();
}
