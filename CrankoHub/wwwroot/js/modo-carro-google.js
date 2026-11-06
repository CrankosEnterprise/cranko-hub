'use strict';

// Mapa do Google no Modo Carro (só com a chave em Configurações → Integrações). Mesmas funções do window.modoCarro
// (modo-carro.js, MapLibre/OpenStreetMap), para a página trocar um pelo outro. Se a chave for recusada ou o script não
// carregar, avisa o C# (GoogleFalhou) e a página volta para o mapa do OpenStreetMap.
window.modoCarroGoogle = (() => {
  const VELOCIDADE_GIRAR = 7; // km/h: abaixo disso a direção do GPS não é confiável e o mapa não gira

  let mapa = null, carro = null, setaCarro = null, ultima = null, seguindo = true, previa = false, navegando = false;
  let dotnet = null, linhas = [], marcadores = [], animacao = null;

  // Carrega a API uma vez só (o Google não deixa carregar duas vezes na mesma página)
  let carregando = null;
  function carregar(chave) {
    if (window.google?.maps?.importLibrary) return Promise.resolve();
    if (carregando) return carregando;
    carregando = new Promise((ok, erro) => {
      window.__crankoGooglePronto = () => ok();
      // Chave recusada (inválida, API desativada, sem faturamento): o Google chama esta função global
      window.gm_authFailure = () => dotnet?.invokeMethodAsync('GoogleFalhou', 'A chave do Google foi recusada (confira a chave, as APIs ativadas e o faturamento).');
      const s = document.createElement('script');
      s.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(chave)}&v=weekly&language=pt-BR&region=BR&loading=async&callback=__crankoGooglePronto`;
      s.async = true;
      s.onerror = () => { carregando = null; erro(new Error('Não foi possível carregar o Google Maps (sem internet?).')); };
      document.head.appendChild(s);
    });
    return carregando;
  }

  function criarSeta() {
    const el = document.createElement('div');
    el.className = 'mc-carro';
    el.innerHTML = '<svg viewBox="0 0 40 40"><path d="M20 4 33 34 20 27 7 34z"/></svg>';
    return el;
  }

  function bolinha(cor) {
    const el = document.createElement('div');
    el.style.cssText = `width:16px;height:16px;border-radius:50%;background:${cor};box-shadow:0 0 0 3px #fff,0 2px 6px rgba(0,0,0,.5);transform:translateY(8px)`;
    return el;
  }

  // Metros por pixel no zoom atual (para deixar o carro no terço de baixo da tela)
  function metrosPorPixel(lat, zoom) { return 156543.03392 * Math.cos(lat * Math.PI / 180) / Math.pow(2, zoom); }

  // Ponto a "metros" de distância na direção "graus" (aproximação plana, boa para algumas centenas de metros)
  function aFrente(lat, lng, graus, metros) {
    const r = graus * Math.PI / 180;
    return { lat: lat + (metros * Math.cos(r)) / 111320, lng: lng + (metros * Math.sin(r)) / (111320 * Math.cos(lat * Math.PI / 180)) };
  }

  // Anima a câmera até o destino em ~0,8 s (o moveCamera do Google é instantâneo)
  function moverCamera(alvo, duracao) {
    cancelAnimationFrame(animacao);
    if (!duracao) { mapa.moveCamera(alvo); return; }
    const de = { center: mapa.getCenter().toJSON(), zoom: mapa.getZoom(), heading: mapa.getHeading() ?? 0, tilt: mapa.getTilt() ?? 0 };
    let giro = (alvo.heading ?? de.heading) - de.heading;
    if (giro > 180) giro -= 360;
    if (giro < -180) giro += 360;
    const inicio = performance.now();
    const passo = agora => {
      const t = Math.min(1, (agora - inicio) / duracao);
      const k = t * (2 - t);
      mapa.moveCamera({
        center: { lat: de.center.lat + (alvo.center.lat - de.center.lat) * k, lng: de.center.lng + (alvo.center.lng - de.center.lng) * k },
        zoom: de.zoom + ((alvo.zoom ?? de.zoom) - de.zoom) * k,
        heading: de.heading + giro * k,
        tilt: de.tilt + ((alvo.tilt ?? de.tilt) - de.tilt) * k,
      });
      if (t < 1) animacao = requestAnimationFrame(passo);
    };
    animacao = requestAnimationFrame(passo);
  }

  function centralizar(animar) {
    if (!mapa || !ultima) return;
    const zoom = navegando ? Math.max(mapa.getZoom(), 16.5) : Math.max(mapa.getZoom(), 15.5);
    const girar = ultima.velocidade >= VELOCIDADE_GIRAR && ultima.direcao != null;
    const heading = girar ? ultima.direcao : (mapa.getHeading() ?? 0);
    const altura = mapa.getDiv().clientHeight;
    const centro = aFrente(ultima.lat, ultima.lng, heading, altura * 0.17 * metrosPorPixel(ultima.lat, zoom));
    moverCamera({ center: centro, zoom, heading, tilt: 45 }, animar ? 800 : 0);
  }

  function girarSeta() {
    if (!setaCarro || !ultima) return;
    const angulo = (ultima.direcao ?? 0) - (mapa?.getHeading() ?? 0);
    setaCarro.style.transform = `rotate(${angulo}deg)`;
  }

  function mudarSeguir(valor) {
    if (seguindo === valor) return;
    seguindo = valor;
    dotnet?.invokeMethodAsync('SeguindoMudou', valor);
  }

  function limparRotas() {
    for (const l of linhas) l.setMap(null);
    for (const m of marcadores) m.map = null;
    linhas = [];
    marcadores = [];
  }

  return {
    // Devolve false se não deu para abrir o mapa do Google (a página usa o do OpenStreetMap)
    async iniciar(id, ref, chave, mapId) {
      this.destruir();
      dotnet = ref;
      seguindo = true;
      try {
        await carregar(chave);
        const { Map } = await google.maps.importLibrary('maps');
        const { AdvancedMarkerElement } = await google.maps.importLibrary('marker');
        mapa = new Map(document.getElementById(id), {
          center: { lat: -23.55, lng: -46.633 }, // até chegar o primeiro GPS
          zoom: 15,
          mapId: mapId || 'DEMO_MAP_ID',
          colorScheme: google.maps.ColorScheme?.DARK ?? 'DARK',
          disableDefaultUI: true,
          clickableIcons: false,
          gestureHandling: 'greedy',
          tilt: 45,
          heading: 0,
        });
        new google.maps.TrafficLayer().setMap(mapa);
        mapa.addListener('dragstart', () => mudarSeguir(false));
        mapa.addListener('heading_changed', girarSeta);
        setaCarro = criarSeta();
        carro = new AdvancedMarkerElement({ content: setaCarro, zIndex: 1000 });
        this._Marcador = AdvancedMarkerElement;
        return true;
      } catch (e) {
        console.error(e);
        this.destruir();
        return false;
      }
    },

    posicao(lat, lng, direcao, velocidade) {
      if (!mapa) return;
      const primeira = !ultima;
      ultima = { lat, lng, direcao, velocidade };
      carro.position = { lat, lng };
      if (primeira) carro.map = mapa;
      girarSeta();
      if (seguindo && !previa) centralizar(!primeira);
    },

    seguir() {
      previa = false;
      mudarSeguir(true);
      centralizar(true);
    },

    // Mesmo formato do modoCarro.rotas: lista [[[lng, lat]...]...], marcada, pracas/cameras [[lng, lat]...], modo 'previa'|'navegando'|null
    rotas(lista, marcada, pracas, cameras, modo) {
      if (!mapa) return;
      limparRotas();
      const eraPrevia = previa;
      previa = modo === 'previa';
      navegando = modo === 'navegando';
      const ponto = ([lng, lat]) => ({ lat, lng });
      (lista ?? []).forEach((coords, i) => {
        const caminho = coords.map(ponto);
        const escolhida = i === (marcada ?? 0);
        // Borda escura + linha (a escolhida azul por cima; as outras cinza)
        linhas.push(new google.maps.Polyline({ map: mapa, path: caminho, strokeColor: escolhida ? '#0d47a1' : '#2b2f36', strokeWeight: escolhida ? 12 : 9, strokeOpacity: 1, zIndex: escolhida ? 20 : 10 }));
        linhas.push(new google.maps.Polyline({ map: mapa, path: caminho, strokeColor: escolhida ? '#4f8cff' : '#8a93a6', strokeWeight: escolhida ? 7 : 5, strokeOpacity: 1, zIndex: escolhida ? 21 : 11 }));
      });
      for (const p of pracas ?? []) marcadores.push(new this._Marcador({ map: mapa, position: ponto(p), content: bolinha('#ffb300'), zIndex: 30 }));
      for (const c of cameras ?? []) marcadores.push(new this._Marcador({ map: mapa, position: ponto(c), content: bolinha('#ff3b30'), zIndex: 31 }));

      if (previa && (lista?.length ?? 0) > 0) {
        // Prévia: tudo enquadrado, reto (sem inclinar nem girar)
        const caixa = new google.maps.LatLngBounds();
        for (const r of lista) for (const p of r) caixa.extend(ponto(p));
        if (ultima) caixa.extend({ lat: ultima.lat, lng: ultima.lng });
        mapa.moveCamera({ tilt: 0, heading: 0 });
        mapa.fitBounds(caixa, 40);
      } else if (!previa && (eraPrevia || navegando)) {
        this.seguir();
      }
    },

    redimensionar() { /* o Google acompanha o tamanho do elemento sozinho */ },

    destruir() {
      cancelAnimationFrame(animacao);
      limparRotas();
      if (carro) carro.map = null;
      // O Google não tem "remover mapa": basta soltar as referências (o elemento some com a página)
      mapa = carro = setaCarro = ultima = null;
      previa = navegando = false;
    },
  };
})();
