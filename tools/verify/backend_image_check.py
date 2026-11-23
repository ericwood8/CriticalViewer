"""Proves the Critical Viewer backend the way it runs in production: on Linux, in the container image, against a real MySQL. Python 3, no packages, needs Docker.

  python tools/verify/backend_image_check.py linux-tests [--sdk-image IMAGE]
  python tools/verify/backend_image_check.py smoke [--image my-aspnet-api:local] [--build] [--year 2024] [--port 18080] [--db-port 18306]
  python tools/verify/backend_image_check.py --self-test

linux-tests: copies backend/ (without bin and obj) into a throwaway container of the SDK image that global.json names (for example
             mcr.microsoft.com/dotnet/sdk:11.0) and runs `dotnet test` there. The host's obj folders hold Windows paths, so they are left out.
smoke:       starts a throwaway MySQL 8.4 container and a network (names zz-cv-*, label critical_viewer_check=1), loads the schema script
             CriticalViewerDB.sql, applies the EF migrations with `dotnet ef`, checks that the model has no pending changes, starts the
             backend image on 127.0.0.1:<port> and runs the API checks: health, movie list and detail, registration, change-password,
             an anonymous review refused (401) and a signed-in review accepted (201) and listed, and no fail:/crit: line in the container log.
             Everything it started is removed at the end, also after a failure. --build first runs `docker compose build backend-api`.
Passwords and the signing key are generated per run and travel only in environment variables, never on a command line or in the output.
Never pulls an image: it says which `docker pull` is missing (exit 3). The `dotnet ef` tool must have the major version of the EF Core packages.
Output: one PASS or FAIL line per check, then `ok=N failed=M`. Exit codes: 0 all passed, 1 a check failed, 3 a prerequisite is missing, 4 a name or port is busy.
"""
import argparse
import io
import json
import os
import re
import secrets
import socket
import subprocess
import sys
import tarfile
import threading
import time
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
BACKEND = REPO / "backend"
API_PROJECT = "src/CriticalViewer.Api"
INFRA_PROJECT = "src/CriticalViewer.Infrastructure"
API_CSPROJ = BACKEND / API_PROJECT / "CriticalViewer.Api.csproj"
SEED_SQL = BACKEND / INFRA_PROJECT / "Migrations" / "CriticalViewerDB.sql"
DEFAULT_IMAGE = "my-aspnet-api:local"
DB_IMAGE = "mysql:8.4"
LABEL = "critical_viewer_check=1"
NETWORK, DB_NAME, API_NAME = "zz-cv-net", "zz-cv-db", "zz-cv-api"
SKIP_DIRECTORIES = {"bin", "obj", ".vs", "node_modules"}


def text_of(result):
    return ((result.stdout or b"") + (result.stderr or b"")).decode("utf-8", "replace")


def docker(*args, env=None, input_bytes=None, timeout=None):
    return subprocess.run(["docker", *args], capture_output=True, input=input_bytes, timeout=timeout, env={**os.environ, **env} if env else None)


def image_exists(image):
    return docker("image", "inspect", image).returncode == 0


def sdk_major(global_json_text):
    return int(json.loads(global_json_text)["sdk"]["version"].split(".")[0])


def sdk_image():
    major = sdk_major((REPO / "global.json").read_text(encoding="utf-8-sig"))
    return f"mcr.microsoft.com/dotnet/sdk:{major}.0"


def ef_package_major(csproj_text):
    match = re.search(r'Microsoft\.EntityFrameworkCore\.Design"\s+Version="(\d+)\.', csproj_text)
    return int(match.group(1)) if match else None


def tar_filter(info):
    return None if any(part in SKIP_DIRECTORIES for part in info.name.split("/")) else info


def backend_tar():
    buffer = io.BytesIO()
    with tarfile.open(fileobj=buffer, mode="w") as archive:
        archive.add(str(BACKEND), arcname=".", filter=tar_filter)
    return buffer.getvalue()


def port_free(port):
    with socket.socket() as probe:
        probe.settimeout(1)
        return probe.connect_ex(("127.0.0.1", port)) != 0


def call(method, url, body=None, token=None):
    """(status, text); status 0 when nothing answered."""
    data = json.dumps(body).encode() if body is not None else None
    headers = {"Content-Type": "application/json"} if body is not None else {}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    request = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            return response.status, response.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode("utf-8", "replace")
    except OSError as error:
        return 0, str(error)


