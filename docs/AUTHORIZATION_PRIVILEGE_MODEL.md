# Authorization & Privilege Model

Status: Required architecture baseline before production implementation.

## Core rule

Authentication proves identity. Authorization decides whether that identity may perform a specific action on a specific resource under the current risk/context.

Klyvesta must use **server-side deny-by-default authorization**. Client UI visibility is never an authorization control.

Use a hybrid model:
- RBAC for broad job/customer roles,
- ABAC/resource ownership for account/customer/order/portfolio scope,
- entitlement checks for paid product features,
- risk/context checks for high-risk actions.

`subscription_tier` is never a role.

## Customer principals

### Investor
May:
- read own profile/account/portfolio/orders/statements,
- create manual order intents where enabled,
- approve/reject AI-assisted recommendations,
- configure/revoke own mandate within allowed rules,
- request withdrawal to verified beneficiary/bank,
- manage own trusted devices/security settings subject to step-up.

May not:
- read another customer,
- bypass risk/compliance/account restrictions,
- modify broker execution state,
- edit ledger/history,
- change regulatory policy,
- enable unlicensed features.

### Customer API/automation token
If introduced later, scope separately from interactive customer login. Never inherit all interactive privileges by default.

## Internal human roles

### Support L1
- read minimal/redacted customer profile and case context,
- create support cases/notes,
- trigger approved verification workflow.

Cannot trade, withdraw, change bank/beneficiary, modify mandate, change KYC decision, see secrets, or edit ledger.

### Support L2
Adds limited diagnostic visibility and security/session reset requests through approval workflow. Still no financial authority.

### KYC Analyst
- read required KYC artifacts,
- review onboarding cases,
- approve/reject/escalate according to policy.

Cannot trade or move funds.

### AML/Compliance Analyst
- review monitoring alerts,
- place/recommend restrictions or freezes according to policy,
- document disposition.

Cannot originate customer trades or redirect money.

### Compliance Officer
May approve high-impact compliance actions and policy releases via maker-checker. No unilateral customer withdrawal/trading authority.

### Risk Analyst
- inspect exposure/risk dashboards,
- propose risk-policy changes,
- simulate impact,
- pause Auto at account/cohort/system level where explicitly authorized.

Cannot promote own policy change to production without approval.

### Risk Approver
Separate principal from change author. Approves risk-policy/model-risk changes.

### Reconciliation/Finance Operator
- view broker/cash/ledger reconciliation,
- resolve documented reconciliation cases using compensating workflow,
- initiate correction proposal.

Cannot edit historical ledger rows or create arbitrary balances.

### Reconciliation Approver
Approves sensitive adjustment entries. Must be separate from creator for thresholded/high-risk corrections.

### Security Analyst
- inspect login/device/security telemetry,
- revoke sessions/devices,
- freeze account security state,
- investigate incidents.

PII/portfolio visibility limited to case need.

### Platform Admin
- application configuration and non-financial operational settings only.

No global wildcard that silently bypasses financial authorization.

### DevOps/SRE
- deploy approved artifacts,
- view infrastructure telemetry,
- operate platform availability.

No standing access to production customer data, broker secrets or financial override APIs. Use workload identity and break-glass where unavoidable.

### Auditor
Read-only access to approved audit/evidence scope. No mutations.

## Machine principals

### AI Coach
Read-only approved customer portfolio summary + educational context. No financial writes.

### Research Agent
Read approved market/research data. No customer money/order write authority.

### Portfolio Agent
May create structured portfolio proposal only.

### Rebalance Agent
May create structured rebalance/order-intent proposals only.

### Risk Governor
Deterministic policy service. May PASS/DENY/PAUSE; no broker credentials.

### Compliance Gate
Deterministic policy service. May PASS/DENY/RESTRICT; no broker credentials.

### Execution Validator
May submit only already-authorized normalized order intents to Broker Adapter.

### Broker Adapter
Narrow service credential. May perform broker API operations required by approved execution/funding/account workflows. It cannot create business intent itself.

### Notification Service
May read sanitized notification payloads/templates and send them. No account/portfolio mutation privilege.

## Resource authorization

Every request must authorize both **action** and **resource scope**.

Examples:
- `portfolio.read` requires `portfolio.user_id == principal.user_id` for customer principals.
- `order.create` requires account ownership + account ACTIVE + security/risk context + entitlement/product availability.
- `recommendation.approve` requires recommendation belongs to same user/account, is unexpired, unchanged and approved mode is allowed.
- `withdrawal.request` requires own account + verified beneficiary + strong step-up + no security/compliance hold.

Never trust client-supplied `user_id`, `account_id`, `role`, `risk_level`, `entitlement`, `price`, `balance` or `permission` without server-side resolution/validation.

## Maker-checker actions

At minimum require separate proposer/approver for:
- production risk-policy change,
- production model promotion,
- compliance rule change,
- manual financial adjustment above threshold,
- broker credential rotation/access change,
- emergency override of trading restrictions,
- internal change to customer bank/beneficiary,
- restoration of a fraud/security-frozen account when high risk,
- changes to global Auto kill-switch policy.

## Break-glass

No permanent superuser.

Break-glass access must:
- require phishing-resistant MFA,
- require explicit incident/ticket reason,
- be time-limited,
- grant minimal temporary scope,
- alert security/compliance,
- be fully recorded,
- receive after-action review.

Where practical, require second-person approval.

## Account recovery privileges

A successfully authenticated session after recovery is not automatically fully trusted.

After sensitive recovery/change, restrict:
- withdrawal,
- beneficiary/bank change,
- mandate activation/risk increase,
- adding new device as high trust,
- API token creation,
until risk-based re-verification/cool-off is satisfied.

## Entitlements

Commercial packages control product capability, not security authority.

Example entitlements:
- `ai.explanations.basic`
- `ai.recommendations.personalized`
- `analytics.stress_testing`
- `alerts.whatsapp`
- `auto.recurring_investment`
- `auto.rebalancing`

Even if entitlement is true, legal/product/risk/security gates may still deny the action.

Decision formula:

`authenticated AND authorized(resource, action) AND entitled(feature) AND legal/product-enabled AND risk/security-allowed`

## Testing requirements

Automated authorization matrix tests must cover every role/action/resource combination, including:
- same-user vs other-user object IDs,
- role escalation attempts,
- mass assignment/property tampering,
- disabled/restricted accounts,
- expired/changed recommendations,
- package entitlement bypass,
- support/admin impersonation,
- break-glass expiry,
- machine-principal scope violations.

Zero tolerance: any test showing cross-customer access, unauthorized financial mutation or privilege escalation blocks release.
