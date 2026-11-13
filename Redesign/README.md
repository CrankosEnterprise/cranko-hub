# Redesign — conceitos

Quatro direções visuais para o CrankoHub, com as mesmas telas (Início, Carro, Assinaturas) e os mesmos dados de exemplo.

| Conceito | Ideia | Botões da janela |
|---|---|---|
| **A · Atelier** | Herdeiro do CrankoForge: claro, linhas finas, números grandes e leves, azul-marinho do logo | Semáforo do mac |
| **B · Meia-noite** | Escuro em azul-marinho, trilho de ícones, mosaico de blocos, cada módulo brilha na sua cor | Semáforo do mac (menor) |
| **C · Vitrine** | Claro e colorido, menu em cápsula no topo, cartões tingidos com a cor do módulo | Cápsula à direita (— ▢ ✕) |
| **D · Cockpit** | Painel de instrumentos: grafite, mostradores, barras em segmentos, âmbar | Traço fino, estilo Windows |

- `imagens/00-visao-geral.png` — todos lado a lado
- `imagens/<conceito>-<tela>.png` — cada tela em 3200×2000
- `conceitos/*.html` — fonte de cada conceito (abra no navegador com `#inicio`, `#carro` ou `#assinaturas`)
- `renderizar.sh` — gera os PNGs de novo com o Edge headless
