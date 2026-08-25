# Product Requirements

## Personas

### Beginner Investor
Has money to invest but little market knowledge. Wants simple goals, clear risk, and minimal decisions.

### Assisted Investor
Wants AI recommendations but prefers confirming trades.

### Experienced Investor
Wants manual trading, charts, watchlists, orders, and AI research as optional tooling.

### Compliance / Operations
Needs complete customer, KYC, transaction, recommendation, model, and exception history.

### Risk Officer
Needs portfolio exposure, breaches, model behavior, drawdown, liquidity and kill-switch controls.

## Core journeys

### Registration and KYC
1. Create account.
2. Verify email/phone/device.
3. Identity/KYC flow through approved broker/API path.
4. Complete suitability questionnaire.
5. Create investor risk profile.
6. Create brokerage account.
7. Show funding instructions/status.
8. Activate only after all required approvals.

### Deposit
Customer funds must follow the approved broker/custody funding flow. Klyvesta must not silently treat ordinary company funds as client investment money.

### First investment
Customer chooses:
- Manual
- AI Assisted
- Guarded Auto (only if legally enabled)

### Manual trade
Search -> instrument detail -> order ticket -> pre-trade checks -> confirmation -> broker routing -> execution/fill -> ledger/portfolio -> notification.

### AI Assisted
Goal/risk -> AI proposal -> explanation -> scenario/risk view -> explicit user approval -> pre-trade checks -> execution -> monitoring.

### Guarded Auto
Mandate -> risk envelope -> funding -> allocation plan -> policy checks -> execution -> continuous monitoring -> rebalancing -> explainable notifications.

## Functional requirements

### Identity & Account
- registration/login
- passkeys/2FA
- device management
- KYC state
- broker account state
- consent/version tracking
- risk profile
- beneficiary/bank details where applicable

### Funding
- deposit status
- withdrawal request/status
- bank/funding references
- reconciliation
- ledger
- statements

### Markets
- market overview
- instruments
- search
- watchlists
- quote/candle data
- fundamentals where licensed/available
- corporate actions
- announcements/news if licensed

### Trading
- market/limit orders as supported
- cancel/modify
- order state machine
- partial fills
- rejected orders
- idempotency
- trading calendar/session controls
- position and cash validation

### Portfolio
- holdings
- cash
- allocation
- realized/unrealized P&L
- performance
- benchmark comparison
- risk metrics
- contribution analysis
- transaction history

### AI
- conversational investor assistant
- recommendation cards
- portfolio construction
- recurring investment planner
- rebalance proposals
- risk alerts
- “Why this?” explanation
- uncertainty/confidence display
- evidence freshness
- model/version disclosure internally

### Auto Mandate
- versioned mandate
- explicit consent
- limits
- pause/resume
- emergency kill switch
- exit-to-cash instruction where legally/operationally supported
- cancellation/termination workflow
- audit history

### Admin
- customers
- KYC/broker status
- accounts
- deposits/withdrawals
- orders/fills
- reconciliation
- complaints
- risk breaches
- AI decisions
- prompt/model versions
- compliance cases
- feature flags
- incident controls

## Out of scope for initial launch
- leverage
- margin
- short selling
- derivatives
- crypto
- copy trading
- social trading
- guaranteed-return products
- high-frequency trading
- unbounded autonomous strategies
