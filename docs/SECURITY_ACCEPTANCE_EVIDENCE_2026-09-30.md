# Security Acceptance Evidence — 2026-09-30

Repository: `Vertex-Systems-Network/vsn-klyvesta`

## Decision

**Repository-owned non-live security acceptance: 70% — technically evidenced foundation.**

The repository contains the security contracts, fail-closed boundaries and CI checks needed for the non-live/paper-shadow scope. This document records the evidence mapping and the remaining gates. It does not authorize production, real-money trading, customer KYC, custody, funding, or broker promotion.

## Evidence matrix

| Area | Repository evidence | Result |
| --- | --- | --- |
| Authentication and session requirements | `docs/SECURITY.md`, `docs/AUTH_SESSION_ARCHITECTURE_V1.md` | Specified; runtime production evidence pending |
| Authorization and privilege separation | `docs/AUTHORIZATION_PRIVILEGE_MODEL.md`, `config/operations/access-matrix.json` | Deny-by-default control package present |
| Maker-checker / break-glass | `docs/AUTHORIZATION_PRIVILEGE_MODEL.md`, `config/operations/promotion-gates.json` | Two-person approval and audit requirements present |
| AI execution boundary | `docs/adr/ADR-001-AI-CANNOT-EXECUTE-DIRECTLY.md`, broker adapter contracts | Non-live boundary preserved |
| Replay/idempotency/reconciliation | `docs/ANPOS_BATCH_5_RELIABILITY_RECONCILIATION_2026-09-30.md`, persistence tests | Repository-owned evidence complete |
| External input and provider boundary | `docs/THREAT_MODEL.md`, PyPSX adapter guards and negative tests | Fail-closed boundary present; provider certification pending |
| Secret-safe CI and static analysis | `.github/workflows/codeql.yml`, repository governance workflow | CodeQL/security checks passed on PR #183 and PR #184 |
| Database integrity | F2 PostgreSQL checkpoint, migration and SQL constraint suite | Non-live technical acceptance complete |
| Production governance | `docs/governance/MAIN_PROTECTION.md`, protection scripts | Contract documented; hosted ruleset evidence pending |
| Runtime penetration/incident/DR evidence | Security contract and threat model | Not complete; requires runtime environment and approved test evidence |
| Provider/legal/regulatory acceptance | `.ai/acceptance-gates.yaml`, broker evidence checklist | External blocker; not available in repository |

## Zero-tolerance boundary

The following remain fail-closed and must stay at zero:

- unauthorized financial command;
- cross-customer resource access;
- AI direct broker execution;
- duplicate financial effect from replay;
- secret or restricted PII leakage into logs;
- ledger imbalance.

## Remaining completion gates

Security Acceptance cannot be marked 100% until all of these are evidenced:

1. Hosted GitHub branch protection/ruleset enforcement is verified.
2. Runtime security test/penetration evidence is attached to the target deployment.
3. Incident response and disaster-recovery exercises pass.
4. Provider sandbox/production security requirements are confirmed.
5. Legal/regulatory and broker operating-model approvals are recorded.

Conclusion: the repository-owned security foundation is real and evidenced at 70%; production security acceptance remains intentionally blocked.
