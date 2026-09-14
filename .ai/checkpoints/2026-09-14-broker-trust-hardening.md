# Broker trust hardening checkpoint — 2026-09-14

Scope: API-independent broker/market-data trust controls for Issue #5 while pyPSX partner API evidence remains unavailable under Issue #20.

Implemented:
- provider-neutral signed broker-event verification boundary;
- timestamp/future-skew and event-id/nonce replay protection;
- market-session, quote freshness and reference-deviation validation;
- per-account reconciliation pause and global dependency circuit-breaker semantics;
- explicit verified-recovery requirement before clearing a global pause;
- dedicated auto-discovered BrokerTrust verifier.

Hard boundaries:
- no pyPSX transport or signature algorithm is assumed;
- no production broker endpoint or credential;
- no customer PII;
- no funding/withdrawal provider integration;
- no real-money execution.

Validation target: `dotnet-foundation` exact-head run on this `parallel/**` branch. Any compiler, analyzer or verifier failure must be fixed without suppressing the check.
