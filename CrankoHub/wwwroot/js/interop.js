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
  aplicarDestaque(claro, escuro, texto) {
    const s = document.documentElement.style;
    s.setProperty('--destaque-claro', claro);
    s.setProperty('--destaque-escuro', escuro);
    s.setProperty('--destaque-texto', texto);
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
