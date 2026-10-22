"""Deploys Critical Viewer to AWS Lightsail Container Service and proves the result. Python 3, no packages, needs the AWS CLI.

  python tools/deploy/lightsail/lightsail_deploy.py status                    service, deployments, and whether containers.json is behind the pushed images
  python tools/deploy/lightsail/lightsail_deploy.py push [--only proxy,frontend-ui] [--build] [--dry-run]
  python tools/deploy/lightsail/lightsail_deploy.py deploy [--dry-run] [--no-wait] [--timeout 900]
  python tools/deploy/lightsail/lightsail_deploy.py logs [CONTAINER ...] [--lines 20]
  python tools/deploy/lightsail/lightsail_deploy.py --self-test

push:    pushes my-nginx-proxy:local, my-aspnet-api:local and my-react-ui:local (the images docker-compose.yml builds) and prints the
         version Lightsail gave each. --build first runs `docker compose build <service>` for each (build only: no dependent services are started).
deploy:  checks deploy/lightsail/containers.json, moves each image to the highest pushed version of its label, writes the file back,
         creates the deployment, polls the DEPLOYMENT state (not the service state) until ACTIVE or FAILED, prints the container logs on a
         failure, and on success requests / and /api/health from the public URL. --dry-run prints the plan and the command and changes nothing.
logs:    the last lines of each container's log, with passwords and keys from containers.json replaced by REDACTED.

Checks before every deploy (the Lightsail rules that cost time): container names are lower case with hyphens, images are numeric versions of
this service (never :latest), no REPLACE_ME placeholder is left, the proxy reaches its siblings on localhost (no per-container DNS), the public
endpoint names one container that exists. containers.json holds real secrets and is gitignored; nothing here prints a value from it.
Options before the command name: --service (default container-service-1), --region (default us-east-2). Exit code 0 on success, 1 otherwise.
"""
import argparse
import json
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
CONFIG_DIR = REPO / "deploy" / "lightsail"
CONTAINERS_FILE = CONFIG_DIR / "containers.json"
ENDPOINT_FILE = CONFIG_DIR / "public-endpoint.json"
CONTAINERS_ARGUMENT = "file://deploy/lightsail/containers.json"
ENDPOINT_ARGUMENT = "file://deploy/lightsail/public-endpoint.json"

DEFAULT_SERVICE = "container-service-1"
DEFAULT_REGION = "us-east-2"
CONTAINER_NAME = re.compile(r"^(?:[a-z0-9]{1,2}|[a-z0-9][a-z0-9-]+[a-z0-9])$")
SECRET_NAME = re.compile(r"password|secret|key|token|pwd|connection", re.I)
UPSTREAM_NAMES = ("BACKEND_API_UPSTREAM", "FRONTEND_UI_UPSTREAM")
POLL_SECONDS = 15
HEALTH_ATTEMPTS = 6

# container name in containers.json -> (image label in Lightsail, service in docker-compose.yml). The local image is <label>:local.
CONTAINERS = {
    "proxy": ("my-nginx-proxy", "proxy"),
    "backend-api": ("my-aspnet-api", "backend-api"),
    "frontend-ui": ("my-react-ui", "frontend-ui"),
}


class Settings:
    service = DEFAULT_SERVICE
    region = DEFAULT_REGION
    secrets = []


def fail(message):
    print(redact(message), file=sys.stderr)
    sys.exit(1)


def redact(text):
    for secret in Settings.secrets:
        text = text.replace(secret, "REDACTED")
    return re.sub(r"(?i)\b(password|pwd)\s*=\s*[^;\s'\"]+", r"\1=REDACTED", text)


def secrets_of(containers):
    """The values that must never be printed: every secret looking environment variable and the password inside a connection string."""
    found = []
    for spec in containers.values():
        for name, value in (spec.get("environment") or {}).items():
            if isinstance(value, str) and value:
                if SECRET_NAME.search(name):
                    found.append(value)
                found += re.findall(r"(?i)(?:password|pwd)\s*=\s*([^;\s]+)", value)
    return sorted(set(found), key=len, reverse=True)


def aws(*args):
    result = subprocess.run(["aws", "lightsail", *args, "--region", Settings.region, "--output", "json"], cwd=REPO, capture_output=True, text=True)
    if result.returncode != 0:
        fail(f"aws lightsail {args[0]} failed:\n{result.stderr.strip()}")
    return json.loads(result.stdout) if result.stdout.strip() else {}


