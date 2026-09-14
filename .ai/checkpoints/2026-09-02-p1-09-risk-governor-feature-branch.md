# P1-09 Deterministic Risk Governor — Feature Branch Checkpoint

Date: 2026-09-02
Status: **NON-LIVE / STACKED FEATURE BRANCH / NOT PRODUCTION AUTHORITY**
Issue: #74
Base: P1-08 exact technically verified head `f325b8383bc23881ccd6cfabbb5caf1af2455342` from draft PR #73.

## Scope implemented on this branch

- versioned paper risk policy model;
- explicit allowed-instrument universe;
- instrument eligibility and asset-class metadata;
- fresh/positive market-evidence requirements;
- minimum daily-traded-value liquidity check;
- exact-decimal order-notional limit;
- projected single-position concentration;
- projected sector concentration;
- projected gross exposure;
- bounded activity-window order-count and turnover checks;
- fail-closed shorting, margin, leverage and derivative rules;
- system kill switch;
- structured `ALLOW | DENY | HOLD` decisions with policy version and ordered reason codes;
- provider-neutral pre-side-effect `RiskGuardBrokerAdapter`;
- broker-order keyed in-memory decision record for this paper slice;
- dedicated deterministic risk verifier wired into foundation CI.

## Authority boundaries

This work is synthetic paper/shadow infrastructure only. Risk thresholds are test configuration, not production business truth. The risk decision engine is not authorization, compliance, mandate, suitability/advice, accounting, or AI authority.

No live pyPSX payload/status/auth mapping, broker credential, production customer PII, live customer advice, real-money execution, leverage/margin/shorting/derivative support, or production risk approval is authorized by this branch.

## Fail-closed behavior

- unknown/ineligible instrument -> `DENY`;
- missing market/portfolio/activity evidence -> `HOLD`;
- stale/invalid market evidence -> `DENY`;
- liquidity/order/exposure/concentration/activity breach -> `DENY`;
- prohibited short/margin/leverage/derivative request -> `DENY`;
- kill switch -> `DENY`;
- only `ALLOW` may reach the inner paper broker adapter.

## Verification target

The branch is not technically accepted until a fresh exact-head PR run proves:

- repository governance;
- formatting/build;
- architecture verifier;
- PaperBroker regression verifier;
- OMS regression verifier;
- portfolio/reconciliation regression verifier;
- dedicated risk verifier;
- PostgreSQL regression lane;
- CodeQL.

Any failing head remains non-accepted; fixes require a new exact head and fresh checks.

## Independent blockers unchanged

- Issue #1: hosted `main` is unprotected;
- Issue #20 / P0-T1: actual pyPSX technical/commercial partner evidence unresolved;
- P0/P0-PAR production gates remain fail-closed;
- substantive P1 merge remains blocked absent protected-main enforcement or a new explicit owner risk decision.

This checkpoint is not independent review and is not production authorization.
