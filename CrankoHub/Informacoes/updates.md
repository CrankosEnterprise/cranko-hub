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

## 11. Modo Carro (CarLink) 🚧 (mapa, Spotify, rotas com pedágios e limites funcionando no celular parado; falta testar dirigindo e os radares)

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
- **Testado no celular (Poco F3, Android 13, APK de debug via `adb`):** o mapa ficava preto porque o CSS do MapLibre põe `position: relative` no elemento do mapa (anula o `inset: 0`, altura 0): agora com `width/height: 100%`. Cores do estilo escuro trocadas por camada (`realcar()` em `modo-carro.js`: ruas cinza, avenidas claras, água azulada, nomes legíveis), créditos recolhidos no "i" e tela cheia (barras do Android escondidas com `WindowInsetsControllerCompat`, voltam ao sair).

### Rota dentro do app ✅ (implementada e testada parada em 04/11/2026)
- **`Services/RotaService.cs`** (compartilhado, registrado nos três projetos):
  - **Busca** pelo Photon (komoot, OpenStreetMap), num quadrado de ~300 km em volta da posição, com duas consultas juntas e intercaladas: "perto de você" (a Avenida Brasil do bairro) e "mais relevante" (a Avenida Paulista da capital, que a preferência por perto escondia). Trechos da mesma rua no mesmo bairro aparecem uma vez só. Com menos de 3 resultados, amplia filtrando só o Brasil. A distância em linha reta aparece na frente de cada resultado.
  - **Rotas pelo Valhalla** (servidor público da FOSSGIS, `valhalla1.openstreetmap.de/route`, POST JSON): até 3 opções (`alternates`), instruções já em pt-BR (`language`), "evitar pedágios" (`exclude_tolls`) e o `heading` da posição para não mandar fazer retorno. A linha vem codificada (polyline 6) e cada manobra entra pelo `begin_shape_index`. O OSRM saiu: devolvia só uma opção, não marcava pedágio e recusava "evitar pedágio".
  - **Pedágios e limites** de cada opção pelo `trace_attributes` do próprio Valhalla (`edge_walk` na linha): nós `toll_booth` viram praças (cabines a menos de 500 m contam uma vez) e `edge.speed_limit` vira trechos com limite (`TrechoLimite`). Ex.: Piracicaba → Av. Paulista pela Bandeirantes = 3 praças; limites de 50/60 nas avenidas e 80 a 120 na rodovia.
  - **Radares** (`Services/RadaresOsm.cs`): nós `highway=speed_camera` do OpenStreetMap pelo Overpass, em caixas de ~40 km ao longo das opções, tentando 4 servidores (12 s cada) em segundo plano; o que chega fica em `radares.json` no aparelho e é usado quando nenhum servidor responde. Radar a até 25 m da linha entra na rota, com a velocidade (`maxspeed`) quando o mapa informa.
  - **Andamento** a cada posição do GPS (e na hora de iniciar/recalcular, com a última posição, porque parado o GPS não manda posição nova): próxima manobra, metros até ela, km e tempo restantes, horário de chegada, limite do trecho, próximo radar e próximo pedágio. Fora da linha por mais de 60 m em 3 leituras → recalcula pelo Valhalla (no máximo a cada 15 s), reaproveitando os radares já conhecidos.
  - **Voz opcional** (`Config.VozModoCarro`, desligada por padrão; em Configurações → Aplicativo → Modo Carro, só no Android): um aviso por vez, na ordem chegada → radar (até 500 m) → pedágio (até 1,5 km) → acima do limite (3 s com mais de 7 km/h de folga, uma vez por trecho) → manobra (ao virar a próxima, até 2 km, e uns 8 s antes). Ao iniciar: "Rota para X, 2 horas e 23 minutos. Com 3 pedágios."
  - **Recentes:** últimos 6 destinos em `Config.DestinosRecentes` (ficam no aparelho), mostrados com o campo de busca vazio.
- **Tela:**
  - **"Para onde?"** → busca por cima do mapa → **prévia** na coluna da direita (a hora e o Sair somem para caber): até 3 opções com tempo, km, chegada, "N pedágios"/"Sem pedágio" e "N radares"; a marcada em azul no mapa, as outras em cinza, tudo enquadrado; chave **Sem pedágio** (refaz as opções, fica guardada em `Config.EvitarPedagios`); **Cancelar** e **Iniciar**.
  - **Navegando:** cartão verde da manobra, linha azul, praças (âmbar) e radares (vermelho) no mapa, alertas "Radar a 300 m · 60 km/h" e "Pedágio a 800 m", **placa do limite** acima do velocímetro (que fica vermelho acima dele) e barra **chegada · tempo · km · Encerrar**.
  - O `LayoutCarro` também atende os pedidos de navegação de fora do Blazor (toque numa notificação dentro do Modo Carro).
- **Sem trânsito em tempo real e sem preço do pedágio:** o tempo vem dos limites das vias; preço só em serviços pagos (Google Routes, TollGuru).

**Como testar**
1. ✅ `dotnet build` dos três projetos sem erros nem avisos.
2. ✅ No celular, parado (Poco F3, APK de debug via `adb`): busca "Avenida Paulista" → a de Piracicaba (4,6 km) e a da capital (134 km); prévia com **2h23 · 157 km · 3 pedágios**, **2h36 · 165 km · 4 pedágios** e **2h58 · 187 km · 3 pedágios**, praças da Bandeirantes no mapa; trocar de opção; "Sem pedágio" → **4h41 · 226 km**, 4h58 e 5h02, todas sem pedágio; Iniciar → "80 m · Vire à direita para Avenida Rio das Pedras", "chegada 22:40 · 2:23 h · 157 km"; Encerrar; recentes; cartão Modo Carro em Configurações.
3. ⏳ **Radares:** o Overpass estava fora do ar (502/timeout em todos os servidores) durante o teste: falta ver os radares aparecerem.
4. Falta **dirigindo:** o mapa segue e gira; a manobra avança; a placa de limite (a rua do teste não tem limite no mapa); alertas de pedágio/radar; a voz (ligada em Configurações); sair da rota recalcula; chegada.
5. `adb logcat -s CrankoHub.Carro` mostra as transmissões do Spotify e erros de GPS/voz.

### Próximos passos
- **Favoritos:** Casa e Trabalho no "Para onde?".
- **Faixa já percorrida** apagada da linha.
- **Limite sem rota:** consultar o `trace_attributes` com as últimas posições a cada poucos segundos.

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
