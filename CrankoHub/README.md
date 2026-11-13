# CrankoHub — notas de desenvolvimento

- **[Informacoes/updates.md](Informacoes/updates.md):** o que ainda vamos fazer (em andamento e não começados).
- **[Informacoes/finalizados.md](Informacoes/finalizados.md):** o que já foi entregue, com as decisões e os detalhes de implementação.

## Como o projeto funciona (referência rápida)

- **Tecnologia:** WPF + Blazor Hybrid (`BlazorWebView`), .NET 10. As telas são `.razor` em `Pages/`, a lógica fica em C# em `Services/` e os modelos em `Models/`, cada um com uma subpasta por módulo (`Carro/`, `Agenda/`...) e uma `Comum/` para o que é compartilhado. As subpastas não mudam o namespace: nos `.cs` ele continua `CrankoHub.Services`/`CrankoHub.Models`, e cada `.razor` em subpasta começa com `@namespace CrankoHub.Pages` (ou `CrankoHub.Components`), porque o Razor tiraria o namespace do caminho da pasta. Arquivo novo segue o mesmo padrão. Partes só do Windows ficam em `Services/Windows/` (excluída inteira nos projetos Linux e Android).
- **Dados:** ficam na conta do Supabase (item 9). O app exige login (item 10) e guarda só um cache da conta em `%LocalAppData%\CrankoHub\contas\<user_id>\dados.json`, para abrir sem internet. Gerenciado por `Services/Dados/ArmazenamentoService.cs` e sincronizado por `Services/Dados/SincronizacaoService.cs`.
  - Raiz: `EstadoApp` (`Models/Comum/EstadoApp.cs`).
  - Campo novo exige três passos:
    - adicionar no modelo;
    - tratar no `Normalizar` (`??= []`, remover nulos, gerar ids faltando);
    - criar a coluna no Supabase (migration nova em `supabase/migrations/`) e, se precisar, incluir nos contadores da página Conta em `Pages/Conta/MinhaConta.razor`.
  - Campos novos devem ser opcionais, para o cache antigo continuar abrindo.
- **Gráficos:** `Components/Graficos/GraficoLinha.cs` e `Components/Graficos/GraficoBarras.cs` (dados em `Models/Comum/Graficos.cs`: `BarraDado`, `SerieLinha`, `FormatoNumero`).
- **Formatação pt-BR:** `Services/Comum/Formatacao.cs` (`Money`, `Num`, `Pf` para ler "1.234,56", `DataBR`, `MesLabel`, `UltimosMeses`, `Plural`, `Hoje`).
- **Alertas:** `Services/Comum/Alertas.cs` é a fonte única do card "Precisa de atenção" do Início e das notificações do Windows (`Services/Comum/AvisosService.cs`).
- **Menu:** `Layout/LayoutPrincipal.razor` (NavLink + `TituloDaPagina`). Ícones em `Components/Comum/Icones.cs`.
- **Publicar o .exe:** `dotnet publish -c Release -p:PublishProfile=win-x64` gera `publicar\CrankoHub.exe`.
- **Lançar uma versão:** `git tag crankohub-v1.2.0` e `git push origin crankohub-v1.2.0`. O workflow `.github/workflows/crankohub-release.yml` compila e cria o Release com o `CrankoHub.exe`; a versão vem da tag.
- **Atualizar os outros PCs:** Configurações → Aplicativo → Atualizações → "Verificar atualizações" → "Atualizar agora" (`Services/Windows/AtualizacaoService.cs`). Sem token: os Releases vêm do repositório público `CrankosEnterprise/cranko-hub`. PC novo: baixar o `CrankoHub.exe` do Releases.
