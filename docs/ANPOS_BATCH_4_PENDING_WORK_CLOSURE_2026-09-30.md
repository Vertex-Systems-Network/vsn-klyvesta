# ANPOS Batch 4 — Provider-Independent Pending Work

Date: 2026-09-30  
Repository: `Vertex-Systems-Network/vsn-klyvesta`

## Completed in this slice

- Added bounded SSE consumption for the documented pyPSX stream route.
- Added event-name/data normalization with local observation timestamp.
- Added exponential reconnect backoff with a maximum retry budget.
- Added fail-closed termination after the retry budget is exhausted.
- Added a reusable source-timestamp freshness guard.
- Added an in-memory replay window for client-side duplicate request/event keys.

## Deliberate limits

- The stream client does not claim delivery guarantees or fabricate provider sequence numbers.
- Provider acknowledgements, durable idempotency, settlement semantics and live-feed certification still require pyPSX evidence.
- The freshness guard is a safety boundary; callers must provide the provider source timestamp before accepting a quote.
- The replay window is process-local and is not a substitute for provider-side idempotency or durable storage.

## Verification

CI must pass before merge. Provider reply and sandbox stream evidence remain separate acceptance gates.
