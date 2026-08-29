---
name: progress-log
description: Writes or updates today's day-by-day status file in docs/progress/, one line per feature in the kickoff brief's own format (e.g. "Account Creation. In progress. User tables and API endpoints complete."). Use at the end of a work session, after /build-feature completes, or whenever asked for a daily/status report.
---

# Progress log

Produces the report the brief asks for: something the project manager can
scan in under a minute and get an accurate, current picture from.

## Process

1. Run (or reuse a just-run) `gap-analysis` for all 4 features — the log
   must reflect actual current state, not what was true this morning.
2. Determine today's date (system date, not a guess) and the file path
   `docs/progress/YYYY-MM-DD.md`.
3. If that file already exists (this command already ran today), update
   it in place — don't create a second file or duplicate feature lines
   for the same day. Overwrite each feature's line with the current
   status; a feature that flipped from "In progress" to "Complete" today
   should just show "Complete" with what shipped, not both entries.
4. Follow `docs/progress/TEMPLATE.md` exactly:
   - One line per feature: `- <Feature>. <Not started|In progress|Blocked (why)|Complete>. <one-line detail>.`
   - A `## Blockers` section — `<none>` or one line per real blocker with
     what's needed to unblock it. Don't invent blockers; only list ones
     actually encountered (e.g. a missing decision, a broken dependency).
   - A `## Lint / test status` section — run (or reuse a just-run)
     `quality-gate` and summarize its result in one line, e.g. "backend
     and frontend suites green" or "frontend lint red — 3 unused-import
     violations in MovieDetailPage.tsx, fix in progress".
5. Keep each feature's detail line genuinely one line and specific — good:
   "User tables and API endpoints complete. User views in progress."; bad:
   "Making progress."

## Output

Write the file via the Write/Edit tool at `docs/progress/YYYY-MM-DD.md`,
then echo its final contents back in the response so the update is
visible without opening the file.
