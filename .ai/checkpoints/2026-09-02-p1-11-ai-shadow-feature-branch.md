# P1-11 AI / Quant Shadow Boundary — Feature-Branch Checkpoint

Status: FEATURE-BRANCH IMPLEMENTATION / PAPER-SHADOW ONLY / NOT P1 ACCEPTED / MAIN MERGE BLOCKED

Canonical issue: #78
Branch: `agent/20260902-vsn-klyvesta-p1-11-ai-shadow`
Stack base: P1-10 exact technically verified head `a6b116671f087fbfca1c4509270b45ac7b8c341e` from draft PR #77.

## Implemented slice

- strict structured AI proposal contract with proposal/context/evidence/model/prompt/freshness/confidence/uncertainty/explanation fields;
- strict JSON parser with unmapped-field rejection;
- deterministic proposal semantic/freshness validation;
- deterministic paper-only target-allocation optimizer using authoritative runtime portfolio and market evidence;
- sequential working-portfolio/activity projection across multi-action plans;
- direct deterministic Risk Governor evaluation for actionable shadow items;
- direct deterministic Compliance Gate evaluation for risk-cleared shadow items;
- Auto-simulation compliance mode requirement for AI shadow planning;
- auditable material proposal/optimizer/risk/compliance/plan outputs;
- AI orchestrator/planner with no broker adapter or credential dependency;
- separate concrete `PaperBrokerAdapter`-only executor with ready-item evidence revalidation and deterministic idempotent order identity;
- deterministic AI shadow verifier wired into dotnet-foundation CI.

## Trust / authority boundary

AI/model output is untrusted proposal data only. It cannot supply authoritative account balances, prices, broker-order IDs, credentials or execute-now commands because unmapped fields are rejected. Explanation text is inert audit content and is not interpreted as control flow.

Proposal confidence/uncertainty does not grant authority. Deterministic runtime portfolio/market evidence drives optimization, followed by Risk and Compliance decisions. Only an item carrying matching Risk ALLOW + Compliance ALLOW evidence can be marked ready for the separate PaperBroker-only executor.

## Explicit non-authority

This branch does not authorize or implement:

- live pyPSX mapping/API/auth/credentials;
- real-money execution;
- production customer PII;
- production LLM/model/provider or model credentials;
- personalized production investment advice;
- production suitability/legal approval;
- production optimizer/business-policy approval;
- authoritative ledger/accounting acceptance;
- production authorization/BOLA acceptance;
- autonomous live trading or production Guarded Auto;
- full P1 acceptance.

## Verification state

The implementation and verifier are candidates only until fresh exact-head PR checks pass. Required evidence includes repository-governance, dotnet-foundation, f2-postgres, CodeQL, AI shadow verifier runtime assertions, zero unresolved review threads and an exact-head non-authorizing self-review.

## Governance blockers

Issue #1 remains OPEN and hosted `main` remains unprotected. Do not merge this stack to `main` unless protected-main enforcement is verified or a new explicit owner risk decision is durably recorded.

Issue #20 / P0-T1 remains independently OPEN awaiting actual partner/API evidence.
