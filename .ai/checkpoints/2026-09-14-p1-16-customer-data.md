# P1-16 Customer Data Foundation — 2026-09-14

Scope: API-independent customer profile, deterministic risk-profile history, goals and watchlist domain/application foundation.

Implemented on canonical `parallel/customer-data`:
- authenticated customer ownership checks on every service operation;
- optimistic workspace revision control;
- versioned customer profile updates;
- deterministic, append-only risk-profile scoring history;
- customer-owned goals with unique identifiers and revisioned removal;
- normalized, idempotent watchlist additions and fail-closed removals;
- in-memory `ICustomerProfileStore` implementation for non-production acceptance;
- dedicated verifier covering ownership, versioning, concurrency, validation and restricted-PII field exclusion.

Explicitly excluded:
- PostgreSQL/EF migrations or model snapshots;
- API endpoint composition;
- real KYC/CNIC/passport/biometric/bank identifiers;
- pyPSX or broker/provider calls;
- real-money authority or production suitability decisions.

Final database persistence remains owned by the canonical `database-integration` lane.
