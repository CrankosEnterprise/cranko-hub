using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>Item das listas da FIPE (marca, modelo/versão ou ano).</summary>
public record FipeItem(string Codigo, string Nome);

/// <summary>Valor e dados de uma versão num ano.</summary>
public record FipeDetalhe(string Marca, string Modelo, int Ano, string? Combustivel, double? Valor, string? CodigoFipe, string? Referencia);

/// <summary>
/// Consulta a tabela FIPE (API pública da Parallelum, sem chave, ~500 consultas por dia por IP).
/// Traz marca, versão, ano, combustível e valor; o resto da ficha sai do texto da versão (<see cref="LerVersao"/>).
/// Marcas e modelos ficam em memória para não repetir consultas.
/// </summary>
public partial class FipeService(HttpClient http)
{
    private const string Base = "https://parallelum.com.br/fipe/api/v2/cars";

    private List<FipeItem>? _marcas;
    private readonly Dictionary<string, List<FipeItem>> _modelos = [];

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private record ItemApi(string Code, string Name);
    private record DetalheApi(string? Brand, string? Model, int ModelYear, string? Fuel, string? Price, string? CodeFipe, string? ReferenceMonth);

    /// <summary>Marcas já carregadas (para sugerir no campo Marca sem nova consulta).</summary>
    public IReadOnlyList<FipeItem>? MarcasCarregadas => _marcas;

    public async Task<List<FipeItem>> Marcas() =>
        _marcas ??= (await Get<List<ItemApi>>("/brands")).Select(i => new FipeItem(i.Code, i.Name)).OrderBy(i => i.Nome, StringComparer.OrdinalIgnoreCase).ToList();

    public async Task<List<FipeItem>> Modelos(string marca)
    {
        if (_modelos.TryGetValue(marca, out var l)) return l;
        l = (await Get<List<ItemApi>>($"/brands/{marca}/models")).Select(i => new FipeItem(i.Code, i.Name)).ToList();
        return _modelos[marca] = l;
    }

    /// <summary>Anos da versão, do mais novo ao mais antigo, sem o "32000" (zero km).</summary>
    public async Task<List<FipeItem>> Anos(string marca, string modelo) =>
        (await Get<List<ItemApi>>($"/brands/{marca}/models/{modelo}/years"))
            .Where(i => !i.Code.StartsWith("32000"))
            .Select(i => new FipeItem(i.Code, i.Name))
            .OrderByDescending(i => i.Codigo)
            .ToList();

    public async Task<FipeDetalhe> Detalhe(string marca, string modelo, string ano)
    {
        var d = await Get<DetalheApi>($"/brands/{marca}/models/{modelo}/years/{ano}");
        return new FipeDetalhe(d.Brand ?? "", d.Model ?? "", d.ModelYear, d.Fuel, Preco(d.Price), d.CodeFipe, d.ReferenceMonth);
    }

    /// <summary>Filtra versões pelas palavras digitadas (todas precisam aparecer, sem diferenciar maiúsculas).</summary>
    public static List<FipeItem> Filtrar(IEnumerable<FipeItem> modelos, string termo, int maximo = 30)
    {
        var palavras = termo.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (palavras.Length == 0) return [];
        return modelos.Where(m => palavras.All(p => m.Nome.Contains(p, StringComparison.OrdinalIgnoreCase))).Take(maximo).ToList();
    }

    private async Task<T> Get<T>(string caminho)
    {
        try
        {
            using var resp = await http.GetAsync(Base + caminho);
            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                throw new BuscaException("Limite diário de consultas da FIPE atingido. Preencha manualmente ou tente amanhã.");
            resp.EnsureSuccessStatusCode();
            return await JsonSerializer.DeserializeAsync<T>(await resp.Content.ReadAsStreamAsync(), Json)
                   ?? throw new BuscaException("A FIPE não retornou dados.");
        }
        catch (BuscaException) { throw; }
        catch (Exception)
        {
            throw new BuscaException("Não foi possível consultar a FIPE agora. Verifique a internet ou preencha manualmente.");
        }
    }

