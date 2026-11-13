// Dados de exemplo compartilhados pelos 4 conceitos (valores fictícios, só para o layout)
const D = {
  usuario: "Diego",
  data: "Sexta-feira, 13 de novembro",
  modulos: [
    { id: "dieta", nome: "Dieta", icone: "scale", cor: "#FF2D55" },
    { id: "carro", nome: "Carro", icone: "car", cor: "#007AFF" },
    { id: "assinaturas", nome: "Assinaturas", icone: "repeat", cor: "#AF52DE" },
    { id: "entretenimento", nome: "Entretenimento", icone: "clapperboard", cor: "#FF9500" },
    { id: "tarefas", nome: "Tarefas", icone: "square-check-big", cor: "#34C759" },
    { id: "agenda", nome: "Agenda", icone: "calendar", cor: "#30B0C7" },
  ],
  peso: {
    atual: 82.4, meta: 78, inicio: 89.1, var30: -1.9,
    serie: [89.1, 88.7, 88.9, 88.2, 87.8, 87.9, 87.1, 86.8, 86.9, 86.2, 85.9, 85.6, 85.8, 85.1, 84.7, 84.9, 84.3, 84.0, 83.8, 83.9, 83.3, 83.0, 82.8, 82.9, 82.4],
  },
  carro: {
    nome: "Civic", modelo: "Honda Civic EXL 2.0 · 2019", placa: "FHK-2B47",
    odometro: 68420, kml: 11.8, kmlAnterior: 11.2, custoKm: 0.52, gastoMes: 512.8, tanque: 0.64,
    consumo: [10.9, 11.4, 11.1, 12.3, 11.6, 10.8, 11.9, 12.1, 11.5, 11.2, 12.4, 11.8],
    meses: ["dez", "jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov"],
    abastecimentos: [
      { data: "10 nov", posto: "Shell · Av. Brasil", litros: 38.2, valor: 233.4, comb: "Gasolina", kml: 11.8 },
      { data: "29 out", posto: "Ipiranga · Centro", litros: 41.5, valor: 249.0, comb: "Gasolina", kml: 12.4 },
      { data: "16 out", posto: "Shell · Av. Brasil", litros: 36.8, valor: 221.9, comb: "Gasolina", kml: 11.2 },
      { data: "03 out", posto: "Petrobras · Rod. SP-330", litros: 44.0, valor: 207.2, comb: "Etanol", kml: 8.4 },
      { data: "19 set", posto: "Ipiranga · Centro", litros: 39.1, valor: 238.5, comb: "Gasolina", kml: 11.5 },
    ],
    lembretes: [
      { nome: "Troca de óleo", falta: "1.580 km", pct: 0.79, estado: "atencao" },
      { nome: "Pastilhas de freio", falta: "6.200 km", pct: 0.38, estado: "ok" },
      { nome: "Alinhamento", falta: "3 meses", pct: 0.5, estado: "ok" },
    ],
    documentos: [
      { nome: "IPVA 2027", info: "Vence em 18 dias", estado: "atencao" },
      { nome: "Licenciamento", info: "Pago", estado: "ok" },
      { nome: "Seguro", info: "Até 02/03/2027", estado: "ok" },
      { nome: "CNH", info: "Até 2029", estado: "ok" },
    ],
  },
  assinaturas: [
    { nome: "Netflix", plano: "Padrão", valor: 44.9, dia: 22, cor: "#E50914", cat: "Streaming" },
    { nome: "Spotify", plano: "Família", valor: 34.9, dia: 14, cor: "#1DB954", cat: "Música" },
    { nome: "iCloud+", plano: "200 GB", valor: 16.9, dia: 3, cor: "#3693F3", cat: "Armazenamento" },
    { nome: "ChatGPT", plano: "Plus", valor: 119.9, dia: 27, cor: "#10A37F", cat: "Produtividade" },
    { nome: "Amazon Prime", plano: "Mensal", valor: 19.9, dia: 9, cor: "#00A8E1", cat: "Streaming" },
    { nome: "Game Pass", plano: "Ultimate", valor: 59.99, dia: 18, cor: "#107C10", cat: "Jogos" },
    { nome: "YouTube Premium", plano: "Individual", valor: 26.9, dia: 30, cor: "#FF0033", cat: "Streaming" },
    { nome: "Smart Fit", plano: "Black", valor: 129.9, dia: 5, cor: "#FFB800", cat: "Saúde" },
  ],
  alertas: [
    { modulo: "carro", texto: "IPVA 2027 vence em 18 dias", sub: "R$ 2.148,00 · cota única com 3% de desconto", nivel: "atencao" },
    { modulo: "carro", texto: "Troca de óleo em 1.580 km", sub: "Última em 58.420 km", nivel: "atencao" },
    { modulo: "assinaturas", texto: "Spotify cobra amanhã", sub: "R$ 34,90 no cartão final 4471", nivel: "info" },
    { modulo: "tarefas", texto: "2 tarefas atrasadas", sub: "Quadro Casa", nivel: "serio" },
  ],
  tarefas: { feitas: 3, total: 7, hoje: ["Pagar condomínio", "Revisar orçamento da reforma", "Ligar para a oficina", "Comprar ração"] },
  agenda: [
    { hora: "15:30", titulo: "Dentista", local: "Clínica Sorriso" },
    { hora: "19:00", titulo: "Futsal", local: "Arena Norte" },
  ],
  entretenimento: [
    { titulo: "Duna", tipo: "Livro", progresso: 0.62, sub: "Página 412 de 664", cor1: "#C77B3A", cor2: "#5A2E14" },
    { titulo: "The Bear", tipo: "Série", progresso: 0.45, sub: "T3 · E5", cor1: "#2F4858", cor2: "#0E1A22" },
    { titulo: "Hollow Knight: Silksong", tipo: "Jogo", progresso: 0.3, sub: "22 h jogadas", cor1: "#8B2C3C", cor2: "#1C0A10" },
  ],
  atividade: [
    { icone: "fuel", texto: "38,2 L de gasolina · R$ 233,40", sub: "Civic · 11,8 km/l", quando: "10 nov", modulo: "carro" },
    { icone: "scale", texto: "82,4 kg registrados", sub: "Depois do treino", quando: "09 nov", modulo: "dieta" },
    { icone: "check", texto: "Concluiu “Agendar revisão”", sub: "Quadro Carro", quando: "08 nov", modulo: "tarefas" },
    { icone: "book-open", texto: "Duna: página 412", sub: "Entretenimento", quando: "07 nov", modulo: "entretenimento" },
  ],
};

