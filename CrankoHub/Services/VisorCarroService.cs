using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// A rota no visor da central (Bluetooth) no Windows e no Linux: não existe (<see cref="Disponivel"/> = false).
/// A versão de verdade fica em Android/Plataforma/VisorCarroService.cs, com a mesma API, e o projeto Android exclui este arquivo.
/// </summary>
public sealed class VisorCarroService
{
    public bool Disponivel => false;

    public bool Testando => false;

    /// <summary>O visor está com o CrankoHub (no teste ou durante a rota).</summary>
    public bool Ligado => false;

    /// <summary>Índice em <see cref="TesteVisor.Etapas"/>.</summary>
    public int Etapa => 0;

    /// <summary>O que está indo para o visor agora.</summary>
    public TextosVisor? Enviando => null;

    /// <summary>As últimas linhas do log (a mais nova no fim).</summary>
    public IReadOnlyList<string> Linhas => [];

#pragma warning disable CS0067 // só a versão Android avisa mudanças
    public event Action? Mudou;
#pragma warning restore CS0067

    /// <summary>Abriu o Modo Carro: passa a acompanhar a rota e liga o visor quando ela começar (Configuracoes.VisorCentral).</summary>
    public void Entrar() { }

    /// <summary>Saiu do Modo Carro: para o teste e devolve o visor ao Spotify.</summary>
    public void Sair() { }

    /// <summary>Começa o teste com log (etapas em <see cref="TesteVisor.Etapas"/>).</summary>
    public void Iniciar() { }

    public void ProximaEtapa() { }

    /// <summary>Guarda no log o que a pessoa viu (<see cref="TesteVisor.Apareceu"/>...).</summary>
    public void Anotar(string resposta) { }

    public Task CompartilharLog() => Task.CompletedTask;

    public void Parar() { }
}
