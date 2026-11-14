// Baixa para dados/ o que o montar.js precisa (uns 70 MB; a pasta fica fora do git):
//   cnefe.csv         endereços com coordenada do município (CNEFE do Censo 2022, IBGE)
//   vias.json         ruas do município com geometria (OpenStreetMap, Overpass)
//   inmetro-sp.json   medidores de velocidade aferidos em SP (Inmetro), para conferir velocidade e sentido
//   contorno-*.json   contornos simplificados de São Paulo e da cidade (Nominatim), para o Municipios.cs
// Uso: node baixar.js            (Node 20 ou mais novo; o zip do CNEFE é aberto pelo tar.exe do Windows 10+ ou pelo unzip)
const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");
const lista = require("./piracicaba.js");

const DADOS = path.join(__dirname, "dados");
const UA = { "User-Agent": "CrankoHub/1.0 (ferramentas/radares)" };

async function baixar(url, arquivo, opcoes = {}) {
  const destino = path.join(DADOS, arquivo);
  process.stdout.write(`${arquivo} ← ${url.slice(0, 90)}… `);
  const resp = await fetch(url, { ...opcoes, headers: { ...UA, ...(opcoes.headers || {}) } });
  if (!resp.ok) throw new Error(`${resp.status} em ${url}`);
  fs.writeFileSync(destino, Buffer.from(await resp.arrayBuffer()));
  console.log(`${(fs.statSync(destino).size / 1e6).toFixed(1)} MB`);
  return destino;
}

async function overpass(consulta, arquivo) {
  for (const servidor of ["https://overpass-api.de/api/interpreter", "https://overpass.kumi.systems/api/interpreter", "https://overpass.private.coffee/api/interpreter"]) {
    try {
      return await baixar(servidor, arquivo, {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: "data=" + encodeURIComponent(consulta),
      });
    } catch (e) {
      console.log(`falhou (${e.message}), tentando outro servidor`);
    }
  }
  throw new Error("nenhum servidor do Overpass respondeu");
}

async function contorno(params, arquivo) {
  await baixar(`https://nominatim.openstreetmap.org/${params}&format=json&polygon_geojson=1&polygon_threshold=0.004`, arquivo);
  await new Promise(r => setTimeout(r, 1100)); // limite do Nominatim: 1 por segundo
}

(async () => {
  fs.mkdirSync(DADOS, { recursive: true });

  // CNEFE: a pasta do estado lista um zip por município ("3538709_PIRACICABA.zip")
  const base = "https://ftp.ibge.gov.br/Cadastro_Nacional_de_Enderecos_para_Fins_Estatisticos/Censo_Demografico_2022/Arquivos_CNEFE/CSV/Municipio/35_SP/";
  const indice = await (await fetch(base, { headers: UA })).text();
  const zip = indice.match(new RegExp(`href="(${lista.codigoIbge}[^"]*\\.zip)"`))?.[1];
  if (!zip) throw new Error(`zip do município ${lista.codigoIbge} não achado em ${base}`);
  const arquivoZip = await baixar(base + zip, "cnefe.zip");
  // Windows: o tar.exe do sistema abre zip (o do Git Bash não, e confunde "C:" com um servidor); Linux/macOS: unzip
  if (process.platform === "win32")
    execFileSync(path.join(process.env.SystemRoot || "C:\\Windows", "System32", "tar.exe"), ["-xf", "cnefe.zip"], { cwd: DADOS });
  else
    execFileSync("unzip", ["-o", "-q", "cnefe.zip"], { cwd: DADOS });
  const csv = fs.readdirSync(DADOS).find(f => f.startsWith(lista.codigoIbge) && f.endsWith(".csv"));
  fs.renameSync(path.join(DADOS, csv), path.join(DADOS, "cnefe.csv"));
  fs.rmSync(arquivoZip);

  await overpass(
    `[out:json][timeout:200];area["name"="${lista.cidade}"]["admin_level"="8"]->.p;(way(area.p)["highway"]["name"];way(area.p)["highway"]["bridge"];);out tags geom;`,
    "vias.json");

  await baixar("https://servicos.rbmlq.gov.br/dados-abertos/SP/medidores.json", "inmetro-sp.json");

  await contorno("lookup?osm_ids=R298285", "contorno-sao-paulo.json");
  await contorno(`search?city=${encodeURIComponent(lista.cidade)}&state=S%C3%A3o%20Paulo&country=Brasil&limit=1`, "contorno-cidade.json");

  console.log("Pronto. Agora: node montar.js");
})().catch(e => {
  console.error(e);
  process.exit(1);
});
