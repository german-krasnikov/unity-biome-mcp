"""T19: checkpoint_create, checkpoint_restore MCP tools."""
from __future__ import annotations

import contextlib
import uuid
from datetime import datetime, timezone

from ._annotations import DEL as _DEL
from ._annotations import RW as _RW
from ._common import bind

_send = None
_args = None


def _get_content_store():
    from ..changeset_store import get_store
    store = get_store()
    if store is None:
        raise RuntimeError("ContentStore not initialized — call init_store() first")
    return store


def _get_cp_store():
    from ..changeset_store import get_fingerprint, get_store
    from ..checkpoint_store import CheckpointStore
    fp = get_fingerprint()
    store = get_store()
    if fp is None or store is None:
        raise RuntimeError("Store not initialized — call init_store() first")
    return CheckpointStore(fp, store)


def _parse_checkpoint_response(resp: str) -> tuple[int, str]:
    """Parse 'Checkpoint: ...\ngroup_id=N\ndomain_stamp=S' → (group_id, stamp)."""
    group_id = -1
    domain_stamp = ""
    for line in (resp or "").splitlines():
        if line.startswith("group_id="):
            with contextlib.suppress(ValueError):
                group_id = int(line.split("=", 1)[1])
        elif line.startswith("domain_stamp="):
            domain_stamp = line.split("=", 1)[1].strip()
    return group_id, domain_stamp


def _parse_domain_stamp(diag: str) -> str:
    """Extract stamp=<value> from diagnose response."""
    for line in (diag or "").splitlines():
        if line.startswith("stamp="):
            val = line[6:].strip()
            return val if val != "UNDETERMINED" else ""
    return ""


async def checkpoint_create(paths: str = "") -> str:
    """Create a durable checkpoint before an agent turn.
    paths: comma-separated file paths to snapshot. Empty = open dirty scenes."""
    from ..changeset_store import get_fingerprint
    from ..checkpoint import Checkpoint
    from ..checkpoint_manifest import scan_manifest

    # 1. Create Unity Undo checkpoint
    resp = await _send("checkpoint", {"label": "pre-turn"})
    group_id, domain_stamp = _parse_checkpoint_response(resp)

    # 2. Resolve file paths
    if paths.strip():
        path_list = [p.strip() for p in paths.split(",") if p.strip()]
    else:
        dirty_resp = await _send("editor", {"action": "dirty_scenes"})
        path_list = [p.strip() for p in (dirty_resp or "").splitlines() if p.strip()]

    # 3. Snapshot files into blob store
    content_store = _get_content_store()
    manifest = scan_manifest(path_list, content_store)

    # 4. Build and persist checkpoint
    cp_store = _get_cp_store()
    cp = Checkpoint(
        checkpoint_id=str(uuid.uuid4()),
        turn_id=0,
        state="preparing",
        fingerprint=get_fingerprint() or "",
        manifest=manifest,
        undo_group_id=group_id,
        domain_stamp=domain_stamp,
        created_at=datetime.now(timezone.utc).isoformat(),
        finalized_at=None,
    )
    cp_store.save(cp)
    cp_store.update_state(cp.checkpoint_id, "ready")

    return (
        f"checkpoint_id={cp.checkpoint_id}  state=ready  "
        f"files={len(manifest.entries)}  undo_group={group_id}"
    )


async def checkpoint_restore(checkpoint_id: str, force: bool = False) -> str:
    """Restore files to their pre-turn state.
    Tries Unity Undo first when domain stamp matches; falls back to file restore.
    MVP: after_refs always empty — no ChangeSet-based conflict detection yet."""
    from ..checkpoint_restore import restore_files

    cp_store = _get_cp_store()
    cp = cp_store.load(checkpoint_id)
    if cp is None:
        return f"err: checkpoint_not_found  id={checkpoint_id}"

    # Get current domain stamp via diagnose
    diag = await _send("diagnose", {})
    current_stamp = _parse_domain_stamp(diag)

    # Try Unity Undo if same domain load and valid group
    if (cp.undo_group_id >= 0 and len(cp.domain_stamp) > 0
            and current_stamp == cp.domain_stamp):
        resp = await _send("checkpoint_undo_restore", {
            "group_id": str(cp.undo_group_id),
            "domain_stamp": cp.domain_stamp,
        })
        if (resp or "").strip() == "ok":
            cp_store.update_state(checkpoint_id, "restored")
            n = len(cp.manifest.entries)
            return f"restored={n}  skipped=0  conflicts=none  method=undo"

    # File restore fallback (no ChangeSet integration in MVP → empty after_refs)
    content_store = _get_content_store()
    after_refs: dict[str, str] = {}
    result = restore_files(cp.manifest, after_refs, content_store, force=force)
    cp_store.update_state(checkpoint_id, "restored")

    conflict_paths = ",".join(c.path for c in result.conflicts) if result.conflicts else "none"
    return (
        f"restored={len(result.restored)}  skipped={len(result.skipped)}  "
        f"conflicts={conflict_paths}  method=file"
    )


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(checkpoint_create)
    mcp.tool(annotations=_DEL)(checkpoint_restore)
