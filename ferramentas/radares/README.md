# Radares de Piracicaba (lista própria do Modo Carro)

O aviso de radar do Modo Carro (`CrankoHub/Services/Carro/ModoCarro/RadaresSp.cs`) baixa sozinho os radares do DER, da CET, do
Inmetro/rodovias federais (Pardal) e do OpenStreetMap. Em **Piracicaba** a prefeitura não publica coordenadas, então a lista
vem dentro do app, em `RadaresPiracicaba.cs`, **gerada por estes scripts**. Dentro da cidade ela substitui o OSM e o Inmetro;
o DER continua valendo nas rodovias.

| Arquivo | O que faz |
|---|---|
| `piracicaba.js` | **A lista**: rua, número ou cruzamento, sentido (B/C ou C/B), velocidade. É o único arquivo para editar. |
| `baixar.js` | Baixa para `dados/` (fora do git, ~70 MB): endereços do CNEFE/IBGE, ruas do OSM, cadastro do Inmetro e os contornos. |
| `montar.js` | Acha a coordenada e o sentido de cada radar e gera `RadaresPiracicaba.cs` e `Municipios.cs`. No fim, lista os medidores do Inmetro na cidade que não estão em `piracicaba.js`. |

## Como atualizar

Precisa do Node 20 ou mais novo.

```
cd ferramentas/radares
node baixar.js      # só na primeira vez ou para atualizar os dados (CNEFE, OSM, Inmetro)
node montar.js
```

1. **Radar novo:** inclua uma linha em `piracicaba.js`, com um `id` que ainda não existe, a `rua` (regex sem acento, em
   maiúsculas), o `numero` **ou** o `cruza` (a outra rua), o `sentido` (`"BC"`, `"CB"` ou `null`) e o `kmh`. A velocidade e o
   sentido oficiais estão no cadastro do Inmetro: o `montar.js` mostra no fim os medidores que ainda não estão na lista.
2. **Radar desligado:** apague a linha e não reaproveite o `id`.
3. Rode `node montar.js` e confira a saída. `✓` é um ponto achado. `!` é um número longe do pedido (o CNEFE não tem esse
   número). `✗` é um ponto não achado: confira o nome da rua no OSM. "Sentido ambíguo" quer dizer que a rua contorna o
   centro e o radar vai avisar nos dois sentidos; se ficar errado, use `semSentido: true`.
4. Compile o app e faça o commit do `piracicaba.js` e dos dois `.cs` gerados.

## Como a posição e o sentido são calculados

- **Número de porta:** o endereço do CNEFE (Censo 2022) com o número mais perto, do mesmo lado da rua; sem nenhum a até 20
  números, interpola entre os vizinhos. O ponto é encaixado na linha da via do OSM.
- **Cruzamento:** o ponto em comum das duas vias no OSM (até 40 m de distância entre elas).
- **Sentido:** "bairro → centro" é o lado da via que leva ao ponto da mesma rua mais perto do centro (Praça José Bonifácio).
  Quando a rua contorna o centro, ou passa quase de lado para ele, o sentido fica `null` e o radar avisa nos dois.
- Para outra cidade (Campinas, Limeira...): copie `piracicaba.js`, troque `cidade`, `codigoIbge` e `centro`, e ajuste o
  `montar.js` para gerar outra classe. O app precisa de uma fonte e de um contorno novos em `RadaresSp.cs` e `Municipios.cs`.
