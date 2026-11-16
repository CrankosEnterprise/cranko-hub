using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Modo Carro no Windows e no Linux: não existe (<see cref="Disponivel"/> = false).
/// A versão de verdade fica em Android/Plataforma/ModoCarroService.cs.
/// </summary>
public sealed class ModoCarroIndisponivel : IModoCarroService
{
    public bool Disponivel => false;

    public bool Ativo => false;

    public RadarProximo? Radar => null;

    // Nunca muda: nada a avisar
    public event Action? Mudou { add { } remove { } }

    public bool AbrirApp(string app) => false;

    public bool Navegar(string app, LugarBusca? destino) => false;

    public void TocarAviso(string tom) { }

    public void Sair() { }

    public Task<PosicaoCarro?> PosicaoRapida() => Task.FromResult<PosicaoCarro?>(null);
}
