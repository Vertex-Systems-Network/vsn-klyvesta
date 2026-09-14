# Platform / CI Audit — M-AGENT-09

Status: ACTIVE — exact-head verification required before submission.

Worker: `ChatGPT-Platform-01`
Branch: `parallel/platform-ci`
Accepted product baseline: `54a968b51f0ccca59a8b5654209f6aefa8a546ad`

## Purpose

This lane exists to provide the missing canonical Platform/CI capacity described by the repository's parallel-development plan without creating another product-source writer or weakening the six-agent concurrency floor.

## Strict scope

Allowed substantive work is limited to:

- canonical registration of `parallel/platform-ci` in `.ai/agent-orchestration.yaml` and `.ai/parallel-branch-registry.yaml`;
- `.ai/work-items/platform-ci/**` and checkpoints;
- `tools/Klyvesta.PlatformVerifier/**`;
- this audit document.

The work item explicitly forbids `src/**`, API composition, contracts, migrations/model snapshots, shared package props and direct `.github/workflows/**` mutation.

## Verifier contract

`Klyvesta.PlatformVerifier` fails closed when any of these invariants drift:

1. `dotnet-foundation` stops convention-based `*Verifier.csproj` discovery in restore/format/build/run stages;
2. an external GitHub Action is referenced by a mutable tag/branch instead of an immutable commit SHA;
3. a workflow using `actions/checkout` does not disable persisted credentials;
4. workflow permissions are implicit or `write-all` is granted;
5. `pull_request_target` is introduced without a separately reviewed threat model;
6. canonical shared engineering paths disappear from centralized ownership policy;
7. the platform lane loses its canonical registration/worker assignment;
8. the platform work item stops explicitly forbidding product source, contract, or migration writes;
9. the current concurrency floor/ceiling is silently weakened from 6/10;
10. no-slot or accepted-baseline refresh safety signals drift.

## Non-goals

This lane does not certify production, regulatory, broker, PII, financial, release, or live environment authority. It does not modify business implementation and cannot substitute for independent review or the one-at-a-time integration train.

## Acceptance

Submission requires exact-head `agent-orchestration` and `dotnet-foundation` success, including execution of `Klyvesta.PlatformVerifier`, ownership validation, zero unauthorized changed paths, instruction-drift evidence, and independent review before integration.
