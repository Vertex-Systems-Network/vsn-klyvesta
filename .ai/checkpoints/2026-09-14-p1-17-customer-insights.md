# P1-17 Customer Insights — 2026-09-14

Scope: API-independent deterministic customer-facing observations derived only from supplied portfolio, risk-policy, activity, goal and valuation evidence.

Canonical branch: `parallel/customer-insights`
Submission PR: `#111`
Accepted integration baseline at implementation start: `499fe56c7448169f22b86172a2c25e8d5729398e`

Implemented:
- deterministic single-position and sector concentration observations;
- gross-exposure near-limit and over-limit observations using the supplied risk policy;
- supplied peak-to-current drawdown observation;
- stale valuation evidence warning with suppression of valuation-derived concentration/exposure/drawdown insights;
- future market evidence and duplicate instrument evidence fail-closed validation;
- validated goal-progress elapsed-time pacing without return forecasts;
- recent activity-intensity observation using supplied paper-risk envelopes;
- stable ordinal insight ordering;
- explicit authority boundary: informational-only, cannot place orders, cannot override risk policy, and is not a suitability decision;
- dedicated 20-case `Klyvesta.CustomerInsightsVerifier`.

Explicitly excluded:
- API endpoint composition;
- database persistence, migrations or model snapshots;
- live market-data retrieval or fabricated prices;
- pyPSX or broker/provider behavior;
- trade execution or order blocking;
- personalized investment recommendations or return forecasts;
- real-money authority or production suitability decisions.

Acceptance requires exact-head `agent-orchestration` and `dotnet-foundation` success with zero unresolved review threads before integration into `parallel/integration-staging`.