def run_checks(base, year=2024):
    """The API checks over HTTP: a list of (name, passed, detail). Nothing here needs Docker, so the self-test runs it against a stub."""
    results = []

    def record(name, passed, detail=""):
        results.append((name, bool(passed), detail))

    status, _ = call("GET", f"{base}/api/health")
    record("health answers 200", status == 200, f"status {status}")

    status, text = call("GET", f"{base}/api/movies?year={year}&pageSize=1")
    try:
        items = json.loads(text)["items"]
    except (ValueError, KeyError, TypeError):
        items = []
    movie_id = items[0]["id"] if items else None
    record(f"movie list for {year} has a movie", status == 200 and movie_id, f"status {status}, {len(items)} item(s)")

    email = f"check-{secrets.token_hex(4)}@example.com"
    password, new_password = f"Chk{secrets.token_hex(6)}!a1", f"Chk{secrets.token_hex(6)}!b2"
    status, text = call("POST", f"{base}/api/auth/register", {"email": email, "password": password})
    try:
        token = json.loads(text).get("token")
    except (ValueError, AttributeError):
        token = None
    record("register answers 200 with a token", status == 200 and token, f"status {status}")

    status, _ = call("POST", f"{base}/api/auth/change-password", {"currentPassword": password, "newPassword": new_password})
    record("change-password without a token is refused (401)", status == 401, f"status {status}")
    status, _ = call("POST", f"{base}/api/auth/change-password", {"currentPassword": password, "newPassword": new_password}, token)
    record("change-password with a token answers 204", status == 204, f"status {status}")

    if not movie_id:
        record("movie detail and reviews", False, "no movie to test with")
        return results
    status, _ = call("GET", f"{base}/api/movies/{movie_id}")
    record("movie detail answers 200", status == 200, f"status {status}")
    review = {"rating": 4, "body": f"Check review {secrets.token_hex(3)}."}
    status, _ = call("POST", f"{base}/api/movies/{movie_id}/reviews", review)
    record("anonymous review is refused (401)", status == 401, f"status {status}")
    status, _ = call("POST", f"{base}/api/movies/{movie_id}/reviews", review, token)
    record("signed-in review answers 201", status == 201, f"status {status}")
    status, text = call("GET", f"{base}/api/movies/{movie_id}/reviews")
    record("the new review is listed", status == 200 and review["body"] in text, f"status {status}")
    return results


def report(results):
    for name, passed, detail in results:
        print(f"PASS {name}" if passed else f"FAIL {name}: {detail}")
    failed = sum(1 for _, passed, _ in results if not passed)
    print(f"ok={len(results) - failed} failed={failed}")
    return 1 if failed else 0


def cmd_linux_tests(args):
    image = args.sdk_image or sdk_image()
    if docker("info").returncode != 0:
        print("the Docker engine is not running: start Docker Desktop first", file=sys.stderr)
        return 3
    if not image_exists(image):
        print(f"{image} is not on this machine: `docker pull {image}` (about 0.9 GB, a download to approve) and run this again", file=sys.stderr)
        return 3
    script = "mkdir /w && tar -xf - -C /w && cd /w && dotnet test CriticalViewer.sln --nologo -v q"
    result = docker("run", "-i", "--rm", image, "sh", "-c", script, input_bytes=backend_tar())
    output = text_of(result)
    summary = [line for line in output.splitlines() if re.search(r"Passed!|Failed!|\berror\b", line)]
    print(f"{image}: dotnet test exit {result.returncode}")
    print("\n".join(summary) if summary else "\n".join(output.splitlines()[-30:]))
    return 0 if result.returncode == 0 else 1


def prerequisites(args):
    """A message and exit code when something is missing, otherwise None."""
    if docker("info").returncode != 0:
        return "the Docker engine is not running: start Docker Desktop first", 3
    if args.build:
        built = subprocess.run(["docker", "compose", "build", "backend-api"], cwd=REPO, capture_output=True)
        if built.returncode != 0:
            return "docker compose build backend-api failed:\n" + text_of(built)[-1500:], 3
    for image in (DB_IMAGE, args.image):
        if not image_exists(image):
            return f"image {image} is not on this machine (`docker pull {DB_IMAGE}` or `docker compose build backend-api`; a pull is a download to approve)", 3
    if not SEED_SQL.is_file():
        return f"{SEED_SQL} is missing", 3
    for port in (args.port, args.db_port):
        if not port_free(port):
            return f"port {port} is already in use", 4
    taken = docker("ps", "-a", "--filter", "name=zz-cv-", "--format", "{{.Names}}")
    if taken.stdout.strip():
        return "containers named zz-cv-* already exist (" + taken.stdout.decode().split()[0] + "): remove them first, this tool only removes what it started", 4
    ef = subprocess.run(["dotnet", "ef", "--version"], capture_output=True, text=True)
    printed = (ef.stdout + ef.stderr).strip()
    match = re.search(r"(\d+)\.\d+\.\d+", printed.splitlines()[-1]) if ef.returncode == 0 and printed else None
    wanted = ef_package_major(API_CSPROJ.read_text(encoding="utf-8-sig"))
    if not match:
        return "`dotnet ef` is not installed: dotnet tool install -g dotnet-ef --version " + f"{wanted}.0.0", 3
    if wanted and int(match.group(1)) != wanted:
        return f"dotnet-ef is {match.group(0)} but the EF Core packages are {wanted}.x: dotnet tool update -g dotnet-ef --version {wanted}.0.0", 3
    return None


