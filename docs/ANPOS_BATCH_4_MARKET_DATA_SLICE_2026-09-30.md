# ANPOS Batch 4 — Market-Data Adapter Slice

Date: 2026-09-30  
Repository: `Vertex-Systems-Network/vsn-klyvesta`  
Boundary: sandbox-only, authenticated read access

## Implemented

The pyPSX adapter now exposes documented market-data routes through the same normalized result boundary used by account and order calls:

- `GET /v1/partner-api/market/quote/{symbol}`
- `GET /v1/partner-api/market/quotes?symbols=...`
- `GET /v1/partner-api/market/depth/{symbol}?levels=...`
- `GET /v1/partner-api/market/klines/{symbol}?limit=...`
- `GET /v1/partner-api/market/instruments`

The adapter validates symbols, bounds depth/klines query sizes, removes duplicate batch symbols, URI-escapes path/query values, and preserves provider response JSON without inventing prices or timestamps.

## Safety and evidence boundary

- The existing options guard remains sandbox-only and restricts the base host to `brokerapi.paper.pypsx.com`.
- API key headers are attached only to authenticated market-data calls.
- HTTP failures continue to map to `Rejected` or `RetryableFailure`; timeout/network ambiguity remains `Unknown`.
- `is_synthetic` and provider timestamps, when returned, remain provider-owned response fields.
- No live-feed enablement, streaming/SSE, fee calculation, or production authority is added by this slice.

## Verification

Required repository CI must pass before this slice can be treated as merged evidence. README progress must remain at 50% until CI and a sandbox/public endpoint run are recorded. After those pass, the next Batch 4 slice is fees/cost schedule or streaming, depending on provider access.
