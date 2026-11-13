#!/usr/bin/env bash
# Gera os PNGs dos conceitos com o Edge headless (2x, 1600×1000).
# Uso: ./renderizar.sh            → todos
#      ./renderizar.sh a-atelier  → só um conceito
set -e
cd "$(dirname "$0")"
EDGE="/c/Program Files (x86)/Microsoft/Edge/Application/msedge.exe"
BASE="file:///$(cygpath -m "$PWD")/conceitos"
mkdir -p imagens
conceitos=${1:-"a-atelier b-meia-noite c-vitrine d-cockpit"}
for c in $conceitos; do
  for tela in inicio carro assinaturas; do
    out="$(cygpath -w "$PWD/imagens/$c-$tela.png")"
    "$EDGE" --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=2 \
      --window-size=1600,1000 --virtual-time-budget=8000 --screenshot="$out" "$BASE/$c.html#$tela" 2>/dev/null
    echo "ok  imagens/$c-$tela.png"
  done
done
