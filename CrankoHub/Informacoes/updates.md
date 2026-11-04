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

## 11. Modo Carro (CarLink) 🚧 (segunda versão implementada: mapa próprio + Spotify; falta testar no celular)

Um "Android Auto" caseiro na tela do celular (no suporte do carro), dentro do app Android: **mapa integrado** seguindo o carro, o **Spotify** e atalhos. A central é a **Pioneer AVH-X2550BT**. Fica num botão **"Modo carro"** na página Carro, que só aparece no Android. Origem: `Desktop\Meus Projetos\Projeto_Multimidia\PLANO_CarLink.md`.

### Limitações da central (pesquisado)
- A **USB não recebe vídeo nem toque**: só lê pendrive/iPod e carrega o celular. Nenhum app consegue "projetar a tela" por ela.
- O **Bluetooth** (HFP + A2DP + AVRCP) é o canal que funciona: leva o áudio (música e voz do GPS), mostra **texto** no display (título/artista pelo AVRCP) e devolve os **botões** da central (próxima/anterior/play) para o celular.

### O que mudou depois do primeiro teste (04/11/2026)
- A primeira leva lia a próxima curva e o player pelas **notificações** do Waze/Maps (`NotificationListenerService` + `MediaSessionManager`). No celular, o Android trata o APK (fora da Play Store) como "não seguro" e **não deixa liberar o acesso às notificações**, então nada aparecia. Tudo isso saiu (listener, parser, cartão de permissão, `MediaSession`).
- **Mapa do Google dentro do app** foi avaliado: a navegação curva a curva do Google (Navigation SDK) só sai por contrato, o Waze não pode ser embutido e o mapa do Google pede projeto no Google Cloud com cartão. Decisão: **mapa próprio** com OpenStreetMap.

### Segunda versão ✅ (implementada em 04/11/2026)
- **Tela** (`Pages/ModoCarro.razor`, `/carro/modo`, `Layout/LayoutCarro.razor`): sempre escura, tela ligada e paisagem. Em paisagem, o mapa à esquerda e uma coluna à direita com hora/Sair, o player e os botões **Spotify, Maps e Waze** (só esses três). Em retrato, o mapa em cima.
- **Mapa:** MapLibre GL JS 4.7.1 dentro do app (`wwwroot/lib/maplibre-gl/`, carregado só nessa página por `crankohub.carregarScript`/`carregarCss`) com o estilo escuro do **OpenFreeMap** (`tiles.openfreemap.org/styles/dark`, grátis e sem chave). `wwwroot/js/modo-carro.js` (`window.modoCarro`): segue o carro com o carro no terço de baixo, inclinação de 45° e o mapa girando com a direção acima de 7 km/h; arrastar para de seguir e mostra **Recentralizar**; velocímetro no canto.
- **GPS:** `Geolocation.StartListeningForegroundAsync` (1 s, precisão máxima) no `ModoCarroService` do Android → `Posicao` → a página repassa ao mapa. Permissão comum de localização (`ACCESS_FINE_LOCATION`/`ACCESS_COARSE_LOCATION`), sem bloqueio para app fora da Play Store. Sem permissão, o mapa mostra "Permitir localização".
- **Spotify:** `BroadcastReceiver` dinâmico para `com.spotify.music.metadatachanged`/`playbackstatechanged` (exige ligar **"Transmissão do status do dispositivo"** nas configurações do Spotify). Capa pelo oEmbed público (`open.spotify.com/oembed`, sem login), com cache por faixa. Botões anterior/tocar/próxima pelas teclas de mídia (`AudioManager.DispatchMediaKeyEvent`).
- **Windows/Linux:** `Services/ModoCarroService.cs` vazio (`Disponivel = false`): o botão não aparece e `/carro/modo` só explica que é do celular.

**Como testar**
1. ✅ `dotnet build` dos três projetos sem erros nem avisos.
2. No celular (APK de debug via `adb`): aceitar a localização; o mapa escuro aparece e segue o carro andando (gira com a direção, velocidade certa); arrastar mostra Recentralizar; com a transmissão ligada no Spotify, trocar de música atualiza título, artista e capa; botões do player; Spotify/Maps/Waze abrem; Sair volta a orientação e desliga o GPS.
3. `adb logcat -s CrankoHub.Carro` mostra cada transmissão recebida do Spotify.

### Próxima etapa: rota dentro do app
- **Busca de destino:** autocompletar do Photon (komoot), com a posição atual como referência.
- **Rota:** OSRM (`router.project-osrm.org/route/v1/driving/...?steps=true&geometries=geojson`), linha desenhada no mapa.
- **Próxima curva em pt-BR:** montada de `maneuver.type`/`modifier` + nome da rua; distância pelo GPS; avança ao passar da manobra; recalcula se sair da rota.
- **Favoritos:** Casa e Trabalho.

### Mais para frente
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