def wait_database(password, seconds=120):
    """A TCP login proves the real server: during first-run setup MySQL listens on a socket only, then restarts."""
    began, good = time.monotonic(), 0
    while time.monotonic() - began < seconds:
        ping = docker("exec", "-e", "MYSQL_PWD", DB_NAME, "mysql", "-h127.0.0.1", "-uroot", "-e", "SELECT 1", env={"MYSQL_PWD": password})
        good = good + 1 if ping.returncode == 0 else 0
        if good >= 2:
            return True
        time.sleep(2)
    return False


def wait_health(base, seconds=60):
    began = time.monotonic()
    while time.monotonic() - began < seconds:
        if call("GET", f"{base}/api/health")[0] == 200:
            return True
        time.sleep(2)
    return False


def cmd_smoke(args):
    problem = prerequisites(args)
    if problem:
        print(problem[0], file=sys.stderr)
        return problem[1]
    password, signing_key = secrets.token_hex(12), "zz-" + secrets.token_hex(24)
    host_connection = f"Server=127.0.0.1;Port={args.db_port};Database=CriticalViewer;User=root;Password={password};"
    api_connection = f"Server={DB_NAME};Port=3306;Database=CriticalViewer;User=root;Password={password};"
    base = f"http://127.0.0.1:{args.port}"
    results = []

    def record(name, passed, detail=""):
        results.append((name, bool(passed), detail))
        return passed

    def clean(text):
        return text.replace(password, "REDACTED")

    try:
        docker("network", "create", NETWORK)
        started = docker("run", "-d", "--rm", "--name", DB_NAME, "--network", NETWORK, "--label", LABEL, "-p", f"127.0.0.1:{args.db_port}:3306",
                         "-e", "MYSQL_ROOT_PASSWORD", "-e", "MYSQL_DATABASE=CriticalViewer", DB_IMAGE, env={"MYSQL_ROOT_PASSWORD": password})
        if not record("MySQL container started", started.returncode == 0, clean(text_of(started))[-300:]):
            return report(results)
        if not record("MySQL accepts logins", wait_database(password), "no login within 120 s"):
            return report(results)

        seeded = docker("exec", "-i", "-e", "MYSQL_PWD", DB_NAME, "mysql", "-uroot", "CriticalViewer", env={"MYSQL_PWD": password}, input_bytes=SEED_SQL.read_bytes())
        if not record("schema script CriticalViewerDB.sql loaded", seeded.returncode == 0, clean(text_of(seeded))[-300:]):
            return report(results)

        ef_environment = {**os.environ, "ConnectionStrings__DefaultConnection": host_connection}
        migrated = subprocess.run(["dotnet", "ef", "database", "update", "--project", INFRA_PROJECT, "--startup-project", API_PROJECT],
                                  cwd=BACKEND, capture_output=True, text=True, env=ef_environment)
        if not record("EF migrations applied", migrated.returncode == 0 and "Done." in migrated.stdout + migrated.stderr, clean(migrated.stdout + migrated.stderr)[-500:]):
            return report(results)
        pending = subprocess.run(["dotnet", "ef", "migrations", "has-pending-model-changes", "--no-build", "--project", INFRA_PROJECT, "--startup-project", API_PROJECT],
                                 cwd=BACKEND, capture_output=True, text=True, env=ef_environment)
        record("the model has no pending migration", pending.returncode == 0 and "No changes have been made" in pending.stdout + pending.stderr, clean(pending.stdout + pending.stderr)[-300:])

        api = docker("run", "-d", "--rm", "--name", API_NAME, "--network", NETWORK, "--label", LABEL, "-p", f"127.0.0.1:{args.port}:8080",
                     "-e", "ASPNETCORE_ENVIRONMENT=Development", "-e", "ConnectionStrings__DefaultConnection", "-e", "Jwt__SigningKey", args.image,
                     env={"ConnectionStrings__DefaultConnection": api_connection, "Jwt__SigningKey": signing_key})
        if not record(f"{args.image} started", api.returncode == 0, clean(text_of(api))[-300:]):
            return report(results)
        if not record("the API answers on /api/health within 60 s", wait_health(base), "see the container log below"):
            print(clean(text_of(docker("logs", "--tail", "30", API_NAME))))
            return report(results)

        results.extend(run_checks(base, args.year))
        log = clean(text_of(docker("logs", API_NAME)))
        bad = [line for line in log.splitlines() if line.startswith(("fail:", "crit:"))]
        record("no fail: or crit: line in the container log", not bad, "; ".join(bad[:3]))
        return report(results)
    finally:
        docker("stop", API_NAME, DB_NAME)
        docker("network", "rm", NETWORK)


