# ANPOS Batch 2 — Public API Evidence Review

Date: 2026-09-30 (Asia/Karachi)
Repository: `Vertex-Systems-Network/vsn-klyvesta`
Batch status: **PUBLIC EVIDENCE COMPLETE; SANDBOX CORE TEST PASSED; PARTNER/LEGAL GATES STILL BLOCKED**
Related issue: #20

## Sources reviewed

- Official site: https://pypsx.com/
- Official developer docs: https://docs.pypsx.com/
- Official Broker API overview: https://docs.pypsx.com/broker-api/
- Official authentication guide: https://docs.pypsx.com/broker-api-authentication/
- Official accounts guide: https://docs.pypsx.com/broker-api-accounts/
- Official orders/fills guide: https://docs.pypsx.com/broker-api-orders/
- Official portfolio/reports guide: https://docs.pypsx.com/broker-api-portfolio/
- Official market-data guide: https://docs.pypsx.com/broker-api-market-data/
- Official streaming guide: https://docs.pypsx.com/broker-api-streaming/
- Official dashboard guide: https://docs.pypsx.com/broker-api-dashboard/
- Official errors guide: https://docs.pypsx.com/broker-api-errors/
- Official brokerage-account/KYC guide: https://docs.pypsx.com/kyc-api/

These are public web documents checked on 2026-09-30. They are evidence of published documentation only.

## Publicly verified claims

| Area | Public documentation evidence | Klyvesta disposition |
| --- | --- | --- |
| Product separation | Broker API is presented as distinct from the Trading API and intended for businesses embedding PSX investing for their users. | **Publicly documented; partner contract unverified** |
| Environments | Docs publish sandbox `https://brokerapi-paper.pypsx.com`, live `https://brokerapi.pypsx.com`, and dashboard `https://broker.pypsx.com`. | **Publicly documented; connectivity/credential access not tested** |
| Sandbox model | Docs describe sandbox cash and simulated/PSX-price fills, with a health endpoint example. | **Publicly documented; no Klyvesta sandbox run completed** |
| Organisation keys | Docs describe dashboard token versus organisation API key, separate authority boundaries, key IDs and one-time secrets. | **Publicly documented; no key issued to Klyvesta** |
| Scopes and rotation | Docs publish accounts/trading scopes, key revocation, IP allowlist and server-only secret handling. | **Publicly documented; security controls not tested** |
| Config | Docs publish `GET /v1/partner-api/config` and state that commission, starting cash, scopes and config version are returned. | **Publicly documented; values not authoritative for production** |
| Accounts | Docs publish sub-account creation and account identifiers/status/balance examples. | **Publicly documented; schema/status semantics not contractually confirmed** |
| Orders/fills | Official docs publish order/fill pages and supported order examples. | **Publicly documented; idempotency, timeout and reconciliation still unverified** |
| Portfolio/reports | Official docs publish portfolio, statements and order-history surfaces. | **Publicly documented; financial integrity and settlement semantics unverified** |
| Market data | Official docs publish Broker API market-data documentation and separate unauthenticated public data tooling. | **Publicly documented; source/licensing/redistribution rights unverified** |
| Streaming | Official docs publish Broker API streaming documentation and the wider platform documents WebSocket/live-feed capabilities. | **Publicly documented; signing, replay, ordering and production rights unverified** |
| Errors | Official docs publish structured HTTP/code error handling and an errors guide. | **Publicly documented; negative/rate-limit tests not run** |
| KYC/account opening | Official docs publish an account-opening/KYC guide and describe CDC/Alpha Capital account flow in the Broker API overview. | **Publicly documented; legal responsibility and data-processing terms unverified** |
| Live activation | Official docs say production order access is switched on per organisation and may return a disabled-order error until enabled. | **Publicly documented; Klyvesta production approval absent** |

## Executed sandbox evidence

GitHub Actions run: https://github.com/Vertex-Systems-Network/vsn-klyvesta/actions/runs/36631970370

Result: **SUCCESS** on 2026-09-29.

Verified by the run:

- public sandbox health endpoint responded successfully;
- authenticated \/config request succeeded;
- returned environment was confirmed as `sandbox`;
- a sandbox sub-account was created;
- the sandbox portfolio endpoint was read successfully;
- secrets were supplied through GitHub Actions and were not printed.

Not yet executed in this run: order placement/fill/cancel/reconciliation. The workflow keeps the order test as an explicit manual input so no order is submitted accidentally.

## What public docs do not prove

Public docs do not prove:

- that VSN/Klyvesta has been accepted as a partner;
- that credentials or sandbox access have been issued to VSN;
- current contract terms, SLA, commercial fees or support obligations;
- the exact SECP-licensed broker legal entity and responsibility split;
- KYC/AML, CDC, UIN, NCCPL, custody or customer-money legal allocation;
- production order enablement for Klyvesta;
- market-data licensing or redistribution rights for Klyvesta users;
- Securities Manager/advisory/discretionary-management permission;
- a legal classification for AI-assisted or Guarded Auto modes.

## Public evidence implementation completed

The following repository changes are now justified by official public docs:

1. Maintain separate Trading API and Broker API boundaries.
2. Model environment identity explicitly: sandbox versus production.
3. Keep organisation secrets backend-only and support key rotation/revocation.
4. Preserve scope-aware authorization and IP-allowlist controls.
5. Treat `/config` as runtime configuration input rather than hard-coded fee authority.
6. Keep account, order, fill, portfolio, market-data and stream surfaces behind the existing `BrokerAdapter` contract.
7. Preserve fail-closed production behavior until partner enablement and legal/provider gates are evidenced.

No live adapter, live credential, KYC PII path or production trading flow was enabled by this evidence review.

## Batch 2 status

**Public-evidence portion: COMPLETE.**

**Overall Batch 2: INCOMPLETE / BLOCKED.**

Issue #20 remains open because public documentation cannot satisfy the direct partner-contract and legal/regulatory acceptance gates. The next required evidence is a current written partner response and/or contract, followed by sandbox credentials and controlled tests.

## Evidence classification

- `D`: official public documentation.
- `T`: executed Klyvesta test — none for Broker API in this review.
- `C`: current partner contract/authoritative response — none recorded.
- `L`: legal/regulatory confirmation — none recorded.

Current Batch 2 evidence state: **D = strong for published API surface; T = core sandbox connectivity/account/portfolio passed; order/fill/cancel/reconciliation T = pending; C = 0; L = 0.**
