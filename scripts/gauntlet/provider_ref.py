"""Resolve a Source Patch provider pin's floating ref to a deterministic SHA.

`scripts/source_patch_provider_pin.json` pins the FSR provider fork to a
branch (`ref_kind: "branch"`) so master can move without a manual re-lock.
Every lane run must resolve that branch to one exact commit SHA before any
worker is created, so every cell in the run installs the identical tree.
`resolve_provider_ref` is the single place that resolution happens; it is
fail-closed: any git error, any missing pin field, or any ambiguous/malformed
`git ls-remote` reply raises `ProviderRefError` rather than silently picking
one candidate. See Plans/MUTATION-REGRESSION-MODULE.md §3.
"""

import argparse
import json
import re
import subprocess
from datetime import UTC, datetime
from pathlib import Path

GIT_LS_REMOTE_TIMEOUT_SECONDS = 30
SHA_HEX_LENGTH = 40
_SHA_RE = re.compile(rf"[0-9a-f]{{{SHA_HEX_LENGTH}}}")
REQUIRED_PIN_FIELDS = ("package_name", "git_url", "ref")


class ProviderRefError(RuntimeError):
    pass


def _load_pin(pin_path: Path) -> dict[str, object]:
    if not pin_path.is_file():
        raise ProviderRefError(f"Provider pin not found: {pin_path}")
    payload = json.loads(pin_path.read_text(encoding="utf-8"))
    missing = [key for key in REQUIRED_PIN_FIELDS if not payload.get(key)]
    if missing:
        raise ProviderRefError(f"Provider pin {pin_path} missing field(s): {', '.join(missing)}")
    return payload


def _strip_query(git_url: str) -> str:
    return git_url.split("?", 1)[0]


def _ls_remote_sha(git_url: str, ref: str) -> str:
    url = _strip_query(git_url)
    command = ["git", "ls-remote", "--exit-code", "--", url, f"refs/heads/{ref}", f"refs/tags/{ref}^{{}}"]
    try:
        result = subprocess.run(
            command, capture_output=True, text=True, check=False, timeout=GIT_LS_REMOTE_TIMEOUT_SECONDS
        )
    except subprocess.TimeoutExpired as error:
        raise ProviderRefError(f"git ls-remote timed out resolving {ref!r} at {url}") from error
    if result.returncode != 0:
        raise ProviderRefError(
            f"git ls-remote found no match for {ref!r} at {url} "
            f"(exit {result.returncode}): {result.stderr.strip()}"
        )
    lines = [line for line in result.stdout.splitlines() if line.strip()]
    if len(lines) != 1:
        raise ProviderRefError(
            f"git ls-remote for {ref!r} at {url} returned {len(lines)} match(es), expected exactly 1: {lines}"
        )
    fields = lines[0].split()
    if len(fields) < 2 or not _SHA_RE.fullmatch(fields[0]):
        raise ProviderRefError(f"git ls-remote for {ref!r} at {url} returned malformed output: {lines[0]!r}")
    return fields[0]


def resolve_provider_ref(pin_path: Path, out_path: Path) -> dict[str, object]:
    """Read `pin_path`, resolve its `ref` to an exact SHA, write the resolved
    pin to `out_path`, and return it. Passthrough (no git call) when `ref` is
    already a 40-hex SHA."""
    pin = _load_pin(pin_path)
    ref = str(pin["ref"])
    if _SHA_RE.fullmatch(ref):
        resolved = {**pin, "requested_ref": ref, "resolved_by": "passthrough"}
    else:
        sha = _ls_remote_sha(str(pin["git_url"]), ref)
        resolved = {**pin, "ref": sha, "requested_ref": ref, "resolved_by": "git ls-remote"}
    resolved["resolved_at_utc"] = datetime.now(UTC).isoformat()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(resolved, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return resolved


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pin", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    options = parser.parse_args()
    resolved = resolve_provider_ref(options.pin, options.out)
    print(json.dumps(resolved, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()


__all__ = ["ProviderRefError", "resolve_provider_ref"]
