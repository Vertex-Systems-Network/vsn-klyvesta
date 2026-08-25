# Authorization Matrix V1

Status: Testable planning baseline derived from `AUTHORIZATION_PRIVILEGE_MODEL.md`.

Legend:
- **A** = allowed directly within scoped resources and current policy.
- **R** = read-only / redacted scope.
- **M** = maker only; separate approval required.
- **P** = approver only; cannot approve own proposal.
- **E** = emergency/break-glass only.
- **D** = denied.

All `A/R/M/P/E` outcomes still require valid authentication, resource scope, account state, legal/product availability and security/risk context. Deny by default.

## Customer-facing actions

| Action | Investor | Support L1 | Support L2 | KYC | AML/Compliance | Risk | Reconciliation | Security | Platform Admin | SRE | Auditor |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Read own customer profile | A | R | R | R | R | R | R | R | D | D | R |
| Read another customer's profile | D | R | R | R | R | R | R | R | D | D | R |
| Read customer portfolio/positions | A-own | R-min | R-case | D | R-case | R | R | R-case | D | D | R |
| Create manual order intent | A-own | D | D | D | D | D | D | D | D | D | D |
| Approve AI recommendation | A-own | D | D | D | D | D | D | D | D | D | D |
| Activate/change own Auto mandate | A-own | D | D | D | D | D | D | D | D | D | D |
| Pause own Auto mandate | A-own | D | D | D | D | A-pause-only | D | A-security-freeze | D | D | D |
| Request own withdrawal | A-own | D | D | D | D | D | D | D | D | D | D |
| Add/change beneficiary | A-own | D | D | D | D | D | D | D | D | D | D |
| Edit posted ledger/execution history | D | D | D | D | D | D | D | D | D | D | D |

`A-own` requires server-resolved ownership; client-provided ownership IDs are untrusted.

## Internal operations

| Action | Support L1 | Support L2 | KYC Analyst | AML Analyst | Compliance Officer | Risk Analyst | Risk Approver | Recon Operator | Recon Approver | Security Analyst | Platform Admin | SRE | Auditor |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Create support case/note | A | A | A-case | A-case | A-case | A-case | A-case | A-case | A-case | A-case | D | D | R |
| Approve/reject KYC | D | D | A | D | P/escalation | D | D | D | D | D | D | D | R |
| Place compliance restriction/freeze | D | D | D | M/limited | A/P | D | D | D | D | A-security-only | D | D | R |
| Propose risk policy change | D | D | D | D | D | M | D | D | D | D | D | D | R |
| Approve risk policy change | D | D | D | D | D | D | P | D | D | D | D | D | R |
| Pause global/cohort Auto | D | D | D | D | A-policy | A-if-granted | A | D | D | A-security-emergency | E | E | R |
| Propose financial correction | D | D | D | D | D | D | D | M | D | D | D | D | R |
| Approve financial correction | D | D | D | D | D | D | D | D | P | D | D | D | R |
| Modify historical ledger row | D | D | D | D | D | D | D | D | D | D | D | D | D |
| Revoke user sessions/devices | D | M-request | D | D | D | D | D | D | D | A | D | D | R |
| Unfreeze high-risk security case | D | D | D | D | P-case | D | D | D | D | M | D | D | R |
| Rotate broker credential | D | D | D | D | D | D | D | D | D | M-security | P/config | M-deploy | R |
| Deploy approved artifact | D | D | D | D | D | D | D | D | D | D | P/config | A | R |
| Read production secrets | D | D | D | D | D | D | D | D | D | E | D | E-min | D |
| Change customer bank destination internally | D | D | D | D | D | D | D | D | D | D | D | D | D |

No role may both propose and approve the same protected action. Enforcement uses proposal/approval records with distinct principal IDs.

## Machine principals

