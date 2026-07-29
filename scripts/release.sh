#!/usr/bin/env bash
# Non-publishing release preflight.
#
# This script intentionally never changes versions, stages files, commits,
# tags, pushes, or creates a GitHub release. Publication requires the reviewed
# internal release workflow and its visual, documentation, and privacy gates.
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

if [[ $# -lt 1 || "$1" != "--preflight" || $# -gt 2 ]]; then
    echo "scripts/release.sh no longer publishes releases." >&2
    echo "Usage: ./scripts/release.sh --preflight [EXPECTED_VERSION]" >&2
    echo "Use the reviewed internal finish-task/create-release workflow to publish." >&2
    exit 2
fi
EXPECTED_VERSION="${2:-}"

if [[ -x "$ROOT/server/.venv/bin/python" ]]; then
    PYTHON="$ROOT/server/.venv/bin/python"
else
    PYTHON="${PYTHON:-python3}"
fi

if [[ -n "$EXPECTED_VERSION" ]]; then
    "$PYTHON" - "$EXPECTED_VERSION" <<'PY'
import pathlib
import re
import sys

expected = sys.argv[1].removeprefix("v")
content = pathlib.Path("server/pyproject.toml").read_text(encoding="utf-8")
match = re.search(r'^version = "([^"]+)"$', content, re.MULTILINE)
if match is None:
    raise SystemExit("Could not read the current version from server/pyproject.toml")
actual = match.group(1)
if actual != expected:
    raise SystemExit(
        f"Expected version {expected}, but the synchronized working-tree version is {actual}"
    )
PY
fi

echo "==> Checking synchronized versions"
"$PYTHON" scripts/sync_versions.py --check
cmp CHANGELOG.md unity-plugin/CHANGELOG.md

echo "==> Checking generated README facts and presentation surfaces"
"$PYTHON" scripts/update_readme.py --check-facts
"$PYTHON" scripts/update_readme.py --check
"$PYTHON" -m pytest scripts/tests -q

if command -v xmllint >/dev/null 2>&1; then
    xmllint --noout docs/assets/*.svg
fi

git diff --check

echo "==> Preflight passed"
echo "No files were changed, staged, committed, tagged, pushed, or released."
echo "Continue with the reviewed internal release workflow."
