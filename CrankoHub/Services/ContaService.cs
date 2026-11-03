using System.Net.Http;

namespace CrankoHub.Services;

/// <summary>
/// Conta do CrankoHub no Supabase: entrar com usuário e senha, sair e manter a sessão entre aberturas.
/// Não existe cadastro pelo app: os usuários são criados à mão no painel do Supabase (README, item 14).
/// Logado, os dados ficam na pasta da conta (contas/&lt;user_id&gt;/) e são sincronizados por <see cref="SincronizacaoService"/>.
/// Entrar e sair reiniciam o app (quem chama faz isso), para trocar de pasta.
/// </summary>
public class ContaService(SupabaseCliente supabase)
{
    public static bool Configurado => SupabaseConfig.Configurado;

    public bool Logado => ConfigLocal.Atual.ContaId is not null;

    /// <summary>Nome mostrado na tela ("diego"), sem o domínio interno.</summary>
    public string? Usuario => ConfigLocal.Atual.ContaUsuario;

    public string? UserId => ConfigLocal.Atual.ContaId;

    /// <summary>Logado, mas não foi possível falar com o servidor ao abrir (sem internet).</summary>
    public bool Offline { get; private set; }

    /// <summary>Entrou, saiu ou mudou o estado da conexão. Pode vir de outra thread.</summary>
    public event Action? Mudou;

    /// <summary>"diego" → "diego@crankohub.local". Um texto que já é e-mail passa como está.</summary>
    public static string ParaEmail(string usuario)
    {
        var u = usuario.Trim().ToLowerInvariant();
        return u.Contains('@') ? u : $"{u}@{SupabaseConfig.DominioUsuario}";
    }

    /// <summary>
    /// Lança <see cref="InvalidOperationException"/> com a mensagem para mostrar ao usuário.
    /// Depois de entrar é preciso reiniciar o app (<see cref="AtualizacaoService.Reiniciar"/>) para usar a pasta da conta.
    /// </summary>
    public async Task Entrar(string usuario, string senha)
    {
        if (!Configurado) throw new InvalidOperationException("A conta ainda não está disponível nesta versão.");
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrEmpty(senha))
            throw new InvalidOperationException("Informe o usuário e a senha.");
        try
        {
            var sessao = await supabase.EntrarComSenha(ParaEmail(usuario), senha);
            // Só marca como logado depois de a pasta da conta estar pronta: senão o app abriria vazio
            ArmazenamentoService.PrepararPastaDaConta(sessao.UserId);
            var email = sessao.Email ?? ParaEmail(usuario);
            var sufixo = "@" + SupabaseConfig.DominioUsuario;
            ConfigLocal.Atual.ContaUsuario = email.EndsWith(sufixo, StringComparison.OrdinalIgnoreCase) ? email[..^sufixo.Length] : email;
            ConfigLocal.Atual.ContaId = sessao.UserId;
            ConfigLocal.Atual.Salvar();
            Offline = false;
        }
        catch (SupabaseErro ex) when (ex.Codigo is "invalid_credentials" or "invalid_grant")
        {
            throw new InvalidOperationException("Usuário ou senha incorretos.");
        }
        catch (SupabaseErro ex)
        {
            throw new InvalidOperationException($"O servidor recusou o login: {ex.Message}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException("Sem conexão com o servidor. Confira a internet e tente de novo.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await supabase.Sair();
            throw new InvalidOperationException($"Não foi possível preparar a pasta da conta: {ex.Message}");
        }
        Mudou?.Invoke();
    }

    /// <summary>Garante um token válido (renovando se preciso). false = sem internet ou sessão recusada (aí sai da conta).</summary>
    public async Task<bool> GarantirSessao()
    {
        if (!Logado) return false;
        if (supabase.Atual is { } s && s.ExpiraEm > DateTimeOffset.UtcNow.AddSeconds(60)) return true;
        var refresh = supabase.Atual?.RefreshToken ?? CofreSessao.Ler();
        if (refresh is null) { Limpar(); Mudou?.Invoke(); return false; }
        try
        {
            await supabase.Renovar(refresh);
            if (Offline) { Offline = false; Mudou?.Invoke(); }
            return true;
        }
        catch (SupabaseErro)
        {
            CofreSessao.Apagar();
            Limpar();
            Mudou?.Invoke();
            return false;
        }
        catch
        {
            if (!Offline) { Offline = true; Mudou?.Invoke(); }
            return false;
        }
    }

    /// <summary>Depois de sair é preciso reiniciar o app para voltar aos dados de antes do login.</summary>
    public async Task Sair()
    {
        await supabase.Sair();
        Limpar();
        Mudou?.Invoke();
    }

    /// <summary>Ao abrir o app: renova a sessão salva. Se o servidor recusar (senha trocada, sessão revogada), sai sozinho.</summary>
    public async Task Restaurar()
    {
        if (!Configurado) return;
        if (!Logado)
        {
            CofreSessao.Apagar(); // sobra de um login que não terminou
            return;
        }
        await GarantirSessao(); // sem internet: continua logado e tenta de novo depois
        Mudou?.Invoke();
    }

    private void Limpar()
    {
        Offline = false;
        ConfigLocal.Atual.ContaUsuario = null;
        ConfigLocal.Atual.ContaId = null;
        try { ConfigLocal.Atual.Salvar(); } catch { }
    }
}
