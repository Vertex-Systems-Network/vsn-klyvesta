# New Agent Onboarding

This is the canonical onboarding rule for agents joining an active Klyvesta parallel-development wave.

## Mandatory arrival state

A new agent always arrives on **`main`**. It must not begin module implementation from another feature/integration branch before Supervisor assignment.

## Supervisor allocation

Immediately after the new agent arrives, the Supervisor reads `.ai/parallel-branch-registry.yaml` and the durable work-item registry.

A free slot exists only when all are true:

- registry status is `READY`;
- occupancy is `OPEN`;
- the corresponding work item is `READY`;
- no active agent already owns the module/path.

The Supervisor selects the lowest merge-order group first, then module name for deterministic tie-breaking. It marks the slot `OCCUPIED`, records the agent name/start time/start status, marks the work item `ACTIVE`, and tells the agent to checkout the already-created canonical module branch.

Starting from `main` is an onboarding requirement; actual implementation happens on the assigned module branch. Before coding, that branch must contain the current accepted `parallel/integration-staging` baseline.

## No free slot

If every eligible module slot is occupied/unavailable, the Supervisor stops onboarding immediately and sends exactly:

**Go Home Come Back Next Time**

The unassigned agent does not create another branch, does not choose a module itself, and does not start implementation.

## Helper

Supervisor-side deterministic allocation helper:

`NEW_AGENT_NAME=<name> NEW_AGENT_START_BRANCH=main ruby scripts/onboard-new-agent.rb`

Use `--apply` only when the Supervisor intends to update the plan files in its working tree and commit the assignment.
