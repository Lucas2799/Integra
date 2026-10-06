# Intrega

App **freemium** de roteirização para entregadores de marketplaces (Shopee, Mercado Livre, Amazon...)
e pequenas transportadoras. Calcula a melhor ordem de entrega, recalcula quando o destinatário não está,
lê a etiqueta do pacote, importa planilhas e guia o entregador até a porta.

| Pasta | O que tem |
|---|---|
| `backend/` | API em **.NET 10**: monólito modular, PostgreSQL, OR-Tools, OSRM |
| `mobile/intrega/` | App **Flutter** (Android/iOS), offline-first |
| `infra/` | `docker-compose`, script do OSRM próprio e teste de fumaça |
| `docs/` | Arquitetura e decisões técnicas |

## Rodando localmente

Há duas formas de subir o backend. As duas usam a porta **5080** e aplicam as migrations do banco sozinhas.

### Opção A: tudo em container (só precisa de Docker ou Podman)

Indicada para testar o app ou a API sem instalar o .NET.

```bash
cd infra
cp .env.exemplo .env                 # opcional: senha do banco, chave JWT, ambiente
docker compose up -d --build         # sobe Postgres + API (o primeiro build leva alguns minutos)
docker compose ps                    # os dois devem estar "Up"; o banco como "healthy"
curl http://localhost:5080/saude     # deve responder "Healthy"
```

- Documentação interativa da API: http://localhost:5080/documentacao
- Logs da API: `docker compose logs -f api`
- Depois de mudar o código do backend: `docker compose up -d --build api`
- Parar tudo: `docker compose down` (os dados ficam no volume; `docker compose down -v` apaga também o banco)

Por padrão o container roda em `Development`, com a documentação e as compras de teste liberadas.
Em servidor, defina `AMBIENTE=Production` e uma `CHAVE_JWT` forte no `infra/.env`.

> O build da imagem baixa o SDK do .NET (~1 GB) e compila tudo do zero: em máquinas com pouca memória,
> feche a API rodando localmente antes de construir.

### Opção B: API pelo .NET (melhor para desenvolver o backend)

Pré-requisito: .NET SDK 10. Inicia mais rápido e permite depurar pela IDE.

```bash
cd infra && docker compose up -d banco               # só o banco em container
cd ../backend && dotnet run --project src/Intrega.Api
```

Não rode as duas opções ao mesmo tempo: ambas usam a porta 5080. Pare o container da API antes
(`docker compose stop api`).

### Conferir e rodar o app

```bash
# Percorre o dia de um entregador contra a API que estiver no ar (opção A ou B)
./infra/teste-de-fumaca.sh                     # ou: ./infra/teste-de-fumaca.sh http://outro-endereco:5080

# App Flutter (precisa do Flutter 3.44+). O emulador Android enxerga o computador em 10.0.2.2
cd mobile/intrega && flutter run --dart-define=URL_API=http://10.0.2.2:5080
#    Celular físico: use o IP do computador na rede, ex.: --dart-define=URL_API=http://192.168.0.10:5080
```

### Testes

```bash
cd backend && dotnet build -m:1 && dotnet test --no-build   # 80 testes (unitários + arquitetura)
cd mobile/intrega && flutter analyze && flutter test
```

> Não passe `-m:N` direto para o `dotnet test`: com xUnit v3 o argumento vai para o executor e nenhum teste roda.

## Custo zero (e como trocar depois)

Tudo roda com serviços gratuitos, cada um atrás de uma interface para trocar sem mexer no resto:

| Necessidade | Hoje (grátis) | Trocar por | Onde |
|---|---|---|---|
| Matriz de tempos e traçado | OSRM público (routing.openstreetmap.de) | OSRM próprio (`infra/osrm`), Google Routes, HERE | `IMotorDeRotas` |
| Otimização da ordem | Google OR-Tools (Apache 2.0) | — | `IOtimizadorDeRotas` |
| CEP | BrasilAPI → ViaCEP | qualquer | `IProvedorDeCep` |
| Endereço → coordenada | Nominatim (OpenStreetMap) | Nominatim próprio, Google, HERE | `IGeocodificador` |
| Autocompletar | Photon (komoot) | Google Places, Mapbox | `IProvedorDeBusca` |
| OCR e QR da etiqueta | Google ML Kit (no celular) | — | app |
| Mapa no app | tiles do OpenStreetMap | MapTiler, Stadia, servidor próprio | `URL_TILES` |
| Eventos entre módulos | em memória | RabbitMQ/Azure Service Bus | `IBarramentoDeEventos` |
| Fotos de comprovante | disco local | S3, Cloudflare R2 | `IArmazenamentoDeArquivos` |
| Validação de compras | modo de teste (`dev-*`) | Google Play/App Store/RevenueCat | `IVerificadorDeCompraNaLoja` |

**Antes de lançar:** os serviços públicos (OSRM, Nominatim, tiles OSM) têm política de uso justo.
Com usuários reais, suba o OSRM próprio (`infra/osrm/preparar-osrm.sh`) e troque os tiles do mapa.

## Planos (freemium)

Definidos em um único lugar: `backend/src/Modulos/Identidade/Intrega.Modulos.Identidade.Contratos/Planos.cs`.

| Recurso | Gratuito | Pro | Frota |
|---|---|---|---|
| Paradas por rota | 25 | ilimitado | ilimitado |
| Otimizações por dia | 2 | ilimitado | ilimitado |
| Leituras de etiqueta por dia | 10 | ilimitado | ilimitado |
| Recálculos por rota | 1 | ilimitado | ilimitado |
| Importar planilha, janelas de horário, navegação no app, foto de comprovante, relatório de 30 dias | — | ✓ | ✓ |
| Gestão de frota (convites, despacho, mapa ao vivo) | — | — | ✓ |

Novos usuários ganham 7 dias de Pro; novas organizações, 14 dias de Frota.

## O que ainda falta para publicar

- **Compras nas lojas:** integrar `in_app_purchase` ou RevenueCat no app e implementar `IVerificadorDeCompraNaLoja`.
- **Etiquetas reais:** o interpretador foi escrito com etiquetas sintéticas. Ao ter fotos reais de Shopee/Mercado Livre,
  adicione o texto do OCR como casos em `backend/tests/Intrega.TestesUnitarios/Etiquetas` e ajuste as regras.
- **Rastreamento em segundo plano:** hoje o GPS é registrado com o app aberto. Para registrar com a tela apagada,
  configure o serviço em primeiro plano do Android (as lojas exigem justificativa para localização em segundo plano).
- Política de privacidade (LGPD), ícone, splash e chaves de produção (`CHAVE_JWT`).
