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

## 11. Modo Carro (CarLink) 🚧 (4ª versão: aviso de radar por cima do Maps/Waze; falta testar no carro)

O CrankoHub não tem mapa nem rota próprios: a navegação fica com o **Google Maps** ou o **Waze**, e o CrankoHub põe por cima deles um **aviso pequeno do próximo radar** (velocidade e metros que faltam), que só aparece com um radar perto. Fica num botão **"Modo Carro"** na página Carro, que só aparece no Android. A central é a **Pioneer AVH-X2550BT**. Origem: `DesktopMeus ProjetosProjeto_MultimidiaPLANO_CarLink.md`.

### Por que mudou
- **14/11/2026 (3ª versão):** o mapa e as rotas próprios (MapLibre, Valhalla, Google Maps com chave, visor da central) davam umas 3 mil linhas e competiam com o Maps/Waze. Saíram; ficou uma barra da música por cima deles.
- **14/11/2026 (4ª versão):** a barra da música cobria a manobra e o trajeto do Maps/Waze e não tinha utilidade. Saiu (sessões de mídia, acesso às notificações, reserva do Spotify); a janela sobreposta agora é o **aviso de radar**.

### Fonte dos radares (pesquisado)
- **CET-SP:** só a capital; o site tem lista de endereços e um mapa, sem API pública com coordenadas (e o site não respondeu da rede usada no desenvolvimento).
- **DER-SP:** rodovias estaduais (cerca de 650 radares) por rodovia + km + velocidade, **sem lat/lon**.
- **OpenStreetMap** (`highway=speed_camera`): o estado inteiro com coordenadas. Em 14/11/2026: **2.982 radares em SP, 2.495 com `maxspeed`**, 319 com `direction` (a maioria "forward/backward", que não dá para usar sem a linha da via). É a fonte usada.

### Como funciona
- **Tela rápida** (`Pages/Carro/ModoCarro.razor`, `/carro/modo`): escolhe **Google Maps** ou **Waze** (`Config.AppNavegacaoCarro`), busca **"Para onde?"** pelo Photon (`Services/Carro/ModoCarro/BuscaLugares.cs`) e os 6 **recentes**. Abre o app **já navegando** (`google.navigation:q=lat,lng` / `waze://?ll=lat,lng&navigate=yes`). Mostra o que falta liberar (sobrepor e localização).
- **Radares** (`Services/Carro/ModoCarro/RadaresSp.cs`): uma consulta do Overpass para o estado inteiro (`area["ISO3166-2"="BR-SP"]`), com 4 servidores de reserva e User-Agent próprio (sem ele o Overpass responde 406). Fica em `radares-sp.json` no aparelho; baixa de novo ao entrar no Modo Carro se tiver mais de 7 dias, ou no botão "Atualizar agora" da engrenagem. `Proximo(lat, lon, rumo)` devolve o mais perto até 1,5 km dentro de um cone de 40° do rumo do GPS (com `direction` em graus, também na mesma via).
- **Aviso** (`Android/Plataforma/RadarCarroService.cs`): serviço em primeiro plano (`specialUse` + `location`) com a notificação **"Modo Carro ativo"** e o botão **Sair**. GPS a cada 1 s / 3 m (`LocationManager`); abaixo de ~10 km/h mantém o radar que já estava e só atualiza os metros. A pílula (placa de velocidade + "Radar 350 m") é uma janela `TYPE_APPLICATION_OVERLAY` **sem foco e sem toques**, na borda esquerda (em cima, no meio ou embaixo: `Config.AvisoRadarPosicao`), visível só com radar a até **1 km**; fica vermelha acima do limite + 2 km/h; bipe curto ao aparecer um radar novo (`Config.AvisoRadarSom`, volume das notificações). Some com o CrankoHub na tela.
- **Bluetooth do carro** (`Android/Plataforma/CarroBluetoothService.cs`): ao ligar o carro, notificação "Modo Carro pronto" ou abre a tela rápida; ao desligar, guarda **onde estacionou** (GPS liberado pelo serviço do aviso) e sai do Modo Carro (`Config.SairAoDesconectar`).
- **Onde ficam as opções:** na **engrenagem da página Carro** (`Components/Carro/ConfigModoCarro.razor`: aviso de radar, Bluetooth do carro, onde estacionei). As **permissões** continuam em Configurações → Modo Carro.

**Como testar**
1. ✅ `dotnet build` dos três projetos sem erros nem avisos.
2. ⏳ No celular: liberar "Sobrepor a outros apps" e "Localização"; Carro → Modo Carro → escolher um destino no Waze. Na engrenagem do Carro aparece "2.9xx radares no estado de SP".
3. ⏳ Dirigindo (ou com um app de GPS falso): a pílula aparece a ~1 km de um radar com a velocidade, conta os metros, fica vermelha acima do limite e some depois de passar; sem radar perto nada fica por cima do Maps/Waze.
4. ⏳ No carro: ligar (notificação/abrir), desligar ("Carro estacionado" com o endereço e o aviso some).
5. `adb logcat -s CrankoHub.Carro` mostra os erros do GPS, da pílula e do bipe.

### Mais para frente
- **CET na capital:** se aparecer o endpoint do mapa de "Locais fiscalizados" com coordenadas, juntar aos do OSM (radares que faltam no OSM).
- **Radares do DER pelo km:** cruzar rodovia + km com a linha da rodovia no OSM (`ref=SP xxx`) para preencher os que faltam.
- **Favoritos:** Casa e Trabalho na tela rápida.
- **Fase 2, imagem e toque na tela da central (pesquisa):**
  - Vídeo: o dashboard numa tela secundária (`Presentation` API) → HDMI → adaptador Pioneer CD-AH200 → entrada RGB (modo AppRadio). Exige um celular com saída de vídeo.
  - Toque: no modo AppRadio volta ao celular pelo Bluetooth SPP com o protocolo da Pioneer (ver o projeto "AppRadio Unchained").
  - Saída mais simples: entrada AV (vídeo composto) com conversor HDMI→AV, sem toque.

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
