# Demo User Data Foundation

Status: demo-only, non-production, API-independent product preview.

The demo environment persists a synthetic investor profile without PostgreSQL or pyPSX so the user experience can be tested with state that survives application restarts.

## Persisted demo state

- investor display name;
- experience level;
- investment horizon;
- primary investment goal;
- monthly paper contribution;
- deterministic demo risk answers, score and band;
- user-created investment goals;
- synthetic watchlist symbols;
- seeded paper portfolio and paper-order display state;
- recent demo activity events.

Default file:

```text
src/Klyvesta.Api/App_Data/demo-user-state.json
```

The runtime directory is excluded from Git. The file must never be used for real customer KYC, restricted PII, broker credentials, bank identifiers or production financial records.

The path can be overridden through:

```text
DemoMode__DataFile=/absolute/or/host-specific/path/demo-user-state.json
```

The hosting process needs write permission to the configured directory.

## Boundaries

This store is intentionally not a replacement for the canonical PostgreSQL customer/domain model. It exists only to make the DB-free demo stateful while the canonical database/user-data lanes are developed.

The demo remains fail-closed for Production: `DemoMode:Enabled=true` is accepted only in `Development` or `Demo` environments.

No pyPSX request, live quote, real order, deposit, withdrawal, KYC verification or real-money side effect is performed.

## Demo endpoints

Authenticated demo session:

- `GET /api/demo/dashboard`
- `GET /api/demo/profile`
- `PUT /api/demo/profile`
- `PUT /api/demo/risk-profile`
- `GET /api/demo/goals`
- `POST /api/demo/goals`
- `DELETE /api/demo/goals/{goalId}`
- `POST /api/demo/watchlist`
- `DELETE /api/demo/watchlist/{symbol}`
- `POST /api/demo/reset`

State-changing requests require the same-origin demo session cookie and the custom `X-Demo-Request: 1` header used by the bundled UI. These controls are demo safeguards, not production authentication or authorization.

## Risk indicator

The risk score is deterministic and derived only from four synthetic questionnaire dimensions:

- loss tolerance;
- market experience;
- horizon capacity;
- liquidity need.

It is explicitly a UI/product-development indicator. It must not be represented as production suitability assessment, regulated advice or an eligibility decision.
