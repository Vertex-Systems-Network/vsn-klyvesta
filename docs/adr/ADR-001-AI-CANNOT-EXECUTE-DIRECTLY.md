# ADR-001 — AI Cannot Execute Broker Orders Directly

Status: Accepted for planning.

## Decision
LLM/agent components cannot possess broker execution credentials and cannot directly invoke broker order endpoints.

AI produces a structured `OrderIntentProposal`.

A deterministic chain validates:
- identity/account
- authorization
- mandate
- suitability
- market state
- cash/position
- risk
- compliance
- idempotency

Only then can the Execution Validator invoke the Broker Adapter.

## Reason
LLMs are probabilistic and exposed to hallucination, prompt injection, tool misuse and context corruption. Financial execution requires deterministic controls and auditability.
