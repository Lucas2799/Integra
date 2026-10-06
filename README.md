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

Pré-requisitos: .NET SDK 10, Flutter 3.44+, Docker ou Podman.

```bash
# 1. Banco de dados
cd infra && docker compose up -d banco

# 2. API (aplica as migrations sozinha) → documentação em http://localhost:5080/documentacao
cd ../backend && dotnet run --project src/Intrega.Api

# 3. Conferir o fluxo completo do entregador
../infra/teste-de-fumaca.sh

# 4. App (emulador Android enxerga o computador em 10.0.2.2)
cd ../mobile/intrega && flutter run --dart-define=URL_API=http://10.0.2.2:5080
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
