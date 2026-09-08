#!/usr/bin/env python3
"""Fetch the pinned Source Patch adapter sources for the offline compile
contract (`unity-plugin/Tests~/MutationAdapterContract/`).

Resolves the floating provider ref from `scripts/source_patch_provider_pin.json`
via `gauntlet.provider_ref.resolve_provider_ref` (same resolver the cell
driver's `resolve_provider_pin` uses -- not duplicated here), fetches the 5
adapter `.cs` files under `Assets/BiomeSourcePatchAdapter/Editor/` at that
SHA from the fork repo (raw.githubusercontent.com -- the one documented
fetch mechanism; GitHub does not support `git archive --remote` for
arbitrary repos), and verifies each file's sha256 against a tracked lock
(`adapter-sources.lock.json`). Fails closed -- non-zero exit, no partial
cache directory -- on any network error, missing file, or hash mismatch.

Verification is content-based only: each fetched file's sha256 is compared
against `adapter-sources.lock.json["files"][name]`. The lock's top-level
`source_sha` is informational (records which SHA last produced these
hashes) and is never compared to the newly resolved SHA -- a no-op commit
on the provider's `master` (new SHA, byte-identical adapter files) is
accepted silently. The sentinel this tool guards against is adapter
*content* drift, not ref drift.
See Plans/MUTATION-REGRESSION-MODULE.md Phase C1.
"""
import argparse
import hashlib
import json
import shutil
import sys
import urllib.error
import urllib.request
from collections.abc import Callable
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = REPO_ROOT / "scripts"
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))

import gauntlet.provider_ref as provider_ref  # noqa: E402

GITHUB_RAW_BASE = "https://raw.githubusercontent.com"
ADAPTER_SOURCE_DIR = "Assets/BiomeSourcePatchAdapter/Editor"
ADAPTER_FILES = (
    "BiomeAutomaticModeGuardLogic.cs",
    "BiomeBodyOnlyMethodClassifier.cs",
    "BiomeFsrAutomaticModesGuard.cs",
    "BiomeFsrSourcePatchProvider.cs",
    "BiomeSingleFlightGate.cs",
)
LOCK_SCHEMA_VERSION = 1
FETCH_TIMEOUT_SECONDS = 30.0

Fetcher = Callable[[str], bytes]


class AdapterFetchError(RuntimeError):
    pass


def _parse_github_owner_repo(git_url: str) -> tuple[str, str]:
    base = git_url.split("?", 1)[0]
    if base.endswith(".git"):
        base = base[: -len(".git")]
    parts = [part for part in base.split("/") if part]
    if len(parts) < 2:
        raise AdapterFetchError(f"Cannot parse owner/repo from git_url: {git_url!r}")
    return parts[-2], parts[-1]


def _raw_url(owner: str, repo: str, sha: str, filename: str) -> str:
    return f"{GITHUB_RAW_BASE}/{owner}/{repo}/{sha}/{ADAPTER_SOURCE_DIR}/{filename}"


