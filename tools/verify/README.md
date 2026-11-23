# Backend verification tool

`backend_image_check.py` (Python 3, no packages, needs Docker) proves the backend the way it runs in production: on Linux, in the container image, against a real MySQL. Run it after a change to the framework, a package, the Dockerfile, the EF model or startup code. The unit tests alone use an in-memory database and never touch the MySQL provider.

| Command | What it does |
|---|---|
| `linux-tests [--sdk-image IMAGE]` | Runs `dotnet test` on `backend/` (without `bin` and `obj`, whose files hold Windows paths) inside a throwaway container of the SDK image named by `global.json`. |
| `smoke [--image my-aspnet-api:local] [--build] [--year 2024]` | Starts a throwaway MySQL 8.4 and the backend image, loads `CriticalViewerDB.sql`, applies the EF migrations, checks for pending model changes, then calls the API: health, movie list and detail, registration, change-password, an anonymous review refused and a signed-in review accepted and listed, and no `fail:` or `crit:` line in the log. Everything it started is removed, also after a failure. |
| `--self-test` | Runs the checks against a stub API (a correct one, one that lets an anonymous review in, and none) with no Docker. |

```bash
python tools/verify/backend_image_check.py linux-tests
python tools/verify/backend_image_check.py smoke --build
```

Prerequisites it checks first (exit 3 with the fix): the Docker engine answers, the `mysql:8.4` image and the API image are local (it never pulls), and the `dotnet ef` tool has the major version of the EF Core packages (`dotnet tool update -g dotnet-ef --version 11.0.0`). Exit 4 means a `zz-cv-*` container or a port (18080, 18306) is already in use. Passwords and the signing key are generated per run and never printed.

Not covered: the front end, the proxy, the Lightsail deployment itself (`tools/deploy/lightsail`), and the container on Lightsail's own network.
