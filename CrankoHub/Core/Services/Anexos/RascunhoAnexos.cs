using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Os anexos de um formulário aberto (tarefa, serviço de manutenção), até salvar ou cancelar.
/// Os arquivos compactados nele ficam protegidos da limpeza (<see cref="AnexosService.EmEdicao"/>);
/// cancelar apaga esses arquivos novos e salvar apaga os que foram removidos.
/// Usado pelo componente CampoAnexos. Só na thread da tela.
/// </summary>
public class RascunhoAnexos
{
    private List<Anexo> _originais = [];
    private readonly List<Anexo> _novos = [];

    /// <summary>Como o formulário mostra agora (cópias: mudar a categoria e cancelar não altera o item salvo).</summary>
    public List<Anexo> Lista { get; private set; } = [];

    /// <summary>Formulário aberto. Um arquivo que termina de compactar com ele fechado é descartado.</summary>
    public bool Aberto { get; private set; }

    public void Iniciar(IEnumerable<Anexo> atuais)
    {
        Descartar();
        _originais = [.. atuais];
        Lista = _originais.Select(Copia).ToList();
        Aberto = true;
    }

    public void Adicionar(Anexo a)
    {
        AnexosService.EmEdicao.Add(a.Arquivo);
        _novos.Add(a);
        Lista.Add(a);
    }

    public void Remover(Anexo a) => Lista.Remove(a);

    /// <summary>Salvou: apaga os arquivos removidos e devolve a lista para guardar no item.</summary>
    public List<Anexo> Confirmar()
    {
        var ficam = Lista.Select(a => a.Arquivo).ToHashSet();
        foreach (var a in _originais.Where(a => !ficam.Contains(a.Arquivo))) AnexosService.Excluir(a.Arquivo);
        foreach (var a in Lista.Where(a => string.IsNullOrEmpty(a.Categoria))) a.Categoria = null;
        var r = Lista;
        Liberar();
        return r;
    }

    /// <summary>Cancelou (ou fechou a página): apaga os arquivos compactados neste formulário.</summary>
    public void Descartar()
    {
        foreach (var a in _novos) AnexosService.Excluir(a.Arquivo);
        Liberar();
    }

    /// <summary>O item foi excluído: apaga os arquivos dele.</summary>
    public static void ExcluirTodos(IEnumerable<Anexo> anexos)
    {
        foreach (var a in anexos) AnexosService.Excluir(a.Arquivo);
    }

    private void Liberar()
    {
        foreach (var a in _novos) AnexosService.EmEdicao.Remove(a.Arquivo);
        _novos.Clear();
        _originais = [];
        Lista = [];
        Aberto = false;
    }

    private static Anexo Copia(Anexo a) => new()
    {
        Id = a.Id,
        Nome = a.Nome,
        Tipo = a.Tipo,
        Tamanho = a.Tamanho,
        TamanhoGuardado = a.TamanhoGuardado,
        Arquivo = a.Arquivo,
        Formato = a.Formato,
        Categoria = a.Categoria,
    };
}
