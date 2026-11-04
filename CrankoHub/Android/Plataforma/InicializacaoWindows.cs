namespace CrankoHub.Services;

/// <summary>
/// Versão Android: não existe "iniciar com o sistema" (a opção fica escondida em Configurações).
/// O nome da classe é o mesmo da versão Windows para as telas compilarem sem mudança.
/// </summary>
public static class InicializacaoWindows
{
    public const string ArgMinimizado = "--minimizado";

    public static void Aplicar(bool ligado) { }
}
