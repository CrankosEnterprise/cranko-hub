using System.Text.Json;

namespace CrankoHub.Services;

/// <summary>
/// Ajustes só deste aparelho, em %LocalAppData%\CrankoHub\local.json: qual conta está conectada
/// e o que não deve ir para os outros aparelhos.
/// </summary>
public class ConfigLocal
{
    public static readonly string Pasta = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrankoHub");

    private static readonly string Caminho = Path.Combine(Pasta, "local.json");

    private static readonly JsonSerializerOptions Opcoes = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public static ConfigLocal Atual { get; } = Carregar();

    /// <summary>null até migrar a opção antiga, que ficava nos dados (e iria junto para o outro PC).</summary>
    public bool? IniciarComWindows { get; set; }

    /// <summary>Usuário da conta conectada neste computador (null = sem conta). A sessão fica em <see cref="CofreSessao"/>.</summary>
    public string? ContaUsuario { get; set; }

    /// <summary>Id (user_id) da conta conectada no Supabase.</summary>
    public string? ContaId { get; set; }

    public void Salvar()
    {
        Directory.CreateDirectory(Pasta);
        File.WriteAllText(Caminho, JsonSerializer.Serialize(this, Opcoes));
    }

    private static ConfigLocal Carregar()
    {
        try
        {
            if (File.Exists(Caminho) && JsonSerializer.Deserialize<ConfigLocal>(File.ReadAllText(Caminho), Opcoes) is { } c)
                return c;
        }
        catch { }
        return new ConfigLocal();
    }
}
