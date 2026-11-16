// Gera CrankoHub/Core/Services/Carro/ModoCarro/RadaresPiracicaba.cs (e o Municipios.cs, com os contornos) a partir de
// piracicaba.js e dos arquivos de dados/ (rode antes `node baixar.js`).
//   Posição: número de porta pelo CNEFE (o número mais perto, do mesmo lado da rua; sem um a até 20, interpola entre os
//            vizinhos), encaixado na linha da via do OSM. Cruzamento: o ponto em comum das duas vias no OSM.
//   Sentido: "BC"/"CB" vira graus pela via no ponto e pelo ponto da mesma rua mais perto do centro. Quando a rua contorna o
//            centro (pouco ganho de distância, ou a via quase perpendicular ao rumo do centro), fica null: avisa nos dois.
// No fim, lista os medidores válidos do Inmetro na cidade cuja rua não aparece em piracicaba.js (candidatos a radar novo).
// Uso: node montar.js            (só lê dados/ e escreve os dois .cs)
const fs = require("fs");
const path = require("path");
const lista = require("./piracicaba.js");

const DADOS = path.join(__dirname, "dados");
const SAIDA = path.join(__dirname, "..", "..", "CrankoHub", "Core", "Services", "Carro", "ModoCarro");
const ler = f => fs.readFileSync(path.join(DADOS, f), f.endsWith(".csv") ? "latin1" : "utf8");

// ===== Geometria =====
const R = 6371000, rad = Math.PI / 180;
const norm = s => (s || "").normalize("NFD").replace(/[̀-ͯ]/g, "").toUpperCase().replace(/[^A-Z0-9 ]/g, " ").replace(/\s+/g, " ").trim();
const dist = (a, b) => {
  const x = (b[0] - a[0]) * rad, y = (b[1] - a[1]) * rad;
  const h = Math.sin(x / 2) ** 2 + Math.cos(a[0] * rad) * Math.cos(b[0] * rad) * Math.sin(y / 2) ** 2;
  return 2 * R * Math.asin(Math.sqrt(h));
};
const rumo = (a, b) => {
  const f1 = a[0] * rad, f2 = b[0] * rad, dl = (b[1] - a[1]) * rad;
  return (Math.atan2(Math.sin(dl) * Math.cos(f2), Math.cos(f1) * Math.sin(f2) - Math.sin(f1) * Math.cos(f2) * Math.cos(dl)) / rad + 360) % 360;
};
const difAng = (a, b) => { const d = Math.abs(((a - b) % 360 + 360) % 360); return d > 180 ? 360 - d : d; };

/** Ponto mais perto de p no segmento a-b (plano local). */
function projeta(p, a, b) {
  const k = Math.cos(p[0] * rad);
  const ax = a[1] * k, ay = a[0], bx = b[1] * k, by = b[0], px = p[1] * k, py = p[0];
  const dx = bx - ax, dy = by - ay;
  const t = Math.max(0, Math.min(1, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy || 1)));
  return [ay + t * dy, (ax + t * dx) / k];
}

/** O ponto das vias mais perto de p, com o segmento (para o eixo da via). */
function encaixa(p, vias) {
  let m = null;
  for (const v of vias)
    for (let i = 1; i < v.g.length; i++) {
      const q = projeta(p, v.g[i - 1], v.g[i]);
      const d = dist(p, q);
      if (!m || d < m.d) m = { d, q, a: v.g[i - 1], b: v.g[i], v };
    }
  return m;
}

// ===== Dados =====
const vias = JSON.parse(ler("vias.json")).elements
  .map(e => ({ nome: norm(e.tags.name), tags: e.tags, g: (e.geometry || []).map(p => [p.lat, p.lon]) }))
  .filter(v => v.g.length > 1);
const viasDe = re => vias.filter(v => re.test(v.nome));

