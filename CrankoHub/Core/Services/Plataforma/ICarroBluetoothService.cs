using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Bluetooth do carro (Modo Carro automático e carro estacionado). Só existe no Android
/// (Android/Plataforma/CarroBluetoothService.cs); no Windows e no Linux fica o <see cref="CarroBluetoothIndisponivel"/>.
/// </summary>
public interface ICarroBluetoothService
{
    bool Disponivel { get; }

    /// <summary>Mudou o carro estacionado ou o estado de uma permissão (o app voltou para a frente).</summary>
    event Action? Mudou;

    /// <summary>O que o Modo Carro precisa do Android, com o estado de cada um (engrenagem da página Carro).</summary>
    IReadOnlyList<PermissaoCarro> Permissoes();

    /// <summary>Pede a permissão ou abre a tela do Android onde ela é ativada (<see cref="PermissoesCarro"/>).</summary>
    Task Ativar(string permissao);

    /// <summary>Aparelhos pareados (vazio sem a permissão do Bluetooth).</summary>
    IReadOnlyList<AparelhoBluetooth> Pareados();

    /// <summary>Abre o Google Maps a pé até o carro estacionado.</summary>
    void ComoChegar();

    void EsquecerEstacionado();
}
