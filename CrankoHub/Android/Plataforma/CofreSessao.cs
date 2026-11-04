using Android.Content;
using AndroidX.Security.Crypto;

namespace CrankoHub.Services;

/// <summary>
/// Versão Android: guarda o refresh token da conta (nunca a senha) em SharedPreferences criptografadas
/// com uma chave do Android Keystore. Mesma API das versões Windows/Linux.
/// </summary>
/// <remarks>
/// Não usa o SecureStorage do MAUI: ele grava com Apply(), que termina de escrever no disco depois.
/// Ao entrar na conta o app reinicia encerrando o processo, e a gravação pendente se perdia:
/// ao abrir de novo não havia sessão e o app saía da conta sozinho ("Sessão encerrada").
/// Aqui a gravação usa Commit(), que só volta depois de o arquivo estar no disco.
/// </remarks>
public static class CofreSessao
{
    private const string Chave = "crankohub.sessao";
    private const string Arquivo = "crankohub.cofre";

    private static readonly Lock Trava = new();
    private static ISharedPreferences? _prefs;

    private static ISharedPreferences Prefs()
    {
        lock (Trava)
        {
            if (_prefs is not null) return _prefs;
            var ctx = global::Android.App.Application.Context;
            // Marcadas como obsoletas pelo Google, mas são as mesmas que o SecureStorage do MAUI usa por dentro
#pragma warning disable CS0618
            var chave = new MasterKey.Builder(ctx).SetKeyScheme(MasterKey.KeyScheme.Aes256Gcm!)!.Build();
            return _prefs = EncryptedSharedPreferences.Create(ctx, Arquivo, chave,
                EncryptedSharedPreferences.PrefKeyEncryptionScheme.Aes256Siv!,
                EncryptedSharedPreferences.PrefValueEncryptionScheme.Aes256Gcm!)!;
#pragma warning restore CS0618
        }
    }

    public static void Salvar(string refreshToken)
    {
        if (Prefs().Edit()!.PutString(Chave, refreshToken)!.Commit()) return;
        throw new IOException("Não foi possível guardar a sessão no aparelho.");
    }

    public static string? Ler()
    {
        try { return Prefs().GetString(Chave, null) is { Length: > 0 } t ? t : null; }
        catch { return null; } // chave do Keystore perdida (ex.: app restaurado em outro aparelho): como se não houvesse sessão
    }

    public static void Apagar()
    {
        try { Prefs().Edit()!.Remove(Chave)!.Commit(); } catch { }
    }
}
