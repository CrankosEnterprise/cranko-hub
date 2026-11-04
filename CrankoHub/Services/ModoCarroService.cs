using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Modo Carro no Windows e no Linux: não existe (<see cref="Disponivel"/> = false).
/// A versão de verdade fica em Android/Plataforma/ModoCarroService.cs, com a mesma API, e o projeto Android exclui este arquivo.
/// </summary>
public sealed class ModoCarroService
{
    public bool Disponivel => false;

    /// <summary>Próxima manobra da rota ativa (Waze/Maps), ou null sem navegação.</summary>
    public InstrucaoNavegacao? Instrucao => null;

    /// <summary>O que está tocando no player ativo, ou null.</summary>
    public MusicaAtual? Musica => null;

#pragma warning disable CS0067 // só a versão Android avisa mudanças
    public event Action? Mudou;
#pragma warning restore CS0067

    /// <summary>O app pode ler as notificações (rota do Waze/Maps e o player).</summary>
    public bool TemAcessoNotificacoes => false;

    /// <summary>Abre a tela do sistema para liberar o acesso às notificações.</summary>
    public void PedirAcessoNotificacoes() { }

    /// <summary>Abre as informações do app no sistema (onde fica "Permitir configurações restritas").</summary>
    public void AbrirInfoDoApp() { }

    /// <summary>Inicia a rota até o destino no app escolhido (<see cref="AppsCarro"/>). false = app não instalado.</summary>
    public bool Navegar(string destino, string app) => false;

    /// <summary>Abre o app (<see cref="AppsCarro"/>). false = app não instalado.</summary>
    public bool AbrirApp(string app) => false;

    public void TocarPausar() { }

    public void Proxima() { }

    public void Anterior() { }

    /// <summary>Entrou no Modo Carro: tela sempre ligada e paisagem.</summary>
    public void Entrar() { }

    /// <summary>Saiu do Modo Carro: volta a tela e a orientação ao normal.</summary>
    public void Sair() { }
}
