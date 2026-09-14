# Demo User Data Foundation Checkpoint — 2026-09-14

Scope: owner-directed, demo-only user-data functionality on top of the DB + pyPSX-free preview.

Implemented:
- local JSON persistence for synthetic investor profile/state;
- deterministic demo risk score and band;
- editable goals and watchlist;
- stored activity timeline;
- profile-aware deterministic demo insight;
- mutation header + same-origin demo cookie checks;
- CSP/no-cache/noindex demo response headers;
- runtime demo state excluded from Git;
- no PostgreSQL, pyPSX, real KYC/PII or real-money authority.

This checkpoint is non-production evidence only. Canonical PostgreSQL user/customer persistence remains a separate database/customer-data integration task.
