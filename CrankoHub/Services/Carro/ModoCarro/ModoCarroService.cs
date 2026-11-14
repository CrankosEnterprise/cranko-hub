using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Modo Carro no Windows e no Linux: não existe (<see cref="Disponivel"/> = false).
/// A versão de verdade fica em Android/Plataforma/ModoCarroService.cs, com a mesma API, e o projeto Android exclui este arquivo.
/// </summary>
public sealed class ModoCarroService
{
    public bool Disponivel => false;

    /// <summary>O Modo Carro está ativo: a barra da música fica por cima do Maps/Waze até sair.</summary>
    public bool Ativo => false;

    /// <summary>O que está tocando agora (qualquer player), ou null.</summary>
    public MusicaAtual? Musica => null;

#pragma warning disable CS0067 // só a versão Android avisa mudanças
    public event Action? Mudou;
#pragma warning restore CS0067

    /// <summary>Abre o app (<see cref="AppsCarro"/>) na tela inicial dele. false = app não instalado.</summary>
    public bool AbrirApp(string app) => false;

    /// <summary>
    /// Liga o Modo Carro (barra da música por cima) e abre o Maps ou o Waze já navegando até <paramref name="destino"/>
    /// (null = só abre o app). false = o app não está instalado.
    /// </summary>
    public bool Navegar(string app, LugarBusca? destino) => false;

    /// <summary>Desliga o Modo Carro: tira a barra e a notificação.</summary>
    public void Sair() { }

    /// <summary>Última posição conhecida do GPS (sem esperar), para a busca preferir lugares perto. null sem permissão.</summary>
    public Task<PosicaoCarro?> PosicaoRapida() => Task.FromResult<PosicaoCarro?>(null);

    public void TocarPausar() { }

    public void Proxima() { }

    public void Anterior() { }
}
