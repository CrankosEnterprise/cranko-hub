'use strict';

// Agenda: cliques e arrastos na grade de horas (visões Dia e Semana). Carregado como módulo pelo AgendaInterop (C#).
// - clique num horário vazio  → Clicou(dia, minutos)                    (cria um evento ali)
// - clique num evento         → Abriu(id, data)                         (abre o editor)
// - tarefa com horário (.ag-tarefa-bloco) não arrasta nem cria evento: o clique fica com o Blazor
// - arrastar o evento         → Soltou({ id, data, inicio, fim })       (minutos que o início e o fim andaram)
// - puxar a alça de baixo     → Soltou com inicio = 0                   (muda só a duração)
// No toque, o arrasto começa depois de segurar o evento um instante; antes disso o dedo só rola a grade.

const CLICOU = 'Clicou';
const ABRIU = 'Abriu';
const SOLTOU = 'Soltou';

const ENCAIXE = 15;          // minutos
const MOVER_PX = 4;          // abaixo disso é clique
const SEGURAR_MS = 380;      // toque: tempo segurando para começar a arrastar

class GradeAgenda {
  #dotnet;
  #grade;
  #pxPorMinuto;
  #abortar = new AbortController();
  #arrasto = null;
  #engolirClique = false;

  constructor(grade, dotnet, pxPorHora) {
    this.#grade = grade;
    this.#dotnet = dotnet;
    this.#pxPorMinuto = pxPorHora / 60;
    const op = { signal: this.#abortar.signal };
    grade.addEventListener('pointerdown', e => this.#apertou(e), op);
    window.addEventListener('pointermove', e => this.#moveu(e), op);
    window.addEventListener('pointerup', e => this.#soltou(e), op);
    window.addEventListener('pointercancel', () => this.#cancelar(), op);
    grade.addEventListener('click', e => this.#clicou(e), { ...op, capture: true });
    // Durante o arrasto no toque, o dedo não rola a grade
    grade.addEventListener('touchmove', e => { if (this.#arrasto?.ativo) e.preventDefault(); }, { ...op, passive: false });
  }

  async #chamar(metodo, ...args) {
    try { await this.#dotnet.invokeMethodAsync(metodo, ...args); } catch { /* página fechada */ }
  }

  #apertou(e) {
    if (e.button !== 0) return;
    const ev = e.target.closest('.ag-evento[data-id]');
    if (!ev || !this.#grade.contains(ev)) return;
    const coluna = ev.closest('.ag-dia');
    const colunas = [...this.#grade.querySelectorAll('.ag-dia')];
    this.#arrasto = {
      ev, coluna, colunas,
      indice: colunas.indexOf(coluna),
      redimensionar: !!e.target.closest('.ag-alca'),
      x: e.clientX, y: e.clientY,
      altura: ev.offsetHeight,
      alturaOriginal: ev.style.height,  // o height inline que o Blazor pôs (para restaurar)
      alturaArrasto: null,              // o height que o arrasto pôs
      ativo: false, dias: 0, minutos: 0,
      toque: e.pointerType === 'touch',
      timer: null,
    };
    if (this.#arrasto.toque) {
      this.#arrasto.timer = setTimeout(() => this.#ativar(), SEGURAR_MS);
    }
  }

  #ativar() {
    const a = this.#arrasto;
    if (!a || a.ativo) return;
    a.ativo = true;
    a.ev.classList.add('arrastando');
    this.#grade.classList.add('arrastando');
    if (a.toque) navigator.vibrate?.(15);
  }

  #moveu(e) {
    const a = this.#arrasto;
    if (!a) return;
    const dx = e.clientX - a.x, dy = e.clientY - a.y;
    if (!a.ativo) {
      if (Math.hypot(dx, dy) < MOVER_PX) return;
      // No toque, mexer antes de segurar é rolagem: desiste do arrasto
      if (a.toque) { this.#cancelar(); return; }
      this.#ativar();
    }
    e.preventDefault();
    a.minutos = Math.round(dy / this.#pxPorMinuto / ENCAIXE) * ENCAIXE;
    if (a.redimensionar) {
      const minimo = ENCAIXE * this.#pxPorMinuto;
      a.ev.style.height = Math.max(minimo, a.altura + a.minutos * this.#pxPorMinuto) + 'px';
      a.alturaArrasto = a.ev.style.height; // lido de volta: o navegador pode arredondar
    } else {
      // Dia: a coluna na altura horizontal do ponteiro. Não usa elementFromPoint: debaixo do ponteiro
      // quase sempre está o próprio evento arrastado, que ainda mora na coluna original — o dia "voltava".
      const i = this.#colunaEm(a, e.clientX);
      a.dias = i - a.indice;
      const dx = a.colunas[i].getBoundingClientRect().left - a.coluna.getBoundingClientRect().left;
      a.ev.style.transform = `translate(${dx}px, ${a.minutos * this.#pxPorMinuto}px)`;
    }
    this.#mostrarHorario(a);
  }

  // Índice da coluna de dia sob o x da tela; fora da grade, fica na primeira/última
  #colunaEm(a, x) {
    let i = 0;
    for (; i < a.colunas.length - 1; i++) {
      if (x < a.colunas[i].getBoundingClientRect().right) break;
    }
    return i;
  }

  // Atualiza o horário escrito no evento enquanto arrasta
  #mostrarHorario(a) {
    const campo = a.ev.querySelector('.ag-ev-hora');
    const ini = Number(a.ev.dataset.ini), fim = Number(a.ev.dataset.fim);
    if (!campo || Number.isNaN(ini) || Number.isNaN(fim)) return;
    const novoIni = a.redimensionar ? ini : ini + a.minutos;
    const novoFim = Math.max(novoIni + ENCAIXE, fim + a.minutos);
    campo.textContent = `${hora(novoIni)} – ${hora(novoFim)}`;
  }

  async #soltou() {
    const a = this.#arrasto;
    if (!a) return;
    clearTimeout(a.timer);
    this.#arrasto = null;
    if (!a.ativo) return; // foi um clique: o evento "click" cuida
    this.#engolirClique = true;
    setTimeout(() => { this.#engolirClique = false; }, 0);
    a.ev.classList.remove('arrastando');
    this.#grade.classList.remove('arrastando');
    const total = a.dias * 1440 + a.minutos;
    if (a.redimensionar ? a.minutos === 0 : total === 0) { this.#limpar(a); return; }
    // Mantém o evento onde foi solto até o Blazor redesenhar no lugar novo
    // (antes ele piscava de volta no dia antigo). Se a pessoa cancelar a pergunta "só este/todos", volta.
    await this.#chamar(SOLTOU, {
      id: a.ev.dataset.id,
      data: a.ev.dataset.data,
      inicio: a.redimensionar ? 0 : total,
      fim: total,
    });
    this.#limpar(a);
  }

  #cancelar() {
    const a = this.#arrasto;
    if (!a) return;
    clearTimeout(a.timer);
    this.#arrasto = null;
    this.#limpar(a);
  }

  #limpar(a) {
    a.ev.classList.remove('arrastando');
    this.#grade.classList.remove('arrastando');
    a.ev.style.transform = '';
    // Só desfaz a altura se ainda for a do arrasto: se o Blazor já redesenhou, a altura nova é dele
    if (a.alturaArrasto !== null && a.ev.style.height === a.alturaArrasto) a.ev.style.height = a.alturaOriginal;
  }

  #clicou(e) {
    if (this.#engolirClique) { e.stopPropagation(); e.preventDefault(); return; }
    if (e.target.closest('.ag-tarefa-bloco')) return; // tarefa na grade: o Blazor abre a tarefa
    const ev = e.target.closest('.ag-evento[data-id]');
    if (ev) {
      e.stopPropagation();
      this.#chamar(ABRIU, ev.dataset.id, ev.dataset.data);
      return;
    }
    const coluna = e.target.closest('.ag-dia');
    if (!coluna || !this.#grade.contains(coluna)) return;
    const y = e.clientY - coluna.getBoundingClientRect().top;
    const minutos = Math.max(0, Math.min(1410, Math.floor(y / this.#pxPorMinuto / 30) * 30)); // meia em meia hora
    this.#chamar(CLICOU, coluna.dataset.dia, minutos);
  }

  rolar(minutos) {
    this.#grade.scrollTop = Math.max(0, minutos * this.#pxPorMinuto);
  }

  descartar() {
    this.#abortar.abort();
    this.#arrasto = null;
  }
}

function hora(min) {
  min = ((min % 1440) + 1440) % 1440;
  return `${String(Math.floor(min / 60)).padStart(2, '0')}:${String(min % 60).padStart(2, '0')}`;
}

let atual = null;

export function iniciar(grade, dotnet, pxPorHora, rolarMinutos) {
  atual?.descartar();
  atual = new GradeAgenda(grade, dotnet, pxPorHora);
  atual.rolar(rolarMinutos);
}

export function rolar(minutos) {
  atual?.rolar(minutos);
}

export function descartar() {
  atual?.descartar();
  atual = null;
}
