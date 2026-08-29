# Critical Viewer — Technical Brief

## Stack Summary

- Frontend: React; TypeScript; Vite (SPA, dev-mode server, no production build shipped yet)
- Backend / API: C#; ASP.NET Core (.NET 10); EF Core
- Auth: ASP.NET Core Identity (`AspNetUsers`, GUID keys); JWT bearer tokens
- Database: MySQL 8.4 (migrated from SQL Server); EF provider `Microting.EntityFrameworkCore.MySql` (community Pomelo fork tracking .NET 10)
- Reverse proxy: nginx (official image); templated config via `envsubst`; single public entry point
- Containerization: Docker; Docker Compose (local dev — 4 services: `database`, `db_seed`, `backend-api`, `frontend-ui`, `proxy`)
- CI: GitHub Actions — lint + unit tests per PR, scoped to whichever of frontend/backend changed
- Testing: xUnit (backend); Vitest (frontend)
- Local quality gate: `dotnet format --verify-no-changes` + `dotnet test`; `npm run lint` + `npm test`; enforced via a `PreToolUse` git hook
- Styling: plain CSS, single shared base stylesheet (`docs/index.css`) — no CSS framework

## Hosting / Infrastructure — AWS Lightsail

- Compute: **Lightsail Container Service** (`container-service-1`), `nano` power, region `us-east-2`
- 4 containers per deployment, one Lightsail service:
  - `proxy` (nginx) — the only publicly-reachable container; Lightsail Container Service allows exactly one public container per deployment
  - `backend-api` (ASP.NET Core)
  - `frontend-ui` (Vite dev server, not yet a production build)
  - *(local-only, not deployed)* `db_seed` — one-shot MySQL seeding container
- Networking: all containers in one deployment share a single network namespace (pod-style) — no per-container DNS, so upstreams are addressed via `localhost:<port>`, not service names (differs from Docker Compose's per-service DNS)
- Database: **Lightsail Managed Database**, MySQL 8.4.11, separate app-scoped DB user (`criticalviewer_app`) distinct from the master user
- Public endpoint: single URL, `https://container-service-1.8w23bem0htgvm.us-east-2.cs.amazonlightsail.com/`, health-checked at `/api/health` via `proxy`
- Image versioning: numeric only (no `:latest`/`.LATEST` token) — current version checked manually via `aws lightsail get-container-images` before each push
- Deploy path: manual `aws lightsail` CLI — `push-container-image`, then `create-container-service-deployment --containers file://... --public-endpoint file://...` (config split across `deploy/lightsail/containers.json` and `deploy/lightsail/public-endpoint.json`)
- IaC: none — no Terraform for this deployment target (an earlier `infra/terraform/` layout targeting ECS + RDS was removed as it never matched the real deployment path); Lightsail is managed by hand via CLI
- CI/CD: not wired up — every deployment tonight was run manually, no automated pipeline triggers a Lightsail deployment yet

## Notable Design Decisions

- Lightsail's one-public-container constraint is why a dedicated nginx `proxy` container exists at all — it fronts both the API and the SPA so they share one public origin, which also removes the need for CORS in production
- Deployment target (Lightsail over ECS/RDS) was a cost-driven decision — see `docs/progress/2026-08-26.md`
- MySQL over SQL Server was likewise a migration for platform/cost reasons — see the same progress log
