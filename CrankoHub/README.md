# CrankoHub — próximos updates

Lista do que ainda vamos fazer no CrankoHub. Cada item tem detalhes suficientes para começar sem precisar redescobrir o projeto.

## Como o projeto funciona (referência rápida)

- **Tecnologia:** WPF + Blazor Hybrid (`BlazorWebView`), .NET 10. As telas são `.razor` em `Pages/`, a lógica fica em C# em `Services/` e os modelos em `Models/`.
- **Dados:** um único arquivo JSON em `%LocalAppData%\CrankoHub\dados.json`, gerenciado por `Services/ArmazenamentoService.cs`.
  - Raiz: `EstadoApp` (`Models/EstadoApp.cs`).
  - Campo novo exige três passos:
    - adicionar no modelo;
    - tratar no `Normalizar` (`??= []`, remover nulos, gerar ids faltando);
    - se fizer sentido, incluir nos dados de exemplo (`Exemplo`) e nos contadores de `Pages/Dados.razor`.
  - Campos novos devem ser opcionais, para backups antigos continuarem importando.
- **Gráficos:** `Components/GraficoLinha.cs` e `Components/GraficoBarras.cs` (dados em `Models/Graficos.cs`: `BarraDado`, `SerieLinha`, `FormatoNumero`).
- **Formatação pt-BR:** `Services/Formatacao.cs` (`Money`, `Num`, `Pf` para ler "1.234,56", `DataBR`, `MesLabel`, `UltimosMeses`, `Plural`, `Hoje`).
- **Alertas:** `Services/Alertas.cs` é a fonte única do card "Precisa de atenção" do Início e das notificações do Windows (`Services/AvisosService.cs`).
- **Menu:** `Layout/LayoutPrincipal.razor` (NavLink + `TituloDaPagina`). Ícones em `Components/Icones.cs`.
- **Publicar o .exe:** `dotnet publish -c Release -p:PublishProfile=win-x64` gera `publicar\CrankoHub.exe`.
- **Lançar uma versão:** `git tag crankohub-v1.2.0` e `git push origin crankohub-v1.2.0`. O workflow `.github/workflows/crankohub-release.yml` compila e cria o Release com o `CrankoHub.exe`; a versão vem da tag.
- **Atualizar os outros PCs:** Configurações → Atualizações → "Verificar atualizações" → "Atualizar agora" (`Services/AtualizacaoService.cs`). Sem token: os Releases vêm do repositório público `DiegoOttani/cranko-hub`. PC novo: baixar o `CrankoHub.exe` do Releases.

---

## 1. Finanças (só despesas) — próximo da fila

**Objetivo:** uma tela que mostra para onde vai o dinheiro no mês. Ela junta os gastos lançados à mão com os que o app já conhece (combustível, manutenção, documentos do carro e assinaturas), sem precisar digitar nada duas vezes.

**Decisões já tomadas:**
- Só **despesas**: sem receitas e sem saldo.
- Os gastos das outras áreas entram **sozinhos**, como lançamentos automáticos, só para leitura.

### Modelo
- `Models/Despesa.cs`: `Id`, `Data` (DateOnly), `Descricao`, `Valor` (double), `Categoria`, `Pagamento?` (cartão, Pix...), `Obs?`.
- `Models/EstadoApp.cs`: nova propriedade `Financas` (classe `DadosFinancas`) com `List<Despesa> Despesas` e `double? OrcamentoMensal` (limite de gastos do mês, opcional).
- `ArmazenamentoService`:
  - no `Normalizar`: `s.Financas ??= new()`, `Despesas ??= []`, remover nulos e gerar ids;
  - no `Exemplo`: ~20 despesas espalhadas nos últimos 6 meses (Mercado, Restaurante, Farmácia, Lazer...) e um orçamento de exemplo.

### Cálculos — `Services/FinancasCalculos.cs`
- `record Lancamento(DateOnly Data, string Descricao, double Valor, string Categoria, string Origem, string? Link, Despesa? Despesa)`.
  - Valores de `Origem`: `"manual"`, `"combustivel"`, `"manutencao"`, `"documento"`, `"assinatura"`.
  - `Link` é a tela de onde o lançamento vem: `carro?aba=abastecimentos&veiculo=...`, `carro?aba=manutencao&veiculo=...`, `carro?aba=documentos&veiculo=...` ou `assinaturas`.
- `DoMes(EstadoApp d, int ano, int mes)` junta:
  - despesas manuais do mês;
  - abastecimentos com `Valor` → categoria **"Carro · Combustível"**, descrição tipo "Gasolina · 36,5 L · {veículo}";
  - manutenções com `Valor` → **"Carro · Manutenção"** (descrição: nome do tipo + descrição);
  - documentos **pagos** com `Valor` → **"Carro · Documentos"**, usando o `Vencimento` como data;
  - cobranças das assinaturas **ativas** no mês → **"Assinaturas"**.
