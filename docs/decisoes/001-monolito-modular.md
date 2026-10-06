# 001 — Monólito modular em vez de microsserviços

**Data:** 2026-10-06 · **Situação:** aceita

## Contexto
A proposta inicial era de serviços separados (inclusão de destinos, planejador, tempo, importação, leitura de
etiqueta, recálculo e navegação). O projeto é desenvolvido e mantido por uma pessoa, com custo zero no início.

## Decisão
Um único processo .NET (`Intrega.Api`) com módulos isolados: cada um tem projeto, schema no banco e contratos
próprios. As fronteiras são verificadas por testes automáticos.

## Consequências
- Um deploy, um banco e um log: operação simples e barata.
- Sem latência ou falhas de rede entre "serviços".
- Extrair um módulo no futuro exige trocar a chamada direta por HTTP/mensageria, sem reescrever o domínio.
