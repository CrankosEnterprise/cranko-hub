// Radares municipais de Piracicaba que fiscalizam velocidade.
// Fonte: lista da prefeitura publicada pelo Jornal de Piracicaba (jan/2026), conferida com o cadastro de aferições do Inmetro
// (dados/inmetro-sp.json, baixado pelo baixar.js), que dá a velocidade, o sentido e os 4 últimos itens (37 a 40).
// Este é o arquivo para mexer quando a prefeitura ligar ou desligar um radar; depois rode `node montar.js`.
//
// Campos:
//   id       número fixo (vira "pira:<id>" no app; não reaproveite o de um radar removido)
//   tipo     "F" fixo, "H" híbrido (velocidade + sinal), "L" lombada eletrônica
//   rua      regex do nome da via, sem acento e em maiúsculas (casa com o OSM e com o CNEFE)
//   numero   número de porta (posição pelo CNEFE do IBGE) ...
//   cruza    ... ou regex da outra via do cruzamento (posição pelo ponto em comum no OSM)
//   andar    metros a andar a partir do ponto, no sentido do trânsito (ex.: "165 m depois da Av. Rio das Pedras")
//   sentido  "BC" bairro → centro, "CB" centro → bairro, null = os dois
//   semSentido  true quando a geometria dá um sentido que contradiz outro radar da mesma rua: avisa nos dois
//   kmh      velocidade do Inmetro (null nas lombadas, que o Inmetro registra como 0)
//   desc     comentário que vai para o .cs

