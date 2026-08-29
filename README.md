# Critical Viewer

Movie review and discovery platform for Critical Viewer LLC. Backend is
ASP.NET Core (.NET 10) against MySQL; frontend is a React + TypeScript
SPA built on the client's own `index.css` design system.

## Repo layout

```
backend/     ASP.NET Core Web API, EF Core, xUnit tests (open backend/CriticalViewer.sln in Visual Studio)
frontend/    React + TypeScript + Vite SPA
infra/       Terraform for the AWS deployment target
.github/     GitHub Actions CI/CD (separate pipelines for frontend and backend)
docs/        Daily progress reports (see docs/progress/README.md)
```

## Prerequisites (Windows)

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 22 LTS](https://nodejs.org/) (already confirmed installed)
- MySQL Server (8.4+ recommended) - a local install, or run the
  `database` service from `docker-compose.yml`
- [Terraform CLI](https://developer.hashicorp.com/terraform/install) - only
  needed when you're touching `infra/`
- [AWS CLI](https://aws.amazon.com/cli/) - only needed for deploys
- Docker Desktop - only needed if you want to build/run the API container
  locally; day-to-day backend dev works fine without it via `dotnet run`

Visual Studio, VS Code, and GitHub Desktop (already installed) cover
everything else.

> This scaffold was authored without network access, so NuGet package
> versions in the `.csproj` files and npm versions in `package.json` are
> reasonable placeholders, not verified-latest. The first thing to do
> after cloning is let Visual Studio's NuGet manager / `npm install`
> resolve them, and bump anything that's out of date.

## Running locally

**Backend:**
```
cd backend
dotnet restore
dotnet ef database update --project src/CriticalViewer.Infrastructure --startup-project src/CriticalViewer.Api
mysql -h localhost -u root -p CriticalViewer < src/CriticalViewer.Infrastructure/Migrations/CriticalViewerDB.sql
dotnet run --project src/CriticalViewer.Api
```
The `dotnet ef database update` step only creates the ASP.NET Core
Identity tables (`AspNetUsers` etc.) - `Movies`/`Reviews`/`Reviewers` are
owned by `CriticalViewerDB.sql`, not EF migrations (see that file's
header), so both steps are needed. Or just open `backend/CriticalViewer.sln`
in Visual Studio and hit F5 - Swagger UI opens automatically at `/swagger`.

The JWT signing key is read from configuration but is **not** in
`appsettings.json`. Set it locally with:
```
cd backend/src/CriticalViewer.Api
dotnet user-secrets set "Jwt:SigningKey" "some-long-random-dev-only-string"
```

**Frontend:**
```
cd frontend
npm install
copy .env.example .env
npm run dev
```
Runs at `http://localhost:5173` and expects the API at
`http://localhost:5080` (matches the backend's `http` launch profile).

## Tests

```
cd backend && dotnet test CriticalViewer.sln
cd frontend && npm test
```
Both run in CI on every PR against `main` (see `.github/workflows/`), along
with lint. **Set these as required status checks under the repo's branch
protection rules for `main`** so a PR genuinely can't be approved while
either is failing, per the engineering standards in the kickoff brief.

## Infrastructure

Deployment target is AWS Lightsail Container Service, `container-service-1`
in `us-east-2` (cost-driven decision — see `docs/progress/2026-08-26.md`),
running MySQL via Lightsail Managed Database rather than a self-managed
container. Managed today via `aws lightsail` CLI commands, not Terraform —
an earlier `infra/terraform/` layout targeting ECS + RDS SQL Server was
removed since it never matched this deployment path. No IaC for the
current setup exists yet; that's a real gap if this needs to be
reproducible beyond one person's shell history.

## Status

`Account Creation` (register) and `Password Change` endpoints work but
have no dedicated tests or frontend UI yet. `Movie List / Search` and
`Movie Detail View` (including leaving a review, and adding a movie) are
implemented on both backend and frontend, with test coverage on each
side. See `docs/progress/` for day-by-day status.
