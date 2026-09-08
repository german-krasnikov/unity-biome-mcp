"""Provider lifecycle regression helpers (S16b/S16c): editing
Packages/manifest.json + packages-lock.json to remove/restore the FSR UPM
provider package, and probing the resulting source_patch_* projection.

Split out of _canary.py to keep it under 300 lines (it is already 273). See
Plans/MUTATION-REGRESSION-MODULE.md "Provider Lifecycle Regression" and
Plans/Reviews/mutation-regression-2026-09-07/provider-lifecycle-investigation.md
for the controlled procedure this mirrors (manifest/lock edit shape,
sync(resolve=true) trigger, source_patch_* field names, byte-exact restore).
"""
import json
from pathlib import Path

from tests.mutation._canary import _status_field

FSR_PACKAGE_KEY = "com.handzlikchris.fastscriptreload"

# server/src/unity_mcp/tools/sync.py:271,281 return 'sync clean' and :227 returns
# 'sync clean (no compile needed)' -- both are valid clean-sync verdicts; sync.py
# does not expose them as named constants, so this is the one place both live.
CLEAN_SYNC_RESULTS = ("sync clean", "sync clean (no compile needed)")


def _manifest_path(project: Path) -> Path:
    return project / "Packages" / "manifest.json"


def _lock_path(project: Path) -> Path:
    return project / "Packages" / "packages-lock.json"


def remove_provider_from_manifest(project: Path) -> dict:
    """Drop the FSR dependency from manifest.json + packages-lock.json.

    Returns the exact pre-removal bytes of both files (not a reconstructed
    JSON round-trip) so restore_provider_to_manifest can put them back
    byte-identical -- mirrors the investigation's shutil.copy2 backup/restore
    without needing a file outside the project."""
    manifest_path = _manifest_path(project)
    lock_path = _lock_path(project)
    saved = {"manifest_bytes": manifest_path.read_bytes(), "lock_bytes": lock_path.read_bytes()}

    manifest_data = json.loads(saved["manifest_bytes"])
    deps = manifest_data.get("dependencies", {})
    if FSR_PACKAGE_KEY not in deps:
        raise AssertionError(f"{FSR_PACKAGE_KEY} not in manifest; nothing to remove")
    del deps[FSR_PACKAGE_KEY]
    manifest_path.write_text(json.dumps(manifest_data, indent=2) + "\n", encoding="utf-8")

    lock_data = json.loads(saved["lock_bytes"])
    lock_deps = lock_data.get("dependencies", {})
    lock_deps.pop(FSR_PACKAGE_KEY, None)
    lock_data["dependencies"] = lock_deps
    lock_path.write_text(json.dumps(lock_data, indent=2) + "\n", encoding="utf-8")

    return saved


def restore_provider_to_manifest(project: Path, saved: dict) -> None:
    """Write manifest.json + packages-lock.json back to the exact bytes
    remove_provider_from_manifest captured before editing."""
    _manifest_path(project).write_bytes(saved["manifest_bytes"])
    _lock_path(project).write_bytes(saved["lock_bytes"])


def probe_provider_state(status_text: str) -> dict:
    """Parse the source_patch_* fields out of a get_status response
    (SourcePatchModePolicy.StatusProjection, SourcePatchModePolicy.cs:44-49,
    mirrored onto the public get_status route)."""
    return {
        "provider": _status_field(status_text, "source_patch_provider"),
        "state": _status_field(status_text, "source_patch_state"),
        "op": _status_field(status_text, "source_patch_op"),
        "recovery": _status_field(status_text, "source_patch_recovery"),
    }


def snapshot_bridge_files() -> tuple[frozenset, frozenset]:
    """Names only of ~/.unity-biome-mcp/ports + state files -- used to assert
    a provider remove/re-add cycle creates no new orphaned bridge files (the
    worker's own port/reload-port/state files may be rewritten in place, but
    no new file names should appear)."""
    root = Path.home() / ".unity-biome-mcp"
    ports_dir, state_dir = root / "ports", root / "state"
    ports = frozenset(p.name for p in ports_dir.glob("*")) if ports_dir.is_dir() else frozenset()
    state = frozenset(p.name for p in state_dir.glob("*")) if state_dir.is_dir() else frozenset()
    return ports, state
