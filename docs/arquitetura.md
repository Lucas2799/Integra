# Arquitetura do Intrega

## Visão geral

```
App Flutter ──HTTP/JSON──► Intrega.Api (host) ──► módulos ──► PostgreSQL (1 schema por módulo)
     ▲                         │
     └──────SignalR────────────┘  (/tempo-real: rota recalculada, progresso de importação, posição da frota)
```

O backend é um **monólito modular**: um único processo (simples de manter sozinho), dividido em módulos
com fronteiras rígidas. Se um dia um módulo precisar escalar separado, ele vira um serviço sem reescrita.

## Módulos

| Módulo | Responsabilidade | Schema |
|---|---|---|
| **Identidade** | contas, JWT, organizações (B2B), planos, limites e medição de uso | `identidade` |
| **Geocodificação** | CEP, endereço → coordenada, autocompletar, cache e correções manuais | `geocodificacao` |
| **Paradas** | inclusão de destinos e ciclo de vida (entregue, ausente, adiada, devolvida), comprovante | `paradas` |
| **Roteirização** | planejamento da melhor rota, recálculo, previsão de chegada, navegação, despacho da frota | `roteirizacao` |
| **Importação** | planilhas .xlsx/.csv com reconhecimento de colunas, processadas em segundo plano | `importacao` |
| **Etiquetas** | interpreta o texto (OCR) e os códigos lidos da etiqueta e cria a parada | — |
| **Rastreamento** | posições GPS, contabilização do tempo, produtividade e painel financeiro | `rastreamento` |

### Regras de fronteira (verificadas pelos testes de arquitetura)

1. Um módulo só referencia o projeto **`.Contratos`** de outro módulo, nunca a implementação.
2. Projetos `.Contratos` só dependem de outros `.Contratos` e do `Intrega.Nucleo`.
3. Entidades de domínio e DbContexts são `internal`: só o próprio módulo mexe nas suas tabelas.

### Comunicação entre módulos

- **Consulta síncrona:** interfaces nos contratos (`IModuloParadas`, `IModuloIdentidade`...).
- **Eventos:** `IBarramentoDeEventos` em memória. Exemplos:
  - `EntregaNaoRealizada` → Roteirização recalcula a rota; Rastreamento registra a tentativa.
  - `ParadaEntregue` → Roteirização avança o status da rota; Rastreamento contabiliza.
  - `RotaAtualizada` → o app recebe pelo SignalR e recarrega a tela.
  - `UsuarioExcluido` → todos os módulos apagam os dados do usuário (LGPD).

## Fluxo da otimização

1. O Planejador busca as paradas pendentes e localizadas (`IModuloParadas`).
2. Monta a matriz de tempos com o OSRM (`IMotorDeRotas`; se cair, usa linha reta × 1,35).
3. O OR-Tools resolve em duas fases:
   - paradas normais, com janelas de horário, "tentar após X min" e prioridade;
   - as de "tentar no fim da rota", partindo da última parada.
4. Calcula trechos, previsões de chegada (com espera de janela) e o traçado.

## App Flutter

```
lib/
  app/                 tema, rotas de tela (go_router), widget raiz
  nucleo/              API (Dio + renovação de sessão), sessão segura, banco local (SQLite),
                       sincronizador offline, SignalR, GPS, componentes visuais
  funcionalidades/     uma pasta por área: autenticacao, rotas, paradas, etiquetas,
                       importacao, navegacao, rastreamento, assinatura, frota, conta
```

- **Estado:** Riverpod 3.
- **Offline-first:** a rota aberta fica no SQLite. Entregas e ausências feitas sem sinal entram numa fila e são
  enviadas quando a conexão volta, com o horário real. As posições GPS também ficam acumuladas.
- **Navegação:** no plano gratuito abre o Waze/Google Maps; no Pro, a navegação curva a curva roda no próprio app,
  com voz em português (passos gerados pela API a partir das manobras do OSRM).