- **Cobranças de assinatura no mês:** cuidado aqui.
  - Hoje `AssinaturasCalculos` só calcula para frente (`ProximaCobranca`, `CobrancasAte`). É preciso uma função nova `CobrancasNoPeriodo(Assinatura a, DateOnly inicio, DateOnly fim)`.
  - Ela parte de `a.Vencimento` e anda para trás e para frente pelo ciclo, com `CicloCobranca.Buscar(a.Ciclo).Avancar(a.Vencimento, n)` e `n` negativo/positivo.
  - O modelo `Assinatura` não tem data de início, então meses passados são **estimados**. A tela deve mostrar "estimado pelo ciclo" nesses lançamentos.
  - Assinaturas canceladas (`Ativa = false`) ficam de fora.
- `PorCategoria(List<Lancamento>)` → `List<BarraDado>` ordenada do maior para o menor.
- `Mensal(EstadoApp d, int meses = 12)` → total por mês (`BarraDado`, rótulo com `Formatacao.MesLabel`).
- `CategoriasSugeridas`: Mercado, Restaurante, Delivery, Farmácia, Saúde, Casa, Contas, Educação, Lazer, Roupas, Presentes, Pets, Transporte, Outros.

### Tela — `Pages/Financas.razor` (`@page "/financas"`)
- **Menu:** item "Finanças" entre Assinaturas e o separador, com ícone novo em `Icones.cs` (carteira ou cifrão). Adicionar `"financas" => "Finanças"` no `TituloDaPagina` do layout.
- **Seletor de mês:** `‹ outubro 2026 ›`. O mês escolhido fica guardado num campo `static`, como `_periodo` em `Pages/CarroAbas/AbaConsumo.razor`.
- **KPIs** (classe `kpis`):
  - total do mês;
  - comparação com o mês anterior (componente `Delta`, com `BomQuandoCai = true`);
  - média dos últimos 6 meses;
  - orçamento restante com barra `progress` (se `OrcamentoMensal` definido; senão, um link "definir orçamento");
  - maior categoria.
- **Formulário "Nova despesa"**, seguindo o padrão do formulário de `Pages/Assinaturas.razor`:
  - Data, Descrição*, Valor* (texto lido com `Pf`), Categoria (input com `datalist` de `CategoriasSugeridas` + categorias já usadas), Forma de pagamento, Observação;
  - edição e cancelar, como nas outras telas;
  - campo para definir o orçamento mensal, numa linha separada ou num pequeno diálogo.
- **Gráficos:**
  - "Gastos por categoria" (`GraficoBarras` com `PorCategoria`);
  - "Gasto mensal" dos últimos 12 meses (`Mensal`).
- **Tabela do mês:**
  - filtro em chips: Todos / Manuais / Automáticos;
  - colunas: Data, Descrição, Categoria, Origem (badge), Pagamento, Valor;
  - lançamentos manuais têm editar/excluir, com confirmação via `IDialogoService.Confirmar`;
  - os automáticos têm um link "ver" que abre a tela de origem (`<a href="@Link">`), sem editar/excluir.

### Integrações
- **Início (`Pages/Inicio.razor`):** link-card "Finanças" com gasto do mês, comparação com o mês anterior e maior categoria, junto dos cards de Assinaturas/Dieta/Carro.
- **Alertas (opcional):** em `Services/Alertas.cs`, avisar quando o gasto do mês passar de 90% do `OrcamentoMensal`. A chave deve ser `orcamento:{ano}-{mes}`, para notificar uma vez por dia.
- **`Pages/Dados.razor`:**
  - contador "Despesas";
  - incluir `d.Financas.Despesas.Count` no `temDados` do "Carregar exemplo";
  - texto do "Apagar tudo" citando as despesas.

### Como testar
1. `dotnet build` sem erros.
2. Carregar os dados de exemplo e conferir que o total do mês bate com a soma de: manuais + abastecimentos com valor + manutenções + documentos pagos + cobranças de assinaturas do mês.
3. Trocar de mês e conferir o comparativo com o mês anterior.
4. Criar, editar e excluir uma despesa manual.

---

## 2. Contas a pagar

- **O que é:** luz, água, internet, aluguel, cartão de crédito. Parecido com Assinaturas, mas o **valor muda todo mês** e cada mês precisa ser marcado como **pago**.
- **Modelo:**
  - `ContaFixa` (nome, dia de vencimento, categoria, valor médio);
  - `PagamentoConta` (conta, mês/ano, valor, pago em).
- **Integrações:**
  - entrar nos alertas ("Conta de luz vence amanhã");
  - entrar nas Finanças como origem `"conta"`.

## 3. Exportar para Excel/CSV

