# Klyvesta Guardrails

## Product
- Never claim guaranteed returns or zero loss.
- Never implement binary-options style fixed-win/fixed-loss wagering.
- No leverage/margin/shorts/derivatives in MVP Auto mode.
- No martingale/double-down recovery logic.
- No AI-driven trade outside a valid customer mandate.
- Auto mode must have user pause and system kill switches.

## Architecture
- LLMs cannot hold broker execution credentials.
- LLMs cannot write ledger balances.
- LLM output is untrusted until schema + policy validation.
- Financial calculations use deterministic decimal arithmetic.
- Broker integration is isolated behind BrokerAdapter.
- All financial commands are idempotent.
- All material decisions are auditable.

## Data
- Portfolio/account truth comes from systems of record, never chat memory.
- Prices used for execution checks must come from approved market/broker sources.
- Stale/unknown critical data => do not trade.
- PII access is least privilege.

## Engineering
- No future roadmap work unless required as prerequisite.
- Acceptance criteria cannot be silently changed.
- Security/risk tests are release gates.
