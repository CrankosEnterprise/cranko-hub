'use strict';

// Mapa do Modo Carro (Pages/ModoCarro.razor): MapLibre GL + tiles do OpenFreeMap (OpenStreetMap, sem chave).
// A posição vem do GPS pelo C# (ModoCarroService.Posicao) e chega aqui em posicao(...).
window.modoCarro = (() => {
  const ESTILO = 'https://tiles.openfreemap.org/styles/dark';
  const VELOCIDADE_GIRAR = 7; // km/h: abaixo disso a direção do GPS não é confiável e o mapa não gira

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

  function centralizar(animar) {
    if (!mapa || !ultima) return;
    const opcoes = {
      center: [ultima.lng, ultima.lat],
      padding: padding(),
      zoom: Math.max(mapa.getZoom(), 15),
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
      mapa.on('load', () => mapa.resize());
      marcador = new maplibregl.Marker({ element: criarMarcador(), rotationAlignment: 'map', pitchAlignment: 'map' });
    },

    posicao(lat, lng, direcao, velocidade) {
      if (!mapa) return;
      const primeira = !ultima;
      ultima = { lat, lng, direcao, velocidade };
      marcador.setLngLat([lng, lat]);
      if (direcao != null) marcador.setRotation(direcao);
      if (primeira) marcador.addTo(mapa);
      if (seguindo) centralizar(!primeira);
    },

    seguir() {
      mudarSeguir(true);
      centralizar(true);
    },

    // A tela girou ou mudou de tamanho
    redimensionar() { mapa?.resize(); },

    destruir() {
      marcador?.remove();
      mapa?.remove();
      mapa = marcador = ultima = aoMudarSeguir = null;
    },
  };
})();
