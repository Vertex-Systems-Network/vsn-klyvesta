# UI/UX Delivery Batch Plan V1

Repository: `Vertex-Systems-Network/vsn-klyvesta`

## Goal

Turn the current demo UI into a lovable, accessible and evidence-driven investor product experience without enabling live trading, customer money movement or production authority.

The visual target is premium financial software: clear, calm, trustworthy, delightful and beginner-friendly. It must not use casino-style urgency, guaranteed-return language, manipulative auto-trading prompts or excessive red/green stimulation.

## Current baseline

Available now:
- demo login;
- one dashboard page with overview, profile, risk, portfolio, goals, watchlist, paper orders, activity, AI insight and safety sections.

Current baseline is a functional demo foundation, not a complete investor product.

## UI delivery batches

| Batch | Name | Scope | Acceptance |
| --- | --- | --- | --- |
| UI-0 | Evidence audit and information architecture | Route inventory, persona journeys, navigation map, content hierarchy, responsive breakpoints and missing-screen register. | Every planned screen has an owner, entry point, exit state and non-live boundary. |
| UI-1 | Design system and visual identity | Klyvesta tokens, typography, spacing, surfaces, icons, charts, states, empty/loading/error patterns, light/dark themes and accessibility baseline. | Tokenized components, WCAG contrast review, keyboard focus, reduced-motion behavior and screenshot evidence. |
| UI-2 | Onboarding and investor setup | Welcome, account preview, investor profile, risk questionnaire, goals and safety education. | Beginner can complete the demo journey without confusion; validation and error states covered. |
| UI-3 | Investor home and portfolio | Home dashboard, portfolio summary, allocation, performance, risk health, cash, holdings, activity and notifications. | Responsive dashboard with clear hierarchy, chart states, freshness labels and no misleading financial claims. |
| UI-4 | Market discovery and paper trading | Market overview, search, instrument detail, watchlist, price/chart states, paper order ticket, confirmation, orders and fills. | Paper-only order flow passes validation, rejection, loading, stale-data and empty-state checks. |
| UI-5 | AI intelligence experience | AI assistant, recommendation cards, “why this”, downside/scenario view, confidence/freshness, approve/reject/edit and evidence history. | AI cannot execute directly; every action is structured, explainable and visibly non-live. |
| UI-6 | Guarded Auto and operations surfaces | Mandate wizard, pause/resume, kill switch, AI action history, admin/risk console, incidents and reconciliation views. | Guarded Auto remains feature-flagged off; controls are visible, auditable and fail closed. |
| UI-7 | Cross-device QA and release certification | Mobile navigation, tablet/desktop layouts, accessibility, visual regression, performance, security headers and demo safety review. | Browser/device matrix passes with screenshot/evidence pack; no critical UI or safety defects. |

## Required screens

### Investor

Onboarding, profile, risk assessment, goals, home, portfolio, holdings, market overview, search, instrument detail, watchlist, paper order ticket, order confirmation, orders/fills, AI assistant, recommendation detail, notifications, funding/status placeholder, statements placeholder and security/settings.

### Guarded Auto

Mandate setup, risk limits, allowed universe, pause conditions, consent summary, active/paused state, AI action history and emergency pause.

### Operations

Customer/KYC status, broker status, funding/reconciliation, orders/fills, risk breaches, AI decisions, complaints, incidents, model/prompt versions, feature flags, health and kill switch.

## Definition of done

A UI batch is complete only when:
- implementation exists in the repository;
- navigation and empty/loading/error states are covered;
- responsive behavior is tested;
- accessibility and reduced-motion behavior are checked;
- screenshots or deterministic browser evidence are attached;
- demo/live boundaries remain explicit;
- CI passes;
- README progress is updated from evidence.

No UI completion unlocks live trading, KYC, custody, funding, withdrawals or production AI authority.