class Stub(BaseHTTPRequestHandler):
    """A stand-in for the API, so the self-test can show that every check passes on a correct API and that one fails on a broken API."""
    anonymous_review_allowed = False
    reviews = []

    def log_message(self, *args):
        pass

    def reply(self, status, body=None):
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        if body is not None:
            self.wfile.write(json.dumps(body).encode())

    def body(self):
        length = int(self.headers.get("Content-Length", 0))
        return json.loads(self.rfile.read(length) or b"{}")

    def signed_in(self):
        return self.headers.get("Authorization", "").startswith("Bearer ")

    def do_GET(self):
        path = self.path.split("?")[0]
        if path == "/api/health":
            self.reply(200, {"status": "ok"})
        elif path == "/api/movies":
            self.reply(200, {"items": [{"id": "m1"}]})
        elif path == "/api/movies/m1":
            self.reply(200, {"id": "m1"})
        elif path == "/api/movies/m1/reviews":
            self.reply(200, {"items": [{"body": body} for body in Stub.reviews]})
        else:
            self.reply(404)

    def do_POST(self):
        path, data = self.path, self.body()
        if path == "/api/auth/register":
            self.reply(200, {"token": "t"})
        elif path == "/api/auth/change-password":
            self.reply(204) if self.signed_in() else self.reply(401)
        elif path == "/api/movies/m1/reviews":
            if self.signed_in() or Stub.anonymous_review_allowed:
                Stub.reviews.append(data["body"])
                self.reply(201, data)
            else:
                self.reply(401)
        else:
            self.reply(404)


def self_test():
    failed = 0

    def check(name, got, expected):
        nonlocal failed
        if got != expected:
            failed += 1
            print(f"FAIL {name}\n  expected {expected!r}\n  got      {got!r}")

    check("sdk major", sdk_major('{"sdk": {"version": "11.0.100", "rollForward": "latestFeature"}}'), 11)
    check("EF package major", ef_package_major('<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="11.0.0">'), 11)
    check("bin skipped", tar_filter(tarfile.TarInfo("./src/Api/bin/Debug/x.dll")), None)
    check("obj skipped", tar_filter(tarfile.TarInfo("./src/Api/obj/project.assets.json")), None)
    kept = tarfile.TarInfo("./src/Api/Program.cs")
    check("source kept", tar_filter(kept) is kept, True)
    check("the backend tar holds the solution and no obj", [n for n in tarfile.open(fileobj=io.BytesIO(backend_tar())).getnames() if n.endswith("CriticalViewer.sln") or "/obj/" in n], ["./CriticalViewer.sln"])

    server = ThreadingHTTPServer(("127.0.0.1", 0), Stub)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    base = f"http://127.0.0.1:{server.server_address[1]}"
    Stub.reviews, Stub.anonymous_review_allowed = [], False
    good = run_checks(base)
    check("a correct API passes every check", [name for name, passed, _ in good if not passed], [])
    check("the checks ran", len(good) >= 9, True)
    Stub.reviews, Stub.anonymous_review_allowed = [], True
    broken = run_checks(base)
    check("an API that lets an anonymous review in fails exactly that check", [name for name, passed, _ in broken if not passed], ["anonymous review is refused (401)"])
    server.shutdown()
    down = run_checks(base)
    check("no API: health fails", [name for name, passed, _ in down if not passed][:1], ["health answers 200"])
    print("all checks passed" if not failed else f"{failed} check(s) failed")
    return 1 if failed else 0


def main():
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    tests = sub.add_parser("linux-tests")
    tests.add_argument("--sdk-image", help="default: the SDK image for the major version in global.json")
    smoke = sub.add_parser("smoke")
    smoke.add_argument("--image", default=DEFAULT_IMAGE)
    smoke.add_argument("--build", action="store_true", help="run `docker compose build backend-api` first")
    smoke.add_argument("--year", type=int, default=2024, help="a release year that has movies in the seed data")
    smoke.add_argument("--port", type=int, default=18080)
    smoke.add_argument("--db-port", type=int, default=18306)
    args = parser.parse_args()
    sys.exit({"linux-tests": cmd_linux_tests, "smoke": cmd_smoke}[args.command](args))


if __name__ == "__main__":
    main()