    // "R$ 56.043,00" → 56043
    private static double? Preco(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpo = texto.Replace("R$", "").Replace(".", "").Replace(",", ".").Trim();
        return double.TryParse(limpo, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    /// <summary>Nome curto sugerido: primeira palavra da versão + ano (ex.: "Onix 2020").</summary>
    public static string NomeSugerido(string versao, int ano)
    {
        var primeira = versao.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? versao;
        // Só nomes de letras viram "Onix"; códigos com número (HB20, RAV4, S10) ficam como estão
        var nome = primeira.Length > 3 && primeira.All(char.IsLetter) ? CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(primeira.ToLowerInvariant()) : primeira;
        return $"{nome} {ano}";
    }

    /// <summary>
    /// Tira da versão da FIPE o que der: cilindrada, válvulas, turbo, portas, câmbio e carroceria.
    /// Ex.: "ONIX HATCH LT 1.0 12V Flex 5p Mec." → 1.0, 12V, 5 portas, Manual, Hatch.
    /// </summary>
    public static FichaVeiculo LerVersao(string versao)
    {
        var f = new FichaVeiculo { Versao = versao };
        if (Cilindrada().Match(versao) is { Success: true } c)
            f.Cilindrada = double.Parse(c.Groups[1].Value, CultureInfo.InvariantCulture);
        if (Valvulas().Match(versao) is { Success: true } v) f.Valvulas = int.Parse(v.Groups[1].Value);
        if (TurboRx().IsMatch(versao)) f.Turbo = true;
        if (Cv().Match(versao) is { Success: true } cv) f.PotenciaCv = double.Parse(cv.Groups[1].Value);
        if (Integral().IsMatch(versao)) f.Tracao = "Integral (4x4/AWD)";
        if (Portas().Match(versao) is { Success: true } p) f.Portas = int.Parse(p.Groups[1].Value);
        f.Cambio =
            CambioCvt().IsMatch(versao) ? "CVT"
            : CambioAutomatizado().IsMatch(versao) ? "Automatizado"
            : CambioAutomatico().IsMatch(versao) ? "Automático"
            : CambioManual().IsMatch(versao) ? "Manual"
            : null;
        f.Carroceria =
            Picape().IsMatch(versao) ? "Picape"
            : Sedan().IsMatch(versao) ? "Sedã"
            : Perua().IsMatch(versao) ? "Perua"
            : Hatch().IsMatch(versao) ? "Hatch"
            : Suv().IsMatch(versao) ? "SUV"
            : Cupe().IsMatch(versao) ? "Cupê"
            : Conversivel().IsMatch(versao) ? "Conversível"
            : null;
        return f;
    }

    [GeneratedRegex(@"(?<![\d.])(\d\.\d)(?![\d])")] private static partial Regex Cilindrada();
    [GeneratedRegex(@"\b(\d{1,2})[Vv]\b")] private static partial Regex Valvulas();
    [GeneratedRegex(@"\b(Turbo|TB|TSI|TFSI|THP|TGDI|T-GDI|TJet|T-Jet|EcoBoost|TCe)\b", RegexOptions.IgnoreCase)] private static partial Regex TurboRx();
    [GeneratedRegex(@"\b([2-5])p\b", RegexOptions.IgnoreCase)] private static partial Regex Portas();
    [GeneratedRegex(@"\b(\d{2,4})\s?cv\b", RegexOptions.IgnoreCase)] private static partial Regex Cv();
    [GeneratedRegex(@"\b4x4\b|\bAWD\b|\b4WD\b|Quattro|4Motion|xDrive|\bAll\s?Grip\b", RegexOptions.IgnoreCase)] private static partial Regex Integral();
    [GeneratedRegex(@"\bCVT\b|X-?Tronic|Multidrive", RegexOptions.IgnoreCase)] private static partial Regex CambioCvt();
    [GeneratedRegex(@"Easytr|Dualogic|I-?Motion|Automatiz|\bAMT\b|\bGSR\b", RegexOptions.IgnoreCase)] private static partial Regex CambioAutomatizado();
    [GeneratedRegex(@"\bAut\b|\bAut\.|\bAT\b|Autom|Tiptr|\bDSG\b|S-?tronic|Steptronic", RegexOptions.IgnoreCase)] private static partial Regex CambioAutomatico();
    [GeneratedRegex(@"\bMec\b|\bMec\.|\bMT\b|Manual", RegexOptions.IgnoreCase)] private static partial Regex CambioManual();
    [GeneratedRegex(@"\bCab\.?\b|\bCD\b|\bCS\b|\bCE\b|Pick-?up|Picape", RegexOptions.IgnoreCase)] private static partial Regex Picape();
    [GeneratedRegex(@"\bSed(an|ã)\b", RegexOptions.IgnoreCase)] private static partial Regex Sedan();
    [GeneratedRegex(@"\bSW\b|Wagon|Perua|Weekend|Variant|Avant|Touring", RegexOptions.IgnoreCase)] private static partial Regex Perua();
    [GeneratedRegex(@"\bHatch\b", RegexOptions.IgnoreCase)] private static partial Regex Hatch();
    [GeneratedRegex(@"\bSUV\b", RegexOptions.IgnoreCase)] private static partial Regex Suv();
    [GeneratedRegex(@"Coup[eé]|Cup[eê]", RegexOptions.IgnoreCase)] private static partial Regex Cupe();
    [GeneratedRegex(@"Convers|Cabrio|Roadster|Spider|Spyder", RegexOptions.IgnoreCase)] private static partial Regex Conversivel();
}
