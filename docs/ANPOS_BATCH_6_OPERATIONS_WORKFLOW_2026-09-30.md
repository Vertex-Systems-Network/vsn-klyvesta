# ANPOS Batch 6 — Panel and Operations Workflow

Repository-owned scope completed:

- Explicit deny-by-default access matrix in `config/operations/access-matrix.json`.
- Sensitive operations separated from read-only evidence/status access.
- Two-person approval requirement defined for environment changes and production promotion.
- Audit requirement defined for diagnostics, order actions and promotion decisions.
- Live trading remains disabled until explicit gates pass.

External/admin dependency:

- Hosted GitHub administrator must verify and enforce the corresponding ruleset and reviewer permissions.
- This document does not claim that an unavailable GitHub ruleset/admin control has been enabled.
