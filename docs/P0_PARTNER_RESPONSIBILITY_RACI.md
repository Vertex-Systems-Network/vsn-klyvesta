# P0 — Broker Partner Responsibility RACI

Status: Template awaiting pyPSX / underlying broker written confirmation.

## Purpose

A working API does not prove regulatory or operational responsibility. This RACI must be completed contractually before real-money integration is approved.

Legend:

- **A** — Accountable: legally/operationally owns the outcome.
- **R** — Responsible: performs the activity.
- **C** — Consulted.
- **I** — Informed.
- **TBD** — must be assigned in writing.

Parties:

- **KLY** — Klyvesta technology/customer experience platform.
- **PYP** — pyPSX integration/platform entity, exact legal role TBD.
- **BRK** — underlying SECP-licensed securities broker/TREC holder, exact legal entity TBD.
- **SM** — Securities Manager, if Guarded Auto/discretionary management is supported; may or may not be the same legal entity as BRK.
- **CUS** — independent custodian where required.
- **MKT** — market-data licensor/provider where applicable.

## Legal / customer relationship

| Activity | KLY | PYP | BRK | SM | CUS | Evidence required |
|---|---|---|---|---|---|---|
| Customer securities-broker relationship | TBD | TBD | TBD | N/A | N/A | Customer agreement + broker licence |
| Customer terms/disclosures | TBD | TBD | TBD | TBD | N/A | Executed legal terms |
| KYC collection | TBD | TBD | TBD | N/A | N/A | API/process contract |
| KYC approval/account acceptance | TBD | TBD | TBD | N/A | N/A | Legal responsibility |
| AML/sanctions monitoring | TBD | TBD | TBD | TBD | N/A | AML policy/RACI |
| Complaint handling | TBD | TBD | TBD | TBD | N/A | Complaint SLA/escalation |
| Regulatory reporting | TBD | TBD | TBD | TBD | TBD | Contract/regulatory mapping |
| Customer-record retention | TBD | TBD | TBD | TBD | TBD | Retention/DPA |

## Account / custody / settlement

| Activity | KLY | PYP | BRK | SM | CUS | Evidence required |
|---|---|---|---|---|---|---|
| NCCPL UIN creation/mapping | TBD | TBD | TBD | N/A | N/A | Process + ownership |
| CDC sub-account / Investor Account setup | TBD | TBD | TBD | N/A | TBD | Account structure |
| Securities custody | I | TBD | TBD | TBD | TBD | Custody agreement |
| Trade settlement | I | TBD | TBD | N/A | TBD | T+1 process |
| Corporate actions/dividends | I | TBD | TBD | TBD | TBD | Operational ownership |
| Statements/contract notes | I | TBD | TBD | TBD | TBD | Delivery responsibility |
| Tax/fee records | I | TBD | TBD | TBD | TBD | Field semantics/reporting |

## Customer money

| Activity | KLY | PYP | BRK | SM | CUS | Evidence required |
|---|---|---|---|---|---|---|
| Deposit destination account | **Must not default to KLY corporate account** | TBD | TBD | N/A | TBD | Bank/funding flow |
| Deposit reconciliation | TBD | TBD | TBD | N/A | TBD | API + ledger reconciliation |
| Buying-power calculation | C | TBD | TBD | N/A | N/A | Defined source of truth |
| Cash reservation for orders | R internal projection | TBD | TBD | N/A | N/A | Broker semantics |
| Withdrawal processing | TBD | TBD | TBD | N/A | TBD | Workflow/security contract |
| Withdrawal beneficiary validation | TBD | TBD | TBD | N/A | N/A | Control ownership |
| Failed/reversed payment handling | TBD | TBD | TBD | N/A | TBD | Reversal process |

## Trading / IBTS

