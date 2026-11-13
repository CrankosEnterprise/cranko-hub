using System.Security.Cryptography;
using System.Text;

namespace CrankoHub.Services;

/// <summary>
/// Guarda o refresh token da conta (nunca a senha) em %LocalAppData%\CrankoHub\sessao.bin,
/// criptografado com o DPAPI do Windows: só o mesmo usuário, neste computador, consegue ler.
/// </summary>
public static class CofreSessao
{
    private static readonly string Caminho = Path.Combine(ConfigLocal.Pasta, "sessao.bin");

    public static void Salvar(string refreshToken)
    {
        Directory.CreateDirectory(ConfigLocal.Pasta);
        var cifrado = ProtectedData.Protect(Encoding.UTF8.GetBytes(refreshToken), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(Caminho, cifrado);
    }

    public static string? Ler()
    {
        try
        {
            if (!File.Exists(Caminho)) return null;
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(Caminho), null, DataProtectionScope.CurrentUser));
        }
        catch { return null; } // arquivo de outro usuário/PC ou corrompido: como se não houvesse sessão
    }

    public static void Apagar()
    {
        try { File.Delete(Caminho); } catch { }
    }
}
