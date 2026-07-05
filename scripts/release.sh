#!/usr/bin/env bash
# One-command release. Bumps all 5 version artifacts, commits, tags, pushes.
# CI (.github/workflows/release.yml) then creates the GitHub Release automatically.
#
#   ./scripts/release.sh 0.71.0
set -euo pipefail

VERSION="${1:?Usage: ./scripts/release.sh X.Y.Z}"
ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

echo "==> Syncing version to $VERSION across all artifacts..."
python scripts/sync_versions.py "$VERSION"
python scripts/sync_versions.py --check   # paranoia gate — abort if anything drifted

echo "==> Committing..."
git add server/pyproject.toml server/src/unity_mcp/__version__.py \
        unity-plugin/package.json unity-plugin/Editor/MCPServer.cs \
        docs/assets/_meta.json CHANGELOG.md unity-plugin/CHANGELOG.md
git commit -m "chore: release v${VERSION}"
git tag "v${VERSION}"

echo "==> Pushing..."
git push
git push origin "v${VERSION}"

echo "==> Done. CI will create the GitHub Release for v${VERSION}."
