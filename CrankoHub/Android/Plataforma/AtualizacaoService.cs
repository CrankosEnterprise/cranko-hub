using System.Net;
using System.Reflection;
using System.Text.Json;
using Android.Content;

namespace CrankoHub.Services;

/// <summary>
/// Versão Android do atualizador: procura no GitHub Releases o arquivo CrankoHub-android.apk,
/// baixa para o cache e abre o instalador do Android (a pessoa confirma tocando em "Atualizar").
/// Mesma API das versões Windows/Linux, para a página Configurações compilar sem mudança.
/// </summary>
public class AtualizacaoService(ArmazenamentoService store, BandejaService bandeja)
{
    private const string Repositorio = "CrankosEnterprise/cranko-hub";
    private const string PrefixoTag = "crankohub-v";
    private const string NomeArquivo = "CrankoHub-android.apk";

    public const string ArgAtualizado = "--atualizado";
    public const string ArgReiniciado = "--reiniciado";

    public static Version VersaoAtual { get; } = Normalizar(typeof(AtualizacaoService).Assembly.GetName().Version ?? new Version(1, 0, 0));

    private static readonly HttpClient Http = CriarHttp();

    private Timer? _timer;

    public record Novidade(Version Versao, string Notas, long ArquivoId, long Tamanho);

    public Novidade? Disponivel { get; private set; }

    public static bool PodeInstalar => true;

    public static string Texto(Version v) => v.ToString(3);

    public async Task<Novidade?> VerificarAsync()
    {
        using var req = Requisicao($"https://api.github.com/repos/{Repositorio}/releases?per_page=30", "application/vnd.github+json");
        using var resp = await Http.SendAsync(req);
        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            throw new InvalidOperationException("Não foi possível consultar o repositório no GitHub (limite de consultas ou sem acesso). Tente mais tarde.");
        resp.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        Novidade? melhor = null;
        foreach (var r in json.RootElement.EnumerateArray())
        {
            if (r.GetProperty("draft").GetBoolean() || r.GetProperty("prerelease").GetBoolean()) continue;
            var tag = r.GetProperty("tag_name").GetString() ?? "";
            if (!tag.StartsWith(PrefixoTag, StringComparison.OrdinalIgnoreCase) || !Version.TryParse(tag[PrefixoTag.Length..], out var v)) continue;
            v = Normalizar(v);
            if (v <= VersaoAtual || (melhor is not null && v <= melhor.Versao)) continue;
            foreach (var a in r.GetProperty("assets").EnumerateArray())
                if (a.GetProperty("name").GetString() == NomeArquivo)
                    melhor = new Novidade(v, r.GetProperty("body").GetString() ?? "", a.GetProperty("id").GetInt64(), a.GetProperty("size").GetInt64());
        }

        Disponivel = melhor;
        store.Dados.Config.UltimaVerificacaoAtualizacao = Formatacao.Hoje;
        store.Salvar();
        return melhor;
    }

    public async Task InstalarAsync(Novidade n, IProgress<double>? progresso = null)
    {
        var ctx = global::Android.App.Application.Context;
        // Android 8+: cada app precisa da permissão "instalar apps desconhecidos"
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && !ctx.PackageManager!.CanRequestPackageInstalls())
        {
            var ajustes = new Intent(global::Android.Provider.Settings.ActionManageUnknownAppSources,
                global::Android.Net.Uri.Parse("package:" + ctx.PackageName));
            ajustes.AddFlags(ActivityFlags.NewTask);
            ctx.StartActivity(ajustes);
            throw new InvalidOperationException("Permita que o CrankoHub instale apps e toque em \"Atualizar agora\" de novo.");
        }

        // Fica no cache: o FileProvider do MAUI já compartilha essa pasta com o instalador
        var pasta = Path.Combine(FileSystem.CacheDirectory, "atualizacao");
        Directory.CreateDirectory(pasta);
        var pacote = Path.Combine(pasta, NomeArquivo);

        using (var req = Requisicao($"https://api.github.com/repos/{Repositorio}/releases/assets/{n.ArquivoId}", "application/octet-stream"))
        using (var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead))
        {
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? n.Tamanho;
            await using var origem = await resp.Content.ReadAsStreamAsync();
            await using var destino = File.Create(pacote);
            var buffer = new byte[81920];
            long lidos = 0;
            int qtd;
            while ((qtd = await origem.ReadAsync(buffer)) > 0)
            {
                await destino.WriteAsync(buffer.AsMemory(0, qtd));
                lidos += qtd;
                if (total > 0) progresso?.Report((double)lidos / total);
            }
        }
        if (new FileInfo(pacote).Length != n.Tamanho)
        {
            File.Delete(pacote);
            throw new InvalidOperationException("O download veio incompleto. Tente de novo.");
        }

        var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(ctx, ctx.PackageName + ".fileProvider", new Java.IO.File(pacote));
        var instalar = new Intent(Intent.ActionView);
        instalar.SetDataAndType(uri, "application/vnd.android.package-archive");
        instalar.AddFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);
        ctx.StartActivity(instalar);
    }

    /// <summary>Fecha e abre o app de novo (ex.: entrou ou saiu da conta e a pasta dos dados mudou).</summary>
    public static void Reiniciar()
    {
        var ctx = global::Android.App.Application.Context;
        var abrir = ctx.PackageManager!.GetLaunchIntentForPackage(ctx.PackageName!);
        if (abrir?.Component is not null)
            ctx.StartActivity(Intent.MakeRestartActivityTask(abrir.Component));
        Java.Lang.Runtime.GetRuntime()!.Exit(0);
    }

    public void Iniciar()
    {
        // Ao abrir, apaga o APK de uma atualização já instalada
        try { Directory.Delete(Path.Combine(FileSystem.CacheDirectory, "atualizacao"), true); } catch { }

        _timer = new Timer(async _ =>
        {
            if (!ArmazenamentoService.PorConta) return; // tela de login: verifica depois de entrar
            var cfg = store.Dados.Config;
            if (!cfg.VerificarAtualizacoes || cfg.UltimaVerificacaoAtualizacao == Formatacao.Hoje)
                return;
            try
            {
                if (await VerificarAsync() is Novidade n)
                    bandeja.Notificar("Nova versão do CrankoHub", $"A versão {Texto(n.Versao)} está disponível em Configurações → Aplicativo.", "configuracoes?aba=aplicativo");
            }
            catch { } // sem internet: tenta de novo mais tarde
        }, null, TimeSpan.FromSeconds(30), TimeSpan.FromHours(6));
    }

    private static HttpRequestMessage Requisicao(string url, string aceita)
    {
        // Repositório público: o GitHub responde sem token (limite de 60 consultas por hora por IP)
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.ParseAdd(aceita);
        req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return req;
    }

    private static HttpClient CriarHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("CrankoHub/" + Texto(VersaoAtual));
        return http;
    }

    private static Version Normalizar(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
