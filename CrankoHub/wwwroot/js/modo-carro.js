'use strict';

// Mapa do Modo Carro (Pages/Carro/ModoCarro.razor): MapLibre GL + tiles do OpenFreeMap (OpenStreetMap, sem chave).
// A posição vem do GPS pelo C# (ModoCarroService.Posicao) e chega aqui em posicao(...).
window.modoCarro = (() => {
  const ESTILO = 'https://tiles.openfreemap.org/styles/dark';
  const VELOCIDADE_GIRAR = 7; // km/h: abaixo disso a direção do GPS não é confiável e o mapa não gira

  // O estilo "dark" do OpenFreeMap tem ruas quase pretas (#181818 sobre #0c0c0c): no carro não dá para ler.
  // Cores próprias por camada, no espírito do mapa noturno do Android Auto (fundo grafite, ruas cinza, vias rápidas claras).
  const CORES = {
    'background': ['background-color', '#1a1c21'],
    'landuse_residential': ['fill-color', '#1e2025'],
    'landcover_wood': ['fill-color', '#1b2620'],
    'landuse_park': ['fill-color', '#1b2620'],
    'water': ['fill-color', '#122434'],
    'waterway': ['line-color', '#122434'],
    'building': ['fill-color', '#24262c'],
    'highway_path': ['line-color', '#2c2f36'],
    'highway_minor': ['line-color', '#3b3f48'],
    'highway_major_casing': ['line-color', '#2a2d34'],
    'highway_major_inner': ['line-color', '#596070'],
    'highway_major_subtle': ['line-color', '#4a505d'],
    'highway_motorway_casing': ['line-color', '#2a2d34'],
    'highway_motorway_inner': ['line-color', '#8a93a6'],
    'highway_motorway_subtle': ['line-color', '#4a505d'],
    'railway': ['line-color', '#34373e'],
    'railway_minor': ['line-color', '#34373e'],
    'railway_transit': ['line-color', '#34373e'],
    'highway_name_other': ['text-color', '#b9bdc7'],
    'highway_name_motorway': ['text-color', '#d4d7de'],
    'water_name': ['text-color', '#6f8fab'],
  };
  const NOMES_LUGARES = ['place_other', 'place_suburb', 'place_village', 'place_town', 'place_city', 'place_city_large'];

  function realcar() {
    for (const [camada, [prop, cor]] of Object.entries(CORES)) {
      if (mapa.getLayer(camada)) mapa.setPaintProperty(camada, prop, cor);
    }
    for (const camada of [...NOMES_LUGARES, 'highway_name_other', 'highway_name_motorway']) {
      if (!mapa.getLayer(camada)) continue;
      if (NOMES_LUGARES.includes(camada)) mapa.setPaintProperty(camada, 'text-color', '#c9ccd3');
      mapa.setPaintProperty(camada, 'text-halo-color', '#14161a');
    }
  }

  let mapa = null, marcador = null, ultima = null, seguindo = true, aoMudarSeguir = null;

  function criarMarcador() {
    const el = document.createElement('div');
    el.className = 'mc-carro';
    el.innerHTML = '<svg viewBox="0 0 40 40"><path d="M20 4 33 34 20 27 7 34z"/></svg>';
    return el;
  }

  // O carro fica no terço de baixo, como nos apps de navegação (mais mapa à frente)
  function padding() {
    const h = mapa.getContainer().clientHeight;
    return { top: Math.round(h * 0.35), bottom: 0, left: 0, right: 0 };
  }

  // ===== Rotas (por baixo dos nomes das ruas) =====
  // Prévia: todas as opções (a marcada em azul, as outras em cinza) + praças de pedágio e radares. Navegando: só a rota escolhida.
  let rotas = [], escolhida = 0, pedagios = [], radares = [], navegando = false, previa = false;

  const linha = c => ({ type: 'Feature', properties: {}, geometry: { type: 'LineString', coordinates: c } });
  const colecao = fs => ({ type: 'FeatureCollection', features: fs });

  function desenharRota() {
    if (!mapa || !mapa.isStyleLoaded()) return;
    const principal = rotas[escolhida] ? colecao([linha(rotas[escolhida])]) : colecao([]);
    const outras = colecao(rotas.filter((_, i) => i !== escolhida).map(linha));
    const pontos = lista => colecao(lista.map(([lng, lat]) => ({ type: 'Feature', properties: {}, geometry: { type: 'Point', coordinates: [lng, lat] } })));
    const pracas = pontos(pedagios);
    const cameras = pontos(radares);

    if (mapa.getSource('rota')) {
      mapa.getSource('rota').setData(principal);
      mapa.getSource('rota-outras').setData(outras);
      mapa.getSource('pedagios').setData(pracas);
      mapa.getSource('radares').setData(cameras);
      return;
    }
    mapa.addSource('rota', { type: 'geojson', data: principal });
    mapa.addSource('rota-outras', { type: 'geojson', data: outras });
    mapa.addSource('pedagios', { type: 'geojson', data: pracas });
    mapa.addSource('radares', { type: 'geojson', data: cameras });
    const antesDosNomes = mapa.getStyle().layers.find(l => l.type === 'symbol')?.id;
    const largura = k => ['interpolate', ['linear'], ['zoom'], 6, 4 * k, 10, 5.5 * k, 15, 9 * k, 18, 18 * k];
    const traco = { 'line-cap': 'round', 'line-join': 'round' };
    mapa.addLayer({ id: 'rota-outras-borda', type: 'line', source: 'rota-outras', layout: traco,
      paint: { 'line-color': '#2b2f36', 'line-width': largura(1.45) } }, antesDosNomes);
    mapa.addLayer({ id: 'rota-outras', type: 'line', source: 'rota-outras', layout: traco,
      paint: { 'line-color': '#7d8594', 'line-width': largura(1) } }, antesDosNomes);
    mapa.addLayer({ id: 'rota-borda', type: 'line', source: 'rota', layout: traco,
      paint: { 'line-color': '#0d47a1', 'line-width': largura(1.45) } }, antesDosNomes);
    mapa.addLayer({ id: 'rota-linha', type: 'line', source: 'rota', layout: traco,
      paint: { 'line-color': '#4f8cff', 'line-width': largura(1) } }, antesDosNomes);
    // Praça de pedágio: círculo âmbar com borda branca (por cima das linhas)
    mapa.addLayer({ id: 'pedagios', type: 'circle', source: 'pedagios',
      paint: { 'circle-color': '#ffb300', 'circle-radius': ['interpolate', ['linear'], ['zoom'], 6, 5, 14, 9], 'circle-stroke-color': '#fff', 'circle-stroke-width': 2.5 } });
    // Radar: círculo vermelho com borda branca
    mapa.addLayer({ id: 'radares', type: 'circle', source: 'radares',
      paint: { 'circle-color': '#ff3b30', 'circle-radius': ['interpolate', ['linear'], ['zoom'], 6, 4, 14, 8], 'circle-stroke-color': '#fff', 'circle-stroke-width': 2.5 } });
  }

  // Prévia: enquadra todas as opções na tela, reta (sem inclinar nem girar)
  function enquadrar() {
    if (!mapa || !rotas.length) return;
    const caixa = new maplibregl.LngLatBounds();
    for (const r of rotas) for (const p of r) caixa.extend(p);
    if (ultima) caixa.extend([ultima.lng, ultima.lat]);
    mapa.fitBounds(caixa, { padding: 40, pitch: 0, bearing: 0, duration: 800, maxZoom: 16 });
  }

  function centralizar(animar) {
    if (!mapa || !ultima) return;
    const opcoes = {
      center: [ultima.lng, ultima.lat],
      padding: padding(),
      // Navegando, aproxima mais (como nos apps de navegação)
      zoom: navegando ? Math.max(mapa.getZoom(), 16.3) : Math.max(mapa.getZoom(), 15),
      pitch: 45,
      duration: animar ? 900 : 0,
      essential: true,
    };
    if (ultima.velocidade >= VELOCIDADE_GIRAR && ultima.direcao != null) opcoes.bearing = ultima.direcao;
    mapa.easeTo(opcoes);
  }

  function mudarSeguir(valor) {
    if (seguindo === valor) return;
    seguindo = valor;
    aoMudarSeguir?.invokeMethodAsync('SeguindoMudou', valor);
  }

  return {
    // Cria o mapa dentro do elemento; dotnet recebe SeguindoMudou(bool) para mostrar o botão Recentralizar
    iniciar(id, dotnet) {
      this.destruir();
      aoMudarSeguir = dotnet;
      seguindo = true;
      mapa = new maplibregl.Map({
        container: id,
        style: ESTILO,
        center: [-46.633, -23.55], // até chegar o primeiro GPS
        zoom: 12,
        pitch: 45,
        attributionControl: { compact: true },
        dragRotate: false,
        pitchWithRotate: false,
        touchPitch: false,
      });
      mapa.touchZoomRotate.disableRotation();
      // Arrastar o mapa com o dedo para de seguir o carro (os movimentos do próprio app não contam)
      mapa.on('dragstart', () => mudarSeguir(false));
      mapa.on('style.load', () => { realcar(); desenharRota(); });
      mapa.on('load', () => mapa.resize());
      // Os créditos (obrigatórios) abrem sozinhos quando os dados chegam: recolhe no "i" quando o mapa termina de carregar
      mapa.once('idle', () => {
        const creditos = mapa?.getContainer().querySelector('.maplibregl-ctrl-attrib');
        creditos?.classList.remove('maplibregl-compact-show');
        creditos?.removeAttribute('open');
      });
      marcador = new maplibregl.Marker({ element: criarMarcador(), rotationAlignment: 'map', pitchAlignment: 'map' });
    },

    posicao(lat, lng, direcao, velocidade) {
      if (!mapa) return;
      const primeira = !ultima;
      ultima = { lat, lng, direcao, velocidade };
      marcador.setLngLat([lng, lat]);
      if (direcao != null) marcador.setRotation(direcao);
      if (primeira) marcador.addTo(mapa);
      // Na prévia o mapa fica parado mostrando as opções inteiras
      if (seguindo && !previa) centralizar(!primeira);
    },

    seguir() {
      previa = false;
      mudarSeguir(true);
      centralizar(true);
    },

    // Linhas no mapa. modo: 'previa' (opções para escolher, enquadradas), 'navegando' (só a escolhida, seguindo o carro)
    // ou null (sem rota). lista: [[[lng, lat], ...], ...]; pracas e cameras: [[lng, lat], ...] (pedágios e radares da opção marcada).
    rotas(lista, marcada, pracas, cameras, modo) {
      const antes = rotas.map(r => r.length).join(',');
      rotas = lista ?? [];
      escolhida = marcada ?? 0;
      pedagios = pracas ?? [];
      radares = cameras ?? [];
      const eraPrevia = previa;
      previa = modo === 'previa';
      navegando = modo === 'navegando';
      desenharRota();
      if (previa && (!eraPrevia || antes !== rotas.map(r => r.length).join(','))) enquadrar();
      else if (!previa && (eraPrevia || navegando)) this.seguir();
    },

    // A tela girou ou mudou de tamanho (layout trocado, painel arrastado): ajusta e mantém o carro no lugar
    redimensionar() {
      if (!mapa) return;
      mapa.resize();
      if (seguindo && !previa && ultima) centralizar(false);
    },

    destruir() {
      marcador?.remove();
      mapa?.remove();
      mapa = marcador = ultima = aoMudarSeguir = null;
      // A rota continua no RotaService: volta a ser desenhada quando a página abrir de novo
      rotas = [];
      pedagios = [];
      radares = [];
      navegando = previa = false;
    },
  };
})();