def read_json(path):
    if not path.exists():
        fail(f"{path.relative_to(REPO)} does not exist" + (" (copy containers.json.example and fill it in)" if path == CONTAINERS_FILE else ""))
    with open(path, encoding="utf-8", newline="") as f:
        raw = f.read()
    return json.loads(raw), "\r\n" in raw


def image_parts(image):
    """(service, label, version) of ':container-service-1.my-react-ui.10', or None."""
    match = re.match(r"^:([a-z0-9-]+)\.([a-z0-9-]+)\.(\d+)$", image or "")
    return (match.group(1), match.group(2), int(match.group(3))) if match else None


def latest_images():
    """label -> (highest version, image reference), compared as numbers (10 is newer than 9)."""
    latest = {}
    for item in aws("get-container-images", "--service-name", Settings.service).get("containerImages", []):
        parts = image_parts(item["image"])
        if parts and parts[0] == Settings.service and (parts[1] not in latest or parts[2] > latest[parts[1]][0]):
            latest[parts[1]] = (parts[2], item["image"])
    return latest


def placeholders(value, path=""):
    if isinstance(value, dict):
        return [p for k, v in value.items() for p in placeholders(v, f"{path}.{k}" if path else k)]
    return [path] if isinstance(value, str) and "REPLACE_ME" in value else []


def validate(containers, endpoint, service):
    problems = []
    for name, spec in containers.items():
        if not CONTAINER_NAME.match(name):
            problems.append(f"container name {name!r}: lower case letters, digits and hyphens only (no underscores)")
        parts = image_parts(spec.get("image"))
        if not parts or parts[0] != service:
            problems.append(f"{name}: image {spec.get('image')!r} is not a numeric version of this service, for example :{service}.my-react-ui.10 (there is no :latest)")
        for upstream in UPSTREAM_NAMES:
            value = (spec.get("environment") or {}).get(upstream)
            if value is not None and not str(value).startswith("localhost:"):
                problems.append(f"{name}: {upstream}={value!r} must be localhost:<port> (containers in a deployment share one network, there is no per-container DNS)")
    for path in placeholders(containers):
        problems.append(f"{path} still holds a REPLACE_ME placeholder")
    if endpoint.get("containerName") not in containers:
        problems.append(f"public-endpoint.json names container {endpoint.get('containerName')!r}, which is not in containers.json")
    return problems


def plan_images(containers, latest):
    """[(container, old image, new image)] where the highest pushed version of the label is newer than the one in the file."""
    changes, missing = [], []
    for name, spec in containers.items():
        parts = image_parts(spec.get("image"))
        if not parts:
            continue
        newest = latest.get(parts[1])
        if not newest:
            missing.append(f"{name}: no image labelled {parts[1]} has been pushed to {Settings.service}")
        elif newest[0] > parts[2]:
            changes.append((name, spec["image"], newest[1]))
    return changes, missing


def write_containers(containers, crlf):
    text = json.dumps(containers, indent=2) + "\n"
    with open(CONTAINERS_FILE, "w", encoding="utf-8", newline="") as f:
        f.write(text.replace("\n", "\r\n") if crlf else text)


def deployment_table(deployments, count=5):
    return [f"  {d['version']:>3}  {d['state']:<10} {d['createdAt']}" for d in deployments[:count]]


def cmd_status(args):
    service = aws("get-container-services", "--service-name", Settings.service)["containerServices"][0]
    print(f"service {service['containerServiceName']}: {service['state']}, power {service['power']}, scale {service['scale']}\nurl: {service['url']}")
    deployments = aws("get-container-service-deployments", "--service-name", Settings.service)["deployments"]
    print("deployments (newest first):\n" + "\n".join(deployment_table(deployments)))
    current = service.get("currentDeployment") or {}
    latest = latest_images()
    local = read_json(CONTAINERS_FILE)[0] if CONTAINERS_FILE.exists() else {}
    print(f"\ncurrent deployment {current.get('version')}:")
    for name, spec in (current.get("containers") or {}).items():
        label = (image_parts(spec["image"]) or (0, "?", 0))[1]
        newest = latest.get(label, (0, "?"))[1]
        notes = []
        if newest != spec["image"]:
            notes.append(f"newer pushed: {newest}")
        if local.get(name) and local[name]["image"] != spec["image"]:
            notes.append(f"containers.json has {local[name]['image']}")
        print(f"  {name}: {spec['image']}" + (f"   ({'; '.join(notes)})" if notes else ""))
    return 0