module.exports = {
  cidade: "Piracicaba",
  codigoIbge: "3538709",
  // Praça José Bonifácio: referência para "bairro → centro"
  centro: [-22.7253, -47.6492],
  radares: [
    { id: 1, tipo: "F", rua: "PADUA DIAS", cruza: "SANTOS DUMONT", sentido: "BC", kmh: 60, desc: "Av. Pádua Dias perto da R. Santos Dumont" },
    { id: 2, tipo: "F", rua: "CENTENARIO", numero: 900, sentido: null, kmh: 50, desc: "Av. Centenário, 900 (shopping/bairro e bairro/shopping)" },
    { id: 3, tipo: "F", rua: "CORCOVADO", numero: 915, sentido: "CB", kmh: 50, desc: "Av. Corcovado, oposto ao 915 (C/B)" },
    { id: 4, tipo: "F", rua: "CORCOVADO", numero: 915, sentido: "BC", kmh: 50, desc: "Av. Corcovado, 915 (B/C)" },
    { id: 5, tipo: "F", rua: "PAULO DE MORAES", cruza: "SILVIO", sentido: "CB", kmh: 50, desc: "Av. Dr. Paulo de Moraes perto da R. Prof. Silvio A. de Souza" },
    { id: 6, tipo: "F", rua: "31 DE MARCO|TRINTA E UM DE MARCO", numero: 780, sentido: "CB", kmh: 50, desc: "Av. 31 de Março, perto do 780" },
    { id: 7, tipo: "F", rua: "VOLLET SACHS", cruza: "ECA DE QUEIROZ", sentido: "BC", kmh: 50, desc: "Av. Vollet Sachs perto da R. Eça de Queiróz" },
    { id: 8, tipo: "F", rua: "FAZANARO", cruza: "RIO DAS PEDRAS", andar: 165, sentido: "BC", kmh: 50, desc: "Av. Antonio Fazanaro, 165 m depois da Av. Rio das Pedras" },
    { id: 9, tipo: "F", rua: "31 DE MARCO|TRINTA E UM DE MARCO", numero: 1001, sentido: "BC", kmh: 50, desc: "Av. 31 de Março, 1001" },
    { id: 10, tipo: "F", rua: "RIO DAS PEDRAS", numero: 518, sentido: "CB", kmh: 50, desc: "Av. Rio das Pedras, 518" },
    { id: 11, tipo: "F", rua: "RIO DAS PEDRAS", numero: 611, sentido: "BC", kmh: 50, desc: "Av. Rio das Pedras, 611" },
    { id: 12, tipo: "F", rua: "INDEPENDENCIA", numero: 1524, sentido: "CB", kmh: 50, desc: "Av. Independência, 1524" },
    { id: 13, tipo: "F", rua: "ARMANDO (DE )?SALL?ES", numero: 924, sentido: "BC", kmh: 50, desc: "Av. Armando de Salles Oliveira, 924" },
    { id: 14, tipo: "F", rua: "VOLLET SACHS", numero: 2200, sentido: "CB", kmh: 50, desc: "Av. Vollet Sachs, perto do 2200" },
    { id: 15, tipo: "F", rua: "PADOVANI", numero: 215, sentido: "BC", kmh: 50, desc: "Av. Cássio Paschoal Padovani, 215" },
    { id: 16, tipo: "F", rua: "BRASILIA", numero: 1080, sentido: null, kmh: 50, desc: "Av. Brasília, 1080 (os dois sentidos)" },
    { id: 18, tipo: "L", rua: "GIOCONDA", cruza: "RUI BARBOSA", sentido: null, kmh: null, desc: "R. Gioconda Fiorim Oriani perto da Av. Rui Barbosa (lombada)" },
    { id: 21, tipo: "H", rua: "INDEPENDENCIA", cruza: "CLEOPATH", sentido: "BC", kmh: 50, desc: "Av. Independência x R. Cristiano Cleopath" },
    { id: 22, tipo: "H", rua: "ARMANDO (DE )?SALL?ES", cruza: "XV DE NOVEMBRO|QUINZE DE NOVEMBRO", sentido: "BC", kmh: 50, desc: "Av. Armando de Salles Oliveira x R. XV de Novembro" },
    { id: 23, tipo: "H", rua: "BENJAMIN CONSTANT", cruza: "PAULO DE MORAES", sentido: "CB", kmh: 50, desc: "R. Benjamin Constant x Av. Dr. Paulo de Moraes" },
    { id: 24, tipo: "H", rua: "DONA FRANCISCA", cruza: "SERRA NEGRA", sentido: null, kmh: 50, desc: "Av. Dona Francisca x Av. Barão de Serra Negra" },
    { id: 25, tipo: "H", rua: "VOLLET SACHS", cruza: "RIO DAS PEDRAS", sentido: "CB", kmh: 50, desc: "Av. Vollet Sachs x Av. Rio das Pedras" },
    { id: 26, tipo: "H", rua: "INDEPENDENCIA", cruza: "LUCIANO GUIDOTTI", sentido: "BC", kmh: 50, desc: "Av. Independência, rotatória com a Av. Com. Luciano Guidotti" },
    // O CNEFE não tem números na casa dos 900 da Av. Limeira: 722 e 901 (180 m) viram um ponto só, nos dois sentidos
    { id: 27, tipo: "H", rua: "AVENIDA LIMEIRA|^LIMEIRA", numero: 722, sentido: null, kmh: 50, desc: "Av. Limeira, 722 e 901" },
    { id: 29, tipo: "H", rua: "RIO DAS PEDRAS", cruza: "FERRAI?OLI", sentido: "BC", kmh: 50, desc: "Av. Rio das Pedras x R. Bruno Ferraioli" },
    { id: 30, tipo: "H", rua: "ARMANDO (DE )?SALL?ES", cruza: "TORQUATO", sentido: "BC", kmh: 50, desc: "Av. Armando de Salles Oliveira x Av. Torquato da Silva Leitão" },
    { id: 31, tipo: "H", rua: "ARMANDO (DE )?SALL?ES", cruza: "REGENTE FEIJO", sentido: "CB", kmh: 50, desc: "Av. Armando de Salles Oliveira x R. Regente Feijó" },
    { id: 32, tipo: "H", rua: "DOM PEDRO I$|DOM PEDRO PRIMEIRO", numero: 1145, sentido: "CB", kmh: 50, desc: "R. Dom Pedro I, 1145" },
    // A geometria dá o mesmo rumo que o radar 5 (C/B) na mesma avenida: na dúvida, os dois sentidos
    { id: 33, tipo: "H", rua: "PAULO DE MORAES", cruza: "DA GLORIA", sentido: "BC", semSentido: true, kmh: 50, desc: "Av. Dr. Paulo de Moraes x R. da Glória" },
    { id: 34, tipo: "H", rua: "RAPOSO TAVARES", cruza: "IGUATEMI", sentido: "BC", kmh: 50, desc: "Av. Raposo Tavares x R. Iguatemi" },
    { id: 35, tipo: "H", rua: "INDEPENDENCIA", cruza: "SALDANHA MARINHO", sentido: "CB", kmh: 50, desc: "Av. Independência x R. Saldanha Marinho" },
    { id: 36, tipo: "H", rua: "31 DE MARCO|TRINTA E UM DE MARCO", cruza: "INDEPENDENCIA", sentido: "BC", kmh: 50, desc: "Av. 31 de Março x Av. Independência" },
    { id: 37, tipo: "F", rua: "MADRE MARIA TEODORA", numero: 913, sentido: "CB", kmh: 50, desc: "Av. Madre Maria Teodora, 913" },
    { id: 38, tipo: "F", rua: "RAPOSO TAVARES", numero: 1234, sentido: "BC", kmh: 50, desc: "Av. Raposo Tavares, 1234" },
    { id: 39, tipo: "F", rua: "LUCIANO GUIDOTTI", numero: 1937, sentido: "CB", kmh: 50, desc: "Av. Com. Luciano Guidotti, oposto ao 1937" },
    { id: 40, tipo: "F", rua: "RIO DAS PEDRAS", numero: 3667, sentido: null, kmh: 50, desc: "Av. Rio das Pedras, 3667 (os dois sentidos)" },
  ],
  // Ficaram de fora: as lombadas dos viadutos Francisco Jesuíno Avanzi (SP-304/Rio Claro) e João Carmignani (Babico),
  // que não têm nome no OSM, e os 14 radares de caminhão (não fiscalizam velocidade).
};
