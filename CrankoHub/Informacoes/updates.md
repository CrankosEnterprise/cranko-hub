# CrankoHub — updates

O que ainda vamos fazer no CrankoHub: itens em andamento e os que ainda não começaram. Cada item tem detalhes suficientes para começar sem precisar redescobrir o projeto. Os itens prontos ficam em [finalizados.md](finalizados.md); a referência rápida do projeto está no [README](../README.md).

## 1. Treinos

- Registro de academia: exercício, carga, séries e repetições, por dia.
- Gráfico de evolução da carga por exercício e volume semanal. Conversa com a Dieta (peso × força).

## 2. Hábitos

- Checklist diário (água, leitura, treino, dormir cedo...), com sequência de dias e um mapa de calor do ano.
- Alerta à noite se o checklist do dia não foi preenchido.

## 3. Saúde

- Remédios com horário (notificação pela bandeja), consultas e exames com data.
- Resultados de exames ao longo do tempo (ex.: colesterol, glicemia) em gráfico de linha.

## 11. Modo Carro (CarLink) 🚧 (primeira leva implementada; falta testar no celular)

Um "Android Auto" caseiro para a central **Pioneer AVH-X2550BT**, dentro do app Android: navegação (Waze/Maps), player (Spotify ou outro) e a próxima curva, numa tela grande para o celular no suporte do carro. Fica num botão **"Modo carro"** na página Carro, que só aparece no Android. Origem: `Desktop\Meus Projetos\Projeto_Multimidia\PLANO_CarLink.md`.

### Limitações da central (pesquisado)
- A **USB não recebe vídeo nem toque**: só lê pendrive/iPod e carrega o celular. Nenhum app consegue "projetar a tela" por ela.
- O **Bluetooth** (HFP + A2DP + AVRCP) é o canal que funciona: leva o áudio (música e voz do GPS), mostra **texto** no display (título/artista pelo AVRCP) e devolve os **botões** da central (próxima/anterior/play) para o celular.

### Primeira leva ✅ (implementada em 04/11/2026)

**Compartilhado (Windows, Linux e Android)**
- `Models/ModoCarro.cs`: `InstrucaoNavegacao` (texto, distância, app) e `MusicaAtual` (título, artista, capa em `data:` URI, tocando, app).
- `Services/NavegacaoNotificacao.cs`: parser puro do título/texto das notificações do Maps (`com.google.android.apps.maps`) e do Waze (`com.waze`). Ex.: "200 m" + "Vire à direita na R. X" → distância + instrução.
- `Services/ModoCarroService.cs`: versão vazia para Windows/Linux (`Disponivel = false`), com a mesma API da versão Android: `Instrucao`, `Musica`, `Mudou`, `TemAcessoNotificacoes`, `PedirAcessoNotificacoes()`, `Navegar(destino, app)`, `AbrirApp(app)`, `TocarPausar()`, `Proxima()`, `Anterior()`, `Entrar()`, `Sair()`. O Android exclui esse arquivo no `.csproj` e usa o dele (mesmo padrão do `BandejaService`).
- `Models/Configuracoes.cs`: favoritos `CasaEndereco` e `TrabalhoEndereco` (ficam no aparelho).
- `Layout/LayoutCarro.razor`: layout em tela cheia, sem a barra lateral.
- `Pages/ModoCarro.razor` (`/carro/modo`): tema escuro, paisagem, botões grandes:
  - **Navegação:** instrução atual e distância em letras grandes ("Sem navegação ativa" quando não há rota).
  - **Player:** capa, título, artista, anterior / tocar-pausar / próxima.
  - **Atalhos:** Casa, Trabalho (Waze ou Maps), abrir Waze, Maps e Spotify, e um campo "Ir para…". Casa/Trabalho editados com o `DialogoTexto`.
  - **Barra de status:** hora e Sair.
  - **Cartão de permissão** quando falta o acesso às notificações, com o passo do Android 13+: *Info do app → ⋮ → Permitir configurações restritas* (vale para apps instalados fora da Play Store) e um botão que abre a tela do sistema.
- `Pages/Carro.razor`: botão "Modo carro" no topo, só quando `ModoCarro.Disponivel`.
- `wwwroot/css/app.css`: seção `.modo-carro` (grade em paisagem; uma coluna em retrato).
- Registrar o `ModoCarroService` no `App.xaml.cs`, `Linux/Program.cs` e `Android/MauiProgram.cs`.

