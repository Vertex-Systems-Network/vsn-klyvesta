# AI-PLAN — Klyvesta

## 1. Product thesis

Make regulated investing understandable to a person with little or no market knowledge while keeping the system auditable, bounded, and reversible.

The product is not an autonomous gambling engine. It is a regulated-investing operating system where AI:
- understands the investor,
- monitors allowed market data,
- proposes portfolios,
- explains trade-offs,
- continuously measures risk,
- proposes or performs rebalancing only when permitted,
- records the evidence behind every material decision.

## 2. AI-native definition

AI-native means AI is a first-class system actor, not a chatbot added to a traditional trading app.

Every agent action must have:
- a typed input,
- an explicit tool permission,
- an output schema,
- a policy decision,
- a model/prompt version,
- source/evidence references,
- an immutable audit record,
- deterministic validation before any financial side effect.

No LLM may call the broker execution endpoint directly.

## 3. Customer modes

### Manual
Customer creates the order. Klyvesta validates it and routes it.

### AI Assisted
AI creates a recommendation with:
- rationale,
- expected risk,
- uncertainty,
- portfolio impact,
- downside scenarios,
- costs/turnover,
- evidence freshness.

Customer must explicitly confirm.

### Guarded Auto
Customer signs/accepts a mandate defining:
- allowed instruments,
- investment horizon,
- risk level,
- maximum single-name exposure,
- maximum sector exposure,
- minimum cash buffer,
- maximum turnover,
- maximum daily order count/value,
- drawdown response,
- rebalance rules,
- prohibited instruments,
- pause/kill-switch conditions.

The AI may act only inside this envelope. The deterministic Risk Governor and Compliance Gate can veto the AI at any time.

**Guarded Auto remains feature-flagged OFF until regulatory acceptance is recorded.**

## 4. AI decision pipeline

Market Data -> Feature Pipeline -> Research/Signal Layer -> Portfolio Constructor -> Risk Governor -> Compliance Gate -> Order Intent -> Execution Planner -> pyPSX Adapter -> Broker -> Fill/Reconciliation -> Portfolio State -> Explainability/Audit.

## 5. Agent hierarchy

AI agents may recommend. Policy services authorize.

- Investor Understanding Agent
- Market Intelligence Agent
- Signal/Regime Agent
- Portfolio Construction Agent
- Rebalancing Agent
- Explainability Agent
- Investor Coach Agent
- Operations Agent

Deterministic authorities:
- Suitability Policy Engine
- Risk Governor
- Compliance Gate
- Execution Validator
- Ledger/Reconciliation Engine

## 6. Initial investment universe

MVP should be intentionally conservative:
- PSX listed equities that pass liquidity/eligibility rules
- ETFs where supported and appropriate
- no leverage
- no margin
- no short selling
- no derivatives/futures in automated mode
- no penny/illiquid names in auto portfolios
- no instruments without reliable price/reference data

Expansion requires a new risk and compliance acceptance gate.

## 7. Risk objective

Do not optimize for raw return.

Primary optimization objective:
maximize expected risk-adjusted return subject to hard constraints.

Example constraints:
- concentration
- liquidity
- volatility
- drawdown
- turnover
- transaction costs
- user horizon
- user suitability
- sector limits
- cash reserve
- mandate limits

## 8. Model strategy

Use different technology for different jobs.

LLMs:
- natural-language onboarding
- research summarization
- explanation
- evidence synthesis
- user support
- workflow orchestration

Quant/statistical models:
- return/risk estimation
- volatility
- correlation
- regime classification
- factor scoring
- anomaly detection
- portfolio optimization

Deterministic code:
- money math
- ledger
- order quantities
- limits
- permissions
- suitability rules
- risk rules
- compliance rules
- reconciliation
- broker API calls

## 9. AI safety rules

- No fabricated market prices.
- No trading on stale market state.
- No execution from free-form natural language without structured confirmation/mandate.
- No direct LLM access to broker API secrets.
- No model may alter its own risk limits.
- No agent may expand a mandate.
- No hidden leverage.
- No averaging-down rule without explicit risk approval.
- No guaranteed-return language.
- No “recover losses” martingale behavior.
- No trading to increase platform commission/revenue.
- Conflicts of interest must be modeled and disclosed.

## 10. Product success metrics

Business:
- funded active investors
- retained investors
- recurring deposits
- net assets on platform
- conversion from onboarding to funded account

Investor outcome:
- risk-adjusted return
- max drawdown
- downside capture
- turnover/cost
- diversification
- mandate adherence
- behavior gap reduction

Safety:
- unauthorized orders = 0
- orders outside mandate = 0
- unreconciled money/security movements = 0 beyond defined reconciliation window
- hallucinated execution facts = 0
- unversioned model decisions = 0
- missing decision audit trail = 0

## 11. AI-native engineering delivery model

The product should also be developed AI-natively. Engineering work must be decomposed into contract-driven modules so multiple specialized agents can execute independent READY work concurrently without weakening review or acceptance.

