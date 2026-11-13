using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Bluetooth do carro no Windows e no Linux: não existe (<see cref="Disponivel"/> = false).
/// A versão de verdade fica em Android/Plataforma/CarroBluetoothService.cs, com a mesma API, e o projeto Android exclui este arquivo.
/// </summary>
public sealed class CarroBluetoothService(ArmazenamentoService store)
{
    public bool Disponivel => false;

#pragma warning disable CS0067 // só a versão Android avisa mudanças
    /// <summary>Mudou o carro estacionado ou o estado de uma permissão (o app voltou para a frente).</summary>
    public event Action? Mudou;
#pragma warning restore CS0067

    /// <summary>O que o Modo Carro precisa do Android, com o estado de cada um (Configurações → Modo Carro).</summary>
    public IReadOnlyList<PermissaoCarro> Permissoes() => [];

    /// <summary>Pede a permissão ou abre a tela do Android onde ela é ativada (<see cref="PermissoesCarro"/>).</summary>
    public Task Ativar(string permissao) => Task.CompletedTask;

    /// <summary>Aparelhos pareados (vazio sem a permissão do Bluetooth).</summary>
    public IReadOnlyList<AparelhoBluetooth> Pareados() => [];

    /// <summary>Abre o Google Maps a pé até o carro estacionado.</summary>
    public void ComoChegar() { }

    public void EsquecerEstacionado()
    {
        store.Dados.Config.Estacionado = null;
        store.Salvar();
    }
}
