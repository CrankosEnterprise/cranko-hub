using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace CrankoHub.Services;

/// <summary>Link para o Carros na Web e o que ele aponta.</summary>
/// <param name="Url">Endereço para abrir.</param>
/// <param name="Versao">Nome da versão no site quando achou a ficha exata ("Onix LT 1.0 Turbo AT"); null = lista do modelo/ano ou busca.</param>
/// <param name="Catalogo">true = lista de versões do modelo no ano (não deu para cravar a versão).</param>
public record LinkCarrosNaWeb(string Url, string? Versao, bool Catalogo);

/// <summary>
/// Monta o link do Carros na Web (potência, torque, medidas) a partir da versão escolhida na FIPE.
/// O site não tem endereço por nome: a ficha usa um código interno. Então abre o catálogo do modelo no ano
/// (catalogo.asp?fabricante=…&amp;varnome=…&amp;anoini=…&amp;anofim=…), que lista as versões com o código de cada uma,
/// e escolhe a versão cujas palavras aparecem todas na versão da FIPE. Sem certeza, fica no catálogo; sem catálogo, na busca do Google.
/// </summary>
public partial class CarrosNaWeb(HttpClient http)
{
    private const string Base = "https://www.carrosnaweb.com.br/";

    // O site é antigo: páginas e parâmetros em ISO-8859-1
    private static readonly Encoding Latin1 = Encoding.Latin1;

    /// <param name="marca">Marca já sem o prefixo da FIPE ("Chevrolet", não "GM - Chevrolet").</param>
    /// <param name="versaoFipe">Modelo/versão da FIPE ("ONIX HATCH LT 1.0 12V TB Flex 5p Aut.").</param>
    public async Task<LinkCarrosNaWeb> Link(string marca, string versaoFipe, int ano)
    {
        var busca = new LinkCarrosNaWeb(LinkBusca(marca, versaoFipe, ano), null, false);
        var modelo = versaoFipe.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(marca) || modelo is null) return busca;

