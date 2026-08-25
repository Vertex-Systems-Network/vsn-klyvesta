# AGENTS.md — AI Engineering Protocol

Before engineering work:

1. Read `.ai/state.json`.
2. Read `.ai/guardrails.md`.
3. Read `.ai/acceptance-gates.yaml`.
4. Inspect current Git HEAD/branch.
5. Inspect open PRs for the active task.
6. Work only on the active task and prerequisites explicitly required by it.
7. Do not silently redefine roadmap or acceptance criteria.
8. Do not mark DONE without acceptance evidence.
9. Record failures/blockers.
10. Update machine-readable state only after acceptance.
11. Commit focused changes.
12. End each session with:
   - current HEAD
   - active task
   - completed work
   - tests executed
   - acceptance result
   - remaining blocker
   - exact next action

Financial/AI rule:
No implementation may allow an LLM or agent to bypass Risk Governor, Compliance Gate, Execution Validator, mandate, or audit recording.
