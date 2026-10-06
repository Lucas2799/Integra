# 003 — Endereço da etiqueta via OCR, não via QR code

**Data:** 2026-10-06 · **Situação:** aceita (validar com etiquetas reais)

## Contexto
A ideia original era obter o endereço escaneando o QR code do pacote. Nas etiquetas de marketplace o QR/código de
barras normalmente traz apenas o identificador do envio (ex.: JSON com `id` e `sender_id` no Mercado Livre).

## Decisão
O app fotografa a etiqueta e, no próprio celular (Google ML Kit), lê os códigos **e** o texto impresso. A API
interpreta o texto: separa destinatário e remetente, extrai CEP, rua, número e cidade, e confere tudo com a
BrasilAPI. O código do pacote evita duplicar a parada.

## Consequências
- Funciona com qualquer marketplace, sem depender de API privada.
- O OCR pode errar: a API devolve uma confiança e o app sempre mostra os campos para o entregador conferir.
- As regras foram escritas com etiquetas sintéticas e precisam ser ajustadas com fotos reais.
