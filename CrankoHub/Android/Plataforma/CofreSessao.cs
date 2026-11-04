namespace CrankoHub.Services;

/// <summary>
/// Versão Android: guarda o refresh token da conta (nunca a senha) no SecureStorage do MAUI,
/// que criptografa com uma chave do Android Keystore. Mesma API das versões Windows/Linux.
/// </summary>
public static class CofreSessao
{
    private const string Chave = "crankohub.sessao";

    // No Android o SecureStorage roda numa thread do pool: esperar o resultado aqui não trava a tela
    public static void Salvar(string refreshToken) =>
        SecureStorage.Default.SetAsync(Chave, refreshToken).GetAwaiter().GetResult();

    public static string? Ler()
    {
        try { return SecureStorage.Default.GetAsync(Chave).GetAwaiter().GetResult() is { Length: > 0 } t ? t : null; }
        catch { return null; } // chave do Keystore perdida (ex.: app restaurado em outro aparelho): como se não houvesse sessão
    }

    public static void Apagar()
    {
        try { SecureStorage.Default.Remove(Chave); } catch { }
    }
}
