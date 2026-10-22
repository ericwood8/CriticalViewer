# Lightsail deploy tool

`lightsail_deploy.py` (Python 3, no packages, needs the AWS CLI) runs the manual deploy path of `docs/technical-brief.md` the same way every time. The config it reads is `deploy/lightsail/containers.json` (real secrets, gitignored) and `deploy/lightsail/public-endpoint.json`.

| Command | What it does |
|---|---|
| `status` | Service state, the newest deployments, the images of the current deployment, and whether a newer image was pushed or `containers.json` differs from what is running. |
| `push [--only proxy,backend-api,frontend-ui] [--build]` | Pushes `my-nginx-proxy:local`, `my-aspnet-api:local`, `my-react-ui:local` and prints the version each got. `--build` runs `docker compose build <service>` first (build only, no dependent service is started). |
| `deploy [--dry-run] [--no-wait] [--timeout 900]` | Validates the config, moves each image to the highest pushed version, writes `containers.json`, creates the deployment, polls the deployment state until ACTIVE or FAILED, prints the redacted container logs on a failure, and on success requests `/` and `/api/health` on the public URL. |
| `logs [container ...] [--lines 20]` | Last log lines per container with passwords and keys replaced by REDACTED. |
| `--self-test` | Checks the validation, the version choice, the redaction and a whole rollout against a fake `aws` (success and failure). |

A normal redeploy after a change:

```bash
python tools/deploy/lightsail/lightsail_deploy.py push --build --only frontend-ui
python tools/deploy/lightsail/lightsail_deploy.py deploy --dry-run
python tools/deploy/lightsail/lightsail_deploy.py deploy
```

`deploy` refuses to run while a `REPLACE_ME` placeholder is left, an image is not a numeric version of this service, a container name has an underscore, a proxy upstream is not `localhost:<port>`, or the public endpoint names a container that is not in the file. It never prints a value from `containers.json`.

Not covered: the Lightsail service itself (`create-container-service`), the database, and rollback (deploy an older `containers.json` image version again).
