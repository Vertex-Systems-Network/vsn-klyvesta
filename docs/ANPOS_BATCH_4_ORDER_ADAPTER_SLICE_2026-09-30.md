# Batch 4 order adapter slice

The sandbox adapter now exposes:

- `SubmitOrderAsync` with positive-quantity validation before network submission.
- `GetOrderAsync` for lifecycle recovery.
- `CancelOrderAsync` with no assumption that cancellation means zero fills.
- Safe normalized states: success, rejected, retryable read failure and unknown.
- Ambiguous timeout/network failure returns `UNKNOWN`; no blind order retry is performed.

This slice uses the already executed sandbox lifecycle evidence. It does not claim idempotency, cancel/fill race guarantees, fees, settlement, quotes or streaming semantics until those provider contracts are evidenced.
