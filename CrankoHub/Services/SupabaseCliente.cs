using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrankoHub.Services;

/// <summary>
/// Conversa com o Supabase (Auth e Data API) por HTTPS. Toda chamada leva a publishable key no
/// header apikey; logado, leva também "Authorization: Bearer" com o token do usuário, renovado sozinho.
/// A publishable key nunca vai no Authorization: o Supabase tentaria ler como JWT e recusaria.
/// </summary>
public class SupabaseCliente
{
    // Cliente próprio: não leva o User-Agent nem os headers da busca do Entretenimento
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>Sessão atual (só em memória; no disco fica apenas o refresh token, em <see cref="CofreSessao"/>).</summary>
    public record Sessao(string AccessToken, string RefreshToken, DateTimeOffset ExpiraEm, string UserId, string? Email);

    public Sessao? Atual { get; private set; }

    private readonly SemaphoreSlim _renovando = new(1, 1);

    /// <summary>Entra com e-mail e senha. Lança <see cref="SupabaseErro"/> se o servidor recusar.</summary>
    public async Task<Sessao> EntrarComSenha(string email, string senha)
    {
        var json = await Auth("/auth/v1/token?grant_type=password", new JsonObject { ["email"] = email, ["password"] = senha });
        return Guardar(json);
    }

    /// <summary>Troca o refresh token por uma sessão nova (o Supabase devolve outro refresh token a cada troca).</summary>
    public async Task<Sessao> Renovar(string refreshToken)
    {
        await _renovando.WaitAsync();
        try
        {
            // Outra chamada já renovou enquanto esperávamos
            if (Atual is { } s && s.RefreshToken != refreshToken && s.ExpiraEm > DateTimeOffset.UtcNow.AddSeconds(60)) return s;
            var json = await Auth("/auth/v1/token?grant_type=refresh_token", new JsonObject { ["refresh_token"] = refreshToken });
            return Guardar(json);
        }
        finally { _renovando.Release(); }
    }

    /// <summary>Encerra a sessão no servidor (se der) e esquece a sessão local.</summary>
    public async Task Sair()
    {
        var s = Atual;
        Atual = null;
        CofreSessao.Apagar();
        if (s is null) return;
        try
        {
            using var req = Requisicao(HttpMethod.Post, "/auth/v1/logout", s.AccessToken);
            using var _ = await Http.SendAsync(req);
        }
        catch { } // offline ou token já expirado: a sessão local já foi apagada
    }

    /// <summary>
    /// Chamada à Data API (ex.: "/rest/v1/veiculos?select=*") com o token do usuário, renovado se estiver perto de expirar.
    /// Lança <see cref="SupabaseErro"/> se não houver sessão ou o servidor recusar.
    /// </summary>
    public async Task<JsonNode?> Enviar(HttpMethod metodo, string caminho, JsonNode? corpo = null, string? prefer = null)
    {
        var s = Atual ?? throw new SupabaseErro(401, "sem_sessao", "Não há conta conectada.");
        if (s.ExpiraEm <= DateTimeOffset.UtcNow.AddSeconds(60))
            s = await Renovar(s.RefreshToken);

        using var req = Requisicao(metodo, caminho, s.AccessToken, corpo);
        if (prefer is not null) req.Headers.Add("Prefer", prefer);
        using var resp = await Http.SendAsync(req);
        var texto = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) throw SupabaseErro.De((int)resp.StatusCode, texto);
        return texto.Length == 0 ? null : JsonNode.Parse(texto);
    }

    private static async Task<JsonNode> Auth(string caminho, JsonNode corpo)
    {
        using var req = Requisicao(HttpMethod.Post, caminho, null, corpo);
        using var resp = await Http.SendAsync(req);
        var texto = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) throw SupabaseErro.De((int)resp.StatusCode, texto);
        return JsonNode.Parse(texto) ?? throw new SupabaseErro((int)resp.StatusCode, "resposta_vazia", "Resposta vazia do servidor.");
    }

    private Sessao Guardar(JsonNode json)
    {
        var expiraEm = json["expires_at"]?.GetValue<long>() is long t
            ? DateTimeOffset.FromUnixTimeSeconds(t)
            : DateTimeOffset.UtcNow.AddSeconds(json["expires_in"]?.GetValue<int>() ?? 3600);
        var sessao = new Sessao(
            json["access_token"]!.GetValue<string>(),
            json["refresh_token"]!.GetValue<string>(),
            expiraEm,
            json["user"]!["id"]!.GetValue<string>(),
            json["user"]?["email"]?.GetValue<string>());
        Atual = sessao;
        CofreSessao.Salvar(sessao.RefreshToken);
        return sessao;
    }

    private static HttpRequestMessage Requisicao(HttpMethod metodo, string caminho, string? accessToken, JsonNode? corpo = null)
    {
        var req = new HttpRequestMessage(metodo, SupabaseConfig.Url.TrimEnd('/') + caminho);
        req.Headers.Add("apikey", SupabaseConfig.ChavePublica);
        if (accessToken is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (corpo is not null) req.Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json");
        return req;
    }
}

/// <summary>O Supabase respondeu com erro (credenciais erradas, token revogado, RLS...).</summary>
public class SupabaseErro(int status, string codigo, string mensagem) : Exception(mensagem)
{
    public int Status { get; } = status;

    /// <summary>Código do erro (ex.: "invalid_credentials", "refresh_token_not_found").</summary>
    public string Codigo { get; } = codigo;

    public static SupabaseErro De(int status, string texto)
    {
        try
        {
            // Auth: { error_code, msg } (versões novas) ou { error, error_description }; Data API: { code, message }
            using var doc = JsonDocument.Parse(texto);
            var r = doc.RootElement;
            string? Ler(string nome) => r.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            return new SupabaseErro(status,
                Ler("error_code") ?? Ler("error") ?? Ler("code") ?? status.ToString(),
                Ler("msg") ?? Ler("error_description") ?? Ler("message") ?? texto);
        }
        catch (JsonException)
        {
            return new SupabaseErro(status, status.ToString(), texto);
        }
    }
}
