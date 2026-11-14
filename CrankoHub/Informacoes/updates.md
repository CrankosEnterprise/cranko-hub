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

## 11. Modo Carro (CarLink) 🚧 (3ª versão: barra da música por cima do Maps/Waze; falta testar no carro)

O CrankoHub não tem mais mapa nem rota próprios: a navegação fica com o **Google Maps** ou o **Waze**, e o CrankoHub põe por cima deles uma **barra fixa com a música** (capa, nome, artista, ⏮ ⏯ ⏭). Fica num botão **"Modo Carro"** na página Carro, que só aparece no Android. A central é a **Pioneer AVH-X2550BT**. Origem: `Desktop\Meus Projetos\Projeto_Multimidia\PLANO_CarLink.md`.

### Por que mudou (14/11/2026)
- O mapa e as rotas próprios (MapLibre/OpenStreetMap, Valhalla, radares do Overpass, Google Maps opcional com chave e cobrança) davam umas 3 mil linhas para manter e competiam com o Maps e o Waze, que já fazem isso melhor e de graça. Saíram o `RotaService`, o `GoogleMaps`, o `RadaresOsm`, os JS do mapa (e o MapLibre de `wwwroot/lib`), os layouts da tela, a voz, a chave do Google e o **visor da central** (`VisorCarroService`, que só existia para mostrar a nossa rota no rádio; sem ela, o rádio volta a mostrar a música pelo próprio Spotify).

### Limitações da central (pesquisado)
- A **USB não recebe vídeo nem toque**: só lê pendrive/iPod e carrega o celular. Nenhum app consegue "projetar a tela" por ela.
- O **Bluetooth** (HFP + A2DP + AVRCP) é o canal que funciona: leva o áudio (música e voz do GPS), mostra **texto** no display (título/artista pelo AVRCP) e devolve os **botões** da central (próxima/anterior/play) para o celular.

### Como funciona
- **Tela rápida** (`Pages/Carro/ModoCarro.razor`, `/carro/modo`, `Layout/LayoutCarro.razor`): escura, botões grandes. Escolhe **Google Maps** ou **Waze** (o último fica marcado, `Config.AppNavegacaoCarro`), busca **"Para onde?"** pelo Photon (sem chave, `Services/Carro/ModoCarro/BuscaLugares.cs`, preferindo lugares perto pela última posição do GPS) e os 6 **recentes** (`Config.DestinosRecentes`). Ao escolher, abre o app **já navegando** (`google.navigation:q=lat,lng` / `waze://?ll=lat,lng&navigate=yes`); "Abrir sem destino" só abre o app. Mostra o que falta liberar (sobrepor e acesso às notificações).
- **Barra da música** (`Android/Plataforma/BarraCarroService.cs`): serviço em primeiro plano (tipo `specialUse`, mais `location` com a permissão dada) com a notificação **"Modo Carro ativo"** e o botão **Sair do Modo Carro**. Desenha uma `View` nativa numa janela `TYPE_APPLICATION_OVERLAY` sem foco (os toques fora dela continuam no Maps/Waze), em cima ou embaixo (engrenagem do Carro, `Config.BarraCarroEmCima`), com até 560 dp de largura. Some enquanto o CrankoHub está na tela (`MainActivity` → `ModoCarroService.AppVisivel`). Tocar na capa ou no nome volta para a tela rápida.
- **Música de qualquer player** (`Android/Plataforma/ModoCarroService.cs`): `MediaSessionManager.GetActiveSessions` + `OnActiveSessionsChangedListener`, que só funcionam com o **acesso às notificações** liberado (`OuvinteMusica`, um `NotificationListenerService` vazio). Mostra o player que está tocando (senão o último); capa pelo `MetadataKeyAlbumArt`; os botões usam os `TransportControls` da sessão.
  - **Atenção:** no 1º teste (04/11/2026, POCO F3, Android 13) o Android barrou o acesso às notificações por ser APK de fora da Play Store ("configuração restrita"). Caminho: Informações do app → ⋮ → **Permitir configurações restritas** (o texto da permissão explica). Sem o acesso, a barra usa a **reserva do Spotify**: a "Transmissão do status do dispositivo" (`com.spotify.music.metadatachanged`), com a capa pelo oEmbed público; os botões viram teclas de mídia.
- **Bluetooth do carro** (`Android/Plataforma/CarroBluetoothService.cs`, sem mudanças grandes): ao ligar o carro, notificação "Modo Carro pronto" ou abre a tela rápida; ao desligar, guarda **onde estacionou** (o GPS é liberado pelo serviço da barra, com o tipo `location`) e depois sai do Modo Carro (`Config.SairAoDesconectar`).
- **Onde ficam as opções:** na **engrenagem da página Carro** (`Components/Carro/ConfigModoCarro.razor`: posição da barra, Bluetooth do carro, onde estacionei). As **permissões** continuam em Configurações → Modo Carro.

**Como testar**
1. ✅ `dotnet build` dos três projetos sem erros nem avisos.
2. ⏳ No celular: liberar "Sobrepor a outros apps" e "Acesso às notificações" em Configurações → Modo Carro; Carro → Modo Carro → escolher um destino no Waze → o Waze abre navegando e a barra aparece; trocar Spotify ↔ YouTube Music (capa e nome mudam, ⏮ ⏯ ⏭ funcionam); tocar no mapa do Waze passa normalmente; mudar Em cima/Embaixo na engrenagem; "Sair do Modo Carro" na notificação tira a barra.
3. ⏳ Sem o acesso às notificações: a barra mostra o Spotify pela transmissão dele.
4. ⏳ No carro: ligar (notificação/abrir), desligar (barra some, "Carro estacionado" com o endereço).
5. `adb logcat -s CrankoHub.Carro` mostra os erros da barra, das sessões de mídia e da capa.

### Mais para frente
- **Próxima manobra na barra** (ou no visor da central): ler a notificação de navegação do Maps/Waze com o mesmo acesso às notificações. Depende do formato das notificações deles.
- **Favoritos:** Casa e Trabalho na tela rápida.
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
- **Anexos nas tarefas e na manutenção (item 15):** rodar `0009_tarefas_anexos.sql` e `0010_manutencao_anexos.sql` no Supabase e testar entre aparelhos (anexar no PC, abrir no celular; remover e conferir que some do bucket `anexos/<user_id>/`).
- **Marcador de combustível (item 16):** rodar `0011_abastecimento_marcador.sql` no Supabase e testar o marcador no celular (arrastar o ponteiro com o dedo).
- **Episódios no Entretenimento (item 17):** rodar `0012_entretenimento_episodios.sql` no Supabase e testar os nomes dos animes pelo Jikan (MyAnimeList), que não respondeu da rede usada no desenvolvimento.