Canonical delivery rules are defined in `docs/PARALLEL_AGENT_DEVELOPMENT.md`, `docs/MULTI_AGENT_REPOSITORY_WORKFLOW.md`, `.ai/agent-orchestration.yaml`, and `.ai/parallel-branch-registry.yaml`.

### Current concurrency target

- use approximately **8-10 concurrent specialized agents** when the dependency graph exposes enough independent work;
- scale toward **12-16 agents** only after path-ownership CI, automatic verifier discovery, dependency/work-item validation, shared-file ownership enforcement, merge-train integration, migration integration and conflict automation are operational;
- do not create artificial parallelism inside a tightly coupled dependency chain.

### Supervisor model

The Main-repo agent acts as **Supervisor**.

Before assigning a new parallel work wave, its first repository action is to create/reserve the module branches that other agents will use. It also owns `parallel/supervisor-platform` for its own work and `parallel/integration-staging` for accepted technical integration while protected-main promotion is blocked.

The canonical module/branch/agent-slot map is `.ai/parallel-branch-registry.yaml`.

The Supervisor reviews every submitted module PR, integrates only after exact-head technical/security/ownership/dependency evidence passes, advances accepted work in dependency order, and remains the only role that decides final promotion into `main`.

### Completion and interrupt protocol

A module agent announces genuine completion by posting the exact top-level PR comment:

**Work Done and Submitted**

That signal causes the Supervisor to checkpoint and pause its own current module work, review the submission, integrate it if approved, verify the resulting accepted baseline, send the required refresh alert, refresh its own branch if affected, and resume from the saved checkpoint.

After every accepted integration the Supervisor sends exactly:

**New changes have been merged — please merge these changes into your branch first, then resume your own work.**

Issue #82 is the durable Supervisor Coordination Feed. Each event includes the new accepted baseline SHA and whether it is `main` or `parallel/integration-staging`.

Every active agent must refresh to that accepted baseline, update dependency/base evidence, rerun affected tests and instruction/ownership checks, and only then resume. Completion time does not override dependency order.

### Module ownership

Each active module/path has one implementation owner. Module agents stay within explicit allowed paths and use stable interfaces/contracts for cross-module dependencies. Shared CI/build/state/contracts/composition/migration integration are owned by Supervisor/Platform/Integration roles unless a work item explicitly grants otherwise.

### Dependency-aware scheduling

Roadmap work should be represented as a DAG with `RESERVED`, `READY`, `ACTIVE`, `BLOCKED`, `VERIFYING`, `VERIFIED`, `SUBMITTED`, and `INTEGRATED` states. Independent modules start from a common stable integration baseline. Feature stacking is used only for genuine dependencies.

The core technical dependency chain is:

`brokerage -> orders -> portfolio -> risk -> compliance -> ai-shadow`

Ledger, Identity/Authorization, Notifications, Observability, Performance/Resilience and other genuinely independent READY slices may run concurrently.

Existing draft implementations must be reconciled before newly reserved parallel branches recreate the same module work.

### Integration model

Module agents produce small reviewable PRs and local verification evidence. Platform/Database/Integration agents own high-contention shared wiring. An Architecture/Conflict agent checks ownership, dependency drift, circular references, duplicate implementation and shared-file collisions before integration.

The requested end-state is reviewed promotion into protected `main`. While the current repository governance gate blocks safe main promotion, accepted work may be technically combined only on `parallel/integration-staging`; staging does not equal production or phase acceptance.

### Instruction synchronization

Every engineering agent must perform an instruction-drift check at every task/session start and after every accepted-baseline refresh. When architecture, workflow, tools, branch assignment, module ownership, dependencies, testing commands, safety boundaries or integration process change, the applicable canonical instructions must be updated in the same PR. Repository-level workflow changes must be reflected in `README.md`; agent process changes in `AGENTS.md`; global engineering protocol changes in `.ai/MASTER_ENGINEERING_PROMPT.md`; ownership/dependency/Supervisor changes in `.ai/agent-orchestration.yaml`; branch/readiness changes in `.ai/parallel-branch-registry.yaml`; module-specific changes in the relevant module documentation/README.

### M-AGENT foundation milestones

- **M-AGENT-01:** documentation + orchestration manifest + Supervisor workflow + branch registry;
- **M-AGENT-02:** path/module ownership validator;
- **M-AGENT-03:** automatic verifier discovery;
- **M-AGENT-04:** per-work-item registry + dependency readiness/base-SHA validation;
- **M-AGENT-05:** stable integration baseline + merge train + completion/refresh coordination automation;
- **M-AGENT-06:** database migration integration workflow;
- **M-AGENT-07:** architecture/conflict automation;
- **M-AGENT-08:** concurrency scale test before raising the recommended ceiling.

Engineering parallelism, Supervisor integration and fast branch refreshes must never bypass financial, regulatory, security, broker, Risk Governor, Compliance Gate, reconciliation, or production authorization gates.
