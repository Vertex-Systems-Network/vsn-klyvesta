# AGENTS.md — Klyvesta AI Engineering Protocol

This repository is governed by `.ai/MASTER_ENGINEERING_PROMPT.md` for engineering process and by the project-specific product/risk rules in `.ai/guardrails.md`, `.ai/acceptance-gates.yaml`, `AI-PLAN.md`, `docs/RISK_COMPLIANCE.md`, `docs/SECURITY.md`, and accepted ADRs.

If a generic engineering preference conflicts with a Klyvesta financial, regulatory, security, or accepted architectural guardrail, the stricter project-specific rule wins.

## Mandatory start-of-session protocol

Before engineering work:

1. Read `AGENTS.md`.
2. Read `.ai/MASTER_ENGINEERING_PROMPT.md`.
3. Read `.ai/state.json`.
4. Read `.ai/guardrails.md`.
5. Read `.ai/acceptance-gates.yaml`.
6. Inspect repository structure and relevant documentation.
7. Inspect current Git HEAD and branch.
8. Inspect recent Git history.
9. Inspect open PRs/issues relevant to the active task.
10. Identify the current checkpoint and unfinished work.
11. Inspect relevant implementation and available dev/test commands.
12. Only then plan or implement.

## Execution rules

- Work only on the active task and prerequisites explicitly required by it.
- Do not silently redefine the roadmap or acceptance criteria.
- Do not trust conversational memory over repository evidence.
- Do not mark work DONE without real acceptance evidence.
- Record failures, blockers, uncertainty, and unverified checks explicitly.
- Keep changes scoped, logical, reviewable, and reversible.
- Use focused, meaningful commits.
- Persist important architectural/security/operational decisions in the repository.
- Update machine-readable state only when the recorded state is actually true.

## Financial / AI authority boundary

No implementation may allow an LLM or AI agent to bypass the Risk Governor, Compliance Gate, Execution Validator, valid customer mandate, suitability rules, reconciliation, or audit recording.

LLMs must not directly hold or use broker execution credentials, write authoritative ledger balances, or treat remembered conversation state as financial truth.

## End-of-session checkpoint

Every meaningful engineering session must end with:

- current HEAD
- active task
- completed work
- research performed
- tests/checks executed
- security review result
- acceptance result
- known failures/risks
- remaining blocker
- exact next action

If an important requirement remains unverified, report the work as PARTIALLY COMPLETE rather than DONE.
