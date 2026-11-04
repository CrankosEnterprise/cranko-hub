using System.Text.RegularExpressions;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Lê a próxima manobra do título/texto da notificação de navegação do Google Maps e do Waze (Modo Carro).
/// É só texto, sem nada do Android, para dar para conferir com exemplos em qualquer plataforma.
/// </summary>
/// <remarks>
/// Os apps não seguem um formato fixo: a distância pode vir sozinha no título ("200 m") com a instrução no texto,
/// ou no começo/fim do mesmo campo ("200 m · R. X", "Vire à direita em 200 m"). Notificações que não são de rota
/// (ex.: "Waze está em execução") ficam de fora: precisa ter distância ou começar com uma manobra conhecida.
/// </remarks>
public static partial class NavegacaoNotificacao
{
    // Distância no começo: "200 m", "1,2 km", "Em 300 m", "200 m · R. Augusta"
    [GeneratedRegex(@"^(?:em\s+|a\s+)?(\d+(?:[.,]\d+)?\s?(?:km|m))(?![a-zà-ú])\.?\s*(?:[·•|\-–—,:]\s*)?(.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DistanciaNoInicio();

    // Distância no fim: "Vire à direita em 200 m", "Siga em frente · 1,5 km"
    [GeneratedRegex(@"\s*(?:[·•|\-–—,]\s*)?(?:em\s+|por\s+|a\s+)?(\d+(?:[.,]\d+)?\s?(?:km|m))\.?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DistanciaNoFim();

    // Começos de instrução em pt-BR (quando a notificação não traz distância)
    [GeneratedRegex(@"^(vire|siga|continue|mantenha|pegue|entre|saia|fa[çc]a|na rotat[óo]ria|no trevo|use a|retorne|chegou|chegada|voc[êe] chegou|seu destino|destino|curva|acesse|em seguida)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ComecoDeManobra();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacos();

    [GeneratedRegex(@"^(\d+(?:[.,]\d+)?)\s?(km|m)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PartesDaDistancia();

    /// <summary>Instrução da notificação, ou null quando não é do Maps/Waze ou não parece uma manobra.</summary>
    public static InstrucaoNavegacao? Ler(string? pacote, string? titulo, string? texto, string? subTexto = null)
    {
        var app = AppsCarro.DoPacote(pacote);
        if (app is not (AppsCarro.Maps or AppsCarro.Waze)) return null;

        var t = Limpar(titulo);
        var x = Limpar(texto);
        if (t is null && x is null) return null;

        string? distancia = null, instrucao = null;
        foreach (var (campo, outro, ehTitulo) in new[] { (t, x, true), (x, t, false) })
        {
            if (campo is null) continue;

            // O que sobra do campo da distância vai junto com o outro campo, na ordem título → texto
            string? resto;
            var inicio = DistanciaNoInicio().Match(campo);
            var fim = DistanciaNoFim().Match(campo);
            if (inicio.Success)
            {
                distancia = Distancia(inicio.Groups[1].Value);
                resto = Limpar(inicio.Groups[2].Value);
            }
            else if (fim.Success && fim.Index > 0)
            {
                distancia = Distancia(fim.Groups[1].Value);
                resto = Limpar(campo[..fim.Index]);
            }
            else continue;

            instrucao = ehTitulo ? Juntar(resto, outro) : Juntar(outro, resto);
            break;
        }

        if (distancia is null)
        {
            // Sem distância: só aceita o que começa como manobra
            instrucao = new[] { t, x }.FirstOrDefault(c => c is not null && ComecoDeManobra().IsMatch(c));
            if (instrucao is null) return null;
        }

        if (instrucao is null) return null;
        instrucao = char.ToUpper(instrucao[0]) + instrucao[1..];
        return new InstrucaoNavegacao(instrucao, distancia, app, Limpar(subTexto));
    }

    private static string? Juntar(string? a, string? b) => (a, b) switch
    {
        (null, null) => null,
        (null, _) => b,
        (_, null) => a,
        _ => $"{a} · {b}",
    };

    /// <summary>"200M" → "200 m", "1.2km" → "1,2 km".</summary>
    private static string Distancia(string bruto)
    {
        var m = PartesDaDistancia().Match(bruto.Trim());
        return m.Success ? $"{m.Groups[1].Value.Replace('.', ',')} {m.Groups[2].Value.ToLowerInvariant()}" : bruto.Trim();
    }

    private static string? Limpar(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var limpo = Espacos().Replace(s, " ").Trim().Trim('·', '•', '|', '-', '–', '—', ',', ':').Trim();
        return limpo.Length == 0 ? null : limpo;
    }
}