const fmt = {
  num: (v, c = 1) => v.toLocaleString("pt-BR", { minimumFractionDigits: c, maximumFractionDigits: c }),
  int: (v) => v.toLocaleString("pt-BR"),
  brl: (v) => "R$ " + v.toLocaleString("pt-BR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }),
};
const mod = (id) => D.modulos.find((m) => m.id === id);
const icon = (n, extra = "") => `<i data-lucide="${n}" ${extra}></i>`;
const totalAssin = D.assinaturas.reduce((s, a) => s + a.valor, 0);

// Caminho SVG de uma série (linha e área), escalado para w×h
function caminho(serie, w, h, pad = 4, min = null, max = null) {
  const lo = min ?? Math.min(...serie), hi = max ?? Math.max(...serie);
  const pts = serie.map((v, i) => [pad + (i * (w - 2 * pad)) / (serie.length - 1), pad + (1 - (v - lo) / (hi - lo || 1)) * (h - 2 * pad)]);
  // curva suave (Catmull-Rom → Bézier)
  let d = `M${pts[0][0]},${pts[0][1]}`;
  for (let i = 0; i < pts.length - 1; i++) {
    const p0 = pts[i - 1] || pts[i], p1 = pts[i], p2 = pts[i + 1], p3 = pts[i + 2] || p2;
    const c1 = [p1[0] + (p2[0] - p0[0]) / 6, p1[1] + (p2[1] - p0[1]) / 6];
    const c2 = [p2[0] - (p3[0] - p1[0]) / 6, p2[1] - (p3[1] - p1[1]) / 6];
    d += ` C${c1[0]},${c1[1]} ${c2[0]},${c2[1]} ${p2[0]},${p2[1]}`;
  }
  const area = `${d} L${pts.at(-1)[0]},${h} L${pts[0][0]},${h} Z`;
  return { linha: d, area, ultimo: pts.at(-1), pts };
}

// Monta a tela pelo hash (#inicio, #carro, #assinaturas)
function montar(telas) {
  const t = (location.hash || "#inicio").slice(1);
  document.documentElement.dataset.tela = t;
  document.getElementById("conteudo").innerHTML = (telas[t] || telas.inicio)();
  document.querySelectorAll("[data-nav]").forEach((a) => a.classList.toggle("ativo", a.dataset.nav === t));
  if (window.lucide) lucide.createIcons({ attrs: { "stroke-width": 1.6 } });
}