def cmd_push(args):
    chosen = [c.strip() for c in args.only.split(",")] if args.only else list(CONTAINERS)
    unknown = [c for c in chosen if c not in CONTAINERS]
    if unknown:
        fail(f"unknown container(s) {', '.join(unknown)}; choose from {', '.join(CONTAINERS)}")
    for name in chosen:
        label, compose_service = CONTAINERS[name]
        image = f"{label}:local"
        commands = ([["docker", "compose", "build", compose_service]] if args.build else []) + [
            ["aws", "lightsail", "push-container-image", "--region", Settings.region, "--service-name", Settings.service, "--label", label, "--image", image]]
        if args.dry_run:
            print("\n".join(subprocess.list2cmdline(c) for c in commands))
            continue
        if not args.build and subprocess.run(["docker", "image", "inspect", image], capture_output=True).returncode != 0:
            fail(f"local image {image} does not exist: run `docker compose build {compose_service}` or pass --build")
        for command in commands:
            result = subprocess.run(command, cwd=REPO, capture_output=True, text=True)
            if result.returncode != 0:
                fail(f"{subprocess.list2cmdline(command[:3])} failed:\n{(result.stderr or result.stdout).strip()[-1500:]}")
        ref = parse_pushed(result.stdout + result.stderr)
        print(f"{name}: pushed {image} as {ref or '(version not found in the output)'}")
    return 0


def parse_pushed(output):
    match = re.search(r'Refer to this image as "(:[^"]+)"', output)
    return match.group(1) if match else None


def cmd_logs(args):
    containers = read_json(CONTAINERS_FILE)[0]
    Settings.secrets = secrets_of(containers)
    show_logs(args.containers or list(containers), args.lines)
    return 0


def show_logs(names, lines):
    for name in names:
        events = aws("get-container-log", "--service-name", Settings.service, "--container-name", name).get("logEvents", [])
        print(f"--- {name}: last {min(lines, len(events))} of {len(events)} log events")
        for event in events[-lines:]:
            print(f"{event['createdAt']}  {redact(event['message'].rstrip())}")


def request_ok(url):
    try:
        with urllib.request.urlopen(url, timeout=10) as response:
            return response.status
    except urllib.error.HTTPError as error:
        return error.code
    except (urllib.error.URLError, OSError):
        return None


def cmd_deploy(args):
    containers, crlf = read_json(CONTAINERS_FILE)
    endpoint, _ = read_json(ENDPOINT_FILE)
    Settings.secrets = secrets_of(containers)
    problems = validate(containers, endpoint, Settings.service)
    changes, missing = plan_images(containers, latest_images())
    problems += missing
    if problems:
        fail("not deploying:\n  " + "\n  ".join(problems))
    print("images: " + ("" if changes else "all containers already use the highest pushed version"))
    for name, old, new in changes:
        print(f"  {name}: {old} -> {new}")
    command = ["aws", "lightsail", "create-container-service-deployment", "--region", Settings.region, "--service-name", Settings.service,
               "--containers", CONTAINERS_ARGUMENT, "--public-endpoint", ENDPOINT_ARGUMENT]
    if args.dry_run:
        print("dry run, nothing changed. Would run:\n  " + subprocess.list2cmdline(command))
        return 0
    if changes:
        for name, _, new in changes:
            containers[name]["image"] = new
        write_containers(containers, crlf)
        print("containers.json updated")

    created = aws("create-container-service-deployment", "--service-name", Settings.service, "--containers", CONTAINERS_ARGUMENT,
                  "--public-endpoint", ENDPOINT_ARGUMENT)
    version = created["containerService"]["nextDeployment"]["version"]
    print(f"deployment {version} created")
    if args.no_wait:
        return 0

    deadline, last = time.time() + args.timeout, None
    while True:
        state = next(d["state"] for d in aws("get-container-service-deployments", "--service-name", Settings.service)["deployments"] if d["version"] == version)
        if state != last:
            print(f"deployment {version}: {state}", flush=True)
            last = state
        if state == "ACTIVE":
            break
        if state in ("FAILED", "INACTIVE") or time.time() > deadline:
            print("deployment " + ("timed out while " + state if state == "ACTIVATING" else "ended as " + state) + "; the container logs:")
            show_logs(list(containers), 20)
            return 1
        time.sleep(POLL_SECONDS)

    url = aws("get-container-services", "--service-name", Settings.service)["containerServices"][0]["url"]
    ok = True
    for path in ("api/health", ""):
        status = None
        for _ in range(HEALTH_ATTEMPTS):
            status = request_ok(url + path)
            if status == 200:
                break
            time.sleep(5)
        print(f"GET {url}{path} -> {status}")
        ok = ok and status == 200
    return 0 if ok else 1


