namespace CrankoHub.Services;

/// <summary>
/// Endereço e chave pública do projeto no Supabase (item 14 do README).
/// A publishable key pode ficar no repositório: quem protege os dados é o RLS do banco.
/// A secret key, a senha do banco e a connection string nunca entram aqui.
/// </summary>
public static class SupabaseConfig
{
    /// <summary>Project URL (ex.: "https://abcdefgh.supabase.co"). Vazio = conta desligada no app.</summary>
    public const string Url = "https://rvvxnqucwjidbzyvcgmb.supabase.co";

    /// <summary>Publishable key ("sb_publishable_..."), enviada no header apikey.</summary>
    public const string ChavePublica = "sb_publishable_OQrcs4RLHUCIWnV3y9707Q_SaVyVI0G";

    /// <summary>
    /// O login é por usuário, mas o Supabase Auth só aceita e-mail: "diego" vira "diego@crankohub.local".
    /// Os usuários são criados à mão no painel (cadastro desligado lá).
    /// </summary>
    public const string DominioUsuario = "crankohub.local";

    public static bool Configurado => Url.Length > 0 && ChavePublica.Length > 0;
}
