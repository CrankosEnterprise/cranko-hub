# CrankoHub — notas de desenvolvimento

- **[Informacoes/updates.md](Informacoes/updates.md):** o que ainda vamos fazer (em andamento e não começados).
- **[Informacoes/finalizados.md](Informacoes/finalizados.md):** o que já foi entregue, com as decisões e os detalhes de implementação.

## Como o projeto funciona (referência rápida)

- **Tecnologia:** WPF + Blazor Hybrid (`BlazorWebView`), .NET 10. As telas são `.razor` em `Pages/`, a lógica fica em C# em `Services/` e os modelos em `Models/`.
- **Dados:** ficam na conta do Supabase (item 9). O app exige login (item 10) e guarda só um cache da conta em `%LocalAppData%\CrankoHub\contas\<user_id>\dados.json`, para abrir sem internet. Gerenciado por `Services/ArmazenamentoService.cs` e sincronizado por `Services/SincronizacaoService.cs`.
  - Raiz: `EstadoApp` (`Models/EstadoApp.cs`).
  - Campo novo exige três passos:
    - adicionar no modelo;
    - tratar no `Normalizar` (`??= []`, remover nulos, gerar ids faltando);
    - criar a coluna no Supabase (migration nova em `supabase/migrations/`) e, se precisar, incluir nos contadores da página Conta em `Pages/MinhaConta.razor`.
  - Campos novos devem ser opcionais, para o cache antigo continuar abrindo.
- **Gráficos:** `Components/GraficoLinha.cs` e `Components/GraficoBarras.cs` (dados em `Models/Graficos.cs`: `BarraDado`, `SerieLinha`, `FormatoNumero`).
- **Formatação pt-BR:** `Services/Formatacao.cs` (`Money`, `Num`, `Pf` para ler "1.234,56", `DataBR`, `MesLabel`, `UltimosMeses`, `Plural`, `Hoje`).
- **Alertas:** `Services/Alertas.cs` é a fonte única do card "Precisa de atenção" do Início e das notificações do Windows (`Services/AvisosService.cs`).
- **Menu:** `Layout/LayoutPrincipal.razor` (NavLink + `TituloDaPagina`). Ícones em `Components/Icones.cs`.
- **Publicar o .exe:** `dotnet publish -c Release -p:PublishProfile=win-x64` gera `publicar\CrankoHub.exe`.
- **Lançar uma versão:** `git tag crankohub-v1.2.0` e `git push origin crankohub-v1.2.0`. O workflow `.github/workflows/crankohub-release.yml` compila e cria o Release com o `CrankoHub.exe`; a versão vem da tag.
- **Atualizar os outros PCs:** Configurações → Aplicativo → Atualizações → "Verificar atualizações" → "Atualizar agora" (`Services/AtualizacaoService.cs`). Sem token: os Releases vêm do repositório público `CrankosEnterprise/cranko-hub`. PC novo: baixar o `CrankoHub.exe` do Releases.
