---
name: quality-gate
description: Runs the same lint + unit test checks CI enforces (dotnet format + dotnet test for backend, eslint + vitest for frontend) and reports pass/fail per suite. Use before calling any feature done, before a commit, or whenever asked to check that lint/tests are green.
---

# Quality gate

Mirrors `.github/workflows/backend.yml` and `.github/workflows/frontend.yml`
exactly, so "green locally" means "green in CI" — per the kickoff brief, a
PR can't be approved if either lint or tests have issues, so this should
never be the first place a failure is discovered.

## Process

Run whichever side(s) changed (or both, if unsure):

**Backend** (from `backend/`):
```bash
dotnet format CriticalViewer.sln --verify-no-changes
dotnet test CriticalViewer.sln --configuration Release
```

**Frontend** (from `frontend/`):
```bash
npm run lint
npm test
```

Run lint before test for each side — a lint failure usually means the
test run isn't worth waiting on yet.

## On failure

Don't just report red/green — fix it:
- `dotnet format` failures are auto-fixable: run `dotnet format
  CriticalViewer.sln` (no `--verify-no-changes`) to apply fixes, then
  re-verify.
- `eslint` failures: check if `npm run lint -- --fix` resolves them; if
  not, fix the specific rule violation.
- Test failures: read the failure output, fix the actual bug or test —
  never loosen an assertion or skip a test just to get green.

Re-run the failed suite after fixing to confirm before reporting done.

## Output format

```
Backend lint:  PASS | FAIL (<reason>)
Backend tests: PASS | FAIL (<n failed> — <short reason>) | SKIPPED
Frontend lint: PASS | FAIL (<reason>) | SKIPPED
Frontend tests: PASS | FAIL (<n failed> — <short reason>) | SKIPPED
```

Mark a suite `SKIPPED` (not a silent omission) when it wasn't run because
that side of the stack didn't change — never omit a row.
