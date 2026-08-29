# Critical Viewer

Movie review & discovery platform for Critical Viewer LLC, built by Struct
Development. Full context lives in
[docs/Critical Viewer Kickoff Brief.pdf](docs/Critical%20Viewer%20Kickoff%20Brief.pdf)
— read it before scoping any feature work. Note: the brief's due dates are
all in the past relative to today; ignore the dates, the feature list and
standards below are still the active spec.

## Product

Visitors can search/browse a movie catalog by title, genre, director, or
year with no account. Registered users can additionally leave a star
rating + written review on a movie and see what others said about it.

## First delivery milestone — 4 features

1. **Account Creation** — email + password signup.
2. **Password Change** — authenticated users only, from an account view.
3. **Movie List / Search** — public, no login. Filter by title, genre,
   director, year (defaults to current release year). Paginated, always
   100 items per page.
4. **Movie Detail View** — title, poster, genre, director, release year,
   tagline, one-paragraph summary. Infinite-scroll review list (reviewer
   username, 5-star rating, 1–4 sentence body), 10 at a time. "Leave a
   Review" button opens the review modal for logged-in users, or prompts
   login first for anonymous visitors.

## Engineering standards (apply to every feature)

- Frontend and backend both ship with unit tests.
- Deployment target is AWS Lightsail Container Service (cost-driven
  decision — see `docs/progress/2026-08-26.md`), managed today via `aws
  lightsail` CLI commands rather than Terraform. The `infra/terraform/`
  layout that previously targeted ECS + RDS was removed since it never
  matched the real deployment path.
- GitHub Actions run lint + the unit test suite on every PR for whichever
  of frontend/backend changed (`.github/workflows/`). **A PR cannot be
  approved if either has issues** — this is enforced locally too, see
  "Quality gate" below.
- UI is built on `docs/index.css`, the client's base stylesheet. Extend it
  as needed; don't introduce a second, competing style system.

## Stack specifics

- Backend: ASP.NET Core (.NET 10) + EF Core against MySQL (migrated from
  SQL Server — see `docs/progress/2026-08-26.md` for why), ASP.NET Core
  Identity for auth (`AspNetUsers`, GUID keys) — this is the real auth
  store; don't confuse it with the demo `Reviewers` table in
  `backend/src/CriticalViewer.Infrastructure/Migrations/CriticalViewerDB.sql`,
  which that file's own header says is a standalone schema, not a
  drop-in replacement for Identity. `Movie`/`Review` entities use `Guid`
  keys and are wired column-for-column to that SQL file (see its header
  comments for the authoritative schema). EF provider is
  `Microting.EntityFrameworkCore.MySql`, a community fork of Pomelo that
  tracks .NET's release cycle — upstream Pomelo hasn't cut an EF Core 10
  release yet; swap back once it does, API surface should be identical.
- Frontend: React + TypeScript + Vite SPA.
- Local backend DB connection targets `localhost:3306` via MySQL
  username/password auth (`appsettings.json`) — MySQL has no Windows
  Integrated Auth equivalent, unlike the SQL Server setup this replaced.

## The Claude Code workflow for this repo

Per the kickoff brief's Workflow Expectations, feature work in this repo
should run through the tooling below rather than ad hoc — it's what keeps
gap analysis, subagent delegation, and daily reporting actually happening
instead of being a one-off.

| Need | Use |
|---|---|
| "What's built vs. what's missing for feature X (or all 4)?" | `/gap-analysis [feature name]` |
| Implement a feature end-to-end | `/build-feature <feature name>` |
| Just check lint/tests are green right now | `/quality-gate` |
| Write/update today's status file | `/daily-report` |

- **Gap analysis** (`gap-analysis` skill) compares the current codebase
  against the 4 features above and reports, per feature: Not Started /
  In Progress / Complete, with the specific files/endpoints/components
  that back that judgment — never a guess.
- **Subagents**: `/build-feature` splits remaining work into scoped
  subagent runs — `backend-builder` for entities/EF/controllers/xUnit,
  `frontend-builder` for components/pages/vitest — dispatched in
  parallel when the remaining backend and frontend work don't depend on
  each other, sequentially when the frontend needs a backend contract
  that isn't there yet.
- **Quality gate** (`quality-gate` skill): `dotnet format --verify-no-changes`
  + `dotnet test` for backend, `npm run lint` + `npm test` for frontend.
  `/build-feature` runs this before calling a feature done. It's also
  enforced by a `PreToolUse` hook (`.claude/hooks/pre-commit-gate.sh`)
  that blocks `git commit` outright if either suite is red — belt and
  suspenders with CI, but catches problems before they even reach a PR.
- **Daily reports** (`progress-log` skill): one file per day at
  `docs/progress/YYYY-MM-DD.md` (see `docs/progress/TEMPLATE.md`), one
  line per feature in the brief's own format:
  `<Feature>. <status>. <one-line detail>.` Re-running `/daily-report`
  the same day updates that day's file rather than duplicating it.

Don't hand-roll a status update or a lint/test check outside these — the
point of checking this workflow in is that it's the same path every time,
so the day-by-day report is trustworthy and nothing merges that fails the
brief's own bar.
