using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Modo Carro no Windows e no Linux: não existe (<see cref="Disponivel"/> = false).
/// A versão de verdade fica em Android/Plataforma/ModoCarroService.cs, com a mesma API, e o projeto Android exclui este arquivo.
/// </summary>
public sealed class ModoCarroService
{
    public bool Disponivel => false;

    /// <summary>Última posição do GPS, ou null antes da primeira.</summary>
    public PosicaoCarro? Posicao => null;

    /// <summary>A permissão de localização foi negada (o mapa mostra um aviso).</summary>
    public bool SemLocalizacao => false;

    /// <summary>O que está tocando no Spotify, ou null antes da primeira transmissão.</summary>
    public MusicaAtual? Musica => null;

#pragma warning disable CS0067 // só a versão Android avisa mudanças
    public event Action? Mudou;
#pragma warning restore CS0067

    /// <summary>Abre o app (<see cref="AppsCarro"/>). false = app não instalado.</summary>
    public bool AbrirApp(string app) => false;

    /// <summary>Pede a permissão de localização de novo e liga o GPS.</summary>
    public Task LigarGps() => Task.CompletedTask;

    /// <summary>Fala o texto (avisos da rota).</summary>
    public Task Falar(string texto) => Task.CompletedTask;

    public void TocarPausar() { }

    public void Proxima() { }

    public void Anterior() { }

    /// <summary>Entrou no Modo Carro: tela sempre ligada, paisagem, GPS (pede a permissão) e Spotify.</summary>
    public Task Entrar() => Task.CompletedTask;

    /// <summary>Saiu do Modo Carro: volta a tela e a orientação ao normal e desliga o GPS e o Spotify.</summary>
    public void Sair() { }
}
