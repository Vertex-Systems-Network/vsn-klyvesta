# P1-19 Customer Preferences — 2026-09-14

Scope: API-independent customer-owned preferences and notification-channel settings.

Canonical branch: `parallel/customer-preferences`
Submission PR: `#115`
Accepted integration baseline at implementation start: `481a20e9f93ed029d1bda384d572b8f955fd2afc`
Pre-submission implementation head: `f97a6b08e13f385182c0421ae25060e0fe2e37a7`

Implemented:
- authenticated-customer ownership isolation for reads and mutations;
- privacy-safe defaults with InApp enabled and Email/SMS/Push/WhatsApp disabled until explicit opt-in;
- supported notification-channel validation with unknown/undefined values rejected;
- optimistic revision concurrency plus idempotent repeated updates;
- no contact destination, email address, phone number, address or other contact PII requirement/storage;
- explicit preference-only authority with no notification dispatch, security-policy mutation, order placement or trading authority;
- in-memory non-production store with customer isolation and cancellation handling;
- dedicated 24-case `Klyvesta.CustomerPreferencesVerifier`.

Pre-submission verification on `f97a6b08e13f385182c0421ae25060e0fe2e37a7`:
- `agent-orchestration`: PASS;
- `dotnet-foundation`: PASS;
- formatting: PASS;
- API graph build: PASS;
- all 19 verifier projects build: PASS;
- all verifier runtime sweep: PASS;
- Customer Preferences verifier: 24/24 PASS.

Explicitly excluded:
- API endpoint composition;
- database persistence, migrations or model snapshots;
- provider delivery or live notification dispatch;
- contact PII storage or contact-destination handling;
- live broker/provider network calls or fabricated pyPSX semantics;
- trade execution, order placement, personalized investment advice or real-money authority.

Final acceptance requires the immediate final code-marker head after this checkpoint to pass exact-head `agent-orchestration` and `dotnet-foundation`, with zero unresolved review threads, before integration into `parallel/integration-staging`.
