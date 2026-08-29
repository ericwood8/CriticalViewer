#!/bin/bash
# PreToolUse hook on the Bash tool. Only acts on `git commit`; every other
# Bash call passes straight through. Enforces the kickoff brief's
# engineering standard that no code gets checked in failing lint or tests
# — this is the local backstop for that, CI is the remote one.
#
# Reads the PreToolUse JSON payload from stdin ({"tool_name", "tool_input":
# {"command": "..."}, ...}) and, only for a git-commit command, runs the
# same checks as .github/workflows/{backend,frontend}.yml. On failure it
# emits a PreToolUse "deny" decision so Claude sees the reason and fixes
# it instead of the commit silently going through broken.

INPUT="$(cat)"

COMMAND="$(node -e '
let d = "";
process.stdin.on("data", c => d += c);
process.stdin.on("end", () => {
  try { process.stdout.write(JSON.parse(d).tool_input.command || ""); }
  catch { process.stdout.write(""); }
});
' <<< "$INPUT")"

case "$COMMAND" in
  *"git commit"*) ;;
  *) exit 0 ;;
esac

deny() {
  node -e '
  const reason = process.argv[1];
  console.log(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: "PreToolUse",
      permissionDecision: "deny",
      permissionDecisionReason: reason
    }
  }));
  ' "$1"
  exit 0
}

cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0

if [ -d backend ]; then
  if ! (cd backend && dotnet format CriticalViewer.sln --verify-no-changes) > /tmp/cv-backend-lint.log 2>&1; then
    deny "Backend lint (dotnet format) is failing — run /quality-gate to see details and fix it before committing. Log: $(tail -n 20 /tmp/cv-backend-lint.log)"
  fi
  if ! (cd backend && dotnet test CriticalViewer.sln --configuration Release) > /tmp/cv-backend-test.log 2>&1; then
    deny "Backend tests are failing — run /quality-gate to see details and fix it before committing. Log: $(tail -n 20 /tmp/cv-backend-test.log)"
  fi
fi

if [ -d frontend ]; then
  if ! (cd frontend && npm run lint) > /tmp/cv-frontend-lint.log 2>&1; then
    deny "Frontend lint is failing — run /quality-gate to see details and fix it before committing. Log: $(tail -n 20 /tmp/cv-frontend-lint.log)"
  fi
  if ! (cd frontend && npm test) > /tmp/cv-frontend-test.log 2>&1; then
    deny "Frontend tests are failing — run /quality-gate to see details and fix it before committing. Log: $(tail -n 20 /tmp/cv-frontend-test.log)"
  fi
fi

exit 0
