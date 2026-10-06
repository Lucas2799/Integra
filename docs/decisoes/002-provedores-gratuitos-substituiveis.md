# 002 — Provedores gratuitos atrás de interfaces

**Data:** 2026-10-06 · **Situação:** aceita

## Contexto
APIs pagas de mapas (Google Distance Matrix/Routes) cobram por elemento da matriz. Uma rota de 80 paradas gera
milhares de elementos, o que inviabiliza um plano gratuito.

## Decisão
OSRM, Nominatim, Photon, BrasilAPI/ViaCEP e OR-Tools, cada um atrás de uma interface (`IMotorDeRotas`,
`IGeocodificador`, `IProvedorDeBusca`, `IProvedorDeCep`, `IOtimizadorDeRotas`), com alternativas automáticas
(linha reta e heurística) quando o serviço cai.

## Consequências
- Custo marginal perto de zero por rota.
- Os serviços públicos têm limites de uso justo: antes de escalar, hospedar OSRM/Nominatim próprios.
- A geocodificação gratuita é menos precisa que a do Google; por isso existe confiança por resultado,
  a confirmação do pino pelo entregador e o cache de correções manuais.