const linhas = ler("cnefe.csv").split("\n");
const H = linhas[0].split(";");
const col = n => H.indexOf(n);
const [iTipo, iTitulo, iNome, iNum, iLat, iLon, iNivel] =
  ["NOM_TIPO_SEGLOGR", "NOM_TITULO_SEGLOGR", "NOM_SEGLOGR", "NUM_ENDERECO", "LATITUDE", "LONGITUDE", "NV_GEO_COORD"].map(col);
const cnefe = [];
for (const l of linhas.slice(1)) {
  const c = l.split(";");
  if (c.length < 20 || +c[iNivel] > 2) continue; // nível 1 e 2: coordenada do próprio endereço
  cnefe.push({ nome: norm(`${c[iTipo]} ${c[iTitulo]} ${c[iNome]}`), so: norm(`${c[iTitulo]} ${c[iNome]}`), n: +c[iNum], p: [+c[iLat], +c[iLon]] });
}

// ===== Posição de cada radar =====
function porNumero(re, num) {
  const cand = cnefe.filter(c => re.test(c.nome) || re.test(c.so));
  let melhor = null;
  for (const c of cand) {
    const dn = Math.abs(c.n - num) + (c.n % 2 !== num % 2 ? 0.5 : 0); // prefere o mesmo lado da rua
    if (!melhor || dn < melhor.dn) melhor = { dn, c };
  }
  if (!melhor) return null;
  if (melhor.dn <= 20) return { p: melhor.c.p, obs: `CNEFE ${melhor.c.nome} ${melhor.c.n}` };
  const com = cand.filter(c => c.n > 0);
  const baixo = com.filter(c => c.n < num).sort((a, b) => b.n - a.n)[0];
  const alto = com.filter(c => c.n > num).sort((a, b) => a.n - b.n)[0];
  if (baixo && alto && dist(baixo.p, alto.p) < 1500) {
    const t = (num - baixo.n) / (alto.n - baixo.n);
    return { p: [baixo.p[0] + t * (alto.p[0] - baixo.p[0]), baixo.p[1] + t * (alto.p[1] - baixo.p[1])], obs: `CNEFE interpolado entre ${baixo.n} e ${alto.n}` };
  }
  return { p: melhor.c.p, obs: `ATENÇÃO: CNEFE ${melhor.c.n}, longe do ${num}` };
}

function porCruzamento(vs, re2) {
  const ws = viasDe(re2);
  let m = null;
  for (const a of vs) for (const pa of a.g) for (const b of ws) for (const pb of b.g) {
    const d = dist(pa, pb);
    if (!m || d < m.d) m = { d, pa, pb, na: a.tags.name, nb: b.tags.name };
  }
  if (!m || m.d >= 40) return null;
  return { p: [(m.pa[0] + m.pb[0]) / 2, (m.pa[1] + m.pb[1]) / 2], obs: `${m.na} x ${m.nb}` };
}

/** "BC"/"CB" em graus, ou null quando a rua contorna o centro. */
function sentidoEmGraus(rotulo, q, eixo, vs) {
  let alvo = null;
  for (const v of vs) for (const g of v.g) if (!alvo || dist(g, lista.centro) < dist(alvo, lista.centro)) alvo = g;
  const ganho = alvo ? dist(q, lista.centro) - dist(alvo, lista.centro) : 0;
  const d = difAng(eixo, alvo ? rumo(q, alvo) : rumo(q, lista.centro));
  if (ganho < 60 || (d > 55 && d < 125)) return null;
  const ida = d <= 90 ? eixo : (eixo + 180) % 360;
  return rotulo === "BC" ? ida : (ida + 180) % 360;
}

