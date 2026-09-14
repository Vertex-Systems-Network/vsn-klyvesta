# P1-06 brokerage trust hardening checkpoint — 2026-09-14

Scope: API-independent broker and market-data trust controls while pyPSX partner API evidence remains unavailable under Issue #20.

Implemented on the canonical registered `parallel/brokerage` lane:
- provider-neutral external event signature-verification boundary;
- timestamp/future-skew and event-id/nonce replay controls;
- market-session, freshness and reference-deviation validation;
- per-account reconciliation pause;
- threshold-based global dependency pause with verified-recovery semantics;
- 16-case `Klyvesta.BrokerTrustVerifier`.

Hard boundary: this code deliberately does not select a pyPSX signature algorithm, endpoint, credential, webhook format, funding path, retry contract or any real-money behavior. Those remain blocked on direct partner evidence.

This evidence-only checkpoint is intended to trigger exact-head repository validation after the implementation and work-item submission metadata are final.