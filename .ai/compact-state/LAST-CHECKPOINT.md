# Last Checkpoint — P1-26 Platform Baseline Reconciliation

Status: VERIFYING

Repository truth:
- PR #140 P1-26 Customer Risk Center assignment merged into `parallel/integration-staging` at `fe25db8e75898876b3a02dc55fbc387400a48dc3`.
- Issue #82 assignment refresh is comment #5781812948.
- PR #143 Customer Risk Center implementation is open from `parallel/customer-risk-center`.
- PR #143 repaired source head is `4c9ab59cce39a9b8184a7f596608fbd13f8c88e5`.
- exact-head orchestration run `35783665697` passed.
- exact-head .NET run `35783665661` built API + all verifiers successfully and Customer Risk Center verifier passed 38/38.
- that .NET run failed only in PlatformVerifier PLAT-013 because it still hardcoded pre-assignment baseline `32fa999a69c93793c46dec525cef6f1ee23c746b`.

Ownership-correct repair:
- `tools/Klyvesta.PlatformVerifier/Program.cs` now expects accepted P1-26 assignment baseline `fe25db8e75898876b3a02dc55fbc387400a48dc3`;
- platform work-item consumption evidence is refreshed to the same accepted baseline;
- README/compact state are synchronized to current #143 verification truth without advancing canonical progress;
- repository-owned non-live progress remains 21/24 = 88% until P1-26 implementation is accepted and lifecycle closeout merges.

Next-lane preflight:
- `parallel/database-integration` is READY but divergent by one legitimate historical audit commit `80d0a744154c0e923155821ea260950b90abb1ae`; controlled reconciliation is required rather than blind fast-forward.
- Security Acceptance remains BLOCKED by production governance/main-protection evidence.

PR #144 is open from `parallel/platform-ci` to `parallel/integration-staging`. Exact next safe action: perform one consolidated exact-head CI/status/review observation; if all authoritative gates pass and base remains fresh, merge with expected-head protection.


## PLAT-013 semantic baseline repair

PR #144 exact-head .NET run `35785092254` passed restore, formatting, API build, all verifier builds, and all product/module verifiers, then failed only at PLAT-013 because the verifier expected one hardcoded baseline SHA while canonical staging and the refreshed implementation legitimately record different consumed baselines.

Repair:
- remove brittle hardcoded P1-26 baseline equality;
- require `accepted_baseline_sha` to be one full immutable 40-hex SHA;
- require `base_sha` and `consumed_runtime_baseline_sha` to equal that accepted baseline record;
- require `runtime_integration_baseline_ancestor_verified: true`;
- retain all customer scope, dependency ancestry, production-authority and no-advice/no-trading/provider checks.

This strengthens consistency without granting any live/security authority.
