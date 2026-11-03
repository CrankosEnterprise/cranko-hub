namespace CrankoHub.Services;

/// <summary>
/// Versão Linux: guarda o refresh token da conta (nunca a senha) em ~/.local/share/CrankoHub/sessao,
/// com permissão 600 (só o próprio usuário lê). O Linux não tem um DPAPI simples como o Windows.
/// </summary>
public static class CofreSessao
{
    private static readonly string Caminho = Path.Combine(ConfigLocal.Pasta, "sessao");

    public static void Salvar(string refreshToken)
    {
        Directory.CreateDirectory(ConfigLocal.Pasta);
        if (OperatingSystem.IsWindows()) { File.WriteAllText(Caminho, refreshToken); return; } // só para o analisador: este arquivo roda no Linux
        var opcoes = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        };
        using (var escrita = new StreamWriter(Caminho, opcoes))
            escrita.Write(refreshToken);
        // O UnixCreateMode só vale para arquivo novo: garante 600 também se ele já existia
        File.SetUnixFileMode(Caminho, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public static string? Ler()
    {
        try { return File.Exists(Caminho) ? File.ReadAllText(Caminho).Trim() is { Length: > 0 } t ? t : null : null; }
        catch { return null; }
    }

    public static void Apagar()
    {
        try { File.Delete(Caminho); } catch { }
    }
}
