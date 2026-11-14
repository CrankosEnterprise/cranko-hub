'use strict';

// Funções pequenas chamadas pelo C# (IJSRuntime)
window.crankohub = {
  // Aplica o tema salvo ("light" | "dark" | null = segue o Windows)
  aplicarTema(tema) {
    if (tema) document.documentElement.dataset.theme = tema;
    else delete document.documentElement.dataset.theme;
  },

  // Alterna claro/escuro e devolve o novo tema para ser salvo
  alternarTema() {
    const root = document.documentElement;
    const atual = root.dataset.theme || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
    const novo = atual === 'dark' ? 'light' : 'dark';
    root.dataset.theme = novo;
    return novo;
  },

  // Cor de destaque escolhida em Configurações (versões clara/escura e cor do texto sobre ela)
  aplicarDestaque(claro, escuro, texto, textoEscuro) {
    const s = document.documentElement.style;
    s.setProperty('--destaque-claro', claro);
    s.setProperty('--destaque-escuro', escuro);
    s.setProperty('--destaque-texto', texto);
    s.setProperty('--destaque-texto-escuro', textoEscuro || texto);
  },

  // Janela sem a moldura do Windows (Layout/LayoutPrincipal.razor): a página faz o papel da barra de título.
  // Apertar o botão num [data-arrastar] (fora de botões e links) move a janela; duas vezes maximiza;
  // as bordas .borda-janela redimensionam. Quem mexe na janela de verdade é o C# (JanelaService → MainWindow).
  registrarJanela(ponte) {
    if (window.__janela) return;
    window.__janela = ponte;
    // Janela sem foco: o semáforo fica cinza, como no macOS
    const raiz = document.documentElement;
    window.addEventListener('blur', () => raiz.classList.add('inativa'));
    window.addEventListener('focus', () => raiz.classList.remove('inativa'));
    const interativo = (el) => el.closest('a, button, input, select, textarea, [role="menu"], .sem-arrastar');
    document.addEventListener('mousedown', (e) => {
      if (e.button !== 0) return;
      const borda = e.target.closest?.('.borda-janela');
      if (borda) { e.preventDefault(); ponte.invokeMethodAsync('Redimensionar', borda.dataset.borda); return; }
      if (!e.target.closest?.('[data-arrastar]') || interativo(e.target)) return;
      if (e.detail === 2) { ponte.invokeMethodAsync('AlternarMaximizar'); return; }
      e.preventDefault();
      ponte.invokeMethodAsync('Arrastar');
    });
  },

  // Menu suspenso (ex.: a conta no topo): clicar fora dele ou apertar Esc chama ponte.FecharMenu().
  // Não dá para usar um fundo "position: fixed" atrás do menu: a barra do topo é um container (CSS) e o fundo
  // só cobriria a barra — e, por ela arrastar a janela, o clique viraria um arraste. Devolve { parar() }.
  fecharAoClicarFora(el, ponte) {
    const fechar = () => { parar(); ponte.invokeMethodAsync('FecharMenu'); };
    const aoApertar = (e) => {
      if (el.contains(e.target)) return;
      // Clicar fora só fecha o menu: na barra do topo não começa a arrastar a janela
      if (e.target.closest?.('[data-arrastar]')) e.stopPropagation();
      fechar();
    };
    const aoTeclar = (e) => { if (e.key === 'Escape') fechar(); };
    const parar = () => {
      document.removeEventListener('mousedown', aoApertar, true);
      document.removeEventListener('keydown', aoTeclar, true);
    };
    document.addEventListener('mousedown', aoApertar, true);
    document.addEventListener('keydown', aoTeclar, true);
    return { parar };
  },

  // Quem rola é o "palco" (ver LayoutPrincipal), não a janela
  rolarParaTopo() { document.getElementById('palco')?.scrollTo(0, 0); },

  // Mesmo limite do CSS em que o menu lateral passa a abrir por cima
  telaPequena() { return matchMedia('(max-width: 860px)').matches; },

  rolarAte(el) { el?.scrollIntoView({ behavior: 'smooth', block: 'center' }); },

  focar(el) { el?.focus(); el?.select?.(); },

  // Carrega um script/estilo só quando a página precisa (ex.: o mapa do Modo Carro). Repetir não recarrega.
  carregarScript(url) {
    if (document.querySelector(`script[data-src="${url}"]`)) return window.__carregados?.[url] ?? Promise.resolve();
    window.__carregados ??= {};
    return window.__carregados[url] = new Promise((ok, erro) => {
      const s = document.createElement('script');
      s.src = url;
      s.dataset.src = url;
      s.onload = () => ok();
      s.onerror = () => erro(new Error(`Não foi possível carregar ${url}`));
      document.head.appendChild(s);
    });
  },

  // Esvazia um elemento (ex.: o mapa do Google antes de abrir o do OpenStreetMap no mesmo lugar)
  limpar(id) {
    const el = document.getElementById(id);
    if (!el) return;
    el.replaceChildren();
    el.removeAttribute('style');
    el.className = el.className.split(' ').filter(c => c.startsWith('mc-')).join(' ');
  },

  carregarCss(url) {
    if (document.querySelector(`link[data-href="${url}"]`)) return;
    const l = document.createElement('link');
    l.rel = 'stylesheet';
    l.href = url;
    l.dataset.href = url;
    document.head.appendChild(l);
  },
};

// Enter em campos marcados com data-sem-enviar (ex.: novo item do checklist) não envia o formulário
document.addEventListener('keydown', e => {
  if (e.key === 'Enter' && e.target.closest?.('[data-sem-enviar]')) e.preventDefault();
}, true);