const pontos = [];
let problemas = 0;
for (const r of lista.radares) {
  const re = new RegExp(r.rua), vs = viasDe(re);
  const achado = r.numero ? porNumero(re, r.numero) : r.cruza ? porCruzamento(vs, new RegExp(r.cruza)) : null;
  if (!achado || !vs.length) {
    console.log(`✗ ${r.id} ${r.desc}: NÃO ACHADO (${vs.length} vias com esse nome)`);
    problemas++;
    continue;
  }
  const e = encaixa(achado.p, vs);
  let q = e.q;
  const eixo = rumo(e.a, e.b);
  let sentido = r.sentido && !r.semSentido ? sentidoEmGraus(r.sentido, q, eixo, vs) : null;
  if (r.andar) {
    const rumoAndar = sentido ?? sentidoEmGraus(r.sentido ?? "BC", q, eixo, vs) ?? eixo;
    const d = r.andar / R / rad;
    q = encaixa([q[0] + d * Math.cos(rumoAndar * rad), q[1] + d * Math.sin(rumoAndar * rad) / Math.cos(q[0] * rad)], vs).q;
  }
  const aviso = r.sentido && !r.semSentido && sentido == null ? " · sentido ambíguo: avisa nos dois" : "";
  console.log(`${achado.obs.startsWith("ATENÇÃO") ? "!" : "✓"} ${String(r.id).padStart(2)} ${r.desc} → ${q[0].toFixed(6)}, ${q[1].toFixed(6)}`
    + ` · ${e.v.tags.name} (${Math.round(e.d)} m) · ${achado.obs} · sentido ${sentido == null ? "os dois" : Math.round(sentido) + "°"}${aviso}`);
  if (achado.obs.startsWith("ATENÇÃO")) problemas++;
  pontos.push({ ...r, lat: q[0], lon: q[1], graus: sentido == null ? null : Math.round(sentido) });
}

// ===== RadaresPiracicaba.cs =====
const hoje = new Date();
const mes = ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"][hoje.getMonth()] + "/" + hoje.getFullYear();
const linhasCs = pontos.map(p =>
  `        new("pira:${p.id}", ${p.lat.toFixed(6)}, ${p.lon.toFixed(6)}, ${p.kmh ?? "null"}, ${p.graus ?? "null"}, ${p.tipo === "L"}), // ${p.desc}`);
fs.writeFileSync(path.join(SAIDA, "RadaresPiracicaba.cs"), `using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Radares municipais de Piracicaba. <b>Arquivo gerado</b> por ferramentas/radares/montar.js a partir de
/// ferramentas/radares/piracicaba.js: para incluir ou tirar um radar, mexa lá e rode de novo (ver o README da pasta).
/// A lista vem da prefeitura (jornal JP, jan/2026), conferida com o cadastro de aferições do Inmetro, que dá a velocidade
/// e o sentido. Posição: número de porta pelo CNEFE do IBGE (Censo 2022), encaixado na via do OpenStreetMap; cruzamento
/// pelo ponto em comum das duas vias. Sentido: "B/C" (bairro → centro) ou "C/B" em graus onde a geometria deixou claro;
/// null = avisa nos dois sentidos. As rodovias que passam pela cidade vêm do DER.
/// <b>Não se atualiza sozinho.</b>
/// </summary>
internal static class RadaresPiracicaba
{
    public const string Versao = "${mes}";

    // Id, latitude, longitude, km/h, sentido do trânsito (graus) e lombada
    private static readonly (string Id, double Lat, double Lon, int? Kmh, double? Sentido, bool Lombada)[] Pontos =
    [
${linhasCs.join("\n")}
    ];

    public static List<RadarSp> Lista() =>
        [.. Pontos.Select(p => new RadarSp(p.Id, p.Lat, p.Lon, p.Kmh, null, FontesRadar.Piracicaba, p.Lombada, p.Sentido))];
}
`);

