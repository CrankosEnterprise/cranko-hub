namespace CrankoHub.Services;

/// <summary>
/// Título da página no cabeçalho do layout. Por padrão é o nome e a descrição do módulo; uma página pode trocar
/// pelo seu (ex.: o Carro mostra o nome do veículo e a placa) com o componente CabecalhoPagina.
/// Quem definiu é o "dono": ao sair da página, ele só limpa se ainda for o dono (a página nova pode já ter definido).
/// </summary>
public class CabecalhoService
{
    private object? _dono;

    public string? Sobre { get; private set; }
    public string? Titulo { get; private set; }
    public string? Selo { get; private set; }

    public event Action? Mudou;

    public void Definir(object dono, string? sobre, string? titulo, string? selo)
    {
        if (_dono == dono && Sobre == sobre && Titulo == titulo && Selo == selo) return;
        (_dono, Sobre, Titulo, Selo) = (dono, sobre, titulo, selo);
        Mudou?.Invoke();
    }

    public void Limpar(object dono)
    {
        if (_dono != dono) return;
        (_dono, Sobre, Titulo, Selo) = (null, null, null, null);
        Mudou?.Invoke();
    }
}
