# Security Requirements

## Identity
- passkeys and/or strong 2FA
- device/session management
- step-up auth for sensitive actions
- withdrawal confirmation
- account recovery with strong controls

## API
- OAuth/OIDC internally where appropriate
- short-lived tokens
- scopes/least privilege
- mTLS/service authentication for sensitive internal calls
- signed webhooks
- replay prevention
- idempotency keys for financial commands
- strict schemas
- rate limits
- WAF/DDoS controls

## Data
- encryption in transit
- encryption at rest
- field-level protection/tokenization for highly sensitive PII where appropriate
- separate PII access boundary
- immutable audit trail
- backup/restore testing
- retention/deletion policy

## Secrets
- KMS/HSM/Vault-class storage
- no secrets in code/logs/prompts
- no broker credentials available to LLM runtime
- environment separation
- rotation

## Financial safety
- double-entry ledger
- atomic transactions
- deterministic decimal money math
- unique external reference IDs
- duplicate-order prevention
- reconciliation
- account freeze/auto-pause controls

## AI security
- prompt injection tests
- tool-permission tests
- untrusted-context isolation
- output schema enforcement
- model fallback behavior
- no side effects on malformed/partial AI output
- sensitive-data redaction
- no training-provider data sharing unless contractually approved

## SDLC
- protected main branch
- required reviews
- signed/traceable releases
- secret scanning
- dependency scanning
- SAST
- DAST
- container/image scanning
- SBOM
- provenance
- production access logs