**Android**
- `Android/Plataforma/ModoCarroService.cs` (`Disponivel = true`):
  - Navegar: intents `google.navigation:q=…` (Maps) e `https://waze.com/ul?q=…&navigate=yes` (Waze); app não instalado → toast.
  - Abrir app: `PackageManager.GetLaunchIntentForPackage`.
  - Player: `MediaSessionManager.GetActiveSessions(componente do listener)` + `AddOnActiveSessionsChangedListener` + `MediaController.Callback` (metadados, estado, capa); comandos pelos `TransportControls`. Não precisa do SDK do Spotify.
  - Acesso às notificações: `NotificationManagerCompat.GetEnabledListenerPackages`; pedir abre `Settings.ActionNotificationListenerSettings`.
  - `Entrar()`/`Sair()`: tela sempre ligada (`KeepScreenOn`) e orientação em paisagem; ao sair, volta ao normal.
- `Android/Platforms/Android/NavNotificationListener.cs`: `NotificationListenerService` (`BIND_NOTIFICATION_LISTENER_SERVICE`) que filtra Maps/Waze, passa pelo parser e atualiza o serviço; ao conectar, liga o controle de mídia.
- `AndroidManifest.xml`: bloco `<queries>` com `com.waze`, `com.google.android.apps.maps` e `com.spotify.music` (visibilidade de pacotes no Android 11+).

**Além do plano:** `AbrirInfoDoApp()` (botão "Info do app" no cartão de permissão, para o passo das configurações restritas), `Config.AppNavegacao` (escolha Waze/Maps dos atalhos, padrão Waze), ícones novos em `Components/Icones.cs` e, sem acesso às sessões de mídia, os botões do player mandam as teclas de mídia para o player ativo (sem capa/título).

**Como testar**
1. ✅ `dotnet build` dos três projetos sem erros nem avisos; no Windows/Linux o botão "Modo carro" não aparece (`Disponivel = false`) e `/carro/modo` só explica que é do celular.
2. 🚧 Parser conferido com 13 exemplos de formatos prováveis (distância no título, no texto, no começo e no fim; notificações que não são de rota ficam de fora). Falta conferir com textos **reais**: o listener grava cada notificação do Maps/Waze no log (`adb logcat -s CrankoHub.Carro`).
3. No celular, pelo cabo: APK de debug com `-p:EmbedAssembliesIntoApk=true` via `adb`; liberar as configurações restritas e o acesso às notificações; abrir o Modo Carro (paisagem, tela ligada); tocar Spotify (capa/título e botões); iniciar rota no Waze e no Maps (cartão mostra a próxima curva, conferir também no `adb logcat`); atalhos Casa/Trabalho/"Ir para…"; Sair volta a orientação.

### Segunda leva (mais para frente)
- **Display da Pioneer (AVRCP):** uma `MediaSession` própria do CrankoHub com metadados alternando entre a próxima curva (título) e a música (artista); os botões da central chegam nela e são repassados ao player real. *Risco:* a central mostra os metadados da sessão de áudio ativa; validar no carro se a sessão do app "ganha" sem cortar o áudio do Spotify. Plano B: falar a instrução por voz (TTS) pelo Bluetooth.
- **Foreground service:** o Android não fecha o app durante a viagem.
- **Abrir sozinho** quando o Bluetooth da Pioneer conectar (`BroadcastReceiver` de `ACTION_ACL_CONNECTED`, filtrando o nome/MAC da central).
- **Fase 2, imagem e toque na tela da central (pesquisa):**
  - Vídeo: o dashboard numa tela secundária (`Presentation` API) → HDMI → adaptador Pioneer CD-AH200 → entrada RGB (modo AppRadio). Exige um celular com saída de vídeo (Motorola/Xiaomi em geral não têm; serviria um Android barato com HDMI como unidade dedicada).
  - Toque: no modo AppRadio volta ao celular pelo Bluetooth SPP com o protocolo da Pioneer (ver o projeto "AppRadio Unchained"); como vai para o nosso app, não precisa de root.
  - Saída mais simples: entrada AV (vídeo composto) com conversor HDMI→AV, sem toque (os botões da central fazem o papel).

## Pendências dos itens finalizados

Sobras dos itens que já estão em [finalizados.md](finalizados.md).

- **Linux (item 6):** ícone na bandeja (AppIndicator), se fizer falta; os textos "Windows" em Configurações aparecem também no Linux.
- **Atualização (item 7):** item "Verificar atualizações" no menu da bandeja (`BandejaService`), como no ai-usagebar.
- **Supabase (item 9), testes que faltam:**
  - RLS com duas contas: a conta B não lê nem altera dados da conta A.
  - Capas no Storage: item novo com capa aparece em `capas/<user_id>/`; imagem apagada de `contas/<user_id>/capas/` volta na próxima sincronização; capa trocada ou item excluído some do bucket; sem login, o download pelo `curl` é recusado; redução funcionando no Linux.
- **Supabase (item 9), antes de abrir o login ao público:** cadastro (religar "Allow new users to sign up" e criar a tela, com e-mail de verdade), confirmação de e-mail com SMTP próprio e redefinição de senha, LGPD (política de privacidade, exportação e excluir conta por Edge Function), revisão de RLS no Security Advisor e conferência dos limites do plano gratuito.
