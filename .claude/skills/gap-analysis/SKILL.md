---
name: gap-analysis
description: Compares the Critical Viewer codebase as-it-stands against the 4 features in the kickoff brief (Account Creation, Password Change, Movie List/Search, Movie Detail View) and reports Not Started/In Progress/Complete per feature, backed by specific files. Use before starting feature work, when asked "what's left", or as the first step of /build-feature and /daily-report.
---

# Gap analysis

Determine, for one feature or all 4, what the codebase actually has versus
what [CLAUDE.md](../../../CLAUDE.md) and
`docs/Critical Viewer Kickoff Brief.pdf` ask for. Every status claim must
cite a real file — no "probably done".

## Process

1. Read the feature list in `CLAUDE.md` (or re-read the brief PDF if a
   requirement's exact wording matters). If scoped to one feature, only
   analyze that one.
2. For each feature in scope, check both sides of the stack:
   - **Backend**: does an entity/DbContext config exist for the data it
     needs? Is there a controller/endpoint? Does it match the feature's
     described behavior (e.g. Movie List/Search must default to the
     *current* release year and paginate at exactly 100/page — check the
     actual query logic, not just that a `/movies` endpoint exists).
     Relevant paths: `backend/src/CriticalViewer.Core/Entities/`,
     `backend/src/CriticalViewer.Infrastructure/`,
     `backend/src/CriticalViewer.Api/Controllers/`,
     `backend/src/CriticalViewer.Api/Contracts/`.
   - **Frontend**: does a page/component exist and is it wired to the
     real API client (`frontend/src/api/`), not stubbed/hardcoded?
     Relevant paths: `frontend/src/pages/`, `frontend/src/components/`.
   - **Tests**: does either side have a test that actually exercises the
     feature (not just a scaffold placeholder)?
3. For anything not Complete, name the specific next step (e.g. "needs a
   `MoviesController` with title/genre/director/year filters and
   pagination" — not "needs backend work").
4. Grep `docs/progress/` for the most recent dated file to see what was
   already reported as in-progress/blocked, so the new analysis is
   consistent with (or explicitly corrects) the last one instead of
   silently contradicting it.

## Output format

One block per feature:

```
### <Feature name>
Status: Not Started | In Progress | Complete
Backend: <what exists, with file paths, or "none">
Frontend: <what exists, with file paths, or "none">
Tests: <what exists, or "none">
Next: <the specific next step, omit if Complete>
```

Keep it factual and short — this is a diagnostic, not a status report for
the client (that's what `progress-log` is for).