| Capability | AI Coach | Research Agent | Portfolio Agent | Rebalance Agent | Risk Governor | Compliance Gate | Execution Validator | Broker Adapter | Notification Service | Reconciliation Worker |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Read approved portfolio summary | A | context-only | A | A | A | A | A | A-min | D | A |
| Read approved market/research data | A-limited | A | A | A | A | A-limited | A-exec-only | D | D | A-reference |
| Create recommendation | D | D | A-proposal | D | D | D | D | D | D | D |
| Create rebalance/order-intent proposal | D | D | D | A-proposal | D | D | D | D | D | D |
| Change mandate/risk limits | D | D | D | D | D | D | D | D | D | D |
| PASS/DENY risk | D | D | D | D | A | D | D | D | D | D |
| PASS/DENY compliance | D | D | D | D | D | A | D | D | D | D |
| Submit authorized broker order | D | D | D | D | D | D | A-through-adapter | A-narrow | D | D |
| Create business intent | D | D | proposal-only | proposal-only | D | D | D | D | D | D |
| Write authoritative ledger | D | D | D | D | D | D | D | D | D | approved-recon-path-only |
| Send sanitized notification | D | D | D | D | D | D | D | D | A | D |

## Object-level rules

Customer requests must resolve ownership server-side:

- `customer_id` must belong to authenticated principal.
- `broker_account_id` must belong to that customer and be active/eligible.
- `portfolio_id` must belong to that customer/account.
- `recommendation_id` must belong to that portfolio/customer, be current, unexpired and unchanged.
- `beneficiary_id` must belong to that customer and be active outside any required cool-off.
- `order_intent_id` may be read/cancelled only if owned and current state permits.

Never authorize by trusting a client-supplied `user_id`, role, entitlement, balance, risk level, mandate version or price.

## High-risk dynamic deny conditions

Even an otherwise allowed action is denied/held when applicable:
- account restricted/suspended/closed;
- KYC/AML hold;
- recovery/security hold;
- beneficiary cooling-off/unverified;
- device/session risk exceeds policy;
- stale/unknown market state;
- unresolved critical reconciliation break;
- legal/product feature disabled;
- Auto mandate inactive/expired/superseded;
- daily/value/velocity/risk limit exceeded;
- broker/system kill switch active.

## Policy evaluation order

1. Authenticate principal.
2. Validate session/device security state.
3. Authorize action by principal role/scope.
4. Resolve and verify resource ownership/scope.
5. Check package entitlement if feature-gated.
6. Check legal/product availability.
7. Check customer/account/KYC/compliance state.
8. Check action-specific step-up/recovery/cool-off.
9. Run deterministic risk/compliance/business invariant checks.
10. Execute or return stable reason-coded denial.
11. Record decision/audit evidence.

## Stable denial categories

API should expose user-safe reason codes such as:
- `AUTH_REQUIRED`
- `STEP_UP_REQUIRED`
- `RESOURCE_NOT_FOUND_OR_FORBIDDEN`
- `ACCOUNT_RESTRICTED`
- `KYC_REQUIRED`
- `COMPLIANCE_HOLD`
- `SECURITY_HOLD`
- `BENEFICIARY_COOLING_OFF`
- `FEATURE_NOT_ENTITLED`
- `FEATURE_NOT_LEGALLY_AVAILABLE`
- `RISK_LIMIT_EXCEEDED`
- `STALE_MARKET_STATE`
- `RECONCILIATION_HOLD`
- `INVALID_STATE_TRANSITION`

Do not reveal whether another customer's resource exists.

## Required automated authorization suites

Generate matrix-driven tests covering:
- every role/action pair;
- own-resource vs other-customer resource;
- current vs revoked/expired session;
- trusted vs recovered/restricted device;
- entitled vs non-entitled package;
- active vs restricted customer/account;
- current vs superseded mandate/recommendation;
- maker attempting to approve own proposal;
- machine principal attempting an undeclared capability;
- mass-assignment of role/user/account/approval fields.

Any cross-customer disclosure, unauthorized financial mutation, maker-checker bypass, or AI direct-execution path is release-blocking.
