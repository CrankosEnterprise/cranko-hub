# CrankoHub — finalizados

Itens já entregues (ou encerrados) no CrankoHub, com as decisões e os detalhes de implementação. O que ainda falta fazer está em [updates.md](updates.md).

## 4. Lista de tarefas ✅ (feito)

- Página **Tarefas** com quadros estilo Kanban (Trabalho, Faculdade, Pessoal, Setup... o usuário cria os seus, com emoji).
- Cada quadro tem colunas (padrão *A fazer / Fazendo / Feito*; a última conta como concluída) e cards arrastáveis com prazo, prioridade, etiquetas, descrição e checklist.
- Visão **Hoje** junta, de todos os quadros, o que vence hoje, está atrasado ou foi marcado como foco (☆).
- Resumo do dia no Início, tarefas atrasadas nos alertas e, a partir das 20h, uma notificação com o que foi concluído e o que ficou pendente.

## 5. Melhoria na configurações ✅ (feito)

- Card **Módulos** em Configurações: um interruptor para Dieta, Carro, Assinaturas, Entretenimento e Tarefas.
- Um módulo desligado some do menu, dos cards do Início, do "Precisa de atenção" e das notificações (inclusive o resumo noturno das Tarefas).
- **Os dados não são apagados**: ligar de novo traz tudo de volta. A página ainda abre pelo endereço.
- Fica salvo em `config.modulosOcultos`, uma lista dos **escondidos**, para que módulos novos já apareçam ligados.
- No Início, quando só um card de uma dupla está visível, ele ocupa a linha inteira.
- **Abas nas Configurações** (mesmo padrão do Carro, com `?aba=` no link): **Aparência**, **Módulos**, **Integrações** (chaves TMDB/RAWG), **Conta** (antes "Dados e backup"; ver item 10) e **Aplicativo** (bandeja, notificações, iniciar com o Windows, atualizações). A aba escolhida fica guardada enquanto o app está aberto. Os links do Início e a notificação de versão nova abrem direto na aba certa. A conta saiu daqui e foi para o rodapé da barra lateral (item 14).

## 6. Versão para Linux (Linux Mint) ✅ (feito e testado no Mint)

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

O cache da conta fica em `~/.local/share/CrankoHub/contas/<user_id>/`.

### Publicação e instalação
- O workflow `crankohub-release.yml` ganhou o job `linux`, que anexa o `CrankoHub-linux-x64.tar.gz` ao mesmo Release. O pacote é autossuficiente e não precisa instalar o .NET.
- No Mint: extrair o `.tar.gz` e rodar `./instalar.sh`. Ele copia o app para `~/.local/share/CrankoHub/app` e cria o atalho no menu do Cinnamon.
- Precisa do `libwebkit2gtk-4.1-0`, que já vem no Mint 21/22. O instalador avisa se faltar.
- Para gerar o pacote manualmente: `dotnet publish Linux/CrankoHub.Linux.csproj -c Release -o out`.

### Falta / próximos passos
- Testado no Mint com Cinnamon: instalação pelo Release e funcionamento geral OK.
- Ícone na bandeja (AppIndicator), se fizer falta.
- Os textos "Windows" em Configurações aparecem também no Linux.

## 7. Atualização automática sem token ✅ (feito)