        var catalogo = $"{Base}catalogo.asp?fabricante={Codificar(marca)}&varnome={Codificar(modelo)}&anoini={ano}&anofim={ano}";
        var chave = $"{marca}|{versaoFipe}|{ano}".ToLowerInvariant();
        lock (_cache) if (_cache.TryGetValue(chave, out var guardado)) return guardado;
        LinkCarrosNaWeb link;
        try
        {
            var versoes = Versoes(await Baixar(catalogo), marca);
            // Página certa, mas nenhuma versão: o site não conhece esse modelo/ano com esse nome
            if (versoes.Count == 0) link = busca;
            else link = Escolher(versoes, modelo, versaoFipe) is { } v
                ? new LinkCarrosNaWeb($"{Base}fichadetalhe.asp?codigo={v.Codigo}", v.Nome, false)
                : new LinkCarrosNaWeb(catalogo, null, true);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            // Sem internet, site fora ou limitando consultas: o catálogo ainda abre no navegador da pessoa.
            // Não guarda: na próxima vez tenta de novo.
            return new LinkCarrosNaWeb(catalogo, null, true);
        }
        lock (_cache) _cache[chave] = link;
        return link;
    }

    // Uma consulta por versão/ano enquanto o app estiver aberto (o site limita consultas seguidas)
    private readonly Dictionary<string, LinkCarrosNaWeb> _cache = [];

    /// <summary>Busca no Google restrita ao site (o que o app fazia antes; serve quando o site não reconhece o modelo).</summary>
    public static string LinkBusca(string marca, string modelo, int? ano)
    {
        var termo = string.Join(" ", new[] { "site:carrosnaweb.com.br", marca, modelo, ano?.ToString(), "ficha técnica" }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return "https://www.google.com/search?q=" + Uri.EscapeDataString(termo);
    }

    /// <summary>
    /// A versão do site cujas palavras (tirando marca e modelo) aparecem todas na versão da FIPE; entre essas, a mais completa.
    /// Empate ou nenhuma: null (melhor mostrar a lista do que errar a versão).
    /// </summary>
    internal static (string Codigo, string Nome)? Escolher(List<(string Codigo, string Nome)> versoes, string modelo, string versaoFipe)
    {
        var fipe = Palavras(versaoFipe).ToHashSet();
        var modeloNorm = Palavras(modelo).ToHashSet();
        var candidatas = versoes
            .Select(v => (v.Codigo, v.Nome, Palavras: Palavras(v.Nome).Where(p => !modeloNorm.Contains(p)).ToList()))
            .Where(v => v.Palavras.Count > 0 && v.Palavras.All(fipe.Contains))
            .OrderByDescending(v => v.Palavras.Count)
            .ToList();
        if (candidatas.Count == 0) return null;
        if (candidatas.Count > 1 && candidatas[1].Palavras.Count == candidatas[0].Palavras.Count) return null;
        return (candidatas[0].Codigo, candidatas[0].Nome);
    }

    /// <summary>
    /// Palavras comparáveis: minúsculas, sem acento, "1.0" inteiro, e os apelidos da FIPE no jeito do site
    /// ("TB" → turbo, "Aut." → at). "Mec." e afins somem (o site não escreve "manual").
    /// </summary>
    internal static List<string> Palavras(string texto)
    {
        var sem = RemoverAcentos(texto.ToLowerInvariant());
        var r = new List<string>();
        foreach (Match m in Token().Matches(sem))
        {
            var p = m.Value.Trim('-');
            p = p switch
            {
                "tb" or "turbo" => "turbo",
                "aut" or "at" or "automatico" or "automatica" => "at",
                _ => p,
            };
            if (p is "" or "mec" or "manual") continue;
            r.Add(p);
        }
        return r;
    }

    // ---- Página ----

    private async Task<string> Baixar(string url)
    {
        using var resp = await http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        // Quando limita consultas (ou dá erro), o site redireciona para erro.asp em vez de responder o catálogo
        if (resp.RequestMessage?.RequestUri?.AbsolutePath.EndsWith("erro.asp", StringComparison.OrdinalIgnoreCase) == true)
            throw new HttpRequestException("Carros na Web recusou a consulta.");
        return Latin1.GetString(await resp.Content.ReadAsByteArrayAsync());
    }

    /// <summary>Código e nome (sem a marca) de cada versão listada no catálogo.</summary>
    internal static List<(string Codigo, string Nome)> Versoes(string html, string marca)
    {
        var lista = new List<(string, string)>();
        foreach (Match m in Ficha().Matches(html))
        {
            var titulo = WebUtility.HtmlDecode(m.Groups[2].Value).Replace(' ', ' ');
            titulo = PrefixoTitulo().Replace(titulo, "");
            titulo = string.Join(' ', titulo.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (titulo.StartsWith(marca + " ", StringComparison.OrdinalIgnoreCase)) titulo = titulo[(marca.Length + 1)..];
            if (titulo.Length > 0 && lista.All(x => x.Item1 != m.Groups[1].Value)) lista.Add((m.Groups[1].Value, titulo));
        }
        return lista;
    }

    private static string Codificar(string texto) => System.Web.HttpUtility.UrlEncode(texto, Latin1);

    private static string RemoverAcentos(string texto)
    {
        var d = texto.Normalize(NormalizationForm.FormD);
        return new string(d.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    [GeneratedRegex(@"\d+\.\d+|[a-z0-9-]+")]
    private static partial Regex Token();

    // O título vem no próprio link ou na miniatura logo dentro dele: <a href="fichadetalhe.asp?codigo=N"><img title="…">
    [GeneratedRegex(@"fichadetalhe\.asp\?codigo=(\d+)""(?:[^>]*|[^>]*>\s*<img[^>]*)title=""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex Ficha();

    [GeneratedRegex(@"^Ficha t.cnica, equipamentos e fotos d[oa]\s+", RegexOptions.IgnoreCase)]
    private static partial Regex PrefixoTitulo();
}
