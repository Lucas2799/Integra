#!/usr/bin/env bash
# Prepara os dados do OSRM próprio a partir do OpenStreetMap (gratuito).
#
# Uso: ./preparar-osrm.sh [regiao]
#   regiao: sudeste (padrão), sul, nordeste, norte, centro-oeste ou brasil
#
# Requisitos aproximados: sudeste ~ 8 GB de RAM e 15 min; brasil inteiro ~ 16 GB e 1 h.
set -euo pipefail

REGIAO="${1:-sudeste}"
PASTA="$(cd "$(dirname "$0")" && pwd)/dados"
IMAGEM="ghcr.io/project-osrm/osrm-backend:v5.27.1"
MOTOR_DE_CONTAINER="$(command -v docker || command -v podman)"

if [[ "$REGIAO" == "brasil" ]]; then
  URL="https://download.geofabrik.de/south-america/brazil-latest.osm.pbf"
else
  URL="https://download.geofabrik.de/south-america/brazil/${REGIAO}-latest.osm.pbf"
fi

mkdir -p "$PASTA"
echo ">> Baixando mapa: $URL"
curl -L --fail -o "$PASTA/regiao.osm.pbf" "$URL"

echo ">> Extraindo malha viária (perfil carro)"
"$MOTOR_DE_CONTAINER" run --rm -v "$PASTA:/dados" "$IMAGEM" osrm-extract -p /opt/car.lua /dados/regiao.osm.pbf
echo ">> Particionando"
"$MOTOR_DE_CONTAINER" run --rm -v "$PASTA:/dados" "$IMAGEM" osrm-partition /dados/regiao.osrm
echo ">> Customizando"
"$MOTOR_DE_CONTAINER" run --rm -v "$PASTA:/dados" "$IMAGEM" osrm-customize /dados/regiao.osrm

echo ">> Pronto. Suba com: docker compose --profile osrm up -d"
echo "   e defina OSRM_CARRO=http://osrm-carro:5000/ no arquivo infra/.env"
