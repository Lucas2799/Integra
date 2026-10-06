#!/usr/bin/env bash
# Teste de fumaça da API: percorre o dia de um entregador contra a API rodando localmente.
# Uso: ./infra/teste-de-fumaca.sh [http://localhost:5080]
# Requer: curl e python3. Usa os serviços gratuitos reais (BrasilAPI, Nominatim, OSRM público).
set -euo pipefail

API="${1:-http://localhost:5080}/api"
TEMP="$(mktemp -d)"
trap 'rm -rf "$TEMP"' EXIT

campo() { python3 -c "import sys,json; d=json.load(sys.stdin); print(eval(sys.argv[1]))" "$1"; }
json() { curl -sf -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' "$@"; }

echo "== Cadastro (ganha Pro de teste)"
TOKEN=$(curl -sf -X POST "$API/autenticacao/cadastrar" -H 'Content-Type: application/json' \
  -d "{\"nome\":\"Teste Fumaça\",\"email\":\"fumaca$RANDOM$RANDOM@teste.com\",\"senha\":\"senha-forte-123\"}" | campo 'd["tokenDeAcesso"]')
json "$API/conta" | campo '"plano:", d["plano"], "teste:", d["emPeriodoDeTeste"]'

echo "== Rota saindo da Praça da Sé (moto)"
ROTA=$(json -X POST "$API/rotas" -d '{"nome":"Fumaça","saida":{"latitude":-23.5505,"longitude":-46.6333,"descricao":"Praça da Sé"},"veiculo":"Moto"}' | campo 'd["resumo"]["id"]')

incluir() { json -X POST "$API/paradas" -d "{\"rotaId\":\"$ROTA\",\"parada\":$1}" | campo '" ", d["endereco"]["descricao"], "| confiança", d["confiancaDaLocalizacao"]'; }
echo "== Paradas (rua+número, CEP+número, texto livre, só pino)"
incluir '{"endereco":{"logradouro":"Avenida Paulista","numero":"1578","cidade":"São Paulo","uf":"SP"}}'
incluir '{"endereco":{"cep":"04538-133","numero":"100"}}'
incluir '{"endereco":{"textoLivre":"Rua Augusta, 900, São Paulo"}}'
incluir '{"endereco":{},"latitude":-23.5874,"longitude":-46.6576}'

echo "== Otimizar"
json -X POST "$API/rotas/$ROTA/otimizar" -d '{}' > "$TEMP/rota.json"
campo '" algoritmo:", d["rota"]["algoritmoUsado"], "| motor:", d["rota"]["motorUsado"], "| km:", round(d["rota"]["resumo"]["distanciaTotalMetros"]/1000,1)' < "$TEMP/rota.json"
PRIMEIRA=$(campo 'd["rota"]["proximaParadaId"]' < "$TEMP/rota.json")

echo "== Destinatário ausente → recálculo automático"
json -X POST "$API/paradas/$PRIMEIRA/nao-entregue" -d '{"motivo":"DestinatarioAusente","estrategia":"FimDaRota"}' >/dev/null
json "$API/rotas/$ROTA" | campo '" recálculos:", d["recalculos"], "| status:", d["resumo"]["status"]'

echo "== Entregar a próxima"
PROXIMA=$(json "$API/rotas/$ROTA" | campo 'd["proximaParadaId"]')
json -X POST "$API/paradas/$PROXIMA/entregar" -d '{"recebidoPor":"Porteiro"}' | campo '" status:", d["status"]'

echo "== Navegação (primeiros passos)"
json "$API/rotas/$ROTA/navegacao?lat=-23.56&lng=-46.65" | campo '[p["instrucao"] for p in d["passos"][:3]]'

echo "== Etiqueta (e de novo, sem duplicar)"
ETIQUETA="{\"rotaId\":\"$ROTA\",\"textoLido\":\"SPX\\nDESTINATÁRIO\\nJOAO SOUZA\\nRua Haddock Lobo, 595\\n01414-001 São Paulo - SP\",\"codigos\":[{\"formato\":\"code128\",\"valor\":\"BR2400000000001\"}]}"
json -X POST "$API/etiquetas/paradas" -d "$ETIQUETA" | campo '" já existia:", d["jaExistia"], "|", d["parada"]["endereco"]["descricao"]'
json -X POST "$API/etiquetas/paradas" -d "$ETIQUETA" | campo '" já existia:", d["jaExistia"]'

echo "== Importação de planilha"
printf 'destinatario;cep;rua;numero;cidade;uf;codigo\nDiego;01310-930;Avenida Paulista;2073;São Paulo;SP;PED1\n' > "$TEMP/entregas.csv"
IMPORTACAO=$(curl -sf -H "Authorization: Bearer $TOKEN" -F "arquivo=@$TEMP/entregas.csv" "$API/importacoes?rotaId=$ROTA" | campo 'd["id"]')
for _ in $(seq 1 30); do
  STATUS=$(json "$API/importacoes/$IMPORTACAO" | campo 'd["status"]'); [[ "$STATUS" == "Concluida" ]] && break; sleep 1
done
json "$API/importacoes/$IMPORTACAO" | campo '" status:", d["status"], "| criadas:", d["paradasCriadas"]'

echo "== Resumo do dia"
json "$API/rastreamento/resumo" | campo '" entregas:", d["entregas"], "| não entregues:", d["naoEntregues"]'
echo "OK"
