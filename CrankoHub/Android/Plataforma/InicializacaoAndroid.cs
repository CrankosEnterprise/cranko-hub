namespace CrankoHub.Services;

/// <summary>Versão Android: não existe "iniciar com o sistema" (a opção fica escondida em Configurações).</summary>
public sealed class InicializacaoAndroid : IInicializacaoSistema
{
    public void Aplicar(bool ligado) { }
}
