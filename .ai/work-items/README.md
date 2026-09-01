# Parallel Work Items

Each parallel module owns one durable work-item file under `.ai/work-items/<module>/`.

A work item is the machine-readable handoff contract between the Supervisor and a module agent. It must record the assigned branch/agent slot, exact base SHA, accepted dependency heads, allowed/shared paths, status, acceptance evidence, and instruction-drift result.

Rules:

- Module agents may update only their own module work-item directory plus their normal module/checkpoint paths.
- `READY` means all declared dependencies are satisfied at the recorded accepted baseline and no duplicate/existing-work blocker remains.
- `BLOCKED` requires explicit blocker codes.
- `ACTIVE` requires an assigned branch and base SHA.
- `SUBMITTED` requires a PR plus the exact top-level completion comment `Work Done and Submitted`.
- `INTEGRATED` requires Supervisor review and an accepted integration baseline SHA.
- A refresh alert invalidates stale base/dependency evidence until the work item is refreshed.
- Work-item status never overrides regulatory, production, live-broker, PII, financial, or phase acceptance gates.

Validation is performed by `scripts/validate-work-items.rb` and the `agent-orchestration` workflow.