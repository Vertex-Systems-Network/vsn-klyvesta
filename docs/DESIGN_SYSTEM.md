# Product & UX Design

## Design principle

The interface should reduce cognitive load and communicate risk without encouraging gambling behavior.

Avoid:
- casino-like motion
- confetti for trades
- “hot stock” pressure
- countdown urgency
- guaranteed-profit language
- excessive green/red stimulation
- dark patterns that push Auto mode

## Visual direction

Premium financial interface:
- dark and light themes
- graphite/neutral base
- restrained accent color
- green/red reserved mainly for actual positive/negative financial semantics
- high contrast
- strong typography
- dense data only where user asks for it
- WCAG-oriented accessibility

## Beginner dashboard

Primary hierarchy:
1. Total portfolio value
2. Today / total performance
3. Risk health
4. Cash available
5. AI status
6. Current allocation
7. Next suggested action
8. “Why?” explanation
9. Deposit / Withdraw / Invest

Do not make stock-picking the default beginner home screen.

## AI Home

Conversational command surface:
- “I want to invest PKR 50,000 for 3 years.”
- “Why did my portfolio fall today?”
- “What changed since yesterday?”
- “Can I reduce risk?”
- “Show what AI plans to do next.”

All trade-impacting responses render structured action cards, not just prose.

## Recommendation card

Must show:
- proposed allocation/action
- risk level
- expected horizon
- key reasons
- key downside
- uncertainty
- cost/turnover impact
- data freshness
- approve / reject / edit (where legally applicable)

## Auto mode activation

Dedicated mandate wizard:
- goal
- horizon
- risk
- allowed universe
- max loss tolerance vs no-loss guarantee warning
- concentration
- cash buffer
- rebalance frequency
- permissions
- pause conditions
- final plain-language summary
- explicit consent

Auto mode must always show:
- ACTIVE / PAUSED
- current risk
- next scheduled review
- last AI action
- exact reason
- pause button

## Advanced/manual workspace

- watchlist
- chart
- order book/market depth if available/licensed
- order ticket
- positions
- orders/fills
- AI research panel
- risk impact preview

## Admin/Risk console

- system health
- broker health
- reconciliation exceptions
- risk breaches
- AI decision queue
- model versions
- flagged accounts
- failed KYC
- funding exceptions
- incidents
- feature flags
- global Auto-mode kill switch
