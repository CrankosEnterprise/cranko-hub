namespace CrankoHub.Services;

/// <summary>Liga/desliga a abertura do CrankoHub junto com o sistema (registro no Windows, autostart no Linux).</summary>
public interface IInicializacaoSistema
{
    void Aplicar(bool ligado);
}
