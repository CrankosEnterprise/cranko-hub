namespace CrankoHub.Services;

/// <summary>
/// Procura versões novas no GitHub Releases e instala. Cada plataforma baixa o seu arquivo
/// (.exe no Windows, .tar.gz no Linux, .apk no Android) e troca a versão do seu jeito.
/// </summary>
public interface IAtualizacaoService
{
    /// <summary>Versão em execução.</summary>
    Version VersaoAtual { get; }

    /// <summary>Última versão nova encontrada (null se está em dia ou ainda não verificou).</summary>
    NovaVersao? Disponivel { get; }

    /// <summary>Consulta o GitHub. Retorna a versão mais nova que a instalada, ou null se já está em dia.</summary>
    Task<NovaVersao?> VerificarAsync();

    /// <summary>Baixa e instala a versão nova (no Windows e no Linux o app fecha e abre de novo).</summary>
    Task InstalarAsync(NovaVersao n, IProgress<double>? progresso = null);

    /// <summary>Fecha e abre o app de novo (ex.: entrou ou saiu da conta e a pasta dos dados mudou).</summary>
    void Reiniciar();

    /// <summary>Começa a verificar sozinho, uma vez por dia.</summary>
    void Iniciar();
}

/// <summary>Versão publicada no GitHub Releases: número, notas, id do arquivo e tamanho em bytes.</summary>
public record NovaVersao(Version Versao, string Notas, long ArquivoId, long Tamanho);

public static class Versoes
{
    /// <summary>"1.2.3".</summary>
    public static string Texto(Version v) => v.ToString(3);

    /// <summary>Sempre com três partes (1.2 vira 1.2.0), para comparar versões.</summary>
    public static Version Normalizar(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