def self_test():
    failed = 0

    def check(name, got, expected):
        nonlocal failed
        if got != expected:
            failed += 1
            print(f"FAIL {name}\n  expected {expected!r}\n  got      {got!r}")

    good = {
        "proxy": {"image": ":container-service-1.my-nginx-proxy.9", "environment": {"BACKEND_API_UPSTREAM": "localhost:8080", "FRONTEND_UI_UPSTREAM": "localhost:5173"}},
        "backend-api": {"image": ":container-service-1.my-aspnet-api.4", "environment": {"ConnectionStrings__DefaultConnection": "Server=h;Password=Sekret1;", "Jwt__SigningKey": "Hidden99"}},
    }
    endpoint = {"containerName": "proxy"}
    check("valid config", validate(good, endpoint, "container-service-1"), [])
    check("underscore name", len(validate({"backend_api": good["backend-api"]}, {"containerName": "backend_api"}, "container-service-1")), 1)
    check("latest tag", len(validate({"proxy": {"image": ":container-service-1.my-nginx-proxy.LATEST"}}, endpoint, "container-service-1")), 1)
    check("other service", len(validate({"proxy": {"image": ":other-service.my-nginx-proxy.3"}}, endpoint, "container-service-1")), 1)
    check("placeholder", placeholders({"a": {"b": "REPLACE_ME_x"}, "c": "ok"}), ["a.b"])
    check("container name upstream", len(validate({"proxy": {"image": ":container-service-1.my-nginx-proxy.9", "environment": {"BACKEND_API_UPSTREAM": "backend-api:8080"}}}, endpoint, "container-service-1")), 1)
    check("endpoint container missing", len(validate(good, {"containerName": "nope"}, "container-service-1")), 1)

    Settings.service = "container-service-1"
    latest = {}
    for ref in (":container-service-1.my-react-ui.9", ":container-service-1.my-react-ui.10", ":container-service-1.my-react-ui.5"):
        parts = image_parts(ref)
        if parts[1] not in latest or parts[2] > latest[parts[1]][0]:
            latest[parts[1]] = (parts[2], ref)
    check("10 is newer than 9", latest["my-react-ui"][1], ":container-service-1.my-react-ui.10")
    changes, missing = plan_images({"frontend-ui": {"image": ":container-service-1.my-react-ui.5"}, "x": {"image": ":container-service-1.nothing.1"}}, latest)
    check("plan", (changes, len(missing)), ([("frontend-ui", ":container-service-1.my-react-ui.5", ":container-service-1.my-react-ui.10")], 1))
    check("push output", parse_pushed('Digest: sha256:abc\nRefer to this image as ":container-service-1.my-react-ui.11" in deployments.'), ":container-service-1.my-react-ui.11")
    Settings.secrets = secrets_of(good)
    check("secrets found", sorted(Settings.secrets), ["Hidden99", "Sekret1", "Server=h;Password=Sekret1;"])
    check("redacted", redact("db Password=Other1 and Sekret1 and Hidden99"), "db Password=REDACTED and REDACTED and REDACTED")
    failed += rollout_scenarios()
    print("all checks passed" if not failed else f"{failed} check(s) failed")
    return 1 if failed else 0


