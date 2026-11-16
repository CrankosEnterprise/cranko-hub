namespace CrankoHub.Services;

/// <summary>Versão Linux de "iniciar com o sistema": um atalho em ~/.config/autostart (padrão do Cinnamon).</summary>
public sealed class InicializacaoLinux : IInicializacaoSistema
{
    private static readonly string Arquivo = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart", "crankohub.desktop");

    public void Aplicar(bool ligado)
    {
        if (!ligado || Environment.ProcessPath is not string exe)
        {
            File.Delete(Arquivo);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Arquivo)!);
        File.WriteAllText(Arquivo, $"""
            [Desktop Entry]
            Type=Application
            Name=CrankoHub
            Comment=Painel pessoal de parâmetros
            Exec="{exe}"
            Icon={Path.Combine(AppContext.BaseDirectory, "icone.png")}
            Terminal=false
            X-GNOME-Autostart-enabled=true

            """);
    }
}
