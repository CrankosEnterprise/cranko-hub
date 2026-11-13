'use strict';

// Lista reordenável arrastando pela alça (mouse e toque). Usado em Configurações → Módulos.
// Carregado como módulo pelo OrdenarInterop (C#). Cada item é um filho com [data-id]; a alça é um [data-alca] dentro dele.
// Enquanto arrasta, o item segue o ponteiro e os outros abrem espaço (só transform: o DOM do Blazor não é mexido).
// Ao soltar, chama o método do C# (metodo(id, novoIndice)) e o Blazor redesenha na ordem nova.
// A alça tem touch-action: none, então no toque o arrasto começa na hora e o resto da linha continua rolando a página.

const MOVER_PX = 4; // abaixo disso é clique

class ListaOrdenavel {
  #raiz;
  #dotnet;
  #metodo;
  #abortar = new AbortController();
  #arrasto = null;

  constructor(raiz, dotnet, metodo) {
    this.#raiz = raiz;
    this.#dotnet = dotnet;
    this.#metodo = metodo;
    const op = { signal: this.#abortar.signal };
    raiz.addEventListener('pointerdown', e => this.#apertou(e), op);
    window.addEventListener('pointermove', e => this.#moveu(e), op);
    window.addEventListener('pointerup', () => this.#soltou(), op);
    window.addEventListener('pointercancel', () => this.#cancelar(), op);
    window.addEventListener('keydown', e => { if (e.key === 'Escape') this.#cancelar(); }, op);
  }

  #itens() {
    return [...this.#raiz.children].filter(el => el.dataset.id);
  }

  #apertou(e) {
    if (e.button !== 0 || this.#arrasto) return;
    const alca = e.target.closest('[data-alca]');
    const item = alca?.closest('[data-id]');
    if (!item || item.parentElement !== this.#raiz) return;
    e.preventDefault(); // não seleciona texto nem rola
    const itens = this.#itens();
    this.#arrasto = {
      item, itens,
      de: itens.indexOf(item),
      para: itens.indexOf(item),
      y0: e.clientY,
      ativo: false,
      // Posições no começo: o meio de cada item decide para onde vai
      meios: itens.map(el => { const r = el.getBoundingClientRect(); return r.top + r.height / 2; }),
      altura: item.getBoundingClientRect().height + parseFloat(getComputedStyle(this.#raiz).rowGap || '0'),
    };
  }

  #moveu(e) {
    const a = this.#arrasto;
    if (!a) return;
    const dy = e.clientY - a.y0;
    if (!a.ativo) {
      if (Math.abs(dy) < MOVER_PX) return;
      a.ativo = true;
      this.#raiz.classList.add('ordenando');
      a.item.classList.add('arrastando');
    }
    a.item.style.transform = `translateY(${dy}px)`;

    const centro = a.meios[a.de] + dy;
    let para = a.de;
    while (para < a.itens.length - 1 && centro > a.meios[para + 1]) para++;
    while (para > 0 && centro < a.meios[para - 1]) para--;
    if (para === a.para) return;
    a.para = para;
    a.itens.forEach((el, i) => {
      if (el === a.item) return;
      const desloca = a.de < para && i > a.de && i <= para ? -a.altura
        : a.de > para && i < a.de && i >= para ? a.altura : 0;
      el.style.transform = desloca ? `translateY(${desloca}px)` : '';
    });
  }

  #soltou() {
    const a = this.#arrasto;
    if (!a) return;
    this.#limpar();
    if (a.ativo && a.para !== a.de) this.#dotnet.invokeMethodAsync(this.#metodo, a.item.dataset.id, a.para);
  }

  #cancelar() {
    if (this.#arrasto) this.#limpar();
  }

  #limpar() {
    const a = this.#arrasto;
    this.#arrasto = null;
    this.#raiz.classList.remove('ordenando');
    a.item.classList.remove('arrastando');
    a.itens.forEach(el => { el.style.transform = ''; });
  }

  descartar() {
    if (this.#arrasto) this.#limpar();
    this.#abortar.abort();
  }
}

const listas = new Map();

export function iniciar(raiz, dotnet, metodo) {
  listas.get(raiz)?.descartar();
  listas.set(raiz, new ListaOrdenavel(raiz, dotnet, metodo));
}

export function descartar(raiz) {
  listas.get(raiz)?.descartar();
  listas.delete(raiz);
}