def rollout_scenarios():
    """Runs cmd_deploy against a fake `aws` for a rollout that goes ACTIVATING, ACTIVATING, ACTIVE and for one that ends FAILED."""
    import contextlib
    import io
    import tempfile
    from types import SimpleNamespace

    failures = []
    config = {
        "proxy": {"image": ":container-service-1.my-nginx-proxy.9", "environment": {"BACKEND_API_UPSTREAM": "localhost:8080"}},
        "frontend-ui": {"image": ":container-service-1.my-react-ui.9", "environment": {}},
        "backend-api": {"image": ":container-service-1.my-aspnet-api.4", "environment": {"Jwt__SigningKey": "Hidden99"}},
    }
    images = [":container-service-1.my-nginx-proxy.9", ":container-service-1.my-react-ui.9", ":container-service-1.my-react-ui.10",
              ":container-service-1.my-aspnet-api.4"]

    def run(states):
        calls = {"states": 0, "created": 0}

        def fake_aws(*args):
            if args[0] == "get-container-images":
                return {"containerImages": [{"image": i} for i in images]}
            if args[0] == "create-container-service-deployment":
                calls["created"] += 1
                return {"containerService": {"nextDeployment": {"version": 12}}}
            if args[0] == "get-container-service-deployments":
                state = states[min(calls["states"], len(states) - 1)]
                calls["states"] += 1
                return {"deployments": [{"version": 12, "state": state, "createdAt": "now"}, {"version": 11, "state": "INACTIVE", "createdAt": "then"}]}
            if args[0] == "get-container-services":
                return {"containerServices": [{"url": "https://example.test/"}]}
            if args[0] == "get-container-log":
                return {"logEvents": [{"createdAt": "t", "message": "start failed, key Hidden99 rejected"}]}
            raise AssertionError(args)

        with tempfile.TemporaryDirectory() as folder:
            containers, endpoint = Path(folder, "containers.json"), Path(folder, "public-endpoint.json")
            containers.write_text(json.dumps(config, indent=2).replace("\n", "\r\n"), encoding="utf-8", newline="")
            endpoint.write_text('{"containerName": "proxy"}', encoding="utf-8")
            saved = {name: globals()[name] for name in ("aws", "request_ok", "CONTAINERS_FILE", "ENDPOINT_FILE")}
            saved_sleep = time.sleep
            globals().update(aws=fake_aws, request_ok=lambda url: 200, CONTAINERS_FILE=containers, ENDPOINT_FILE=endpoint)
            time.sleep = lambda seconds: None
            Settings.service = "container-service-1"
            output = io.StringIO()
            try:
                with contextlib.redirect_stdout(output):
                    code = cmd_deploy(SimpleNamespace(dry_run=False, no_wait=False, timeout=900))
            finally:
                globals().update(saved)
                time.sleep = saved_sleep
            return code, output.getvalue(), containers.read_bytes().decode("utf-8"), calls

    code, text, written, calls = run(["ACTIVATING", "ACTIVATING", "ACTIVE"])
    for ok, name in ((code == 0, "success exit code"), ("deployment 12: ACTIVE" in text, "reports ACTIVE"), (":container-service-1.my-react-ui.10" in written, "file moved to .10"),
                     ("\r\n" in written and "\n" not in written.replace("\r\n", ""), "CRLF kept"), (calls["states"] == 3, "polled until ACTIVE"),
                     ("api/health -> 200" in text, "health check")):
        if not ok:
            failures.append(name)
    code, text, written, calls = run(["ACTIVATING", "FAILED"])
    for ok, name in ((code == 1, "failure exit code"), ("ended as FAILED" in text, "reports FAILED"), ("--- backend-api" in text, "prints logs"),
                     ("Hidden99" not in text and "REDACTED" in text, "redacts the log")):
        if not ok:
            failures.append(name)
    for name in failures:
        print("FAIL rollout: " + name)
    return len(failures)


def main():
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--service", default=DEFAULT_SERVICE)
    p.add_argument("--region", default=DEFAULT_REGION)
    sub = p.add_subparsers(dest="command", required=True)
    sub.add_parser("status")
    push = sub.add_parser("push")
    push.add_argument("--only", help="comma list of containers: " + ", ".join(CONTAINERS))
    push.add_argument("--build", action="store_true")
    push.add_argument("--dry-run", action="store_true")
    deploy = sub.add_parser("deploy")
    deploy.add_argument("--dry-run", action="store_true")
    deploy.add_argument("--no-wait", action="store_true")
    deploy.add_argument("--timeout", type=int, default=900, help="seconds to wait for ACTIVE (ACTIVATING can last several minutes)")
    logs = sub.add_parser("logs")
    logs.add_argument("containers", nargs="*")
    logs.add_argument("--lines", type=int, default=20)
    args = p.parse_args()
    Settings.service, Settings.region = args.service, args.region
    sys.exit({"status": cmd_status, "push": cmd_push, "deploy": cmd_deploy, "logs": cmd_logs}[args.command](args))


if __name__ == "__main__":
    main()
