# P0 Public Evidence + Partner Evidence Checklist Checkpoint

Date: 2026-08-26
Canonical task: `P0-T1 — Confirm regulatory and pyPSX Broker API operating model`

## Current repository truth

- `main` before this reconciliation branch: `69449eb33db087dad15491c76f2805ca386e03ad`.
- Issue #1 is OPEN; `main` branch protection is not technically enabled.
- The prior `F0-RISK-001` exception was one-time and is expired for substantive production/security-sensitive implementation.
- P0 discovery/documentation work remains allowed.
- `accepted_phases` remains empty.
- No live Broker API, real-money trading, production PII, personalized production AI advice, regulated AI research, or Guarded Auto capability is authorized.

## P0 evidence completed in this continuation

### Public evidence refresh

Merged to `main` through PR #21:

- `docs/P0_PYPSX_PUBLIC_EVIDENCE_REFRESH_2026-08-26.md`

The refresh records contradictory official public pyPSX rollout evidence: fresher indexed material represented Broker API as live/onboarding partners while directly opened official snapshots still represented early-access / sandbox-coming-soon / live-execution-in-development states.

Public marketing therefore does not establish Broker API production readiness or sandbox availability for Klyvesta.

### Machine-readable partner evidence checklist

Merged to `main` through PR #22:

- `contracts/broker/PYPSX_PARTNER_EVIDENCE_CHECKLIST_V1.yaml`

Exact source blob validated before merge: `bea892b952ab9aea312b2f00a1f0bd566718dacd`.

Local validation using PyYAML 6.0.3 passed:

- YAML parse: PASS;
- required top-level keys: PASS;
- 17 evidence sections;
- 114 evidence fields;
- 0 invalid status values;
- 0 unresolved P0 gate dependency paths;
- direct evidence required for production verification;
- public marketing cannot satisfy production-contract fields;
- contradictory evidence fails closed;
- ambiguous external financial side effect maps to `UNKNOWN`;
- blind retry is disabled;
- LLM broker execution credentials are disabled;
- pre-contract live adapter implementation is disabled;
- all production/live acceptance-policy bypass flags are false.

## Active tracker

Issue #20 — `P0-T1: Resolve pyPSX public rollout-state contradiction and obtain partner contract`.

The last direct partner-response fact remains only the user's confirmation that no pyPSX reply had been received as of 2026-08-25. Do not infer inbox state after that date without direct evidence.

Official public pyPSX documentation exposes `support@pypsx.com` as a contact channel, but no outbound follow-up was sent during this engineering session.

## P0 acceptance impact

No P0 acceptance gate is upgraded by the public refresh or checklist.

Still not met/accepted:

- pyPSX Broker API technical/commercial contract;
- exact underlying regulated broker identity/licence/TREC evidence;
- sandbox and production environment contract;
- authentication/credential semantics;
- order/execution/idempotency/timeout/retry/reconciliation semantics;
- funding and withdrawal flows;
- custody/customer-cash legal model;
- market-data source and redistribution rights;
- AI-assisted legal/partner classification;
- discretionary/Guarded Auto legal/partner path.

## Exact next action

Obtain current direct pyPSX partner technical/commercial evidence and populate `contracts/broker/PYPSX_PARTNER_EVIDENCE_CHECKLIST_V1.yaml` field-by-field. Reconcile verified evidence against the P0 due-diligence matrix, partner RACI, `BROKER_ADAPTER_V1`, funding/custody/market-data flows, threat model and `.ai/acceptance-gates.yaml` before any live integration.