// ===== Municipios.cs =====
function anel(arquivo) {
  const j = JSON.parse(ler(arquivo))[0];
  const g = j.geojson;
  const ring = g.type === "Polygon" ? g.coordinates[0] : g.coordinates.sort((a, b) => b[0].length - a[0].length)[0][0];
  const pares = ring.map(p => `${p[1].toFixed(4)}, ${p[0].toFixed(4)}`);
  const blocos = [];
  for (let i = 0; i < pares.length; i += 6) blocos.push("        " + pares.slice(i, i + 6).join(", ") + ",");
  return { relacao: j.osm_id, texto: blocos.join("\n") };
}
const sp = anel("contorno-sao-paulo.json"), cidade = anel("contorno-cidade.json");
fs.writeFileSync(path.join(SAIDA, "Municipios.cs"), `namespace CrankoHub.Services;

/// <summary>Contorno simplificado de um município (pares latitude, longitude; ~400 m de precisão, do OpenStreetMap).</summary>
internal sealed class Municipio
{
    private readonly double[] _contorno;
    private readonly double _sul, _norte, _oeste, _leste;

    public Municipio(double[] contorno)
    {
        _contorno = contorno;
        _sul = _norte = contorno[0];
        _oeste = _leste = contorno[1];
        for (var i = 0; i < contorno.Length; i += 2)
        {
            _sul = Math.Min(_sul, contorno[i]);
            _norte = Math.Max(_norte, contorno[i]);
            _oeste = Math.Min(_oeste, contorno[i + 1]);
            _leste = Math.Max(_leste, contorno[i + 1]);
        }
    }

    /// <summary>O ponto está dentro do município (raio cruzando as arestas).</summary>
    public bool Contem(double lat, double lon)
    {
        if (lat < _sul || lat > _norte || lon < _oeste || lon > _leste) return false;
        var dentro = false;
        var n = _contorno.Length / 2;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            double yi = _contorno[2 * i], xi = _contorno[2 * i + 1], yj = _contorno[2 * j], xj = _contorno[2 * j + 1];
            if ((yi > lat) != (yj > lat) && lon < (xj - xi) * (lat - yi) / (yj - yi) + xi) dentro = !dentro;
        }
        return dentro;
    }
}

/// <summary>
/// Cidades onde vale uma lista oficial no lugar do OSM e do Inmetro (ver <see cref="RadaresSp"/>).
/// <b>Arquivo gerado</b> por ferramentas/radares/montar.js.
/// </summary>
internal static class Municipios
{
    /// <summary>São Paulo (relação OSM ${sp.relacao}): vale a CET.</summary>
    public static readonly Municipio SaoPaulo = new(
    [
${sp.texto}
    ]);

    /// <summary>${lista.cidade} (relação OSM ${cidade.relacao}): vale a lista da prefeitura (<see cref="RadaresPiracicaba"/>).</summary>
    public static readonly Municipio Piracicaba = new(
    [
${cidade.texto}
    ]);
}
`);

// ===== Conferência com o Inmetro =====
const validade = m => { const [d, mm, y] = (m.DataValidade || "").split("/"); return new Date(+y, +mm - 1, +d); };
const limite = new Date(hoje.getTime() - 60 * 86400e3); // vencido há até 60 dias: provavelmente esperando a nova aferição
const medidores = JSON.parse(ler("inmetro-sp.json"))
  .filter(m => norm(m.Municipio) === norm(lista.cidade) && m.TipoMedidor !== "Portátil" && validade(m) >= limite)
  .filter(m => !/^(SP[AI]? ?\d|ROD |RODOVIA|ANEL)/.test(norm(m.LocalVerificacao))); // rodovias ("SP308", "SPI162/308"): DER
const fora = new Map();
for (const m of medidores) {
  const local = norm(m.LocalVerificacao);
  if (lista.radares.some(r => new RegExp(r.rua).test(local))) continue;
  const kmh = [...new Set(m.Faixas.map(f => f.VelocidadeNominal))].join("/");
  fora.set(local.slice(0, 90), `${m.LocalVerificacao.trim()} · ${kmh} km/h · válido até ${m.DataValidade}`);
}

console.log(`\n${pontos.length} radares → ${path.relative(path.join(__dirname, "..", ".."), path.join(SAIDA, "RadaresPiracicaba.cs"))}${problemas ? ` (${problemas} para conferir: ✗ ou !)` : ""}`);
if (fora.size) {
  console.log(`\nNo Inmetro e não em piracicaba.js (rua não encontrada na lista): confira se é radar novo`);
  for (const t of fora.values()) console.log("  · " + t);
}
