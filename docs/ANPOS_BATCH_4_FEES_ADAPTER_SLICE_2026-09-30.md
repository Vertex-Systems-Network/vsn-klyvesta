# ANPOS Batch 4 — Fees and Cost Adapter Slice

Date: 2026-09-30  
Repository: `Vertex-Systems-Network/vsn-klyvesta`  
Boundary: sandbox-only, provider-priced read access

## Implemented

The pyPSX adapter now exposes:

- `GET /v1/partner-api/fees`

The response remains provider-owned JSON so the application can consume the effective key-specific schedule, including:

- fee model and currency;
- advance-fee rate and applicability;
- commission rate and source;
- SST rate and commission base;
- CGT rates and settlement timing;
- provider formulas and worked examples;
- execution metadata.

No commission, tax, or fee rate is hardcoded in the adapter.

## Safety and evidence boundary

- The existing options guard remains sandbox-only and restricts the base host.
- API-key headers are attached only to the authenticated fees request.
- HTTP failures retain the normalized result states already used by the adapter.
- The application must treat the provider response as the source of truth for the active key.
- This slice does not authorize live trading, fee mutation, production promotion, or regulatory claims.

## Verification

Required repository CI and a real sandbox/public endpoint response must pass before README progress is increased. Until then Batch 4 remains recorded at 50%.