- O CrankoHub agora mora no repositório público [CrankosEnterprise/cranko-hub](https://github.com/CrankosEnterprise/cranko-hub), na organização Crankos Enterprise, com todo o histórico. Antes ficava em `DiegoOttani/cranko-hub`, e antes disso no `MyProjs_`. Na raiz ficam o `README.md` de apresentação, o `cranko-hub.slnx` (Windows + Linux), o `.gitignore` e o workflow `crankohub-release.yml`.
- **Atenção (mudança para a organização, 03/11/2026):** as versões 1.0.x instaladas procuram atualizações em `DiegoOttani/cranko-hub`. Nesses PCs, instale uma vez manualmente o `.exe` do primeiro Release da organização; daí em diante ele se atualiza sozinho.
- O `AtualizacaoService` (Windows e Linux) consulta os Releases **sem token**. O GitHub permite 60 consultas por hora por IP, de sobra para verificar uma vez por dia.
- "Verificar sozinho" funciona sem configuração nenhuma, e o campo do token saiu de Configurações. O `TokenGitHub` também saiu do modelo: arquivos antigos que ainda o tenham continuam abrindo (o campo é ignorado), e ele some do arquivo de dados na próxima gravação.
- O `UserAgent` da busca do Entretenimento aponta para o repositório novo.
- **Atenção:** quem já tem o app instalado de antes continua olhando o `MyProjs_`. Nesses PCs, instale uma vez manualmente o `.exe` do primeiro Release do repositório novo; daí em diante ele se atualiza sozinho.
- Ideia futura, para ficar como o ai-usagebar: item "Verificar atualizações" no menu da bandeja (`BandejaService`).

## 8. Usar em mais de um computador ❌ (removido no item 10: a conta faz esse papel)

**Abordagem:** pasta sincronizada (OneDrive, Google Drive, Dropbox), sem servidor e sem login. Cada pessoa usa a própria nuvem, então funciona igual no app público, sem custo nem dados de terceiros com a gente.

- Configurações → Dados e backup → Armazenamento → **"Usar pasta sincronizada…"**. Se a pasta já tem um `dados.json` (o outro PC), o app usa os dados de lá e guarda os daqui como `dados.antes-sincronizar-*.json`. Se não tem, copia os dados e as `capas/` para lá. Depois o app reinicia.
- A pasta escolhida fica em `%LocalAppData%\CrankoHub\local.json`, que é de cada PC. "Iniciar com o Windows" também passou para lá, para não ligar sozinho no outro PC. WebView2 e a trava de instância continuam na pasta local.
- Com o app aberto, um `FileSystemWatcher` percebe quando o outro PC sincronizou. O app recarrega os dados, recria a página aberta e mostra um toast.
- **Conflito:** se o arquivo mudou por fora e o app vai salvar antes de recarregar, a versão de fora vira `dados.conflito-*.json` e nada se perde. Configurações lista essas cópias, inclusive as do OneDrive (`dados-NOMEDOPC.json`).
- Se a pasta sincronizada não existir ao abrir (ex.: OneDrive desconectado), o app usa os dados locais e avisa em Configurações.
- "Voltar para a pasta local" copia os dados de volta e não apaga a pasta sincronizada.
- Limitação: usar os dois PCs **ao mesmo tempo** não junta as mudanças. A última gravação vale, e a outra fica como cópia de conflito.

### Futuro: servidor com login
- Planejado no item 14 (Supabase).

## 9. Supabase: dados centralizados com login ✅ (feito)

**Objetivo:** contas com login e dados centralizados por usuário, para que o desktop atual e os futuros app de celular e versão web usem a mesma base.

**Situação:** o repositório é público, mas por enquanto só o dono testa essa integração. A estrutura já nasce pronta para vários usuários, cada um vendo só os próprios dados.

**Onde estamos (03/11/2026):**
- ✅ Projeto criado no Supabase, URL e publishable key em `Services/SupabaseConfig.cs`.
- ✅ SQL da fase 1 rodado; cadastro desligado no painel; usuário admin criado à mão.
- ✅ Testado por `curl` só com a publishable key: cadastro recusado (`signup_disabled`), as 11 tabelas negam leitura e gravação sem login (`permission denied`), senha errada recusada.
- ✅ Código das fases 2 (login), 3 (sincronização) e 4 (primeiro login/migração) compilando no Windows e no Linux. O .exe de teste foi gerado com `dotnet publish CrankoHub.csproj -c Release -o <pasta>`.
- ✅ **Testado no app:** login com o usuário admin, "Sincronizado às …" embaixo do nome, dados conferidos no Table Editor do painel e edições/exclusões refletindo.
- ✅ Publicado nos Releases (versão atual: `crankohub-v2.0.0`).
- ✅ Código da fase 5 (capas no Storage, reduzidas com SkiaSharp) compilando no Windows e no Linux.
- ✅ `supabase/migrations/0002_capas.sql` rodado no SQL Editor (bucket `capas` e políticas).

**Login só do admin (decisão atual):**
- Não existe cadastro pelo app: o rodapé da barra lateral tem apenas **Entrar** (abre uma janela com Usuário + Senha) e, logado, o nome com o menu Sincronizar agora / Sair. Sem "criar conta" e sem "esqueci a senha".
- O login é por **usuário**, não por e-mail. O Supabase Auth só aceita e-mail, então o app converte o usuário num e-mail interno (`SupabaseConfig.DominioUsuario`, `ContaService.ParaEmail`).
- O usuário é criado **à mão** no painel. Como a publishable key é pública, tirar o botão não basta: o bloqueio de verdade é desligar **"Allow new users to sign up"** no painel (ver abaixo).
- Futuro: e-mail opcional no menu da conta, para recuperar senha e avisos.

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

- **`Services/SupabaseConfig.cs`:** URL, publishable key e domínio interno dos usuários como constantes. Com a URL vazia, o rodapé da conta não aparece e nada é chamado.
- **`Services/SupabaseCliente.cs`:** `HttpClient` próprio que envia `apikey` e `Authorization: Bearer <token do usuário>`. Nunca envia a publishable key no `Authorization`, porque o Supabase tenta ler como JWT e rejeita. Renova o token sozinho; `Enviar(...)` já serve para a Data API da fase 3.
- **`Services/ContaService.cs`:** entrar (usuário → e-mail interno), sair, restaurar a sessão ao abrir e evento `Mudou`. Sessão recusada pelo servidor (senha trocada, revogada) sai sozinha; sem internet continua logado ("sem conexão agora").
- **Sessão:** só o refresh token, nunca a senha. Windows: DPAPI em `Services/CofreSessao.cs` (`sessao.bin`). Linux: `Linux/Plataforma/CofreSessao.cs`, arquivo `sessao` com permissão 600.
- **`ConfigLocal`:** `ContaUsuario` e `ContaId`, por computador.
- **`Components/ContaRodape.razor`** (no rodapé da barra lateral, em `Layout/LayoutPrincipal.razor`): deslogado mostra **Entrar**, que abre uma janela com usuário/senha (sem criar conta); logado mostra a inicial, o nome e o status curto da sincronização, e o clique abre um menu com o status completo, Informações (página da Conta, `Pages/MinhaConta.razor`), "Sincronizar agora" e Sair. Entrar e sair reiniciam o app (`AtualizacaoService.Reiniciar()`), para trocar para a pasta da conta e voltar (fase 4). O card "Conta" saiu das Configurações.
- **`GarantirSessao()`:** usado pela sincronização; renova o token quando precisa e, se o servidor recusar, sai da conta.

### Fase 3: Sincronização (`Services/SincronizacaoService.cs`) ✅

- **Gatilho:** `ArmazenamentoService.Salvar()` dispara um evento novo, com espera de 3 s para agrupar edições. Também roda ao abrir, ao voltar a internet e a cada 5 min.
- **Envio:** compara cada coleção com `base.json` (última versão confirmada), item a item pelo `Id`.
  - Novos ou alterados: `POST /rest/v1/<tabela>?on_conflict=user_id,id` com `Prefer: resolution=merge-duplicates`.
  - Removidos: preenche `excluido_em`.
  - Ordem pais antes de filhos: `veiculos` → filhos; `quadros` → `colunas` → `cards`.
  - Um `JsonSerializerOptions` com `SnakeCaseLower` mapeia os modelos atuais, sem DTOs. Quadros, colunas (que no app ficam dentro do quadro) e o perfil usam records próprios (`QuadroLinha`, `ColunaLinha`, `PerfilLinha`).
- **Recebimento:** busca `atualizado_em=gt.<cursor>` por tabela (com margem de 1 min), aplica em `Dados`, grava local e atualiza `base.json`. Só aplica o que difere da base (o que acabou de ser enviado volta igual e é ignorado). A tela é atualizada pelo evento `Recebeu` → `RecebeuDaConta` em `Layout/LayoutPrincipal.razor`, no mesmo estilo do `RecarregarDados`.
- **Conflito:** por registro, a última gravação no servidor vence. Editar coisas diferentes em dois aparelhos não gera conflito, um ganho grande sobre o documento inteiro de hoje.
- **Falha ou offline:** não incomoda o usuário. Mudanças ficam pendentes, porque o diff com `base.json` as recupera, e o status aparece embaixo do nome, no rodapé da barra lateral (o menu tem "Sincronizar agora").
- **Pasta sincronizada:** logado, os botões de OneDrive ficam desativados, pois os dois mecanismos não devem rodar juntos. A estratégia do item 12 segue para quem não tem conta.

### Fase 4: Primeiro login (migração) ✅

- **Pasta por conta:** logado, `PastaDados` é `contas/<user_id>/` (`ArmazenamentoService.PorConta`). Ao entrar, `PrepararPastaDaConta` copia os dados e as capas atuais para lá (se a pasta ainda não existe) e o app reinicia. Ao sair, as mudanças pendentes são enviadas, o app reinicia e volta aos dados de antes do login; a pasta da conta fica como cache para o próximo login.
- **Primeira sincronização** (sem `base.json` na pasta da conta): baixa tudo do servidor.
  - Conta vazia: os dados copiados sobem.
  - Conta com dados: eles substituem os copiados, que ficam guardados como `dados.antes-sincronizar-*.json` (os originais também continuam na pasta de antes do login).
- Importar backup, dados de exemplo e "Apagar tudo" refletem na nuvem pelo mesmo diff; logado, as confirmações avisam disso.
- **Detalhes da implementação:** as linhas são geradas dos modelos com `SnakeCaseLower` (quadros, colunas e perfil têm records próprios, com `ordem` = posição na lista). Filhos cujo pai não existe localmente não sobem, para não esbarrar nas chaves estrangeiras. O layout fornece `NaTela` (thread do Blazor) para ler e aplicar os dados; mudanças recebidas recriam a página aberta e reaplicam tema/cor.

### Fase 5: Capas (Storage) ✅

A coluna `entretenimento.capa` continua levando só o nome do arquivo. A imagem vai **à parte**, para o Storage. Se fosse na linha do item, mudar a nota ou o status reenviaria a imagem, gastando a cota de tráfego de 5 GB/mês e pesando o `base.json`.

- **Bucket privado `capas`** (`supabase/migrations/0002_capas.sql`): caminho `<user_id>/<arquivo>`, até 2 MB por arquivo, só jpeg/png/webp/gif. Políticas de select/insert/update/delete apenas para o dono da pasta; nada para `anon`. O Storage tem 1 GB no plano gratuito e não divide os 500 MB do banco.
- **Uma vez só:** o nome do arquivo é único e nunca muda. Depois das linhas, `SincronizacaoService.Capas` sobe as capas que o servidor ainda não tem, baixa as que faltam no aparelho e apaga do servidor as que nenhum item usa mais (item excluído ou capa trocada). O `base.json` guarda a lista das capas que já estão no servidor (`Capas`), e só essas podem ser apagadas, então uma capa nova de outro PC nunca some.
- **Capa que ainda não chegou** (o outro PC ainda não subiu): o item aparece sem capa e a próxima rodada tenta de novo. Ao baixar, a página aberta é recriada e a capa aparece sem reiniciar.
- **Tamanho:** `CapasService.Reduzir` (SkiaSharp) deixa as capas com no máximo 600 px de largura, no mesmo formato, para o nome não mudar. Vale para as capas novas (buscadas ou importadas) e para as antigas, que são reduzidas antes de subir. Gif e imagens menores ficam como estão.
- **Pasta local:** continua sendo o cache, e o app sem conta segue igual.
- **SupabaseCliente:** `EnviarArquivo` (com `x-upsert`), `BaixarArquivo` (`/object/authenticated/...`, null quando não existe) e `ExcluirArquivo`.
- **Linux:** o pacote `SkiaSharp.NativeAssets.Linux.NoDependencies` dispensa a `libfontconfig` no Mint.

### Antes de abrir o login ao público (fora desta entrega)

- **Cadastro:** hoje fechado no painel e sem botão no app. Abrir exige religar "Allow new users to sign up" e criar a tela de cadastro (com e-mail de verdade).
- **Confirmação de e-mail:** desligada agora, para testar; ligar, com SMTP próprio e página de redefinição de senha (o desktop não recebe esse link).
- **LGPD:** política de privacidade, exportação de dados e **excluir conta** (Edge Function com a secret key).
- **Revisão de RLS:** rodar o Security Advisor do painel e testar com duas contas.
- **Plano:** conferir limites e pausa por inatividade do plano gratuito.

### Arquivos principais

- **Novos:** `supabase/migrations/0001_esquema_inicial.sql`, `Services/SupabaseConfig.cs`, `Services/SupabaseCliente.cs`, `Services/ContaService.cs`, `Services/SincronizacaoService.cs`, `Services/CofreSessao.cs`, `Linux/Plataforma/CofreSessao.cs`.
- **Alterados:** `Services/ArmazenamentoService.cs` (`PorConta`, `PastaDaConta`, `PrepararPastaDaConta`, evento `Salvou`, `Substituir`, `NormalizarESalvar`), `Services/ConfigLocal.cs`, `Pages/Configuracoes.razor`, `Layout/LayoutPrincipal.razor` (`NaTela`, `RecebeuDaConta`, rodapé com a conta), `App.xaml.cs`, `Linux/Program.cs`, `Linux/CrankoHub.Linux.csproj`.
- **Fase 5:** `supabase/migrations/0002_capas.sql` (novo), `Services/CapasService.cs` (`Reduzir`, `TipoMime`), `Services/SupabaseCliente.cs` (Storage), `Services/SincronizacaoService.cs` (`Capas`), pacote `SkiaSharp` nos dois `.csproj`.

### O que fazer no painel do Supabase ✅ (feito)

1. Copiar a **Project URL** e a **publishable key** (Connect ou Settings > API Keys) para `Services/SupabaseConfig.cs`.
2. Rodar `supabase/migrations/0001_esquema_inicial.sql` no **SQL Editor** e conferir o **Security Advisor** (nenhuma tabela sem RLS).
3. Authentication → Sign In / Providers: **desligar "Allow new users to sign up"** (é isso que impede outras pessoas de criarem conta) e deixar "Confirm email" desligado.
4. Authentication → Users → **Add user**: o e-mail interno do usuário, a senha e "Auto Confirm User".
5. (Opcional) Inserir a linha em `perfis` com o `user_id` e o nome de usuário criados.
6. Conferir que o cadastro está fechado: `curl -X POST <url>/auth/v1/signup -H "apikey: <publishable>" -H "Content-Type: application/json" -d '{"email":"x@exemplo.com","password":"12345678"}'` deve responder "Signups not allowed".
7. ✅ **Capas:** rodar `supabase/migrations/0002_capas.sql` no **SQL Editor** e conferir em Storage que o bucket `capas` existe e **não** é público.

### Como testar

1. ✅ `dotnet build` do `cranko-hub.slnx` (Windows e Linux) sem erros.
2. **RLS:** ✅ chamadas sem login não leem nem gravam nada (testado por `curl`). Falta: com duas contas, a conta B não lê nem altera dados da conta A.
3. **Migração:** logar com os dados de exemplo e conferir no painel as contagens contra a página Conta (`Pages/MinhaConta.razor`).
4. **Dois PCs (ou duas pastas de teste):** criar, editar e excluir em A e ver em B sem reiniciar.
5. **Conflito:** editar registros diferentes em A e B ao mesmo tempo; as duas mudanças ficam.
6. **Offline:** editar sem internet, reconectar e confirmar o envio.
7. **Sem conta:** o app abre na tela de login (item 10).
8. **Sessão:** reiniciar mantém o login; sair volta para a tela de login, sem misturar contas.
9. **Capas:** ✅ redução testada (1500×2250 → 600×900). Falta:
   - adicionar um item com capa e vê-la em `capas/<user_id>/` no Storage;
   - apagar uma imagem de `contas/<user_id>/capas/` e ver que ela volta na próxima sincronização, sem reiniciar;
   - trocar a capa de um item e excluir outro: as imagens antigas somem do bucket;
   - sem login, `curl <url>/storage/v1/object/authenticated/capas/<user_id>/<arquivo> -H "apikey: <publishable>"` é recusado;
   - no Linux, conferir que a redução funciona (biblioteca nativa do Skia).

## 10. Só com a conta: tela de login e fim do JSON local ✅ (feito)

O app é de uso pessoal e só uma conta existe no Supabase, então o modo sem conta saiu.

- **Tela de login** (`Components/TelaLogin.razor`): sem conta conectada, o `Main.razor` mostra só ela, em tela cheia. Se o servidor recusar a sessão (senha trocada, sessão revogada), ela volta sozinha. Depois de entrar, o app reinicia para abrir na pasta da conta.
- **Cache invisível:** continua o `dados.json` + `base.json` em `contas/<user_id>/`, só para abrir sem internet e enviar as mudanças depois. Sem conta, `ArmazenamentoService.Salvar` não grava nada. Um aparelho novo começa vazio e a primeira sincronização traz tudo.
- **Removido:** modo sem conta, pasta sincronizada (item 8), backup automático (`BackupService`), exportar/importar backup, dados de exemplo, "Apagar tudo" e o alerta de backup. Os diálogos de salvar/abrir backup e de escolher pasta saíram do `IDialogoService` nas três plataformas.
- **Configurações:** a aba "Dados e backup" virou **Conta** (usuário, status da sincronização, "Sincronizar agora", "Sair" e as contagens). O link `configuracoes?aba=dados` continua abrindo essa aba.
- **Arquivos antigos:** o `dados.json` do modo local (na raiz de `%LocalAppData%\CrankoHub`) e os backups ficam no disco, só deixam de ser usados. Campos removidos (`BackupAutomatico`, `PastaBackup`, `BackupsMantidos`, `UltimoBackup`, `ConfigLocal.PastaDados`) são ignorados ao abrir.
- **Fica no aparelho:** `local.json` (conta conectada e iniciar com o Windows) e as preferências que não sobem para o `perfis` (chaves TMDB/RAWG, bandeja, avisos).

## 12. Agenda ✅ (feito em 06/11/2026; falta testar no aparelho)

Calendário estilo Google Agenda, só do CrankoHub (dados no Supabase e sincronizados como os outros módulos, sem login do Google).

- **Página** `Pages/Agenda.razor` (`/agenda`, módulo `Modulo.Agenda`, cor `#30b0c7`):
  - visões **Dia**, **Semana** (começa no domingo) e **Mês**, com Hoje, ◀ ▶ e título do período; a visão fica guardada no aparelho (`Agenda.Visao`). No celular, abre no Dia;
  - lateral com **minicalendário** (ponto nos dias com evento) e **Minhas agendas** (caixa colorida para mostrar/ocultar, editar nome e cor, excluir com os eventos) + "Tarefas com prazo";
  - grade de 24 h (48 px por hora) que abre perto da hora atual (ou às 7h), linha vermelha do "agora" e eventos que se sobrepõem lado a lado (`AgendaCalculos.Blocos`);
  - faixa de **dia inteiro** com os eventos de dia inteiro (e os de 24 h ou mais) e as **tarefas com prazo** (check conclui; o clique abre `tarefas?quadro=X&card=Y`, que abre o editor do card);
  - no Mês, até 3 itens por dia e "+N mais"; clicar no número abre o dia.
- **Criar e mudar:** clique num horário vazio (meia em meia hora) ou na faixa de dia inteiro; **arrastar** move (15 em 15 min, também para outro dia) e a **alça de baixo** muda a duração (`wwwroot/js/agenda.js`, módulo ES importado pelo `Components/AgendaInterop.cs`, seguindo as skills do dotnet/skills). No toque, o arrasto começa depois de segurar o evento.
- **Editor:** título, agenda, dia inteiro, início/fim (mudar o início leva o fim junto), repetição (todo dia / toda semana em dias escolhidos / todo mês / todo ano, a cada N, até uma data), lembrete, local e descrição.
- **Repetição** (`AgendaCalculos.Ocorrencias`/`Datas`): as ocorrências são calculadas na hora (nada é gerado no banco). Num evento que repete, editar, excluir ou arrastar pergunta **"Só este evento"** (vira um evento avulso e a data entra em `excecoes`) ou **"Todos os eventos"**. Mensal no dia 31 pula os meses curtos e anual em 29/02 só acontece em ano bissexto, como no Google.
- **Lembretes:** `AvisosService` confere a cada 30 s e notifica uma vez por ocorrência (chave `agenda:<id>:<horário>` em `AvisosEnviados`, guardada com a data da ocorrência). No dia inteiro, o lembrete conta a partir das 9h. Ainda avisa até 15 min depois do início, se o app abriu atrasado. Clicar abre `agenda?dia=...&visao=dia`.
- **Início:** card "Agenda de hoje" (o que ainda vem hoje e os totais de hoje, amanhã e 7 dias), ao lado do de Tarefas. **Conta:** contagem de eventos.
- **Dados:** `Models/Agenda.cs` (`DadosAgenda`, `AgendaCor`, `EventoAgenda`) em `EstadoApp.Agenda`. A agenda criada sozinha tem o **id fixo `principal`** ("Pessoal"), para dois aparelhos não criarem duas. Horários no relógio local, sem fuso (`timestamp` no banco).
- **Supabase:** `supabase/migrations/0003_agenda.sql` (tabelas `agendas` e `eventos`, mesmo padrão do 0001). Enquanto a migration não roda, a sincronização deixa essas duas tabelas de fora e avisa no card da Conta (`SincronizacaoService.VerificarTabelasNovas`).

**Ajustes depois do primeiro uso (06/11/2026)**
- **Horário de entrega nas tarefas:** `CardTarefa.PrazoHora` (opcional, ao lado do Prazo no editor do card). Na Agenda, a tarefa com horário entra na **grade de horas** (bloco tracejado de 30 min, lado a lado com os eventos, check para concluir, fica vermelho quando passa da hora; não arrasta) e a sem horário continua na faixa de dia inteiro. O badge vira "hoje às 14:00" / "venceu às 14:00" em Tarefas, no Início e nos alertas.
- **Filtro por quadro:** em "Tarefas com prazo", na lateral, cada quadro de Tarefas tem a sua caixa para mostrar/ocultar (`DadosAgenda.QuadrosOcultos`, só deste aparelho como as agendas ocultas). "Todos os quadros" liga/desliga tudo.
- **Supabase:** `supabase/migrations/0004_tarefas_hora.sql` (coluna `cards.prazo_hora`). Enquanto não roda, a sincronização não manda nem recebe essa coluna (`ColunasNovas` em `SincronizacaoService`) e o card da Conta avisa qual migration falta.
- `0003_agenda.sql`: o `enable row level security` saiu do bloco `do $$` e foi para depois de cada `create table`, porque o SQL Editor do Supabase avisava (falso alarme) que as tabelas ficariam sem RLS.

**Como testar**
1. ✅ `dotnet build cranko-hub.slnx` (Windows, Linux e Android) sem erros nem avisos.
2. ✅ Regras (script fora do projeto): semanal seg/qua/sex até uma data, exceção, "só este" + arrasto, mensal no dia 31, anual em 29/02, diária a cada 2 dias desde 2020, sobreposição em colunas, janela do lembrete, tarefa com horário na grade e badge "hoje às"/"venceu às".
3. ✅ `0003_agenda.sql` rodado no Supabase. ⏳ Rodar o `0004_tarefas_hora.sql` e conferir que um evento e uma tarefa com horário criados no PC aparecem no celular.
4. ⏳ No app: criar clicando na grade, arrastar e redimensionar, repetição com "só este"/"todos", agendas e cores, tarefa no dia do prazo, lembrete de 5 min, visão Mês, tema escuro e celular.

## 15. Anexos nas tarefas e na manutenção ✅ (feito em 11/11/2026; falta rodar as migrations e testar entre aparelhos)

- No editor da tarefa, seção **Anexos**: **Adicionar arquivo** (qualquer tipo, vários de uma vez, até **20 MB** cada), **Abrir** (no programa padrão do sistema) e remover. No card do quadro aparece o badge **📎 N**.
- **Compactação ao anexar** (`Services/AnexosService.cs`), para ocupar o mínimo no aparelho e no servidor:
  - **Imagem** (jpg, png, webp, bmp, gif estático): lado maior de até **1920 px**, em **WebP qualidade 80**, com a rotação do EXIF aplicada. Só troca se ficar menor. Ao abrir, sai como `nome.webp`.
  - **Outros arquivos**: **Brotli nível 11** (janela 24), sem perda. Antes, uma amostra de 1 MB em nível 5 mostra se vale a pena: arquivos que já vêm compactados (zip, mp4, docx...) ficam como estão, sem esperar o nível 11 à toa. Só usa o Brotli se economizar pelo menos 3%.
  - O tamanho aparece como "original → guardado" (ex.: "3,2 MB → 240 KB").
- **Onde fica:** o arquivo compactado fica na pasta `anexos/` da conta e no bucket privado **`anexos`** do Storage (`<user_id>/<arquivo>`). A linha do card guarda só os dados (coluna `anexos jsonb`: nome, tipo, tamanhos, arquivo, formato).
- **Sincronização** (`SincronizacaoService.Anexos`): quem anexou envia o arquivo; os outros aparelhos **só baixam ao abrir** e depois guardam. Um anexo que nenhum card usa mais sai do bucket e da pasta local. Arquivos de um formulário cancelado são apagados na hora.
- **Manutenção do Carro:** no formulário do serviço, **Orçamento e nota fiscal**: anexar **PDF ou foto** (jpg, png, webp), com um seletor por arquivo (**Orçamento**, **Nota fiscal** ou Outro; os novos já vêm como Nota fiscal). No histórico, cada anexo vira um botãozinho na coluna "O que foi feito" que abre o arquivo direto. Excluir o serviço (ou o veículo) apaga os arquivos.
- **Código compartilhado:** `Components/CampoAnexos.razor` (lista, adicionar, abrir, remover, categoria) e `Services/RascunhoAnexos.cs` (anexos do formulário até salvar ou cancelar), usados pelas Tarefas e pela Manutenção. O modelo é `Models/Anexo.cs` (`Anexo`, com `Categoria` opcional).
- **Migrations:** `supabase/migrations/0009_tarefas_anexos.sql` (coluna nos cards + bucket de 20 MB + políticas só do dono) e `0010_manutencao_anexos.sql` (coluna `anexos` em `manutencoes`, mesmo bucket). Enquanto não rodarem, os anexos ficam só no aparelho e o card da Conta avisa.

**Como testar**
1. ✅ `dotnet build` no Windows, no Linux e no Android, sem erros nem avisos.
2. ✅ Compactação (script fora do projeto): um texto de 2,4 MB vira 68 KB e volta igual; dados aleatórios (como um zip) ficam "original" em 8 ms; um PNG 4000×3000 vira WebP 1920×1440; um JPG de 590 KB vira 76 KB; as 7 orientações do EXIF saem certas.
3. ⏳ Rodar a `0009_tarefas_anexos.sql` e a `0010_manutencao_anexos.sql` no Supabase.
4. ⏳ No app: na manutenção, anexar o PDF do orçamento e a foto da nota fiscal, marcar cada um e abrir pelo histórico; nas tarefas, anexar foto do celular, PDF e zip; abrir cada um; cancelar o formulário (os arquivos somem de `anexos/`); depois de sincronizar, ver no bucket; abrir pelo celular (baixa na primeira vez e depois abre sem internet).

## 16. Marcador de combustível no abastecimento ✅ (feito em 12/11/2026; falta rodar a migration e testar no aparelho)

Para carro sem computador de bordo e abastecimento sem encher o tanque, o consumo sai do **ponteiro do painel**.

- **Formulário do abastecimento:** "Litros" virou **Litros colocados** (o que a bomba marcou, base do R$/L). Novo campo **Litros gastos desde o anterior** (opcional: vazio = igual aos colocados, como antes). Ao lado ficam o botão **⛽ Marcador** e o interruptor **Enchi o tanque**.
- **Marcador** (`Components/MarcadorCombustivel.razor`): janela com um mostrador igual ao do painel (arco com 1/1, 1/2 e reserva). O ponteiro é marcado **antes de abastecer**, em **oitavos**: tocar/arrastar, −/+ ou setas do teclado. Feito todo em Razor/SVG, sem JS.
- **Conta** (`CarroCalculos.NivelDepois`/`GastoPeloMarcador`): nível depois do anterior = tanque cheio se marcou "Enchi o tanque", senão ponteiro do anterior + litros colocados (no máximo o tanque); **gasto = nível depois do anterior − ponteiro de agora**. Ex.: tanque 44 L, anterior em 1/4 + 30 L → 41 L; hoje em 1/2 (22 L) → gasto ≈ 19 L.
- **Tamanho do tanque** (`FichaVeiculo.TanqueLitros`): se o veículo não tem, o marcador pede na hora e salva na ficha.
- Sem base (primeiro abastecimento, ou o anterior sem ponteiro e sem tanque cheio), ou com o ponteiro acima do esperado ou mais de 60 km/l: avisa e guarda **só o ponteiro**, que vira a base do próximo.
- **Estimados:** selo **"≈ estimado"** no formulário e "≈" no km/l do histórico (o tooltip mostra os litros gastos). Contam normalmente na média, no melhor e no pior. A média passou a usar os litros gastos (`Abastecimento.Consumidos`); preço médio e R$/L continuam com os colocados.
- **Modelo/sync:** `Abastecimento` ganhou `LitrosGastos`, `NivelAntes`, `TanqueCheio` e `GastoEstimado`. A migration é `supabase/migrations/0011_abastecimento_marcador.sql`. Enquanto não rodar, esses campos ficam só no aparelho e o card da Conta avisa.

**Como testar**
1. ✅ `dotnet build`.
2. ⏳ Rodar a `0011_abastecimento_marcador.sql` no Supabase.
3. ⏳ No app: veículo sem tanque → o marcador pede e salva na ficha; abastecimento A com ponteiro 1/4 + 30 L (só guarda o ponteiro); B com 250 km e ponteiro 1/2 → ≈ 19 L e 13,2 km/l com selo; anterior com "Enchi o tanque" → base cheia; ponteiro acima do esperado → aviso; editar mantém os campos; marcar no celular arrastando o ponteiro.