def _default_fetcher(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": "unity-biome-mcp-adapter-fetch"})
    try:
        with urllib.request.urlopen(request, timeout=FETCH_TIMEOUT_SECONDS) as response:  # noqa: S310
            return response.read()
    except (urllib.error.URLError, TimeoutError, ValueError) as error:
        raise AdapterFetchError(f"Fetch failed for {url}: {error}") from error


def _sha256_hex(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _fetch_all(fetch: Fetcher, owner: str, repo: str, sha: str) -> dict[str, bytes]:
    fetched: dict[str, bytes] = {}
    for filename in ADAPTER_FILES:
        url = _raw_url(owner, repo, sha, filename)
        try:
            fetched[filename] = fetch(url)
        except AdapterFetchError:
            raise
        except Exception as error:  # noqa: BLE001 -- normalize any fetcher failure
            raise AdapterFetchError(f"Failed to fetch {filename} from {url}: {error}") from error
    return fetched


def _write_lock(lock_path: Path, *, owner: str, repo: str, sha: str, hashes: dict[str, str]) -> None:
    payload = {
        "schema_version": LOCK_SCHEMA_VERSION,
        "source_repo": f"{owner}/{repo}",
        "source_dir": ADAPTER_SOURCE_DIR,
        "source_sha": sha,
        "files": dict(sorted(hashes.items())),
    }
    lock_path.parent.mkdir(parents=True, exist_ok=True)
    lock_path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def _load_json(path: Path) -> dict:
    text = path.read_text(encoding="utf-8")
    try:
        return json.loads(text)
    except json.JSONDecodeError as error:
        raise AdapterFetchError(f"{path} is not valid JSON: {error}") from error


def _verify_against_lock(lock_path: Path, hashes: dict[str, str]) -> None:
    if not lock_path.is_file():
        raise AdapterFetchError(f"Lock file not found: {lock_path} (use --update-lock to create it)")
    locked = _load_json(lock_path)
    locked_files = locked.get("files") or {}
    missing = sorted(set(ADAPTER_FILES) - set(locked_files))
    if missing:
        raise AdapterFetchError(f"Lock file {lock_path} missing entries for: {missing}")
    mismatches = sorted(name for name in ADAPTER_FILES if locked_files.get(name) != hashes[name])
    if mismatches:
        raise AdapterFetchError(
            f"sha256 mismatch against {lock_path} for: {mismatches} "
            "(adapter source drift -- re-run with --update-lock only after review)"
        )


def _write_out_dir_atomic(out_dir: Path, fetched: dict[str, bytes]) -> None:
    """Write every fetched file into a sibling temp dir, then swap it in for
    `out_dir` -- a failure partway through never leaves a partial `out_dir`
    (the old one, if any, is untouched until the swap) and never leaves a
    stray temp dir behind (cleaned in the except branch)."""
    temp_dir = out_dir.parent / f"{out_dir.name}.tmp"
    if temp_dir.exists():
        shutil.rmtree(temp_dir)
    temp_dir.mkdir(parents=True)
    try:
        for filename, data in fetched.items():
            (temp_dir / filename).write_bytes(data)
    except Exception:
        shutil.rmtree(temp_dir, ignore_errors=True)
        raise
    if out_dir.exists():
        shutil.rmtree(out_dir)
    temp_dir.rename(out_dir)


def fetch_adapter_sources(
    *,
    pin_path: Path,
    out_dir: Path,
    lock_path: Path,
    update_lock: bool = False,
    fetcher: Fetcher | None = None,
    resolved_pin_out: Path | None = None,
) -> dict[str, object]:
    """Resolve the provider SHA, fetch the 5 adapter files at that SHA,
    verify (or, with `update_lock`, regenerate) their sha256 lock, and only
    then write them to `out_dir`. Any failure before the final write leaves
    `out_dir` untouched -- no partial cache."""
    fetch = fetcher or _default_fetcher
    pin = _load_json(pin_path)
    resolved_out = resolved_pin_out or (out_dir.parent / "resolved-adapter-pin.json")
    resolved = provider_ref.resolve_provider_ref(pin_path, resolved_out)
    sha = str(resolved["ref"])
    owner, repo = _parse_github_owner_repo(str(pin["git_url"]))

    fetched = _fetch_all(fetch, owner, repo, sha)
    hashes = {name: _sha256_hex(data) for name, data in fetched.items()}

    if update_lock:
        _write_lock(lock_path, owner=owner, repo=repo, sha=sha, hashes=hashes)
    else:
        _verify_against_lock(lock_path, hashes)

    _write_out_dir_atomic(out_dir, fetched)

    return {
        "source_repo": f"{owner}/{repo}",
        "source_sha": sha,
        "requested_ref": resolved.get("requested_ref"),
        "files": hashes,
        "out_dir": str(out_dir),
        "lock_path": str(lock_path),
        "updated_lock": update_lock,
    }


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pin", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--lock", type=Path, required=True)
    parser.add_argument("--update-lock", action="store_true", help="Rewrite the lock's hashes from a fresh fetch.")
    parser.add_argument(
        "--resolved-pin-out",
        type=Path,
        default=None,
        help="Where to write the resolved-ref side artifact (default: <out>/../resolved-adapter-pin.json).",
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        result = fetch_adapter_sources(
            pin_path=args.pin,
            out_dir=args.out,
            lock_path=args.lock,
            update_lock=args.update_lock,
            resolved_pin_out=args.resolved_pin_out,
        )
    except AdapterFetchError as error:
        print(f"FAILED: {error}", file=sys.stderr)
        return 1
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())


__all__ = [
    "ADAPTER_FILES",
    "ADAPTER_SOURCE_DIR",
    "AdapterFetchError",
    "fetch_adapter_sources",
    "main",
    "parse_args",
]
