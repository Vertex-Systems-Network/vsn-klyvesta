## Purpose

Describe the problem and the smallest maintainable change that solves it.

## Change impact

**Affected:**

**Unaffected:**

**Risk:**

**Migration:**

**Rollback:**

**Verification:**

## Security / financial-safety checklist

- [ ] No secrets, credentials, private keys, tokens, or production identifiers were committed.
- [ ] Authorization is enforced server-side for every affected resource/action.
- [ ] Subscription entitlements are not used as security roles.
- [ ] Financial commands are idempotent where applicable.
- [ ] Money/settlement math uses exact decimal/fixed-precision types.
- [ ] AI/LLM code cannot bypass Risk Governor, Compliance Gate, Execution Validator, mandate, ledger, or reconciliation boundaries.
- [ ] Third-party/broker data is schema-validated and failure behavior is explicit.
- [ ] Sensitive logs/telemetry do not expose unnecessary PII or financial secrets.
- [ ] New dependencies were researched, justified, license-reviewed, and vulnerability-checked.
- [ ] Breaking changes include migration and rollback documentation.

Use `N/A` with a short reason for checklist items that do not apply.

## Quality evidence

- [ ] Formatting passed.
- [ ] Build/type checks passed.
- [ ] Relevant automated tests/checks passed.
- [ ] Security/static analysis passed or findings are documented.
- [ ] Documentation/checkpoint updated when the change is architecturally or operationally meaningful.

## Known risks / not verified

List anything that remains unverified. Do not hide failures.
