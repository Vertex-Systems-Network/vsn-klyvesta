# Integration Merge Train

Supervisor-owned technical integration evidence for `parallel/integration-staging`.

Rules:
- advance one accepted dependency slice at a time;
- preserve current shared orchestration/CI unless a shared-file change is explicitly re-reviewed;
- feature-stack legacy CI registration is omitted when convention-based verifier discovery already covers the verifier;
- every accepted advance is followed by exact-head regression before the next dependency slice;
- integration-staging is technical evidence only and does not authorize production, live brokerage, real money, PII, or regulatory acceptance.
