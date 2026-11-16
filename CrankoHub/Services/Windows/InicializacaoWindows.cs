using Microsoft.Win32;

namespace CrankoHub.Services;

/// <summary>Liga/desliga a abertura do CrankoHub junto com o Windows (só para o usuário atual).</summary>
public sealed class InicializacaoWindows : IInicializacaoSistema
{
    private const string Chave = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Nome = "CrankoHub";

    /// <summary>Argumento usado ao iniciar com o Windows: abre direto na bandeja.</summary>
    public const string ArgMinimizado = "--minimizado";

    public void Aplicar(bool ligado)
    {
        using var run = Registry.CurrentUser.CreateSubKey(Chave);
        if (ligado && Environment.ProcessPath is string exe)
            run.SetValue(Nome, $"\"{exe}\" {ArgMinimizado}");
        else
            run.DeleteValue(Nome, throwOnMissingValue: false);
    }
}
