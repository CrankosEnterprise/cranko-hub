'use strict';

// Layout do Modo Carro (Pages/Carro/ModoCarro.razor): a barra entre o mapa e o painel, que se arrasta com o dedo para mudar
// o tamanho, e o aviso ao mapa (modoCarro ou modoCarroGoogle) quando a área dele muda de tamanho.
// Durante o arraste só a variável CSS muda (--mc-painel na paisagem, --mc-painel-r no retrato); ao soltar, o C# grava
// o valor (PainelMudou). Toque duplo na barra volta ao tamanho padrão (PainelPadrao).
window.modoCarroLayout = (() => {
  let raiz = null, dotnet = null, mapaJs = 'modoCarro', observador = null, arraste = null, ultimoToque = 0;

  const retrato = () => matchMedia('(orientation: portrait)').matches;

  // O elemento que tem o tamanho escolhido: o painel, ou o mapa no layout "Spotify grande"
  const alvo = () => raiz.querySelector(raiz.classList.contains('layout-musica') ? '.mc-mapa' : '.mc-lado');

  function inicio(e) {
    const barra = e.target.closest('.mc-divisoria');
    if (!barra || !raiz) return;
    const el = alvo();
    if (!el) return;
    e.preventDefault();
    barra.setPointerCapture(e.pointerId);
    const r = retrato();
    const estilo = getComputedStyle(raiz);
    const caixa = raiz.getBoundingClientRect();
    // Área de conteúdo (sem o padding): a base do flex-basis em %
    const comeco = r ? caixa.top + parseFloat(estilo.paddingTop) : caixa.left + parseFloat(estilo.paddingLeft);
    const fim = r ? caixa.bottom - parseFloat(estilo.paddingBottom) : caixa.right - parseFloat(estilo.paddingRight);
    const b = barra.getBoundingClientRect(), a = el.getBoundingClientRect();
    // O alvo está depois da barra (à direita/embaixo) ou antes dela
    const depois = r ? a.top > b.top : a.left > b.left;
    arraste = {
      barra, r, comeco, fim, depois, movido: false,
      x: e.clientX, y: e.clientY,
      min: Number(barra.dataset.min) || 20, max: Number(barra.dataset.max) || 70,
      pct: null,
    };
    barra.classList.add('arrastando');
  }

  function mover(e) {
    if (!arraste) return;
    const a = arraste;
    if (!a.movido && Math.hypot(e.clientX - a.x, e.clientY - a.y) < 6) return;
    a.movido = true;
    const pos = a.r ? e.clientY : e.clientX;
    const total = a.fim - a.comeco;
    // A barra fica no meio do espaço (10px) entre o mapa e o painel
    const tamanho = a.depois ? a.fim - pos - 5 : pos - a.comeco - 5;
    a.pct = Math.min(a.max, Math.max(a.min, tamanho / total * 100));
    raiz.style.setProperty(a.r ? '--mc-painel-r' : '--mc-painel', `${a.pct.toFixed(1)}%`);
  }

  function soltar() {
    if (!arraste) return;
    const a = arraste;
    arraste = null;
    a.barra.classList.remove('arrastando');
    if (a.movido && a.pct != null) {
      ultimoToque = 0;
      dotnet?.invokeMethodAsync('PainelMudou', Math.round(a.pct), a.r);
      return;
    }
    // Toque sem arrastar: dois seguidos voltam ao tamanho padrão
    const agora = performance.now();
    if (agora - ultimoToque < 400) {
      ultimoToque = 0;
      dotnet?.invokeMethodAsync('PainelPadrao', a.r);
    } else {
      ultimoToque = agora;
    }
  }

  function redimensionou() { window[mapaJs]?.redimensionar?.(); }

  return {
    // elemento: a raiz .modo-carro; mapa: id do elemento do mapa; js: "modoCarro" ou "modoCarroGoogle"
    iniciar(elemento, ref, mapa, js) {
      this.destruir();
      raiz = elemento;
      dotnet = ref;
      mapaJs = js;
      raiz.addEventListener('pointerdown', inicio);
      raiz.addEventListener('pointermove', mover);
      raiz.addEventListener('pointerup', soltar);
      raiz.addEventListener('pointercancel', soltar);
      const el = document.getElementById(mapa);
      if (el && window.ResizeObserver) {
        let quadro = 0;
        observador = new ResizeObserver(() => {
          cancelAnimationFrame(quadro);
          quadro = requestAnimationFrame(redimensionou);
        });
        observador.observe(el);
      }
    },

    // O mapa trocou (o Google falhou e entrou o OpenStreetMap)
    mapa(js) { mapaJs = js; },

    destruir() {
      observador?.disconnect();
      observador = null;
      if (raiz) {
        raiz.removeEventListener('pointerdown', inicio);
        raiz.removeEventListener('pointermove', mover);
        raiz.removeEventListener('pointerup', soltar);
        raiz.removeEventListener('pointercancel', soltar);
      }
      raiz = dotnet = arraste = null;
    },
  };
})();
