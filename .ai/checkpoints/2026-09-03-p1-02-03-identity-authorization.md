# P1-02-03 Identity / Authorization checkpoint

Date: 2026-09-03
Module: `identity-authorization`
Canonical branch: `parallel/identity-authorization`
Accepted integration baseline: `54a968b51f0ccca59a8b5654209f6aefa8a546ad`
Assigned worker: `ChatGPT-Identity-01` (assignment is canonical on `parallel/supervisor-platform`; the module branch intentionally does not mutate the shared registry snapshot)
Source candidate before this checkpoint commit: `dc40dc92ce2e378c755e59b7e8b78bf59bbba080`

## Scope implemented

- server-authoritative, provider-neutral authentication-context abstraction using an opaque session reference;
- trusted evidence-source boundary that does not accept client-supplied principal/role/customer/account authority fields;
- deterministic deny-by-default identity authorization evaluator;
- principal-type / security-role compatibility enforcement;
- customer own-resource enforcement with non-enumerating `RESOURCE_NOT_FOUND_OR_FORBIDDEN` denial;
- restricted account, recovery/security hold, device trust/revocation and action-bound step-up checks;
- explicit scope requirements for staff/service/machine authorities;
- maker/approver role separation for risk and reconciliation authority classes;
- AI-agent proposal-only boundaries distinct from service execution authorities;
- Execution Validator vs Broker Adapter separation;
- Notification Service sanitized-send scope boundary;
- auto-discovered `Klyvesta.AuthorizationVerifier` with 24 authentication/authorization/privilege-boundary assertions.

## Owned-path audit

Delta from accepted baseline through source candidate contains only:

- `src/Klyvesta.Domain/Identity/IdentitySecurityModels.cs`
- `src/Klyvesta.Application/Identity/AuthenticationAbstractions.cs`
- `src/Klyvesta.Application/Identity/DeterministicIdentityAuthorizationEvaluator.cs`
- `tools/Klyvesta.AuthorizationVerifier/Klyvesta.AuthorizationVerifier.csproj`
- `tools/Klyvesta.AuthorizationVerifier/Program.cs`

No API composition, workflow, migration, model snapshot, package/lockfile, live broker, credentials or shared module source was changed.

## Verification history

Initial implementation head `95616041f7aa8d016e3a70618e093bb3dde15b7f`:

- restore PASS;
- formatting PASS;
- API graph build PASS;
- all verifier builds PASS;
- verifier execution FAILED because recovery-state restriction was folded into effective account restriction before the evaluator could emit the more specific `SECURITY_HOLD` reason;
- orchestration initially FAILED because module-local work-item activation reduced the local capacity snapshot while the actual assignment was canonical on the Supervisor branch.

Corrections:

- module-local work-item snapshot was restored to the accepted branch baseline; canonical `ACTIVE/OCCUPIED` assignment remains on `parallel/supervisor-platform`;
- recovery-specific security denial is evaluated before the effective account restriction so recovery/security holds retain the stable `SECURITY_HOLD` category.

Exact source candidate `dc40dc92ce2e378c755e59b7e8b78bf59bbba080`:

- `agent-orchestration` run `33699576193` — PASS;
- `dotnet-foundation` run `33699576122` — PASS;
- restore PASS;
- formatting PASS;
- API graph build PASS;
- all auto-discovered verifier builds PASS;
- all auto-discovered verifier executions PASS.

This checkpoint commit changes evidence only and therefore requires its own fresh exact-head CI before submission/integration authority.

## Security / authority boundaries

- Authentication ALLOW is not created from client-supplied role, entitlement, account, balance, price, risk or permission fields.
- Identity authorization ALLOW is not final transaction authority; entitlement, legal/product, risk, compliance, business-state, ledger and broker gates remain independently mandatory.
- AI agents cannot obtain broker-submission authority through injected scopes.
- Broker Adapter cannot impersonate Execution Validator business authorization.
- No IdP/OIDC/passkey provider is selected or integrated.
- No bearer-token parser, production PII, production credential, live pyPSX API or real-money operation is introduced.

## Instruction drift

Re-read `AGENTS.md`, `.ai/MASTER_ENGINEERING_PROMPT.md`, `.ai/agent-orchestration.yaml`, parallel workflow docs, guardrails and acceptance gates before implementation.

Result: **no canonical instruction change required**. The existing module ownership, server-authoritative identity, deny-by-default authorization, no-AI-execution and no-live boundaries already cover this slice.

## Remaining gates

- fresh CI on the final checkpoint/submission head;
- PR submission to the Supervisor / accepted integration baseline;
- explicit security review and zero unresolved review threads;
- Supervisor ownership/dependency freshness review;
- integration to `parallel/integration-staging` only after all technical gates pass;
- no promotion to `main` while hosted-main governance remains unresolved.
