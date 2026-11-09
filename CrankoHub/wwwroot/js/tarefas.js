'use strict';

// Tarefas: arrastar cards entre colunas e reordenar os quadros (abas), com o ponteiro (mouse e toque).
// Carregado como módulo pelo TarefasInterop (C#). Só chama o C# ao soltar:
// - card solto  → MoveuCard(id, colunaId, antesDeId | null)   (null = fim da coluna)
// - aba solta   → MoveuQuadro(id, novoIndice)
// Nada do DOM do Blazor é movido: um "fantasma" (clone) segue o ponteiro e uma linha marca onde vai cair.
// No toque, o arrasto começa depois de segurar um instante; antes disso o dedo só rola.

const MOVEU_CARD = 'MoveuCard';
const MOVEU_QUADRO = 'MoveuQuadro';

const MOVER_PX = 5;          // abaixo disso é clique
const SEGURAR_MS = 380;      // toque: tempo segurando para começar a arrastar
const BORDA_PX = 56;         // perto da borda, rola sozinho
const VELOCIDADE = 16;       // px por quadro na rolagem automática

class QuadroTarefas {
  #raiz;
  #dotnet;
  #abortar = new AbortController();
  #arrasto = null;
  #engolirClique = false;
  #raf = 0;

  constructor(raiz, dotnet) {
    this.#raiz = raiz;
    this.#dotnet = dotnet;
    const op = { signal: this.#abortar.signal };
    raiz.addEventListener('pointerdown', e => this.#apertou(e), op);
    window.addEventListener('pointermove', e => this.#moveu(e), op);
    window.addEventListener('pointerup', () => this.#soltou(), op);
    window.addEventListener('pointercancel', () => this.#cancelar(), op);
    window.addEventListener('keydown', e => { if (e.key === 'Escape') this.#cancelar(); }, op);
    // Depois de um arrasto, o "click" que o navegador dispara não pode abrir o card nem trocar de quadro
    raiz.addEventListener('click', e => {
      if (this.#engolirClique) { e.stopPropagation(); e.preventDefault(); }
    }, { ...op, capture: true });
    raiz.addEventListener('contextmenu', e => { if (this.#arrasto?.ativo) e.preventDefault(); }, op);
    // Apertar em cima de texto selecionado faz o navegador começar o arrasto nativo dele, que cancela o nosso
    raiz.addEventListener('dragstart', e => { if (this.#arrasto) e.preventDefault(); }, op);
    // Durante o arrasto no toque, o dedo não rola a página
    raiz.addEventListener('touchmove', e => { if (this.#arrasto?.ativo) e.preventDefault(); }, { ...op, passive: false });
  }

  async #chamar(metodo, ...args) {
    try { await this.#dotnet.invokeMethodAsync(metodo, ...args); } catch { /* página fechada */ }
  }

  #apertou(e) {
    if (e.button !== 0 || this.#arrasto) return;
    if (e.target.closest('input, select, textarea')) return;
    const card = e.target.closest('.card-arrastavel[data-id]');
    const aba = card ? null : e.target.closest('.tab[data-quadro]');
    const el = card ?? aba;
    if (!el || !this.#raiz.contains(el)) return;
    this.#arrasto = {
      tipo: card ? 'card' : 'aba',
      el,
      x: e.clientX, y: e.clientY,
      px: e.clientX, py: e.clientY,
      ativo: false,
      toque: e.pointerType === 'touch',
      timer: null,
      fantasma: null, marca: null, coluna: null,
      alvo: null,
    };
    if (this.#arrasto.toque) this.#arrasto.timer = setTimeout(() => this.#ativar(), SEGURAR_MS);
  }

  #ativar() {
    const a = this.#arrasto;
    if (!a || a.ativo) return;
    a.ativo = true;
    getSelection()?.removeAllRanges();
    const r = a.el.getBoundingClientRect();
    a.dx = a.x - r.left;
    a.dy = a.y - r.top;

    const f = a.el.cloneNode(true);
    f.classList.add('tarefa-fantasma');
    f.removeAttribute('data-id');
    f.removeAttribute('data-quadro');
    f.style.width = r.width + 'px';
    f.style.height = r.height + 'px';
    document.body.append(f);
    a.fantasma = f;

    a.marca = document.createElement('div');
    a.marca.className = 'tarefa-marca';
    document.body.append(a.marca);

    a.el.classList.add('arrastando-origem');
    document.documentElement.classList.add('arrastando-tarefa');
    if (a.toque) navigator.vibrate?.(15);
    this.#atualizar();
    this.#rolarSozinho();
  }

  #moveu(e) {
    const a = this.#arrasto;
    if (!a) return;
    if (!a.ativo) {
      if (Math.hypot(e.clientX - a.x, e.clientY - a.y) < MOVER_PX) return;
      // No toque, mexer antes de segurar é rolagem: desiste do arrasto
      if (a.toque) { this.#cancelar(); return; }
      a.px = e.clientX; a.py = e.clientY;
      this.#ativar();
      return;
    }
    e.preventDefault();
    a.px = e.clientX;
    a.py = e.clientY;
    this.#atualizar();
  }

  // Move o fantasma e recalcula onde o item vai cair
  #atualizar() {
    const a = this.#arrasto;
    if (!a?.ativo) return;
    a.fantasma.style.transform = `translate(${a.px - a.dx}px, ${a.py - a.dy}px)`;
    if (a.tipo === 'card') this.#alvoCard(a); else this.#alvoAba(a);
  }

  // Coluna pelo x do ponteiro (não pelo elemento debaixo dele: ali está o fantasma/o próprio card)
  #alvoCard(a) {
    const colunas = [...this.#raiz.querySelectorAll('.kanban .coluna[data-coluna]')];
    if (colunas.length === 0) return;
    let col = colunas[colunas.length - 1];
    for (const c of colunas) {
      if (a.px < c.getBoundingClientRect().right) { col = c; break; }
    }
    if (a.coluna !== col) {
      a.coluna?.classList.remove('alvo');
      col.classList.add('alvo');
      a.coluna = col;
    }

    const lista = col.querySelector('.coluna-cards');
    const cards = [...lista.querySelectorAll('.card-arrastavel[data-id]')].filter(c => c !== a.el);
    const antes = cards.find(c => { const r = c.getBoundingClientRect(); return a.py < r.top + r.height / 2; }) ?? null;
    a.alvo = { coluna: col.dataset.coluna, antes: antes?.dataset.id ?? null };

    // Linha no vão entre os cards
    const rl = lista.getBoundingClientRect();
    let y;
    if (antes) y = antes.getBoundingClientRect().top - 5;
    else if (cards.length) y = cards[cards.length - 1].getBoundingClientRect().bottom + 3;
    else y = rl.top + 4;
    Object.assign(a.marca.style, { left: rl.left + 'px', top: y + 'px', width: rl.width + 'px', height: '3px' });
  }

  // Posição da aba entre as outras pelo x do ponteiro
  #alvoAba(a) {
    const outras = [...this.#raiz.querySelectorAll('.tab[data-quadro]')].filter(t => t !== a.el);
    let indice = outras.findIndex(t => { const r = t.getBoundingClientRect(); return a.px < r.left + r.width / 2; });
    if (indice < 0) indice = outras.length;
    a.alvo = { indice };

    const ref = outras[indice] ?? outras[outras.length - 1];
    const r = (ref ?? a.el).getBoundingClientRect();
    const x = outras[indice] ? r.left - 3 : r.right + 1;
    Object.assign(a.marca.style, { left: x + 'px', top: r.top + 'px', width: '3px', height: r.height + 'px' });
  }

  // Perto das bordas, rola o quadro (para os lados) ou a página (para cima/baixo)
  #rolarSozinho() {
    cancelAnimationFrame(this.#raf);
    const passo = () => {
      const a = this.#arrasto;
      if (!a?.ativo) return;
      let rolou = false;
      const horizontal = a.tipo === 'card' ? this.#raiz.querySelector('.kanban') : this.#raiz.querySelector('.tabs');
      if (horizontal) {
        const r = horizontal.getBoundingClientRect();
        const v = a.px < r.left + BORDA_PX ? -VELOCIDADE : a.px > r.right - BORDA_PX ? VELOCIDADE : 0;
        if (v && horizontal.scrollWidth > horizontal.clientWidth) {
          const antes = horizontal.scrollLeft;
          horizontal.scrollLeft += v;
          rolou ||= horizontal.scrollLeft !== antes;
        }
      }
      if (a.tipo === 'card') {
        const vertical = rolavelVertical(this.#raiz);
        const topo = vertical === document.scrollingElement ? 0 : vertical.getBoundingClientRect().top;
        const fundo = vertical === document.scrollingElement ? innerHeight : vertical.getBoundingClientRect().bottom;
        const v = a.py < topo + BORDA_PX ? -VELOCIDADE : a.py > fundo - BORDA_PX ? VELOCIDADE : 0;
        if (v) {
          const antes = vertical.scrollTop;
          vertical.scrollTop += v;
          rolou ||= vertical.scrollTop !== antes;
        }
      }
      if (rolou) this.#atualizar();
      this.#raf = requestAnimationFrame(passo);
    };
    this.#raf = requestAnimationFrame(passo);
  }

  async #soltou() {
    const a = this.#arrasto;
    if (!a) return;
    clearTimeout(a.timer);
    this.#arrasto = null;
    if (!a.ativo) return; // foi um clique: o Blazor cuida
    this.#engolirClique = true;
    setTimeout(() => { this.#engolirClique = false; }, 0);
    cancelAnimationFrame(this.#raf);
    a.marca.remove();
    a.coluna?.classList.remove('alvo');
    document.documentElement.classList.remove('arrastando-tarefa');

    let chamada = null;
    if (a.tipo === 'card' && a.alvo) {
      // Soltou no mesmo lugar? (mesma coluna, antes do mesmo vizinho)
      const proximo = proximoCard(a.el);
      const colunaAtual = a.el.closest('.coluna')?.dataset.coluna;
      if (a.alvo.coluna !== colunaAtual || a.alvo.antes !== proximo)
        chamada = this.#chamar(MOVEU_CARD, a.el.dataset.id, a.alvo.coluna, a.alvo.antes);
    } else if (a.tipo === 'aba' && a.alvo) {
      const atual = [...this.#raiz.querySelectorAll('.tab[data-quadro]')].indexOf(a.el);
      if (a.alvo.indice !== atual) chamada = this.#chamar(MOVEU_QUADRO, a.el.dataset.quadro, a.alvo.indice);
    }
    // O fantasma fica onde foi solto até o Blazor redesenhar no lugar novo (sem piscar no lugar antigo)
    if (chamada) await chamada;
    a.fantasma.remove();
    a.el.classList.remove('arrastando-origem');
  }

  #cancelar() {
    const a = this.#arrasto;
    if (!a) return;
    clearTimeout(a.timer);
    this.#arrasto = null;
    cancelAnimationFrame(this.#raf);
    if (!a.ativo) return;
    this.#engolirClique = true;
    setTimeout(() => { this.#engolirClique = false; }, 0);
    a.fantasma.remove();
    a.marca.remove();
    a.coluna?.classList.remove('alvo');
    a.el.classList.remove('arrastando-origem');
    document.documentElement.classList.remove('arrastando-tarefa');
  }

  descartar() {
    this.#cancelar();
    this.#abortar.abort();
  }
}

function proximoCard(el) {
  let n = el.nextElementSibling;
  while (n && !n.matches('.card-arrastavel[data-id]')) n = n.nextElementSibling;
  return n?.dataset.id ?? null;
}

// O primeiro ancestral que rola na vertical (o conteúdo do app pode rolar num painel, não na janela)
function rolavelVertical(el) {
  for (let p = el.parentElement; p && p !== document.body; p = p.parentElement) {
    const s = getComputedStyle(p).overflowY;
    if ((s === 'auto' || s === 'scroll') && p.scrollHeight > p.clientHeight) return p;
  }
  return document.scrollingElement;
}

let atual = null;

export function iniciar(raiz, dotnet) {
  atual?.descartar();
  atual = new QuadroTarefas(raiz, dotnet);
}

export function descartar() {
  atual?.descartar();
  atual = null;
}
