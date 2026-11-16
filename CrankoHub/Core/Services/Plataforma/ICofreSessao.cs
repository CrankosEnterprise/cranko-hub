namespace CrankoHub.Services;

/// <summary>
/// Guarda o refresh token da conta (nunca a senha) do jeito seguro de cada plataforma:
/// DPAPI no Windows, arquivo com permissão 600 no Linux, Keystore no Android.
/// </summary>
public interface ICofreSessao
{
    void Salvar(string refreshToken);

    /// <summary>O token guardado, ou null se não há sessão (ou ela não pode ser lida neste aparelho).</summary>
    string? Ler();

    void Apagar();
}
