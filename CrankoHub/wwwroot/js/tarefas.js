'use strict';

// Tarefas: arrastar cards entre colunas e reordenar os quadros (abas), com o ponteiro (mouse e toque).
// Carregado como módulo pelo TarefasInterop (C#). Só chama o C# ao soltar:
// - card solto  → MoveuCard(id, colunaId, antesDeId | null)   (null = fim da coluna)
// - aba solta   → MoveuQuadro(id, novoIndice)
// - coluna solta (arrastada pelo título) → MoveuColuna(id, novoIndice)
// Nada do DOM do Blazor é movido: um "fantasma" (clone) segue o ponteiro e uma linha marca onde vai cair.
// No toque, o arrasto começa depois de segurar um instante; antes disso o dedo só rola.
// Rolagem lateral do quadro: o dedo rola nativo; o mouse rola arrastando o fundo do quadro (fora dos cards)
// ou pela .kanban-barra, a barra em cima das colunas que anda junto com o .kanban (a dele fica escondida).

const MOVEU_CARD = 'MoveuCard';
const MOVEU_QUADRO = 'MoveuQuadro';
const MOVEU_COLUNA = 'MoveuColuna';
const FANTASMA_COLUNA_MAX = 360; // a coluna inteira pode ser bem alta: o fantasma mostra só o começo

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
  #rolagem = null;            // arrastando o fundo do quadro com o mouse
  #agendado = 0;

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

    // Barra de cima: acompanha a largura do quadro (troca de quadro, coluna nova, janela redimensionada)
    raiz.addEventListener('scroll', e => this.#espelhar(e.target), { ...op, capture: true });
    raiz.addEventListener('wheel', e => this.#rodinhaNaBarra(e), { ...op, passive: false });
    const mo = new MutationObserver(() => this.#agendarBarra());
    mo.observe(raiz, { childList: true, subtree: true });
    const ro = new ResizeObserver(() => this.#agendarBarra());
    ro.observe(raiz);
    this.#abortar.signal.addEventListener('abort', () => { mo.disconnect(); ro.disconnect(); cancelAnimationFrame(this.#agendado); });
    this.#agendarBarra();
  }

  // ---- Rolagem lateral do quadro ----

  #agendarBarra() {
    cancelAnimationFrame(this.#agendado);
    this.#agendado = requestAnimationFrame(() => this.#atualizarBarra());
  }

  #atualizarBarra() {
    const kanban = this.#raiz.querySelector('.kanban');
    const barra = this.#raiz.querySelector('.kanban-barra');
    if (!kanban || !barra) return;
    const sobra = kanban.scrollWidth > kanban.clientWidth + 1;
    barra.classList.toggle('visivel', sobra);
    kanban.classList.toggle('pode-rolar', sobra);
    barra.firstElementChild.style.width = kanban.scrollWidth + 'px';
    if (Math.abs(barra.scrollLeft - kanban.scrollLeft) >= 1) barra.scrollLeft = kanban.scrollLeft;
  }

  // Copia o scrollLeft de um para o outro (o scroll que a cópia provoca já encontra os dois iguais e para aí)
  #espelhar(el) {
    if (!(el instanceof Element) || !el.matches('.kanban, .kanban-barra')) return;
    const outro = this.#raiz.querySelector(el.matches('.kanban') ? '.kanban-barra' : '.kanban');
    if (!outro || Math.abs(outro.scrollLeft - el.scrollLeft) < 1) return;
    outro.scrollLeft = el.scrollLeft;
  }

  // Rodinha em cima da barra rola para os lados (no quadro ela continua rolando a página; Shift+rodinha rola para os lados)
  #rodinhaNaBarra(e) {
    if (!e.target.closest?.('.kanban-barra')) return;
    const kanban = this.#raiz.querySelector('.kanban');
    if (!kanban) return;
    e.preventDefault();
    kanban.scrollLeft += Math.abs(e.deltaX) > Math.abs(e.deltaY) ? e.deltaX : e.deltaY;
  }

  // Mouse no quadro fora dos cards e botões (vão entre colunas, título, fundo da coluna): arrastar rola para os lados
  #comecarRolagem(e) {
    if (e.pointerType !== 'mouse') return false;
    const kanban = e.target.closest('.kanban');
    if (!kanban || !kanban.classList.contains('pode-rolar')) return false;
    if (e.target.closest('button, a, input, select, textarea, label')) return false;
    this.#rolagem = { kanban, x: e.clientX, inicio: kanban.scrollLeft, moveu: false };
    e.preventDefault(); // não seleciona texto
    return true;
  }

  #rolar(e) {
    const r = this.#rolagem;
    const dx = e.clientX - r.x;
    if (!r.moveu && Math.abs(dx) < MOVER_PX) return;
    if (!r.moveu) { r.moveu = true; document.documentElement.classList.add('rolando-quadro'); }
    r.kanban.scrollLeft = r.inicio - dx;
  }

  #pararRolagem() {
    const r = this.#rolagem;
    this.#rolagem = null;
    document.documentElement.classList.remove('rolando-quadro');
    if (r?.moveu) {
      this.#engolirClique = true;
      setTimeout(() => { this.#engolirClique = false; }, 0);
    }
  }

  async #chamar(metodo, ...args) {
    try { await this.#dotnet.invokeMethodAsync(metodo, ...args); } catch { /* página fechada */ }
  }

  #apertou(e) {
    if (e.button !== 0 || this.#arrasto) return;
    if (e.target.closest('input, select, textarea')) return;
    const card = e.target.closest('.card-arrastavel[data-id]');
    const aba = card ? null : e.target.closest('.tab[data-quadro]');
    // Coluna: pelo título (os botões de renomear/excluir continuam sendo só clique)
    const coluna = card || aba || e.target.closest('button') ? null : e.target.closest('.coluna-head')?.closest('.coluna[data-coluna]');
    const el = card ?? aba ?? coluna;
    if (!el) { this.#comecarRolagem(e); return; }
    if (!this.#raiz.contains(el)) return;
    this.#arrasto = {
      tipo: card ? 'card' : aba ? 'aba' : 'coluna',
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
    f.removeAttribute('data-coluna');
    f.style.width = r.width + 'px';
    f.style.height = (a.tipo === 'coluna' ? Math.min(r.height, FANTASMA_COLUNA_MAX) : r.height) + 'px';
    if (a.tipo === 'coluna') f.style.overflow = 'hidden';
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
    if (this.#rolagem) { this.#rolar(e); return; }
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
    if (a.tipo === 'card') this.#alvoCard(a);
    else if (a.tipo === 'coluna') this.#alvoColuna(a);
    else this.#alvoAba(a);
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

  // Posição da coluna entre as outras do quadro pelo x do ponteiro; a linha fica no vão entre elas
  #alvoColuna(a) {
    const outras = [...this.#raiz.querySelectorAll('.kanban .coluna[data-coluna]')].filter(c => c !== a.el);
    let indice = outras.findIndex(c => { const r = c.getBoundingClientRect(); return a.px < r.left + r.width / 2; });
    if (indice < 0) indice = outras.length;
    a.alvo = { indice };

    const ref = outras[indice] ?? outras[outras.length - 1] ?? a.el;
    const r = ref.getBoundingClientRect();
    const k = this.#raiz.querySelector('.kanban').getBoundingClientRect();
    const x = outras[indice] ? r.left - 9 : r.right + 6;
    const topo = Math.max(r.top, k.top);
    Object.assign(a.marca.style, { left: x + 'px', top: topo + 'px', width: '3px', height: Math.max(40, Math.min(r.bottom, innerHeight) - topo) + 'px' });
  }

  // Perto das bordas, rola o quadro (para os lados) ou a página (para cima/baixo)
  #rolarSozinho() {
    cancelAnimationFrame(this.#raf);
    const passo = () => {
      const a = this.#arrasto;
      if (!a?.ativo) return;
      let rolou = false;
      const horizontal = this.#raiz.querySelector(a.tipo === 'aba' ? '.tabs' : '.kanban');
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
    if (this.#rolagem) { this.#pararRolagem(); return; }
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
    } else if (a.tipo === 'coluna' && a.alvo) {
      const atual = [...this.#raiz.querySelectorAll('.kanban .coluna[data-coluna]')].indexOf(a.el);
      if (a.alvo.indice !== atual) chamada = this.#chamar(MOVEU_COLUNA, a.el.dataset.coluna, a.alvo.indice);
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
    if (this.#rolagem) { this.#pararRolagem(); return; }
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