- Botões "Exportar CSV" nas tabelas deç abastecimentos, manutenões, documentos, assinaturas e (depois) despesas.
- Usar `;` como separador e números no formato pt-BR, para abrir certo no Excel brasileiro. Salvar via `SaveFileDialog`, como em `DialogoService.SalvarBackup`.

## 4. Treinos

- Registro de academia: exercício, carga, séries e repetições, por dia.
- Gráfico de evolução da carga por exercício e volume semanal. Conversa com a Dieta (peso × força).

## 5. Hábitos

- Checklist diário (água, leitura, treino, dormir cedo...), com sequência de dias e um mapa de calor do ano.
- Alerta à noite se o checklist do dia não foi preenchido.

## 6. Saúde -> IMPORTANTE POREM AINDA MUITO CEDO PARA FAZER

- Remédios com horário (notificação pela bandeja), consultas e exames com data.
- Resultados de exames ao longo do tempo (ex.: colesterol, glicemia) em gráfico de linha.

## 8. Lista de tarefas ✅ (feito)

- Página **Tarefas** com quadros estilo Kanban (Trabalho, Faculdade, Pessoal, Setup... o usuário cria os seus, com emoji).
- Cada quadro tem colunas (padrão *A fazer / Fazendo / Feito*; a última conta como concluída) e cards arrastáveis com prazo, prioridade, etiquetas, descrição e checklist.
- Visão **Hoje** junta, de todos os quadros, o que vence hoje, está atrasado ou foi marcado como foco (☆).
- Resumo do dia no Início, tarefas atrasadas nos alertas e, a partir das 20h, uma notificação com o que foi concluído e o que ficou pendente.

## 9. Melhoria na configurações ✅ (feito)

- Card **Módulos** em Configurações: um interruptor para Dieta, Carro, Assinaturas, Entretenimento e Tarefas.
- Um módulo desligado some do menu, dos cards do Início, do "Precisa de atenção" e das notificações (inclusive o resumo noturno das Tarefas).
- **Os dados não são apagados**: ligar de novo traz tudo de volta, e o backup continua com tudo. A página ainda abre pelo endereço.
- Fica salvo em `config.modulosOcultos`, uma lista dos **escondidos**, para que módulos novos já apareçam ligados.
- No Início, quando só um card de uma dupla está visível, ele ocupa a linha inteira.

## 10. Versão para Linux (Linux Mint) ✅ (feito e testado no Mint)

