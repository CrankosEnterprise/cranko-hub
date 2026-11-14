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

### Fonte dos radares (as três juntas, testado em 14/11/2026)
- **DER-SP** (rodovias estaduais, inclusive as de concessão como SP 330, SP 348 e Rodoanel): `https://dersp.der.sp.gov.br/Upload/XML/radares.xml` (~3 MB), o XML que a página "Pesquisa de Radares" do DER carrega. Tem coordenadas, `rad_vel_regul` ("100/080 Km/h" = leves/pesados) e `Orientacao` (Norte/Sul/Leste/Oeste). Entram Controlador (radar) e Redutor (lombada eletrônica) com `trc_em_operacao=true`: **1.257**. Os Portáteis e os não metrológicos ficam de fora.
- **CET-SP** (capital): `https://raw.githubusercontent.com/cetsp-dados/dados-fiscalizacao-eletronica/main/Base_Locais_Fiscalizados.csv`, do repositório oficial da CET no GitHub. Entram os ativos (sem DESATIVAÇÃO) que medem velocidade (METROLÓGICO = SIM ou "V" no ENQUADRAMENTO): **837**.
- **OpenStreetMap** (`highway=speed_camera`, Overpass): o resto do estado. O radar a menos de 60 m de um oficial sai: **1.618** de 2.982.
- Total: **3.712**. Projeto parecido que junta tudo do Brasil: [Pardal](https://github.com/murkiriel/pardal) (de terceiros, não usado).

### Como funciona
- **Tela rápida** (`Pages/Carro/ModoCarro.razor`, `/carro/modo`): escolhe **Google Maps** ou **Waze** (`Config.AppNavegacaoCarro`), busca **"Para onde?"** pelo Photon (`Services/Carro/ModoCarro/BuscaLugares.cs`) e os 6 **recentes**. Abre o app **já navegando** (`google.navigation:q=lat,lng` / `waze://?ll=lat,lng&navigate=yes`). Mostra o que falta liberar (sobrepor e localização).
- **Radares** (`Services/Carro/ModoCarro/RadaresSp.cs`): as três fontes baixadas em paralelo, cada uma com o próprio cache e a própria data (se uma falha, fica a anterior). O Overpass é consultado para o estado inteiro (`area["ISO3166-2"="BR-SP"]`), com 4 servidores de reserva e User-Agent próprio (sem ele o Overpass responde 406). Fica em `radares-sp.json` no aparelho; baixa de novo ao entrar no Modo Carro se tiver mais de 7 dias, ou no botão "Atualizar agora" da engrenagem. `Proximo(lat, lon, rumo)` devolve o mais perto até 2 km dentro de um cone de 40° do rumo do GPS. Com o `direction` do OSM, o radar tem de estar na mesma via; com o sentido do DER, sai o da pista contrária (rumo a mais de 120° do sentido).
- **Aviso** (`Android/Plataforma/RadarCarroService.cs`): serviço em primeiro plano (`specialUse` + `location`) com a notificação **"Modo Carro ativo"** e o botão **Sair**. GPS a cada 1 s / 3 m (`LocationManager`); abaixo de ~10 km/h mantém o radar que já estava e só atualiza os metros. A pílula (placa de velocidade + "Radar 350 m") é uma janela `TYPE_APPLICATION_OVERLAY` **sem foco e sem toques**, na borda esquerda (em cima, no meio ou embaixo: `Config.AvisoRadarPosicao`), visível com o radar a até `Config.AvisoRadarDistancia` (300 m a 2 km, padrão 1 km) e por `Config.AvisoRadarTempo` (até passar, ou 5/10/15/30 s). Mostra "Lombada" nos redutores do DER e fica vermelha acima do limite + 2 km/h. Ao aparecer um radar novo, toca o som `Config.AvisoRadarTom` (bipe curto, bipe duplo, alerta ou o som de notificação do celular; `SomRadar`, no volume das notificações), com o botão "Testar" na engrenagem. Some com o CrankoHub na tela.
- **Bluetooth do carro** (`Android/Plataforma/CarroBluetoothService.cs`): ao ligar o carro, notificação "Modo Carro pronto" ou abre a tela rápida; ao desligar, guarda **onde estacionou** (GPS liberado pelo serviço do aviso) e sai do Modo Carro (`Config.SairAoDesconectar`).
- **Onde ficam as opções:** na **engrenagem da página Carro** (`Components/Carro/ConfigModoCarro.razor`: aviso de radar, Bluetooth do carro, onde estacionei). As **permissões** continuam em Configurações → Modo Carro.

**Como testar**
1. ✅ `dotnet build` dos três projetos sem erros nem avisos.
2. ⏳ No celular: liberar "Sobrepor a outros apps" e "Localização"; Carro → Modo Carro → escolher um destino no Waze. Na engrenagem do Carro aparecem "3.7xx radares no estado de SP" e a contagem por fonte; "Testar" toca cada som.
3. ⏳ Dirigindo (ou com um app de GPS falso): a pílula aparece a ~1 km de um radar com a velocidade, conta os metros, fica vermelha acima do limite e some depois de passar; sem radar perto nada fica por cima do Maps/Waze.
4. ⏳ No carro: ligar (notificação/abrir), desligar ("Carro estacionado" com o endereço e o aviso some).
5. `adb logcat -s CrankoHub.Carro` mostra os erros do GPS, da pílula e do bipe.

### Mais para frente
- **Outras prefeituras** (Campinas, Santos...) com dados abertos de radares, para ter fonte oficial também fora da capital.
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
