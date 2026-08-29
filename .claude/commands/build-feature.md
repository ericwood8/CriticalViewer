---
description: Implement one kickoff-brief feature end-to-end — gap analysis, subagent dispatch, quality gate, progress log.
---

Feature: $ARGUMENTS

If no feature name was given, ask which of the 4 features in `CLAUDE.md`
to build before doing anything else.

Otherwise, drive this feature through the full workflow:

1. **Gap analysis first.** Run the `gap-analysis` skill scoped to this
   feature. Don't skip this even if you think you remember the state from
   earlier in the conversation — code may have changed since.
2. **Plan the split.** From the gap analysis, identify what backend work
   and what frontend work remain. If both remain and the frontend work
   doesn't depend on an API contract that isn't designed yet, dispatch
   `backend-builder` and `frontend-builder` as parallel subagents (single
   message, both Agent calls) — give each one the specific gap-analysis
   findings relevant to its side, not the whole conversation. If the
   frontend needs a backend contract (endpoint shape, DTO) that doesn't
   exist yet, run `backend-builder` first, then `frontend-builder` with
   the resulting contract described concretely (endpoint, request/response
   shape).
3. **Quality gate.** Once subagents report back, run the `quality-gate`
   skill yourself against the whole repo (not just the changed side) — a
   backend contract change can break a frontend that was already passing.
   Fix anything red before moving on; don't hand that back to the user.
4. **Progress log.** Run the `progress-log` skill to record today's
   result for this feature (and re-verify the other 3 while at it, since
   the skill re-runs gap-analysis for all 4 anyway).
5. **Report back** in the day-by-day format from the brief: feature name,
   status, one-line detail — plus anything the user needs to decide
   (ambiguous requirement, a blocker) surfaced explicitly, not buried.
