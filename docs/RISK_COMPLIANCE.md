# Risk & Compliance

## Regulatory gate

Broker API access is not automatically equivalent to permission to provide personalized investment advice or discretionary portfolio management.

Before enabling production AI recommendations or Guarded Auto, obtain written legal/compliance determination of:
- role of Klyvesta
- role of underlying securities broker
- securities adviser requirements
- securities manager/discretionary portfolio requirements
- custody/funding responsibilities
- KYC/AML responsibilities
- customer agreement/mandate requirements
- complaint handling
- reporting/record retention
- marketing/performance claim restrictions
- data/privacy obligations

## Product risk policy

Initial Auto universe should exclude:
- leverage
- margin
- derivatives/futures
- shorting
- highly illiquid instruments
- suspended/restricted symbols
- securities failing data-quality thresholds

## Example hard constraints

These are design defaults, not final regulated policy:
- single-name concentration cap
- sector concentration cap
- minimum cash buffer
- minimum liquidity/ADV threshold
- maximum portfolio volatility band
- maximum turnover per rebalance
- maximum daily traded value
- maximum order-size vs liquidity
- stale-price threshold
- price-deviation guard
- drawdown warning/pause thresholds
- no self-increasing risk after losses

Final numerical values must be approved by investment/risk governance.

## Drawdown behavior

Never implement martingale or “double down to recover.”

Possible policy:
1. warn
2. reduce risk within mandate
3. increase cash
4. pause new risk
5. require human/risk review

The system must not represent these controls as eliminating losses.

## Suitability

Risk profile should include:
- investment objective
- horizon
- loss capacity
- emergency liquidity
- knowledge/experience
- income/stability
- concentration elsewhere if captured
- restrictions/preferences

A user selecting “high return” does not automatically authorize high risk.

## Conflict of interest

AI objective must not optimize:
- commissions
- turnover
- affiliated securities
- engagement metrics

unless legally permissible and explicitly subordinated to customer interest with appropriate disclosure.

## Compliance evidence

Store:
- consent text/version
- mandate text/version
- risk profile version
- recommendation
- evidence
- model version
- policy results
- execution correlation
- subsequent modifications
- user notifications
