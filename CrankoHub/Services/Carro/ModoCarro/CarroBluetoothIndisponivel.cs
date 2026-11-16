using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Bluetooth do carro no Windows e no Linux: não existe (<see cref="Disponivel"/> = false).
/// A versão de verdade fica em Android/Plataforma/CarroBluetoothService.cs.
/// </summary>
public sealed class CarroBluetoothIndisponivel(ArmazenamentoService store) : ICarroBluetoothService
{
    public bool Disponivel => false;

    // Nunca muda: nada a avisar
    public event Action? Mudou { add { } remove { } }

    public IReadOnlyList<PermissaoCarro> Permissoes() => [];

    public Task Ativar(string permissao) => Task.CompletedTask;

    public IReadOnlyList<AparelhoBluetooth> Pareados() => [];

    public void ComoChegar() { }

    public void EsquecerEstacionado()
    {
        store.Dados.Config.Estacionado = null;
        store.Salvar();
    }
}