| Activity | KLY | PYP | BRK | SM | Evidence required |
|---|---|---|---|---|---|
| Customer Manual order intent | R | C | A/R execution | N/A | Contract + API |
| Customer authentication | R platform | TBD | A/TBD regulatory | N/A | IBTS/customer-auth allocation |
| Pre-trade product/risk checks | R Klyvesta controls | TBD | A/R broker controls | TBD | Limits/RACI |
| Order routing to PSX | I | TBD | A/R | N/A | TREC/IBTS evidence |
| Broker order state | C/reconcile | TBD | A/R | N/A | Status contract |
| Fill/execution source of truth | C/reconcile | TBD | A/R | N/A | Stable execution IDs |
| Trade confirmation | I/display | TBD | A/R | N/A | Confirmation requirement |
| Exchange/broker outage handling | R client fail-safe | TBD | A/R | N/A | SLA/runbook |
| Reconciliation | R internal | TBD | A/R external evidence | N/A | Daily/intraday process |

## AI-assisted mode

| Activity | KLY | PYP | BRK | SM | Evidence required |
|---|---|---|---|---|---|
| Generic education/analytics | R | I | C | N/A | Permitted scope |
| Personalized recommendation generation | TBD | TBD | TBD | TBD | Legal classification |
| Suitability/risk-profile assessment | TBD | TBD | TBD | TBD | Responsibility + method |
| Recommendation disclosures | TBD | TBD | TBD | TBD | Approved wording/process |
| Final order decision | Customer | N/A | N/A | N/A | Must be explicit for AI-assisted |
| Recommendation audit evidence | R | TBD | TBD | TBD | Retention/audit requirements |

## Guarded Auto / discretionary management

No cell in this section may be treated as approved until the Securities Manager/licensed-partner path is documented.

| Activity | KLY | PYP | BRK | SM | CUS | Evidence required |
|---|---|---|---|---|---|---|
| Customer discretionary mandate | TBD | TBD | TBD | **Expected A** | I | Legal agreement |
| Investment Policy Statement | TBD | TBD | TBD | **Expected A/R** | I | Regulatory process |
| Suitability | TBD | TBD | TBD | **Expected A/R** | I | Method/evidence |
| Portfolio decision authority | Must not assume | TBD | TBD | **Expected regulated authority** | I | Licence + contract |
| AI/model signal generation | R technical only | C | C | A/TBD | I | Model governance |
| Final executable decision | Deterministic validated system only within approved structure | TBD | TBD | A/TBD | I | Authority mapping |
| Independent custody | I | TBD | TBD | A/TBD | **Expected R** | Custody agreement |
| Conflicts-of-interest management | C | TBD | TBD | A/R | I | Policy |
| Performance/reporting | R presentation | TBD | TBD | A/TBD | TBD | Regulatory reporting |

## Security / technology

| Activity | KLY | PYP | BRK | Evidence required |
|---|---|---|---|---|---|
| Klyvesta app/API security | A/R | I | I | Internal controls/tests |
| Broker credential issuance | I | TBD | A/TBD | Credential contract |
| Broker credentials storage | A/R for credentials issued to KLY | C | C | Vault/KMS controls |
| Webhook authentication | R verify | R/TBD sign | A/TBD source | Signing contract |
| API rate limits | R consume safely | A/R expose | C | Limits/SLA |
| API incident notification | I/R client response | TBD | A/TBD | Incident SLA |
| Penetration/security certification | R own platform | TBD | A/R regulated broker scope | Evidence/certification |
| Audit logs | A/R KLY events | TBD | A/R broker events | Retention/integrity |
| DR/BCP | A/R KLY | TBD | A/R broker | RTO/RPO/runbooks |

## Data protection / ownership

Contract must explicitly define:

- data controller/processor roles;
- customer-consent basis;
- PII fields each party receives;
- cross-party access boundaries;
- encryption requirements;
- retention/deletion/legal hold;
- breach notification timelines;
- model/AI use of customer data;
- prohibition on putting broker credentials or unnecessary PII into LLM context;
- ownership/portability on termination.

## Completion rule

This RACI is accepted only when every production-relevant `TBD` has one accountable party and required evidence is linked. A row with multiple parties claiming nobody is accountable is a blocker. A row where Klyvesta is made legally accountable for a regulated activity it is not licensed/authorized to perform is also a blocker until the structure changes.
