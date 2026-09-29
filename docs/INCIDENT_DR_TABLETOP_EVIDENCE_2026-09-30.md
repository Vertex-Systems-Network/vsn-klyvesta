# Incident and disaster-recovery tabletop evidence — 2026-09-30

## Repository-owned tabletop package

Status: **tabletop procedure and decision record defined; exercise result pending**.

Scenarios:
1. provider timeout during an order submission;
2. duplicate/replayed broker event;
3. leaked credential or restricted log entry;
4. database outage during reconciliation;
5. unsafe production-promotion request.

Required response:
- fail closed;
- preserve correlation/idempotency evidence;
- revoke or rotate affected credentials;
- stop promotion and live authority;
- reconcile from authoritative state;
- record incident owner, timeline, decisions and recovery result.

Evidence references:
- `docs/ANPOS_BATCH_5_RELIABILITY_RECONCILIATION_2026-09-30.md`
- `config/operations/promotion-gates.json`
- `.ai/acceptance-gates.yaml`
- `docs/THREAT_MODEL.md`

Conclusion: the tabletop framework is real, but no executed exercise result is claimed without an actual run and signed outcome.