**Abordagem:** um **segundo projeto**, `Linux/CrankoHub.Linux.csproj`, com [Photino.Blazor](https://github.com/tryphotino/photino.Blazor) (janela nativa com WebKitGTK). O projeto Windows (WPF) continua exatamente como era. O projeto Linux compila **os mesmos arquivos** por link (`Pages/`, `Components/`, `Layout/`, `Models/`, `Services/`, `wwwroot/`) e só troca as partes exclusivas do Windows, que ficam em `Linux/Plataforma/` com o mesmo nome e API:

| Windows | Linux (`Linux/Plataforma/`) |
|---|---|
| Janela WPF (`App.xaml`, `MainWindow.xaml`) | `Linux/Program.cs` com `PhotinoBlazorAppBuilder` |
| `BandejaService` (ícone na bandeja + balões) | Sem bandeja (fechar a janela encerra o app); notificações pelo `notify-send` |
| `DialogoService` (diálogos do Windows) | Diálogos GTK do Photino |
| `InicializacaoWindows` (Registro) | `~/.config/autostart/crankohub.desktop` |
| `AtualizacaoService` (troca o `.exe`) | Baixa `CrankoHub-linux-x64.tar.gz`, extrai ao lado e troca a pasta |
| `DispatcherTimer` do WPF (usado pelo `AvisosService`) | Substituto mínimo com `System.Threading.Timer` |
| Mutex de instância única | Arquivo de trava em `~/.local/share/CrankoHub/instancia.lock` |
| Capas em `https://capas.crankohub/` (WebView2) | Pasta das capas servida em `capas/` (`CapasService.Prefixo`) |

Os dados ficam em `~/.local/share/CrankoHub/dados.json`, e os backups do Windows importam direto.

### Publicação e instalação
- O workflow `crankohub-release.yml` ganhou o job `linux`, que anexa o `CrankoHub-linux-x64.tar.gz` ao mesmo Release. O pacote é autossuficiente e não precisa instalar o .NET.
- No Mint: extrair o `.tar.gz` e rodar `./instalar.sh`. Ele copia o app para `~/.local/share/CrankoHub/app` e cria o atalho no menu do Cinnamon.
- Precisa do `libwebkit2gtk-4.1-0`, que já vem no Mint 21/22. O instalador avisa se faltar.
- Para gerar o pacote manualmente: `dotnet publish Linux/CrankoHub.Linux.csproj -c Release -o out`.

### Falta / próximos passos
- Testado no Mint com Cinnamon: instalação pelo Release e funcionamento geral OK.
- Ícone na bandeja (AppIndicator), se fizer falta.
- Os textos "Windows" em Configurações aparecem também no Linux.

## 11. Repositório público e atualização automática sem token ✅ (feito)

- O CrankoHub agora mora no repositório público [DiegoOttani/cranko-hub](https://github.com/DiegoOttani/cranko-hub), com o histórico trazido do `MyProjs_`. Na raiz ficam o `cranko-hub.slnx` (Windows + Linux), o `.gitignore` e o workflow `crankohub-release.yml`.
- O `AtualizacaoService` (Windows e Linux) consulta os Releases **sem token**. O GitHub permite 60 consultas por hora por IP, de sobra para verificar uma vez por dia.
- "Verificar sozinho" funciona sem configuração nenhuma, e o campo do token saiu de Configurações. `TokenGitHub` continua no modelo, sem uso, para não quebrar backups antigos.
- O `UserAgent` da busca do Entretenimento aponta para o repositório novo.
- **Atenção:** quem já tem o app instalado de antes continua olhando o `MyProjs_`. Nesses PCs, instale uma vez manualmente o `.exe` do primeiro Release do repositório novo; daí em diante ele se atualiza sozinho.
- Ideia futura, para ficar como o ai-usagebar: item "Verificar atualizações" no menu da bandeja (`BandejaService`).

## 12. Usar em mais de um computador ✅ (feito, falta testar com dois PCs)

**Abordagem:** pasta sincronizada (OneDrive, Google Drive, Dropbox), sem servidor e sem login. Cada pessoa usa a própria nuvem, então funciona igual no app público, sem custo nem dados de terceiros com a gente.

- Configurações → Armazenamento → **"Usar pasta sincronizada…"**. Se a pasta já tem um `dados.json` (o outro PC), o app usa os dados de lá e guarda os daqui como `dados.antes-sincronizar-*.json`. Se não tem, copia os dados e as `capas/` para lá. Depois o app reinicia.
- A pasta escolhida fica em `%LocalAppData%\CrankoHub\local.json`, que é de cada PC. "Iniciar com o Windows" também passou para lá, para não ligar sozinho no outro PC. WebView2 e a trava de instância continuam na pasta local.
- Com o app aberto, um `FileSystemWatcher` percebe quando o outro PC sincronizou. O app recarrega os dados, recria a página aberta e mostra um toast.
- **Conflito:** se o arquivo mudou por fora e o app vai salvar antes de recarregar, a versão de fora vira `dados.conflito-*.json` e nada se perde. Configurações lista essas cópias, inclusive as do OneDrive (`dados-NOMEDOPC.json`).
- Se a pasta sincronizada não existir ao abrir (ex.: OneDrive desconectado), o app usa os dados locais e avisa em Configurações.
- "Voltar para a pasta local" copia os dados de volta e não apaga a pasta sincronizada.
- Limitação: usar os dois PCs **ao mesmo tempo** não junta as mudanças. A última gravação vale, e a outra fica como cópia de conflito.

### Futuro: servidor com login
- Planejado no item 14 (Supabase).

## 13. Visual Frutiger Aero 🧪 (em teste no branch `layout-frutiger-aero`)

**Ideia:** um visual no estilo **Frutiger Aero**, no clima do logo da Crankos Enterprises e do Aqua dos Macs antigos (Mac OS X 10.0–10.4), com céu, vidro, gel e bolhas. A ideia é que ele **não substitua** o visual atual. No futuro ele vai para o app principal como uma **opção em Configurações**, e o visual de hoje continua sendo o padrão.

### O que já existe no branch
- **`wwwroot/css/aero.css`:** carregado depois do `app.css` e só sobrescreve. Tirar o `<link>` dele do `index.html` volta ao visual atual.
  - **Fundo:** céu com reflexo de sol, raios, nuvens e bolhas. No tema escuro vira um oceano azul-profundo.
  - **Vidro fosco** (`backdrop-filter`): barra lateral, cartões, KPIs, diálogos, tooltips dos gráficos e colunas do Kanban.
  - **Gel do Aqua:** botões em pílula, item ativo do menu, abas, chips e interruptores. Tudo usa a **cor de destaque** de Configurações (`--accent`), então a escolha do usuário continua valendo.
  - **Detalhes de época:** barras de progresso com listras em movimento (desligadas com `prefers-reduced-motion`), tabelas com linhas alternadas e cabeçalho brilhante, barras de rolagem em gel e campos com sombra afundada.
  - **Fonte:** Segoe UI, com Frutiger antes na lista caso esteja instalada.
  - Os tokens do tema ficam no topo do arquivo, nas versões clara e escura (`--sky`, `--glass`, `--gloss`, `--face`, `--gel`...). O `--gel` é declarado em `*` para respeitar um `--accent` trocado localmente, como nos botões de perigo.
- **Logo:** o brasão fica em `wwwroot/img/` em duas versões: `logo-96.png`, usado na barra lateral, e `logo-480.jpg`, usado no banner do Início. O recorte mostra só o globo, com `object-view-box`.
- **`Layout/LayoutPrincipal.razor`:** o brasão substitui o ícone da marca, e o nome virou "**Cranko**" em azul e "**Hub**" em verde, como no letreiro do logo.
- **`Pages/Inicio.razor`:** o banner ganhou o brasão à direita (`hero-logo`) e um anel verde de vidro. O texto agora fica dentro de `hero-texto`.
- **Barra de título (WPF):** gradiente brilhante e "bolinhas" do semáforo com efeito de gel, em `MainWindow.xaml` e no `AplicarTema` do `MainWindow.xaml.cs`.

### Próximo passo: trazer para o app principal com um botão em Configurações
- **Modelo:** `Config.Visual` (string, `"padrao"` ou `"aero"`), opcional, para backups antigos continuarem importando. No `Normalizar`, valor vazio ou desconhecido vira `"padrao"`.
- **Configurações → Aparência:** uma linha "Visual" com chips **Padrão / Frutiger Aero**, junto da cor de destaque. Trocar aplica na hora, sem reiniciar.
- **CSS:** deixar o `<link id="visual-aero" href="css/aero.css" disabled>` no `index.html`. Uma função `crankohub.aplicarVisual(visual)` no `interop.js` liga e desliga o link e marca `data-visual` no `<html>`. Ela é chamada no `OnAfterRenderAsync` do layout, junto do `aplicarTema`/`aplicarDestaque`.
- **Marcação:** o brasão na barra lateral e no banner do Início só aparece no visual Aero. No Padrão continua o ícone `Icones.Logo` e o banner sem imagem.
- **Barra de título:** o `JanelaService` avisa a troca de visual, do mesmo jeito que já faz com `TemaMudou`. O `MainWindow` escolhe entre as cores lisas de hoje e o gradiente Aero, e entre as bolinhas lisas e as de gel. No Linux (Photino) a barra é a do sistema, então não há nada a fazer.
- **Como testar:** alternar entre os visuais nos temas claro e escuro, com várias cores de destaque, em todas as páginas (Início, Dieta, Carro e suas abas, Assinaturas, Entretenimento, Tarefas, Configurações), nos diálogos de confirmação e nos toasts.

## 14. Supabase: dados centralizados com login 🚧 (fases 1 a 4 feitas; falta testar a sincronização no app; capas ficam para depois)

**Objetivo:** contas com login e dados centralizados por usuário, para que o desktop atual e os futuros app de celular e versão web usem a mesma base.

**Situação:** o repositório é público, mas por enquanto só o dono testa essa integração. A estrutura já nasce pronta para vários usuários, cada um vendo só os próprios dados.

**Onde estamos (03/11/2026):**
- ✅ Projeto criado no Supabase, URL e publishable key em `Services/SupabaseConfig.cs`.
- ✅ SQL da fase 1 rodado; cadastro desligado no painel; usuário `diego@crankohub.local` criado à mão.
- ✅ Testado por `curl` só com a publishable key: cadastro recusado (`signup_disabled`), as 11 tabelas negam leitura e gravação sem login (`permission denied`), senha errada recusada.
- ✅ Código das fases 2 (login), 3 (sincronização) e 4 (primeiro login/migração) compilando no Windows e no Linux. O .exe de teste foi gerado com `dotnet publish CrankoHub.csproj -c Release -o <pasta>`.
- ⏳ **Falta testar no app:** entrar com `diego`, ver "Sincronizado às …" no card "Conta", conferir os dados no Table Editor do painel, editar/excluir e ver refletir; depois com um segundo PC (ver "Como testar").
- ⏳ Ainda sem commit e sem Release novo (o .exe de teste continua marcado como 1.0.0, igual ao último Release, então o auto-update não o substitui). Para os outros PCs: tag nova, ex.: `crankohub-v1.1.0`.
- ⏸️ Fase 5 (capas) adiada.

**Login só do admin (decisão atual):**
- Não existe cadastro pelo app: o card "Conta" tem apenas **Usuário + Senha → Entrar** (e Sair). Sem "criar conta" e sem "esqueci a senha".
- O login é por **usuário**, não por e-mail. O Supabase Auth só aceita e-mail, então o app transforma `diego` em `diego@crankohub.local` (`SupabaseConfig.DominioUsuario`, `ContaService.ParaEmail`). Um texto com `@` passa como está.
- O usuário é criado **à mão** no painel. Como a publishable key é pública, tirar o botão não basta: o bloqueio de verdade é desligar **"Allow new users to sign up"** no painel (ver abaixo).
- Futuro: e-mail opcional em Configurações → Conta, para recuperar senha e avisos.

### Formas de conexão do Supabase

| Forma | Serve para | Uso no CrankoHub |
|---|---|---|
| **Data API** (URL + publishable key + login) | Apps desktop, web e celular; a API REST é gerada a partir das tabelas | **Sim, é a base do plano** |
| **Edge Functions** (servidor no Supabase) | Lógica que precisa da secret key | Só depois: excluir conta (LGPD) |
| **Servidor próprio** (ASP.NET Core) | Regra de negócio pesada ou integrações privadas | Não agora: exigiria hospedar e manter um servidor sem ganho real |
| **Connection string** (Postgres direto) | Servidores, migrações, `pg_dump`, ferramentas de banco | **Nunca dentro do app**: dá acesso total ao banco |
| **Framework .NET / pacote `Supabase`** | Cliente C# pronto, mantido pela comunidade | Não: `HttpClient` cobre o necessário sem dependência nova |

**Segurança (o repositório é público):**
- A publishable key (`sb_publishable_...`) pode ir no app e no repositório.
- Quem protege os dados é o **RLS**: cada linha só é acessível pelo próprio dono.
- A `sb_secret_...`, a senha do banco e a connection string nunca entram no app nem no repositório.
- Usar as chaves novas: `anon` e `service_role` são legadas, com descontinuação prevista para o fim de 2026.

A opção "Server Build APIs" da tela de Connect não foi localizada na documentação. O plano assume que é a Data API/construção de APIs sobre o banco. Se for outra coisa, muda apenas a camada de conexão.

### Arquitetura

```
Desktop (WPF/Linux) ─┐
App celular (futuro) ├─ HTTPS + login ─> Supabase: Auth + Data API + RLS + Postgres + Storage
Web (futuro) ────────┘
```

- **Offline primeiro:** o `dados.json` continua como cópia local e as telas continuam lendo `Store.Dados`. Os cerca de 100 pontos de uso e o `Salvar()` não mudam.
- **Sincronização por trás:** um serviço novo compara os dados com a última versão confirmada pelo servidor e envia só o que mudou.
- **Login opcional no desktop:** sem conta, o app funciona como hoje.

### Fase 1: Banco (`supabase/migrations/0001_esquema_inicial.sql`) ✅ (rodado no painel)

Todas as tabelas de dados têm:
- `user_id uuid references auth.users on delete cascade` e `id text`, com **PK `(user_id, id)`**. Mantém os ids atuais do `NovoId()`, então não há conversão.
- `atualizado_em timestamptz`, preenchido por trigger no servidor; é o cursor da sincronização.
- `excluido_em timestamptz`: exclusão lógica, para o outro aparelho saber que algo foi apagado.

| Tabela | Origem | Observação |
|---|---|---|
| `perfis` | `Dieta.Perfil`, `Tema`, `Config.CorDestaque`, `Config.ModulosOcultos` | PK `user_id`, uma linha por usuário |
| `registros_peso` | `Dieta.Registros` | |
| `veiculos` | `Carro.Veiculos` | |
| `abastecimentos`, `manutencoes` | `Carro.*` | FK composta para `veiculos` |
| `documentos_carro` | `Carro.Documentos` | `veiculo_id` nulo para CNH |
| `assinaturas` | `Assinaturas` | |
| `entretenimento` | `Entretenimento` | `capa` guarda o nome do arquivo |
| `quadros` | `Tarefas.Quadros` | Nova coluna `ordem` |
| `colunas` | `Quadro.Colunas` | Nova coluna `ordem`: a última coluna é a de concluídos |
| `cards` | `Tarefas.Cards` | `etiquetas text[]`, `checklist jsonb` |

Tipos: datas em `date`, valores em `numeric`.

RLS e permissões, em todas as tabelas:
- `enable row level security`;
- políticas de select/insert/update com `(select auth.uid()) = user_id`, apenas para `authenticated`; sem política de delete;
- `grant select, insert, update` explícito para `authenticated`, sem depender do padrão do projeto;
- nada liberado para `anon`.

**Ficam fora do banco (por aparelho):** `PastaBackup`, `BackupAutomatico`, avisos enviados, bandeja, `Carro.Ativo`, `Tarefas.Ativo`. As chaves `ChaveTmdb`/`ChaveRawg` também ficam locais nesta etapa.

### Fase 2: Login ✅

- **`Services/SupabaseConfig.cs`:** URL, publishable key e domínio interno dos usuários como constantes. Com a URL vazia, o card "Conta" diz que a conta não está disponível e nada é chamado.
- **`Services/SupabaseCliente.cs`:** `HttpClient` próprio que envia `apikey` e `Authorization: Bearer <token do usuário>`. Nunca envia a publishable key no `Authorization`, porque o Supabase tenta ler como JWT e rejeita. Renova o token sozinho; `Enviar(...)` já serve para a Data API da fase 3.
- **`Services/ContaService.cs`:** entrar (usuário → e-mail interno), sair, restaurar a sessão ao abrir e evento `Mudou`. Sessão recusada pelo servidor (senha trocada, revogada) sai sozinha; sem internet continua logado ("sem conexão agora").
- **Sessão:** só o refresh token, nunca a senha. Windows: DPAPI em `Services/CofreSessao.cs` (`sessao.bin`). Linux: `Linux/Plataforma/CofreSessao.cs`, arquivo `sessao` com permissão 600.
- **`ConfigLocal`:** `ContaUsuario` e `ContaId`, por computador.
- **`Pages/Configuracoes.razor`:** card "Conta" com usuário/senha e Entrar (sem criar conta); logado, mostra o status da sincronização, "Sincronizar agora" e Sair. Entrar e sair reiniciam o app (`AtualizacaoService.Reiniciar()`), para trocar para a pasta da conta e voltar (fase 4).
- **`GarantirSessao()`:** usado pela sincronização; renova o token quando precisa e, se o servidor recusar, sai da conta.

### Fase 3: Sincronização (`Services/SincronizacaoService.cs`) ✅ (falta testar com dois PCs)

- **Gatilho:** `ArmazenamentoService.Salvar()` dispara um evento novo, com espera de 3 s para agrupar edições. Também roda ao abrir, ao voltar a internet e a cada 5 min.
- **Envio:** compara cada coleção com `base.json` (última versão confirmada), item a item pelo `Id`.
  - Novos ou alterados: `POST /rest/v1/<tabela>?on_conflict=user_id,id` com `Prefer: resolution=merge-duplicates`.
  - Removidos: preenche `excluido_em`.
  - Ordem pais antes de filhos: `veiculos` → filhos; `quadros` → `colunas` → `cards`.
  - Um `JsonSerializerOptions` com `SnakeCaseLower` mapeia os modelos atuais, sem DTOs. Quadros, colunas (que no app ficam dentro do quadro) e o perfil usam records próprios (`QuadroLinha`, `ColunaLinha`, `PerfilLinha`).
- **Recebimento:** busca `atualizado_em=gt.<cursor>` por tabela (com margem de 1 min), aplica em `Dados`, grava local e atualiza `base.json`. Só aplica o que difere da base (o que acabou de ser enviado volta igual e é ignorado). A tela é atualizada pelo evento `Recebeu` → `RecebeuDaConta` em `Layout/LayoutPrincipal.razor`, no mesmo estilo do `RecarregarDados`.
- **Conflito:** por registro, a última gravação no servidor vence. Editar coisas diferentes em dois aparelhos não gera conflito, um ganho grande sobre o documento inteiro de hoje.
- **Falha ou offline:** não incomoda o usuário. Mudanças ficam pendentes, porque o diff com `base.json` as recupera, e o status aparece no card "Conta" (com o botão "Sincronizar agora").
- **Pasta sincronizada:** logado, os botões de OneDrive ficam desativados, pois os dois mecanismos não devem rodar juntos. A estratégia do item 12 segue para quem não tem conta.

### Fase 4: Primeiro login (migração) ✅

- **Pasta por conta:** logado, `PastaDados` é `contas/<user_id>/` (`ArmazenamentoService.PorConta`). Ao entrar, `PrepararPastaDaConta` copia os dados e as capas atuais para lá (se a pasta ainda não existe) e o app reinicia. Ao sair, as mudanças pendentes são enviadas, o app reinicia e volta aos dados de antes do login; a pasta da conta fica como cache para o próximo login.
- **Primeira sincronização** (sem `base.json` na pasta da conta): baixa tudo do servidor.
  - Conta vazia: os dados copiados sobem.
  - Conta com dados: eles substituem os copiados, que ficam guardados como `dados.antes-sincronizar-*.json` (os originais também continuam na pasta de antes do login).
- Importar backup, dados de exemplo e "Apagar tudo" refletem na nuvem pelo mesmo diff; logado, as confirmações avisam disso.
- **Detalhes da implementação:** as linhas são geradas dos modelos com `SnakeCaseLower` (quadros, colunas e perfil têm records próprios, com `ordem` = posição na lista). Filhos cujo pai não existe localmente não sobem, para não esbarrar nas chaves estrangeiras. O layout fornece `NaTela` (thread do Blazor) para ler e aplicar os dados; mudanças recebidas recriam a página aberta e reaplicam tema/cor.

### Fase 5: Capas (Storage) ⏸️ (adiada)

Hoje as capas **não sincronizam**: a coluna `entretenimento.capa` leva só o nome do arquivo, e a imagem fica na pasta `capas/` de cada PC. Num PC novo, os itens aparecem sem capa (dá para buscar de novo pelo próprio item).

**Decisão tomada (para quando retomar):**
- A imagem **não** vai na mesma linha do Entretenimento: mudar a nota ou o status reenviaria a imagem, e a busca de "o que mudou" traria as imagens a cada sincronização (gasta a cota de tráfego de 5 GB/mês e pesa o `base.json`).
- A capa fica **separada** e é enviada uma vez só: o nome do arquivo é único e nunca muda, então basta subir quando é nova e baixar quando falta.
- **Preferência: Storage** (bucket privado `capas`, caminho `<user_id>/<arquivo>`, políticas por dono). Dá o mesmo trabalho que uma tabela `capas` no banco, não divide os 500 MB do banco (o Storage tem 1 GB no plano gratuito) e guarda o arquivo como ele é, sem base64. A alternativa no banco continua possível se preferir não usar o Storage.
- Reduzir as capas importadas para ~600 px de largura antes de salvar (hoje o `CapasService` guarda a imagem original), para ficarem em ~100–200 KB.
- O `CapasService` envia a capa nova, baixa as que faltam no aparelho e a pasta local continua como cache.

### Antes de abrir o login ao público (fora desta entrega)

- **Cadastro:** hoje fechado no painel e sem botão no app. Abrir exige religar "Allow new users to sign up" e criar a tela de cadastro (com e-mail de verdade).
- **Confirmação de e-mail:** desligada agora, para testar; ligar, com SMTP próprio e página de redefinição de senha (o desktop não recebe esse link).
- **LGPD:** política de privacidade, exportação de dados e **excluir conta** (Edge Function com a secret key).
- **Revisão de RLS:** rodar o Security Advisor do painel e testar com duas contas.
- **Plano:** conferir limites e pausa por inatividade do plano gratuito.

### Arquivos principais

- **Novos:** `supabase/migrations/0001_esquema_inicial.sql`, `Services/SupabaseConfig.cs`, `Services/SupabaseCliente.cs`, `Services/ContaService.cs`, `Services/SincronizacaoService.cs`, `Services/CofreSessao.cs`, `Linux/Plataforma/CofreSessao.cs`.
- **Alterados:** `Services/ArmazenamentoService.cs` (`PorConta`, `PastaDaConta`, `PrepararPastaDaConta`, evento `Salvou`, `Substituir`, `NormalizarESalvar`), `Services/ConfigLocal.cs`, `Pages/Configuracoes.razor`, `Layout/LayoutPrincipal.razor` (`NaTela`, `RecebeuDaConta`, texto do rodapé do menu), `App.xaml.cs`, `Linux/Program.cs`, `Linux/CrankoHub.Linux.csproj`.
- **Fase 5 (depois):** `Services/CapasService.cs` e um SQL novo para o bucket.

### O que fazer no painel do Supabase ✅ (feito)

1. Copiar a **Project URL** e a **publishable key** (Connect ou Settings > API Keys) para `Services/SupabaseConfig.cs`.
2. Rodar `supabase/migrations/0001_esquema_inicial.sql` no **SQL Editor** e conferir o **Security Advisor** (nenhuma tabela sem RLS).
3. Authentication → Sign In / Providers: **desligar "Allow new users to sign up"** (é isso que impede outras pessoas de criarem conta) e deixar "Confirm email" desligado.
4. Authentication → Users → **Add user**: e-mail `diego@crankohub.local` (o usuário + `@crankohub.local`), a senha e "Auto Confirm User".
5. (Opcional) Inserir a linha em `perfis` com `user_id` do usuário criado e `usuario = 'diego'`.
6. Conferir que o cadastro está fechado: `curl -X POST <url>/auth/v1/signup -H "apikey: <publishable>" -H "Content-Type: application/json" -d '{"email":"x@exemplo.com","password":"12345678"}'` deve responder "Signups not allowed".

### Como testar

1. ✅ `dotnet build` do `cranko-hub.slnx` (Windows e Linux) sem erros.
2. **RLS:** ✅ chamadas sem login não leem nem gravam nada (testado por `curl`). Falta: com duas contas, a conta B não lê nem altera dados da conta A.
3. **Migração:** logar com os dados de exemplo e conferir no painel as contagens contra `Pages/Configuracoes.razor`.
4. **Dois PCs (ou duas pastas de teste):** criar, editar e excluir em A e ver em B sem reiniciar.
5. **Conflito:** editar registros diferentes em A e B ao mesmo tempo; as duas mudanças ficam.
6. **Offline:** editar sem internet, reconectar e confirmar o envio.
7. **Sem conta:** o app segue funcionando como hoje.
8. **Sessão:** reiniciar mantém o login; sair volta aos dados locais, sem misturar contas.

## Pendências de teste do que já existe

- **Iniciar com o Windows** (Configurações → Aplicativo): conferir se a entrada aparece em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` e se o app abre na bandeja ao ligar o PC.
- **Notificações:** conferir a aparência no Windows e se o clique abre a tela certa.
- **Diálogos:** "Restaurar" um backup automático e "Marcar como pago" em Documentos (incluindo a pergunta de criar o do próximo ano).
